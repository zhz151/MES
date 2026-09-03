using MES.Core.DTOs.Shared;
using MES.Data.Entities.Payroll;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MES.Services.Printing;

/// <summary>月工资汇总-打印行（17 列与《工资条及打印.xlsx》列序一致：工号→姓名→月份→出勤天数→…→实发）</summary>
public class PayrollSummaryPrintRow
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string MonthText { get; set; } = string.Empty;
    public int AttendanceDays { get; set; }
    public decimal BaseWage { get; set; }
    public decimal MiscWorkAmount { get; set; }
    public decimal PositionAllowance { get; set; }
    public decimal SeniorityBonus { get; set; }
    public decimal FullAttendanceBonus { get; set; }
    public decimal LeadBonus { get; set; }
    public decimal NightShiftAllowance { get; set; }
    public decimal HighTempAllowance { get; set; }
    public decimal InjurySubsidy { get; set; }
    public decimal Penalty { get; set; }
    public decimal SocialSecurity { get; set; }
    public decimal TotalPayable { get; set; }
    public decimal TotalPaid { get; set; }
}

/// <summary>
/// 月工资汇总打印（QuestPDF，A4 横向，SimSun，列序对齐《工资条及打印.xlsx》17 列）。
/// 两种打印：
/// - 全部打印 GenerateAllPdf：单张整表（表头跨页重复）；
/// - 个人打印 GeneratePersonalPdf：每人一条两行带（表头深色行 + 该员工数值行），带间留空便于裁剪发放。
/// 金额为 0 打印留空（贴近 Excel 样式）；处罚/代缴社保以负数显示。
/// </summary>
public static class PayrollSummaryPrintHelper
{
    /// <summary>17 个打印列定义（Key=PayrollSummaryPrintRow 属性名，顺序与 Excel 一致）</summary>
    private static readonly PrintColumnDef[] Columns =
    {
        new() { Key = "EmployeeCode", Label = "工号" },
        new() { Key = "EmployeeName", Label = "姓名" },
        new() { Key = "MonthText", Label = "月份" },
        new() { Key = "AttendanceDays", Label = "出勤天数" },
        new() { Key = "BaseWage", Label = "本月基础工资" },
        new() { Key = "MiscWorkAmount", Label = "本月杂辅工资" },
        new() { Key = "PositionAllowance", Label = "岗位补贴" },
        new() { Key = "SeniorityBonus", Label = "工龄奖" },
        new() { Key = "FullAttendanceBonus", Label = "满勤奖" },
        new() { Key = "LeadBonus", Label = "带班费" },
        new() { Key = "NightShiftAllowance", Label = "夜班津贴" },
        new() { Key = "HighTempAllowance", Label = "高温费" },
        new() { Key = "InjurySubsidy", Label = "工伤补贴" },
        new() { Key = "Penalty", Label = "处罚" },
        new() { Key = "SocialSecurity", Label = "代缴社保" },
        new() { Key = "TotalPayable", Label = "应发工资及津贴" },
        new() { Key = "TotalPaid", Label = "实发工资及津贴" },
    };

    /// <summary>金额列 Key（值为 0 时打印留空）</summary>
    private static readonly string[] AmountKeys =
    {
        "BaseWage", "MiscWorkAmount", "PositionAllowance", "SeniorityBonus", "FullAttendanceBonus",
        "LeadBonus", "NightShiftAllowance", "HighTempAllowance", "InjurySubsidy", "Penalty",
        "SocialSecurity", "TotalPayable", "TotalPaid",
    };

    /// <summary>打印列（供外部预览/复用）</summary>
    public static List<PrintColumnDef> GetColumns() => Columns.ToList();

    /// <summary>快照记录 → 打印行（月份文本、工号/姓名展示）</summary>
    public static PayrollSummaryPrintRow ToPrintRow(PayrollMonthlySummaryRecord r, string code, string name, int year, int month)
        => new()
        {
            EmployeeCode = code,
            EmployeeName = name,
            MonthText = $"{year}-{month:D2}",
            AttendanceDays = r.AttendanceDays,
            BaseWage = r.BaseWage,
            MiscWorkAmount = r.MiscWorkAmount,
            PositionAllowance = r.PositionAllowance,
            SeniorityBonus = r.SeniorityBonus,
            FullAttendanceBonus = r.FullAttendanceBonus,
            LeadBonus = r.LeadBonus,
            NightShiftAllowance = r.NightShiftAllowance,
            HighTempAllowance = r.HighTempAllowance,
            InjurySubsidy = r.InjurySubsidy,
            Penalty = r.Penalty,
            SocialSecurity = r.SocialSecurity,
            TotalPayable = r.TotalPayable,
            TotalPaid = r.TotalPaid,
        };

    /// <summary>全部打印：一张 A4 横向整表（17 列等宽，表头跨页重复）</summary>
    public static byte[] GenerateAllPdf(string title, List<PayrollSummaryPrintRow> rows)
        => Compose(title, rows, personal: false).GeneratePdf();

