using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Data.Entities.Quality;

namespace MES.Services.Printing;

/// <summary>
/// 不合格报告 PDF 打印模板（QuestPDF — B类富布局）
/// </summary>
public static class NcrPrintHelper
{
    public static byte[] GeneratePdf(List<Ncr> entities)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Portrait());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));

                page.Header().Element(ComposeDocHeader);
                page.Content().Element(c => ComposeContent(c, entities));
                page.Footer().Element(ComposeDocFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeDocHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(5).AlignCenter().Text("不 合 格 报 告")
                .FontSize(22).Bold().FontColor(Colors.Red.Darken3);
        });
    }

    private static void ComposeDocFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingVertical(4)
                .LineHorizontal(1).LineColor(Colors.Black);
            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text($"打印日期：{DateTime.Now:yyyy-MM-dd}").FontSize(8);
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("第 ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" 页 / 共 ").FontSize(8);
                    t.TotalPages().FontSize(8);
                    t.Span(" 页").FontSize(8);
                });
            });
        });
    }

    private static void ComposeContent(IContainer container, List<Ncr> entities)
    {
        container.Column(col =>
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var n = entities[i];
                ComposeNcrReport(col, n);
                if (i < entities.Count - 1)
                    col.Item().PageBreak();
            }
        });
    }

    private static void ComposeNcrReport(ColumnDescriptor col, Ncr n)
    {
        // 报告编号 + 状态
        col.Item().PaddingTop(8).Row(row =>
        {
            row.RelativeItem().AlignLeft().Text($"编号: NCR-{n.Id:D4}").FontSize(11).Bold();
            row.RelativeItem().AlignRight().Text(GetStatusText(n.Status)).FontSize(11).Bold();
        });

        // G1 问题反馈
        ComposeSection(col, "G1 问题反馈", "d32f2f", "fff5f5", table =>
        {
            AppendFieldRow(table, "反馈日期", n.ReportDate.ToString("yyyy-MM-dd"), "反馈部门", n.ReportDepartment ?? "");
            AppendFieldRow(table, "反馈人", n.Reporter ?? "", "钢管类别", GetMaterialTypeText(n.PipeCategory));
            AppendFieldRow(table, "生产编号", n.BatchNo, "工单号", n.WorkOrderNo ?? "");
            AppendFieldRow(table, "主号", n.ProductionMainNo ?? "", "牌号", n.PlantGrade ?? "");
            AppendFieldRow(table, "规格", n.Specification ?? "", "不合格支数", n.DefectiveQuantity?.ToString("G29") ?? "0");
            AppendFieldSpan(table, "问题描述", n.ProblemDescription ?? "");
        });

        // G2 不合格品处置
        ComposeSection(col, "G2 不合格品处置", "f57c00", "fff8e1", table =>
        {
            AppendFieldRow(table, "处置方式", GetDisposalMethodText(n.DisposalMethod), "处置完结", n.DisposalIsCompleted ? "是" : "否");
            AppendFieldRow(table, "处置完结日期", FormatDate(n.DisposalCompleteDate), "", "");
            AppendFieldSpan(table, "处置备注", n.DisposalRemark ?? "");
        });

        // G3 原因分析
        ComposeSection(col, "G3 原因分析", "1976d2", "e3f2fd", table =>
        {
            AppendFieldRow(table, "严重程度", GetSeverityText(n.Severity), "分析确认人", n.AnalysisConfirmer ?? "");
            AppendFieldRow(table, "确认日期", FormatDate(n.AnalysisConfirmDate), "", "");
            AppendFieldSpan(table, "原因分析", n.RootCauseAnalysis ?? "");
        });

        // G4 责任人及处理
        ComposeSection(col, "G4 责任人及处理", "5c6bc0", "e8eaf6", table =>
        {
            AppendFieldRow(table, "责任类别", GetResponsibilityCategoryText(n.ResponsibilityCategory), "责任部门", n.ResponsibleDept ?? "");
            AppendFieldRow(table, "责任人", n.ResponsiblePerson ?? "", "操作日期", FormatDate(n.OperationDate));
            AppendFieldRow(table, "处理完结", n.PersonIsCompleted ? "是" : "否", "完结日期", FormatDate(n.PersonCompleteDate));
            AppendFieldSpan(table, "对责任人的处理", n.PersonDisposition ?? "");
        });

        // G5 纠正预防措施及结果验证
        ComposeSection(col, "G5 纠正预防措施及结果验证", "388e3c", "e8f5e9", table =>
        {
            AppendFieldRow(table, "计划人", n.ActionPlanner ?? "", "计划日期", FormatDate(n.ActionPlanDate));
            AppendFieldRow(table, "验证人", n.ActionVerifier ?? "", "验证日期", FormatDate(n.ActionVerifyDate));
            AppendFieldRow(table, "验证结论", GetVerifyResultText(n.VerifyResult), "结果判定", n.ActionResult ?? "");
            AppendFieldSpan(table, "纠正预防措施", n.CorrectiveAction ?? "");
        });

        // 页脚审计
        col.Item().PaddingTop(8).PaddingBottom(4)
            .AlignCenter().Text(t =>
            {
                t.Span($"创建时间: {FormatDateTime(n.CreatedTime)} | 更新时间: {FormatDateTime(n.UpdatedTime)}")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);
            });
    }

    private static void ComposeSection(ColumnDescriptor col, string title, string borderColor, string bgColor, Action<TableDescriptor> buildFields)
    {
        col.Item().PaddingTop(10);

        // 区块标题
        col.Item().BorderLeft(4f).BorderColor(Color.FromHex(borderColor))
            .Background(Color.FromHex(bgColor))
            .PaddingVertical(4).PaddingHorizontal(8)
            .Text(title).FontSize(11).Bold();

        // 字段表格
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(90);
                c.RelativeColumn();
                c.ConstantColumn(90);
                c.RelativeColumn();
            });

            buildFields(table);
        });
    }

    private static void AppendFieldRow(TableDescriptor table, string label1, string value1, string label2, string value2)
    {
        table.Cell().Element(CellLabelStyle).Text(label1).FontSize(9);
        table.Cell().Element(CellValueStyle).Text(value1).FontSize(9);
        table.Cell().Element(CellLabelStyle).Text(label2).FontSize(9);
        table.Cell().Element(CellValueStyle).Text(value2).FontSize(9);
    }

    private static void AppendFieldSpan(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(CellLabelStyle).Text(label).FontSize(9);
        table.Cell().Element(CellValueSpanStyle).Text(value).FontSize(9);
    }

    // ========== 样式 ==========

    private static IContainer CellLabelStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(Colors.Grey.Medium)
            .Background(Colors.Grey.Lighten4)
            .PaddingVertical(3).PaddingHorizontal(6)
            .AlignMiddle();
    }

    private static IContainer CellValueStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(Colors.Grey.Medium)
            .PaddingVertical(3).PaddingHorizontal(6)
            .AlignMiddle();
    }

    private static IContainer CellValueSpanStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(Colors.Grey.Medium)
            .PaddingVertical(3).PaddingHorizontal(6)
            .AlignMiddle();
    }

    // ========== 枚举/格式化辅助 ==========

    private static string GetStatusText(NcrStatus status) => EnumHelper.GetDisplayName(status);

    private static string GetMaterialTypeText(MaterialType category) => EnumHelper.GetDisplayName(category);

    private static string GetDisposalMethodText(DisposalMethod? method) => method.HasValue ? EnumHelper.GetDisplayName(method.Value) : "";

    private static string GetSeverityText(SeverityLevel? severity) => severity.HasValue ? EnumHelper.GetDisplayName(severity.Value) : "";

    private static string GetResponsibilityCategoryText(ResponsibilityCategory? category) => category.HasValue ? EnumHelper.GetDisplayName(category.Value) : "";

    private static string GetVerifyResultText(VerifyResult? result) => result.HasValue ? EnumHelper.GetDisplayName(result.Value) : "";

    private static string FormatDate(DateTime? dt) => dt?.ToString("yyyy-MM-dd") ?? "";

    private static string FormatDateTime(DateTimeOffset dto) => dto.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
}
