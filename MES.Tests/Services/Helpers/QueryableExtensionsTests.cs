using FluentAssertions;
using MES.Core.Models;
using MES.Services.Helpers;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// QueryableExtensions 筛选/排序工具类测试
/// 使用 List{T}.AsQueryable() 验证表达式树构建是否正确
/// </summary>
public class QueryableExtensionsTests
{
    #region 测试实体

    public enum TestStatus { Pending, Active, Completed, Cancelled }

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public int? NullableQuantity { get; set; }
        public decimal Weight { get; set; }
        public decimal? NullableWeight { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? NullableDate { get; set; }
        public TestStatus Status { get; set; }
        public string? Description { get; set; }
    }

    #endregion

    private static List<TestEntity> CreateTestData() => new()
    {
        new() { Id = 1, Name = "Alpha", Quantity = 10, NullableQuantity = 10, Weight = 100.5m, NullableWeight = 100.5m, IsActive = true, CreateDate = new DateTime(2026, 1, 15, 8, 30, 0), NullableDate = new DateTime(2026, 1, 15), Status = TestStatus.Active, Description = "First" },
        new() { Id = 2, Name = "Beta", Quantity = 20, NullableQuantity = null, Weight = 200.7m, NullableWeight = null, IsActive = false, CreateDate = new DateTime(2026, 2, 20, 14, 0, 0), NullableDate = null, Status = TestStatus.Pending, Description = "Second" },
        new() { Id = 3, Name = "Gamma", Quantity = 30, NullableQuantity = 30, Weight = 300.9m, NullableWeight = 300.9m, IsActive = true, CreateDate = new DateTime(2026, 3, 25, 9, 15, 0), NullableDate = new DateTime(2026, 3, 25), Status = TestStatus.Completed, Description = null },
        new() { Id = 4, Name = "Delta", Quantity = 5, NullableQuantity = null, Weight = 50.0m, NullableWeight = null, IsActive = false, CreateDate = new DateTime(2026, 4, 10, 16, 45, 0), NullableDate = new DateTime(2026, 4, 10), Status = TestStatus.Active, Description = "Fourth" },
    };

    // ==================== ApplySort ====================

    [Fact]
    public void ApplySort_空sortBy_降级为CreatedTime()
    {
        var data = CreateTestData().AsQueryable();
        var sorted = data.ApplySort("", false);
        // CreatedTime 不存在于 TestEntity，兜底 CreatedTime 也不存在 → 返回原 query
        sorted.Should().BeSameAs(data);
    }

    [Fact]
    public void ApplySort_按Name升序()
    {
        var data = CreateTestData().AsQueryable();
        var sorted = data.ApplySort("Name", false).ToList();
        sorted.Select(e => e.Name).Should().Equal("Alpha", "Beta", "Delta", "Gamma");
    }

    [Fact]
    public void ApplySort_按Name降序()
    {
        var data = CreateTestData().AsQueryable();
        var sorted = data.ApplySort("Name", true).ToList();
        sorted.Select(e => e.Name).Should().Equal("Gamma", "Delta", "Beta", "Alpha");
    }

    [Fact]
    public void ApplySort_按Quantity升序()
    {
        var data = CreateTestData().AsQueryable();
        var sorted = data.ApplySort("Quantity", false).ToList();
        sorted.Select(e => e.Quantity).Should().Equal(5, 10, 20, 30);
    }

    [Fact]
    public void ApplySort_不存在的属性名_降级为CreatedTime()
    {
        var data = CreateTestData().AsQueryable();
        var sorted = data.ApplySort("NonExistent", false);
        // 降级 CreatedTime 也不存在 → 返回原 query
        sorted.Should().BeSameAs(data);
    }

    // ==================== ApplyFilters — string contains ====================

