using System.Text.Json;
using FluentAssertions;
using MES.Core.DTOs.Shared;
using MES.Services.Printing;
using QuestPDF.Infrastructure;
using Xunit;

namespace MES.Tests.Services.Printing;

/// <summary>
/// 通用表格打印 Helper 测试：autoWidth（内容自适应列宽）模式下列保底宽度导致总宽超限抛 QuestPDF 布局冲突的回归保护。
/// </summary>
public class TablePrintHelperTests
{
    static TablePrintHelperTests()
    {
        // QuestPDF 社区版许可（测试环境需要手动设置）
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GeneratePdf_autoWidth_真实列规模JsonElement值_不抛布局冲突()
    {
        // 模拟前端「打印选中列表」GetPrintValue 返回的可见列（WorkOrders 全列规模约 25 列）
        var keys = new[]
        {
            "WorkOrderNo", "SalesOrderNo", "ProductionMainNo", "ProductionSubNo", "SignDate",
            "Salesman", "EndCustomer", "DeliveryDate", "DelayPenalty", "SettlementMethod",
            "PlantGrade", "MaterialName", "Specification", "LengthStatus", "MinLength",
            "MaxLength", "TotalQuantity", "TotalWeight", "DeliveryState", "TotalItemCount",
            "Status", "CreatedBy", "CreatedTime", "UpdatedBy", "UpdatedTime"
        };

        var raw = new List<Dictionary<string, object>>
        {
            new()
            {
                ["WorkOrderNo"] = "WO20260801-001-01", ["SalesOrderNo"] = "SO2026-00315",
                ["ProductionMainNo"] = "M26-08-001", ["ProductionSubNo"] = "-",
                ["SignDate"] = "2026-08-01", ["Salesman"] = "张伟",
                ["EndCustomer"] = "华润电力工程有限公司", ["DeliveryDate"] = "2026-09-30",
                ["DelayPenalty"] = "否", ["SettlementMethod"] = "承兑汇票",
                ["PlantGrade"] = "304", ["MaterialName"] = "冷轧无缝管",
                ["Specification"] = "60*6.2*8000-L80ST16-304", ["LengthStatus"] = "定尺6米",
                ["MinLength"] = "6000", ["MaxLength"] = "8000",
                ["TotalQuantity"] = "2", ["TotalWeight"] = "4250",
                ["DeliveryState"] = "未交付", ["TotalItemCount"] = "1",
                ["Status"] = "执行中", ["CreatedBy"] = "admin",
                ["CreatedTime"] = "2026-08-01 09:30", ["UpdatedBy"] = "admin",
                ["UpdatedTime"] = "2026-08-02 10:00"
            }
        };

        // 模拟 MVC JSON 模型绑定：序列化后反序列化 → object 值变 JsonElement
        var json = JsonSerializer.Serialize(raw);
        var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json)!;
        items[0]["Specification"]!.GetType().Name.Should().Be("JsonElement");

        var columns = keys.Select(k => new PrintColumnDef { Key = k, Label = "列" + k }).ToList();

        var bytes = TablePrintHelper.GeneratePdf("工单列表", items, columns,
            autoWidth: true, alignCenter: true, headerMaxLines: 3);

        bytes.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(bytes.Take(4).ToArray()).Should().Be("%PDF");
    }

    [Fact]
    public void GeneratePdf_autoWidth_多列短内容保底抬升总宽_不抛布局冲突()
    {
        // 30 列短内容：多列保底抬升总宽，但仍在可显示列数上限 35 内，正常生成不抛布局冲突
        var keys = Enumerable.Range(1, 30).Select(i => "Col" + i).ToArray();
        var raw = new List<Dictionary<string, object>>
        {
            keys.ToDictionary(k => k, k => (object)"短值")
        };
        var json = JsonSerializer.Serialize(raw);
        var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json)!;
        var columns = keys.Select(k => new PrintColumnDef { Key = k, Label = "字段" + k }).ToList();

        var bytes = TablePrintHelper.GeneratePdf("30列测试", items, columns,
            autoWidth: true, alignCenter: true, headerMaxLines: 3);

        bytes.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GeneratePdf_autoWidth_列数超可显示上限_抛友好业务异常而非布局冲突()
    {
        // 40 列长短混合内容（超过可显示列数上限 35）：应按可显示列数上限拦截为友好 BusinessException（提示精简列），而非裸 QuestPDF DocumentLayoutException(500)。
        var keys = Enumerable.Range(1, 40).Select(i => "Col" + i).ToArray();
        var raw = new List<Dictionary<string, object>>
        {
            keys.Select((k, idx) => new { k, idx })
                .ToDictionary(x => x.k, x => (object)(x.idx % 3 == 0 ? "是" : "这是一个较长的单元格内容示例"))
        };
        var json = JsonSerializer.Serialize(raw);
        var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json)!;
        var columns = keys.Select(k => new PrintColumnDef { Key = k, Label = "字段" + k }).ToList();

        var act = () => TablePrintHelper.GeneratePdf("40列混合", items, columns,
            autoWidth: true, alignCenter: true, headerMaxLines: 3);

        act.Should().Throw<MES.Core.Exceptions.BusinessException>()
            .WithMessage("*打印列数过多*");
    }

    [Fact]
    public void GeneratePdf_autoWidth_临界列数_不抛布局冲突()
    {
        // 可显示列数上限（35）内的长短混合内容：每列保底可容纳中文字符，正常生成不抛布局冲突
        var keys = Enumerable.Range(1, 35).Select(i => "Col" + i).ToArray();
        var raw = new List<Dictionary<string, object>>
        {
            keys.Select((k, idx) => new { k, idx })
                .ToDictionary(x => x.k, x => (object)(x.idx % 3 == 0 ? "是" : "这是一个较长的单元格内容示例"))
        };
        var json = JsonSerializer.Serialize(raw);
        var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json)!;
        var columns = keys.Select(k => new PrintColumnDef { Key = k, Label = "字段" + k }).ToList();

        var bytes = TablePrintHelper.GeneratePdf("35列临界", items, columns,
            autoWidth: true, alignCenter: true, headerMaxLines: 3);

        bytes.Should().NotBeNullOrEmpty();
    }
}