    /// <summary>个人打印：每人一条两行带（表头行 + 数值行），带间留空便于裁剪发放</summary>
    public static byte[] GeneratePersonalPdf(string title, List<PayrollSummaryPrintRow> rows)
        => Compose(title, rows, personal: true).GeneratePdf();

    private static Document Compose(string title, List<PayrollSummaryPrintRow> rows, bool personal)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("SimSun"));

                page.Header().Element(h => h.Column(col =>
                {
                    col.Item().PaddingBottom(4).AlignCenter().Text(title).FontSize(16).Bold();
                    col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Colors.Black);
                }));

                page.Content().Element(content =>
                {
                    if (personal)
                        ComposePersonal(content, rows);
                    else
                        ComposeAll(content, rows);
                });

                page.Footer().Element(f => f.Column(col =>
                {
                    col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Colors.Black);
                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text($"打印日期：{DateTime.Now:yyyy-MM-dd}").FontSize(8);
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.CurrentPageNumber().FontSize(8);
                            t.Span("/").FontSize(8);
                            t.TotalPages().FontSize(8);
                        });
                    });
                }));
            });
        });
    }

    /// <summary>全部打印内容：一张 17 列整表（表头经 table.Header 跨页重复）</summary>
    private static void ComposeAll(IContainer container, List<PayrollSummaryPrintRow> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                for (var i = 0; i < Columns.Length; i++)
                    cd.RelativeColumn(1);
            });

            table.Header(header =>
            {
                foreach (var c in Columns)
                    header.Cell().Element(CellHeaderStyle).Text(c.Label).FontSize(7).Bold().AlignCenter();
            });

            foreach (var row in rows)
            {
                foreach (var c in Columns)
                {
                    var value = GetCellText(c.Key, row);
                    table.Cell().Element(CellStyle).Text(value).FontSize(7).AlignCenter();
                }
            }
        });
    }

    /// <summary>
    /// 个人打印内容：每人一条单行表带（每格 = 表头小标签 + 该员工数值，上下两行），裁剪后每行自带表头。
    /// 每条为单行 QuestPDF 表 → 行不可分页拆开（QuestPDF 单行不跨页），天然保证整条带同页。
    /// </summary>
    private static void ComposePersonal(IContainer container, List<PayrollSummaryPrintRow> rows)
    {
        container.Column(col =>
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var idx = i;

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        for (var k = 0; k < Columns.Length; k++)
                            cd.RelativeColumn(1);
                    });

                    // 每格：表头标签（深色）在上、数值在下，整格边框成网格
                    foreach (var c in Columns)
                    {
                        var label = c.Label;
                        var value = GetCellText(c.Key, row);
                        table.Cell().Element(cell => cell
                            .Border(0.5f).BorderColor(Colors.Grey.Medium)
                            .Padding(1)
                            .Column(cc =>
                            {
                                cc.Item().Background(Colors.Grey.Lighten3)
                                    .PaddingVertical(1)
                                    .AlignCenter()
                                    .Text(label).FontSize(6).Bold();
                                cc.Item().PaddingVertical(1)
                                    .AlignCenter()
                                    .Text(value).FontSize(7);
                            }));
                    }
                });

                // 带间留空（裁剪线），末条不带尾随空隙
                if (idx < rows.Count - 1)
                    col.Item().Height(14);
            }
        });
    }

    /// <summary>按列 Key 取值；金额列 0 → 空串（贴近 Excel），处罚/代缴为负数正常带负号</summary>
    private static string GetCellText(string key, PayrollSummaryPrintRow row)
    {
        return key switch
        {
            "EmployeeCode" => row.EmployeeCode,
            "EmployeeName" => row.EmployeeName,
            "MonthText" => row.MonthText,
            "AttendanceDays" => row.AttendanceDays.ToString(),
            "BaseWage" => Money(row.BaseWage),
            "MiscWorkAmount" => Money(row.MiscWorkAmount),
            "PositionAllowance" => Money(row.PositionAllowance),
            "SeniorityBonus" => Money(row.SeniorityBonus),
            "FullAttendanceBonus" => Money(row.FullAttendanceBonus),
            "LeadBonus" => Money(row.LeadBonus),
            "NightShiftAllowance" => Money(row.NightShiftAllowance),
            "HighTempAllowance" => Money(row.HighTempAllowance),
            "InjurySubsidy" => Money(row.InjurySubsidy),
            "Penalty" => Money(row.Penalty),
            "SocialSecurity" => Money(row.SocialSecurity),
            "TotalPayable" => Money(row.TotalPayable),
            "TotalPaid" => Money(row.TotalPaid),
            _ => string.Empty,
        };
    }

    private static string Money(decimal value) => value == 0 ? string.Empty : value.ToString("G29");

    private static IContainer CellHeaderStyle(IContainer container)
        => container.Border(0.5f).BorderColor(Colors.Black)
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(3).PaddingHorizontal(2)
            .AlignMiddle();

    private static IContainer CellStyle(IContainer container)
        => container.Border(0.5f).BorderColor(Colors.Grey.Medium)
            .PaddingVertical(2).PaddingHorizontal(2)
            .AlignMiddle();
}
