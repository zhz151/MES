using System.Linq.Expressions;
using System.Reflection;
using MES.Core.Models;

namespace MES.Services.Helpers;

/// <summary>
/// IQueryable 通用扩展方法 — 排序与筛选
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// 通用排序：按属性名反射排序，替代手写 switch。
    /// 属性不存在时兜底按 CreatedTime 降序。
    /// </summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string sortBy, bool isDescending)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            sortBy = "CreatedTime";

        var type = typeof(T);
        var prop = type.GetProperty(sortBy, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
            sortBy = "CreatedTime";

        // 重新获取属性（兜底后）
        if (prop == null)
            prop = type.GetProperty(sortBy, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
            return query;

        var param = Expression.Parameter(type, "e");
        var member = Expression.Property(param, prop);
        var lambda = Expression.Lambda(member, param);

        var methodName = isDescending ? "OrderByDescending" : "OrderBy";
        var resultExpr = Expression.Call(
            typeof(Queryable), methodName,
            [type, prop.PropertyType],
            query.Expression, Expression.Quote(lambda));

        return query.Provider.CreateQuery<T>(resultExpr);
    }

    /// <summary>
    /// 通用筛选：按 FilterDescriptor 列表构建 WHERE 条件（AND 关系）。
    /// 保留 Keyword 模糊搜索的 OR 链不变，本方法仅处理精确筛选。
    /// </summary>
    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, List<FilterDescriptor>? filters)
    {
        if (filters == null || filters.Count == 0)
            return query;

        var type = typeof(T);
        var param = Expression.Parameter(type, "e");
        Expression? combined = null;

        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field))
                continue;

            var prop = type.GetProperty(filter.Field, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
                continue;

            var member = Expression.Property(param, prop);
            var propType = prop.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;

            Expression? condition = filter.Operator.ToLowerInvariant() switch
            {
                "contains" => BuildStringContains(member, filter.Value),
                "equals" => BuildEquals(member, filter.Value, underlyingType, propType),
                "startswith" => BuildStartsWith(member, filter.Value),
                "gt" => BuildComparison(member, filter.Value, underlyingType, propType, Expression.GreaterThan),
                "gte" => BuildComparison(member, filter.Value, underlyingType, propType, Expression.GreaterThanOrEqual),
                "lt" => BuildComparison(member, filter.Value, underlyingType, propType, Expression.LessThan),
                "lte" => BuildComparison(member, filter.Value, underlyingType, propType, Expression.LessThanOrEqual),
                "range" => BuildRange(member, filter.From, filter.To, underlyingType, propType),
                "in" => BuildIn(member, filter.Values, underlyingType),
                "isnull" => null, // 仅查空值，由 IncludeNull 配合 BuildIncludeNull 生成 IS NULL
                _ => null
            };

            // 包含空值：追加 OR field IS NULL
            if (filter.IncludeNull)
                condition = BuildIncludeNull(member, propType, condition);

            if (condition != null)
                combined = combined == null ? condition : Expression.AndAlso(combined, condition);
        }

        if (combined == null)
            return query;

        var lambda = Expression.Lambda<Func<T, bool>>(combined, param);
        return query.Where(lambda);
    }

    // ==================== 私有帮助方法 ====================

    private static Expression? BuildStringContains(Expression member, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        // 只有字符串类型才能调用 .Contains()
        if (member.Type != typeof(string))
            return null;
        var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)]);
        if (containsMethod == null)
            return null;
        var constant = Expression.Constant(value);
        return Expression.Call(member, containsMethod, constant);
    }

    private static Expression? BuildEquals(Expression member, string? value, Type underlyingType, Type propType)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        var constant = ConvertToConstant(value, underlyingType, propType);
        if (constant == null)
            return null;
        return Expression.Equal(member, constant);
    }

    private static Expression? BuildStartsWith(Expression member, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        // 只有字符串类型才能调用 .StartsWith()
        if (member.Type != typeof(string))
            return null;
        var startsWithMethod = typeof(string).GetMethod("StartsWith", [typeof(string)]);
        if (startsWithMethod == null)
            return null;
        var constant = Expression.Constant(value);
        return Expression.Call(member, startsWithMethod, constant);
    }

    private static Expression? BuildComparison(
        Expression member, string? value, Type underlyingType, Type propType,
        Func<Expression, Expression, BinaryExpression> comparator)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        var constant = ConvertToConstant(value, underlyingType, propType);
        if (constant == null)
            return null;
        return comparator(member, constant);
    }

    private static Expression? BuildRange(Expression member, object? from, object? to, Type underlyingType, Type propType)
    {
        if (from == null && to == null)
            return null;

        // 数字类型范围
        if (underlyingType == typeof(decimal) || underlyingType == typeof(double)
            || underlyingType == typeof(float) || underlyingType == typeof(int)
            || underlyingType == typeof(long))
        {
            return BuildNumericRange(member, from, to, underlyingType, propType);
        }

        // DateTime 范围（from/to 可能是 string，需解析）
        var dtFrom = TryParseDateTime(from);
        var dtTo = TryParseDateTime(to);
        if (dtFrom == null && dtTo == null)
            return null;

        Expression? condition = null;

        if (dtFrom.HasValue)
        {
            var fromConst = Expression.Constant(dtFrom.Value, propType);
            condition = Expression.GreaterThanOrEqual(member, fromConst);
        }

        if (dtTo.HasValue)
        {
            // 加 1 天使边界包含当日全天：<= endDate → < (endDate + 1 day)
            var toValue = dtTo.Value.AddDays(1);
            var toConst = Expression.Constant(toValue, propType);
            var toCondition = Expression.LessThan(member, toConst);
            condition = condition == null ? toCondition : Expression.AndAlso(condition, toCondition);
        }

        return condition;
    }

    private static DateTime? TryParseDateTime(object? val)
    {
        if (val == null) return null;
        if (val is DateTime dt) return dt;
        if (val is string s && DateTime.TryParse(s, out var parsed)) return parsed;
        return null;
    }

    private static Expression? BuildNumericRange(Expression member, object? from, object? to, Type underlyingType, Type propType)
    {
        Expression? condition = null;

        if (from != null && decimal.TryParse(from.ToString(), out var fromVal))
        {
            var fromConst = ConvertToConstant(fromVal.ToString("G29"), underlyingType, propType);
            if (fromConst != null)
                condition = Expression.GreaterThanOrEqual(member, fromConst);
        }

        if (to != null && decimal.TryParse(to.ToString(), out var toVal))
        {
            var toConst = ConvertToConstant(toVal.ToString("G29"), underlyingType, propType);
            if (toConst != null)
            {
                var toCondition = Expression.LessThanOrEqual(member, toConst);
                condition = condition == null ? toCondition : Expression.AndAlso(condition, toCondition);
            }
        }

        return condition;
    }

    private static Expression? BuildIn(Expression member, List<string>? values, Type underlyingType)
    {
        if (values == null || values.Count == 0)
            return null;

        // 对于枚举类型，解析为枚举值
        if (underlyingType.IsEnum)
        {
            var parsedValues = values
                .Select(v =>
                {
                    try { return Enum.Parse(underlyingType, v); }
                    catch { return null; }
                })
                .Where(v => v != null)
                .ToList();
            if (parsedValues.Count == 0)
                return null;

            var listType = typeof(List<>).MakeGenericType(underlyingType);
            var list = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add")!;
            foreach (var v in parsedValues)
                addMethod.Invoke(list, [v]);

            var containsMethod = typeof(Enumerable)
                .GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(underlyingType);

            return Expression.Call(containsMethod, Expression.Constant(list), member);
        }

        // 布尔类型：解析 "True"/"False" 字符串为 bool 后匹配
        if (underlyingType == typeof(bool))
        {
            var parsedValues = values
                .Select(v => bool.TryParse(v, out var b) ? (bool?)b : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (parsedValues.Count == 0)
                return null;
            var list = Expression.Constant(parsedValues);
            var containsMethod = typeof(List<bool>).GetMethod("Contains", [typeof(bool)]);
            if (containsMethod == null)
                return null;
            return Expression.Call(list, containsMethod, member);
        }

        // DateTime 类型（含 Nullable<DateTime>）
        if (underlyingType == typeof(DateTime))
        {
            // 解析筛选值为纯日期（去除时间部分），使 "2026-05-23" 匹配带时间的 DateTime
            var parsedDates = values
                .Select(v => DateTime.TryParse(v, out var dt) ? (DateTime?)dt.Date : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (parsedDates.Count == 0)
                return null;
            var dateList = Expression.Constant(parsedDates);
            var dateContains = typeof(List<DateTime>).GetMethod("Contains", [typeof(DateTime)]);
            if (dateContains == null)
                return null;
            // Nullable<DateTime> 类型需取 .Value 以匹配 Contains(DateTime)
            var memberForContains = member;
            if (member.Type != typeof(DateTime))
                memberForContains = Expression.Property(member, "Value");
            // 截取 Date 部分：使带时间的 DateTime 也能被纯日期值匹配
            memberForContains = Expression.Property(memberForContains, "Date");
            return Expression.Call(dateList, dateContains, memberForContains);
        }

        // 整数类型（如 MaterialPlanStatus=0/1/2/3/4 等状态字段）
        if (underlyingType == typeof(int) || underlyingType == typeof(long)
            || underlyingType == typeof(short) || underlyingType == typeof(byte))
        {
            var parsedValues = values
                .Select(v => long.TryParse(v, out var n) ? (long?)n : null)
                .Where(v => v.HasValue)
                .Select(v => Convert.ChangeType(v!.Value, underlyingType))
                .ToList();
            if (parsedValues.Count == 0)
                return null;
            var listType = typeof(List<>).MakeGenericType(underlyingType);
            var list = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add")!;
            foreach (var v in parsedValues)
                addMethod.Invoke(list, [v]);
            var containsMethod = typeof(Enumerable)
                .GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(underlyingType);
            return Expression.Call(containsMethod, Expression.Constant(list), member);
        }

        // 字符串列表
        var stringList = Expression.Constant(values);
        var stringContains = typeof(List<string>).GetMethod("Contains", [typeof(string)]);
        if (stringContains == null)
            return null;
        return Expression.Call(stringList, stringContains, member);
    }

    /// <summary>
    /// 追加 OR field IS NULL 条件（仅在字段可为 null 时有效）
    /// </summary>
    private static Expression? BuildIncludeNull(Expression member, Type propType, Expression? existingCondition)
    {
        // 引用类型（string 等）始终可为 null
        // 值类型需要是 Nullable<T> 才可为 null
        var isNullable = !propType.IsValueType || Nullable.GetUnderlyingType(propType) != null;
        if (!isNullable)
            return existingCondition;

        var nullConst = Expression.Constant(null, propType);
        var nullCheck = Expression.Equal(member, nullConst);

        return existingCondition == null
            ? nullCheck
            : Expression.OrElse(existingCondition, nullCheck);
    }

    /// <summary>
    /// 将字符串值转换为目标类型的 ConstantExpression
    /// </summary>
    private static Expression? ConvertToConstant(string value, Type underlyingType, Type propType)
    {
        try
        {
            if (underlyingType == typeof(string))
                return Expression.Constant(value, propType);

            object parsed;
            if (underlyingType == typeof(int))
                parsed = int.Parse(value);
            else if (underlyingType == typeof(decimal))
                parsed = decimal.Parse(value);
            else if (underlyingType == typeof(double))
                parsed = double.Parse(value);
            else if (underlyingType == typeof(bool))
                parsed = value == "是" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
            else if (underlyingType == typeof(DateTime))
                parsed = DateTime.Parse(value);
            else if (underlyingType.IsEnum)
                parsed = Enum.Parse(underlyingType, value);
            else
                parsed = Convert.ChangeType(value, underlyingType);

            // 用 propType（可能为 Nullable<T>）创建常量，确保与 member 类型匹配
            return Expression.Constant(parsed, propType);
        }
        catch
        {
            return null;
        }
    }
}