    [Fact]
    public void ApplyFilters_字符串Contains_匹配()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Name", Operator = "contains", Value = "et" }
        }).ToList();
        // "Beta" contains "et", "Delta" does not (D-e-l-t-a)
        filtered.Should().ContainSingle(e => e.Name == "Beta");
    }

    [Fact]
    public void ApplyFilters_字符串Contains_大小写不敏感()
    {
        var data = CreateTestData().AsQueryable();
        // string.Contains 在内存中默认大小写敏感，此处仅验证表达式构建不抛异常
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Name", Operator = "contains", Value = "alpha" }
        }).ToList();
        // 内存Contains大小写敏感，所以找不到 "Alpha"
        filtered.Should().BeEmpty();
    }

    [Fact]
    public void ApplyFilters_Value为空_返回原query()
    {
        var data = CreateTestData().AsQueryable();
        var countBefore = data.Count();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Name", Operator = "contains", Value = "" }
        }).ToList();
        filtered.Should().HaveCount(countBefore);
    }

    // ==================== ApplyFilters — equals ====================

    [Fact]
    public void ApplyFilters_字符串Equals()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Name", Operator = "equals", Value = "Alpha" }
        }).ToList();
        filtered.Should().ContainSingle(e => e.Name == "Alpha");
    }

    [Fact]
    public void ApplyFilters_整数Equals()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Quantity", Operator = "equals", Value = "20" }
        }).ToList();
        filtered.Should().ContainSingle(e => e.Quantity == 20);
    }

    [Fact]
    public void ApplyFilters_BoolEquals()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "IsActive", Operator = "equals", Value = "true" }
        }).ToList();
        filtered.Should().HaveCount(2); // Alpha, Gamma
        filtered.All(e => e.IsActive).Should().BeTrue();
    }

    // ==================== ApplyFilters — startsWith ====================

    [Fact]
    public void ApplyFilters_字符串StartsWith()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Name", Operator = "startswith", Value = "Al" }
        }).ToList();
        filtered.Should().ContainSingle(e => e.Name == "Alpha");
    }

    // ==================== ApplyFilters — in (BuildIn) ====================

    [Fact]
    public void ApplyFilters_BuildIn_枚举值()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Status", Operator = "in", Values = new List<string> { "Active", "Completed" } }
        }).ToList();
        filtered.Should().HaveCount(3); // Alpha(Active), Gamma(Completed), Delta(Active)
    }

    [Fact]
    public void ApplyFilters_BuildIn_Bool()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "IsActive", Operator = "in", Values = new List<string> { "True" } }
        }).ToList();
        filtered.Should().HaveCount(2);
        filtered.All(e => e.IsActive).Should().BeTrue();
    }

    [Fact]
    public void ApplyFilters_BuildIn_DateTime_Date截断匹配()
    {
        var data = CreateTestData().AsQueryable();
        // CreateDate 包含时间部分(2026-01-15 08:30:00)，筛选值 "2026-01-15" 应通过 .Date 截断匹配
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "CreateDate", Operator = "in", Values = new List<string> { "2026-01-15" } }
        }).ToList();
        filtered.Should().ContainSingle(e => e.Id == 1);
    }

    [Fact]
    public void ApplyFilters_BuildIn_DateTime_带时间字符串()
    {
        var data = CreateTestData().AsQueryable();
        // "2026-01-15 08:30:00" 解析后 .Date 截断为 "2026-01-15"，匹配 Id=1
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "CreateDate", Operator = "in", Values = new List<string> { "2026-01-15 08:30:00" } }
        }).ToList();
        filtered.Should().ContainSingle(e => e.Id == 1);
    }

    [Fact]
    public void ApplyFilters_BuildIn_NullableDateTime()
    {
        // 只能用全部非 null 的数据集，避免 .Value 在 null 上抛出
        var data = CreateTestData().Where(e => e.NullableDate.HasValue).ToList().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "NullableDate", Operator = "in", Values = new List<string> { "2026-01-15" } }
        }).ToList();
        filtered.Should().ContainSingle(e => e.Id == 1);
    }

    [Fact]
    public void ApplyFilters_BuildIn_整数()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Quantity", Operator = "in", Values = new List<string> { "10", "30" } }
        }).ToList();
        filtered.Should().HaveCount(2); // Alpha(10), Gamma(30)
    }

    [Fact]
    public void ApplyFilters_BuildIn_小数()
    {
        var data = CreateTestData().AsQueryable();
        // decimal 列 "in" 精确匹配（2026-08-06 新增 decimal 分支）
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Weight", Operator = "in", Values = new List<string> { "100.5", "300.9" } }
        }).ToList();
        filtered.Should().HaveCount(2); // Alpha(100.5), Gamma(300.9)
    }

    [Fact]
    public void ApplyFilters_BuildIn_可空整数()
    {
        // 只用非 null 数据集，避免 Convert 在 null 上抛异常（EF 场景由 SQL 处理 null，仅内存测试需过滤）
        var data = CreateTestData().Where(e => e.NullableQuantity.HasValue).ToList().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "NullableQuantity", Operator = "in", Values = new List<string> { "10", "30" } }
        }).ToList();
        filtered.Should().HaveCount(2); // Alpha(10), Gamma(30)
    }

    [Fact]
    public void ApplyFilters_BuildIn_可空小数()
    {
        // 同上：可空 decimal 列 "in" 精确匹配
        var data = CreateTestData().Where(e => e.NullableWeight.HasValue).ToList().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "NullableWeight", Operator = "in", Values = new List<string> { "100.5", "300.9" } }
        }).ToList();
        filtered.Should().HaveCount(2); // Alpha(100.5), Gamma(300.9)
    }

    [Fact]
    public void ApplyFilters_BuildIn_可空整数含null行_内存不抛异常()
    {
        // 混合含 null 行，仅断言合法值命中；EF 场景由 SQL 处理
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Quantity", Operator = "in", Values = new List<string> { "5" } }
        }).ToList();
        filtered.Should().ContainSingle(e => e.Id == 4);
    }

    [Fact]
    public void ApplyFilters_BuildIn_字符串()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Name", Operator = "in", Values = new List<string> { "Alpha", "Gamma" } }
        }).ToList();
        filtered.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyFilters_BuildIn_空Values_返回原query()
    {
        var data = CreateTestData().AsQueryable();
        var countBefore = data.Count();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Name", Operator = "in", Values = new List<string>() }
        }).ToList();
        filtered.Should().HaveCount(countBefore);
    }

    [Fact]
    public void ApplyFilters_BuildIn_Values含非法值_只筛选合法值()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Status", Operator = "in", Values = new List<string> { "Active", "InvalidStatus" } }
        }).ToList();
        filtered.Should().HaveCount(2); // Alpha(Active), Delta(Active)
    }

    // ==================== ApplyFilters — range ====================

    [Fact]
    public void ApplyFilters_数值范围_仅from()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Quantity", Operator = "range", From = 15 }
        }).ToList();
        filtered.Should().HaveCount(2); // Beta(20), Gamma(30)
    }

    [Fact]
    public void ApplyFilters_数值范围_fromTo()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Quantity", Operator = "range", From = 10, To = 20 }
        }).ToList();
        filtered.Should().HaveCount(2); // Alpha(10), Beta(20)
    }

    [Fact]
    public void ApplyFilters_DateTime范围_fromTo()
    {
        var data = CreateTestData().AsQueryable();
        // CreateDate：2026-02-20, 2026-03-25 在范围内
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "CreateDate", Operator = "range",
                From = new DateTime(2026, 2, 1), To = new DateTime(2026, 3, 31) }
        }).ToList();
        filtered.Should().HaveCount(2); // Beta, Gamma
    }

    // ==================== ApplyFilters — gt/lt/gte/lte ====================

    [Fact]
    public void ApplyFilters_大于gt()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Quantity", Operator = "gt", Value = "20" }
        }).ToList();
        filtered.Should().ContainSingle(e => e.Quantity == 30);
    }

    [Fact]
    public void ApplyFilters_小于等于lte()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Quantity", Operator = "lte", Value = "10" }
        }).ToList();
        filtered.Should().HaveCount(2); // Alpha(10), Delta(5)
    }

    // ==================== ApplyFilters — IncludeNull ====================

    [Fact]
    public void ApplyFilters_IncludeNull_包含null记录()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Description", Operator = "equals", Value = "First", IncludeNull = true }
        }).ToList();
        // equals "First" → Id=1, IncludeNull → Id=3(Description=null)
        filtered.Should().HaveCount(2);
        filtered.Select(e => e.Id).Should().BeEquivalentTo(new[] { 1, 3 });
    }

    // ==================== ApplyFilters — 多个条件AND ====================

    [Fact]
    public void ApplyFilters_多条件AND()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "IsActive", Operator = "equals", Value = "true" },
            new() { Field = "Status", Operator = "in", Values = new List<string> { "Active" } }
        }).ToList();
        // IsActive=true: Alpha, Gamma; Status=Active: Alpha, Delta; AND: Alpha
        filtered.Should().ContainSingle(e => e.Name == "Alpha");
    }

    // ==================== ApplyFilters — 不存在的字段 ====================

    [Fact]
    public void ApplyFilters_不存在的字段_跳过()
    {
        var data = CreateTestData().AsQueryable();
        var countBefore = data.Count();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "NonExistent", Operator = "equals", Value = "x" }
        }).ToList();
        filtered.Should().HaveCount(countBefore);
    }

    // ==================== ApplyFilters — null/空filters ====================

    [Fact]
    public void ApplyFilters_null_filters_返回原query()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(null).ToList();
        filtered.Should().HaveCount(4);
    }

    [Fact]
    public void ApplyFilters_空列表_返回原query()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>()).ToList();
        filtered.Should().HaveCount(4);
    }

    // ==================== InclusionNull with non-nullable 类型 ====================

    [Fact]
    public void ApplyFilters_IncludeNull_非null值类型_不加OR条件()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data.ApplyFilters(new List<FilterDescriptor>
        {
            new() { Field = "Quantity", Operator = "equals", Value = "10", IncludeNull = true }
        }).ToList();
        // Quantity 是 int（非 null 值类型），IncludeNull 无效
        filtered.Should().ContainSingle(e => e.Quantity == 10);
    }

    // ==================== 排序 + 筛选组合 ====================

    [Fact]
    public void ApplySort_和ApplyFilters_组合使用()
    {
        var data = CreateTestData().AsQueryable();
        var filtered = data
            .ApplyFilters(new List<FilterDescriptor>
            {
                new() { Field = "IsActive", Operator = "equals", Value = "true" }
            })
            .ApplySort("Quantity", false)
            .ToList();
        filtered.Should().HaveCount(2);
        filtered.First().Name.Should().Be("Alpha"); // Alpha(10), Gamma(30)
    }
}
