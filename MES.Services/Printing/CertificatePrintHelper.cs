using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Core.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;

namespace MES.Services.Printing;

/// <summary>
/// 质量证明书 PDF 打印模板（QuestPDF），A4 横向。
/// 版式对齐「质量证明书详情」页面显示样式：
/// 页眉（左 Logo+公司名称 / 中标题 / 右地址+联系方式）+
/// 基本信息（置顶「证明书编号/签发日期」行 → 「基本信息」标题条 → 客户名称/产品标准/产品名称/交货状态 4 字段行）+
/// 3 张横向明细表（物料信息/化学成分/检验检测，行=子项、列=字段，白底黑字、表头跨页重复）+
/// 每页固定 4 行数据（不足补占位空行，化学/检验「标准值」参照行每页重复，>4 行时按页拆分）+
/// 页脚约整页 1/5 行高（上部说明文字、下部左备注/中盖章/右签发人三栏）。
/// 页眉/页脚/字体内容由 CertificatePrintSetting 配置表驱动，明细表字段显隐/顺序/权重由
/// CertificatePrintColumnDefinition 配置表驱动（columnDefs 为空时回退内置默认列），配置为空时回退默认值。
/// 化学成分(17列)与检验检测(21列)列较多，采用小字号（6.5 / 5.5pt）等宽排布。
/// </summary>
public static class CertificatePrintHelper
{
    /// <summary>Logo 图片最大尺寸（px）</summary>
    private const float LogoSize = 46;

    /// <summary>明细表区块标识（与 CertificatePrintColumnDefinition 配置锚点一致）</summary>
    public const string BlockBasicInfo = "BasicInfo";
    public const string BlockMaterial = "Material";
    public const string BlockChemistry = "Chemistry";
    public const string BlockInspection = "Inspection";

    /// <summary>化学元素键（C~W 共 15 项，默认顺序与种子一致）</summary>
    private static readonly string[] ChemElementKeys = { "C", "Si", "Mn", "P", "S", "Ni", "Cr", "Mo", "Cu", "N", "Nb", "Ti", "Fe", "Al", "W" };

    /// <summary>化学元素英文名（与 ChemElementKeys 一一对应，表头第二行）</summary>
    private static readonly string[] ChemElementEn = { "Carbon", "Silicon", "Manganese", "Phosphorus", "Sulfur", "Nickel", "Chromium", "Molybdenum", "Copper", "Nitrogen", "Niobium", "Titanium", "Iron", "Aluminum", "Tungsten" };

    /// <summary>每页固定数据行数（不足补占位空行，保持每页版式一致）</summary>
    private const int RowsPerPage = 4;

    /// <summary>物料信息表字段键（不含 #，占位空行补齐用）</summary>
    private static readonly string[] WarehouseKeys = { "ProductionBatchNo", "HeatNo", "SteelGrade", "Specification", "LengthDesc", "Quantity", "Meters", "Weight" };

    /// <summary>化学成分表字段键（不含 #，占位空行补齐用）</summary>
    private static readonly string[] ChemKeys = { "Element", "C", "Si", "Mn", "P", "S", "Ni", "Cr", "Mo", "Cu", "N", "Nb", "Ti", "Fe", "Al", "W" };

    /// <summary>成品检验列键（9 项，对应证书子项 Insp* 字段）</summary>
    private static readonly string[] InspectionKeys = { "Pmi", "Visual", "Dimension", "Endoscopy", "Hydro", "UnderwaterPneumatic", "EddyCurrent", "Ultrasonic", "PortDye" };

    /// <summary>理化检测列键（11 项，成对字段合并一列）</summary>
    private static readonly string[] PhysicalKeys = { "TensileStrength", "YieldRp02", "YieldRp10", "Elongation", "Hardness", "GrainSize", "Ferrite", "Expanding", "Flattening", "Intergranular", "Pitting" };

    /// <param name="gradeMappings">标准牌号映射表（按牌号→标准牌号+类别解析化学成分/物理性能标准值），可为 null 则该证书标准值行为空</param>
    /// <param name="chemicalCompositions">标准牌号化学成分表（按标准牌号+类别取各元素范围值），可为 null 则该证书标准值行为空</param>
    /// <param name="gradePhysicalProperties">牌号物理性能表（理化检测标准值行前 5 列），可为 null 则该证书对应单元格为空</param>
    /// <param name="subStandardQuickViews">子标准速览表（理化检测标准值行后 6 列 + 成品检验水压/涡流/超声波），可为 null 则该证书对应单元格为空</param>
    /// <param name="columnDefs">明细表列定义（含默认与配置覆盖，CertificateService 解析后传入）；null 或空则用内置默认 GetDefaultColumnDefs()</param>
    public static byte[] GeneratePdf(List<Certificate> certs, IReadOnlyDictionary<string, string> settings, byte[]? logoBytes = null,
        IReadOnlyList<StandardGradeMapping>? gradeMappings = null,
        IReadOnlyList<GradeChemicalComposition>? chemicalCompositions = null,
        IReadOnlyList<GradePhysicalProperty>? gradePhysicalProperties = null,
        IReadOnlyList<SubStandardQuickView>? subStandardQuickViews = null,
        IReadOnlyList<CertificatePrintColumnDef>? columnDefs = null)
    {
        var defs = columnDefs != null && columnDefs.Count > 0 ? columnDefs : GetDefaultColumnDefs();

        return Document.Create(container =>
        {
            foreach (var cert in certs)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x
                        .FontSize(GetFloat(settings, CertificatePrintKeys.PageFontSize, 9))
                        .FontFamily(GetString(settings, CertificatePrintKeys.PageFontFamily, "SimSun")));

                    page.Header().Element(h => ComposeHeader(h, settings, logoBytes));
                    page.Content().Element(c => ComposeContent(c, cert, settings, gradeMappings, chemicalCompositions, gradePhysicalProperties, subStandardQuickViews, defs));
                    page.Footer().Element(f => ComposeFooter(f, cert, settings));
                });
            }
        }).GeneratePdf();
    }

    /// <summary>
    /// 内置默认列定义（48 行：基本信息 4 / 物料信息 8 / 化学成分 16 / 检验检测 20），含中英文列名。
    /// 供 CertificateService 合并数据库覆盖列，也是配置表无数据时的打印兜底。
    /// </summary>
    public static List<CertificatePrintColumnDef> GetDefaultColumnDefs()
    {
        var defs = new List<CertificatePrintColumnDef>(48);
        void Add(string block, string key, string label, string labelEn, int idx, int weight)
            => defs.Add(new CertificatePrintColumnDef { BlockKey = block, Key = key, Label = label, LabelEn = labelEn, Visible = true, ColumnIndex = idx, ColumnWeight = weight });

        // 基本信息（4）
        Add(BlockBasicInfo, "CustomerName", "客户名称", "Customer Name", 1, 3);
        Add(BlockBasicInfo, "ProductStandard", "产品标准", "Product Standard", 2, 3);
        Add(BlockBasicInfo, "ProductName", "产品名称", "Product Name", 3, 3);
        Add(BlockBasicInfo, "DeliveryStatus", "交货状态", "Delivery Status", 4, 2);

        // 物料信息（8）
        Add(BlockMaterial, "ProductionBatchNo", "生产批号", "Batch No.", 1, 4);
        Add(BlockMaterial, "HeatNo", "炉号", "Heat No.", 2, 4);
        Add(BlockMaterial, "SteelGrade", "牌号", "Steel Grade", 3, 3);
        Add(BlockMaterial, "Specification", "规格", "Specification", 4, 4);
        Add(BlockMaterial, "LengthDesc", "长度", "Length", 5, 3);
        Add(BlockMaterial, "Quantity", "支数", "Qty", 6, 2);
        Add(BlockMaterial, "Meters", "米数", "Meters", 7, 3);
        Add(BlockMaterial, "Weight", "重量(kg)", "Weight (kg)", 8, 3);

        // 化学成分（16 = 元素 + C~W 15 元素）
        Add(BlockChemistry, "Element", "元素", "Element", 1, 2);
        for (int i = 0; i < ChemElementKeys.Length; i++)
            Add(BlockChemistry, ChemElementKeys[i], ChemElementKeys[i], ChemElementEn[i], i + 2, 2);

        // 检验检测（20 = 成品检验 9 + 理化检测 11）
        Add(BlockInspection, "Pmi", "PMI", "PMI", 1, 2);
        Add(BlockInspection, "Visual", "表检", "Visual Inspection", 2, 2);
        Add(BlockInspection, "Dimension", "尺寸", "Dimension", 3, 2);
        Add(BlockInspection, "Endoscopy", "内窥", "Endoscopy", 4, 2);
        Add(BlockInspection, "Hydro", "水压", "Hydrostatic Test", 5, 2);
        Add(BlockInspection, "UnderwaterPneumatic", "水下气压", "Underwater Pressure", 6, 2);
        Add(BlockInspection, "EddyCurrent", "涡流", "Eddy Current", 7, 2);
        Add(BlockInspection, "Ultrasonic", "超声波", "Ultrasonic Test", 8, 2);
        Add(BlockInspection, "PortDye", "端口着色", "Port Coloring", 9, 2);
        Add(BlockInspection, "TensileStrength", "抗拉强度", "Tensile Strength", 10, 3);
        Add(BlockInspection, "YieldRp02", "屈服Rp0.2", "Yield Rp0.2", 11, 3);
        Add(BlockInspection, "YieldRp10", "屈服Rp1.0", "Yield Rp1.0", 12, 3);
        Add(BlockInspection, "Elongation", "伸长率", "Elongation", 13, 3);
        Add(BlockInspection, "Hardness", "硬度", "Hardness", 14, 3);
        Add(BlockInspection, "GrainSize", "晶粒度", "Grain Size", 15, 3);
        Add(BlockInspection, "Ferrite", "铁素体", "Ferrite Content", 16, 3);
        Add(BlockInspection, "Expanding", "扩口", "Expanding Test", 17, 3);
        Add(BlockInspection, "Flattening", "压扁", "Flattening Test", 18, 3);
        Add(BlockInspection, "Intergranular", "晶间腐蚀", "Intergranular Corrosion", 19, 3);
        Add(BlockInspection, "Pitting", "点蚀", "Pitting", 20, 3);

        return defs;
    }

    // ========== 页眉：左 Logo+公司名 / 中标题 / 右地址+联系方式 ==========

    private static void ComposeHeader(IContainer container, IReadOnlyDictionary<string, string> settings, byte[]? logoBytes)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // 左：Logo + 公司名称（相对 1.6，中文第一行 + 英文第二行，可伸缩换行，避免 Logo+文本超宽布局冲突）
                row.RelativeItem(1.6f).AlignLeft().Row(inner =>
                {
                    if (logoBytes is { Length: > 0 })
                        inner.AutoItem().Width(LogoSize).Height(LogoSize).Image(logoBytes);
                    inner.RelativeItem().PaddingLeft(4).AlignMiddle().Column(cc =>
                    {
                        cc.Item().Text(GetString(settings, CertificatePrintKeys.CompanyName, string.Empty))
                            .FontSize(GetFloat(settings, CertificatePrintKeys.HeaderCompanyNameFontSize, 14)).Bold();
                        var nameEn = GetString(settings, CertificatePrintKeys.CompanyNameEn, string.Empty);
                        if (!string.IsNullOrEmpty(nameEn))
                            cc.Item().PaddingTop(1).Text(nameEn)
                                .FontSize(GetFloat(settings, CertificatePrintKeys.HeaderCompanyNameEnFontSize, 9));
                    });
                });

                // 中：标题（整体居中，权重 2.4，中文第一行 + 英文第二行）
                row.RelativeItem(2.4f).AlignCenter().AlignMiddle().Column(cc =>
                {
                    cc.Item().Text(GetString(settings, CertificatePrintKeys.HeaderTitle, "产品质量证明书"))
                        .FontSize(GetFloat(settings, CertificatePrintKeys.HeaderFontSize, 18)).Bold()
                        .FontFamily(GetString(settings, CertificatePrintKeys.HeaderFontFamily, "SimSun"));
                    var titleEn = GetString(settings, CertificatePrintKeys.HeaderTitleEn, string.Empty);
                    if (!string.IsNullOrEmpty(titleEn))
                        cc.Item().PaddingTop(1).Text(titleEn)
                            .FontSize(GetFloat(settings, CertificatePrintKeys.HeaderTitleEnFontSize, 13)).Bold()
                            .FontFamily(GetString(settings, CertificatePrintKeys.HeaderFontFamily, "SimSun"));
                });

                // 右：公司地址（中文/英文两行）+ 联系方式（右对齐，权重 1.2）
                row.RelativeItem(1.2f).AlignRight().AlignMiddle().Column(rcol =>
                {
                    var address = GetString(settings, CertificatePrintKeys.CompanyAddress, string.Empty);
                    if (!string.IsNullOrEmpty(address))
                        rcol.Item().AlignRight().Text(address)
                            .FontSize(GetFloat(settings, CertificatePrintKeys.HeaderAddressFontSize, 8));
                    var addressEn = GetString(settings, CertificatePrintKeys.CompanyAddressEn, string.Empty);
                    if (!string.IsNullOrEmpty(addressEn))
                        rcol.Item().AlignRight().Text(addressEn)
                            .FontSize(GetFloat(settings, CertificatePrintKeys.HeaderAddressEnFontSize, 8));
                    var contact = GetString(settings, CertificatePrintKeys.CompanyContact, string.Empty);
                    if (!string.IsNullOrEmpty(contact))
                        rcol.Item().AlignRight().Text(contact)
                            .FontSize(GetFloat(settings, CertificatePrintKeys.HeaderContactFontSize, 8));
                });
            });

            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    // ========== 内容：基本信息 + 3 张横向明细表 ==========

    private static void ComposeContent(IContainer container, Certificate cert, IReadOnlyDictionary<string, string> settings,
        IReadOnlyList<StandardGradeMapping>? gradeMappings, IReadOnlyList<GradeChemicalComposition>? chemicalCompositions,
        IReadOnlyList<GradePhysicalProperty>? gradePhysicalProperties, IReadOnlyList<SubStandardQuickView>? subStandardQuickViews,
        IReadOnlyList<CertificatePrintColumnDef> columnDefs)
    {
        var baseFont = GetFloat(settings, CertificatePrintKeys.PageFontSize, 9);

        // 按区块分组列定义（渲染时各自过滤 Visible + 按 ColumnIndex 排序）
        var basicDefs = columnDefs.Where(d => d.BlockKey == BlockBasicInfo).ToList();
        var materialDefs = columnDefs.Where(d => d.BlockKey == BlockMaterial).ToList();
        var chemDefs = columnDefs.Where(d => d.BlockKey == BlockChemistry).ToList();
        var inspDefs = columnDefs.Where(d => d.BlockKey == BlockInspection).ToList();

        // 按「首子项牌号 + 产品标准前缀」解析标准牌号化学成分（用于化学成分表标准值行），未命中返回 null → 标准值行为空行
        var stdValues = ResolveStandardValues(cert, gradeMappings, chemicalCompositions);

        // 解析理化检测标准值行：前 5 列（抗拉/屈服0.2/屈服1.0/延伸/硬度）取牌号物理性能，后 6 列（晶粒度~点蚀）取子标准速览
        var physStdRow = BuildPhysicalStdRow(cert, gradeMappings, gradePhysicalProperties, subStandardQuickViews);

        // 解析成品检验标准值行：子标准速览（水压/涡流/超声波 3 列能对应，其余留空）
        var inspStdRow = BuildInspectionStdRow(cert, subStandardQuickViews);

        // 每页固定 RowsPerPage 行数据：按 4 个 Item 一组分页，不足补占位空行；
        // 化学/检验表「标准值」参照行每页重复；第一页含基本信息，续页从明细表开始
        var itemCount = cert.Items.Count;
        var pageCount = Math.Max(1, (int)Math.Ceiling(itemCount / (double)RowsPerPage));

        container.Column(col =>
        {
            for (int page = 0; page < pageCount; page++)
            {
                var pageItems = cert.Items.Skip(page * RowsPerPage).Take(RowsPerPage).ToList();

                // 仅第一页显示基本信息，续页从明细表开始
                if (page == 0)
                    col.Item().Element(c => ComposeBasicInfo(c, cert, settings, basicDefs));

                col.Item().PaddingTop(GetFloat(settings, CertificatePrintKeys.SectionSpacing, 6))
                    .Element(c => ComposeDetailTable(c, "物料信息", "Material Information", materialDefs, BuildWarehouseRows(pageItems),
                        GetFloat(settings, CertificatePrintKeys.MaterialTableFontSize, baseFont - 0.5f), settings));
                col.Item().PaddingTop(GetFloat(settings, CertificatePrintKeys.SectionSpacing, 6))
                    .Element(c => ComposeDetailTable(c, "化学成分", "Chemical Composition", chemDefs, BuildChemRows(stdValues, pageItems),
                        GetFloat(settings, CertificatePrintKeys.ChemistryTableFontSize, 6.5f), settings));
                // 「检验检测」= 成品检验(9) + 理化检测(11) 合并为一张表，21 列用 5.5pt 等宽小字号
                col.Item().PaddingTop(GetFloat(settings, CertificatePrintKeys.SectionSpacing, 6))
                    .Element(c => ComposeDetailTable(c, "检验检测", "Inspection & Testing", inspDefs, BuildInspectionRows(inspStdRow, physStdRow, pageItems),
                        GetFloat(settings, CertificatePrintKeys.InspectionTableFontSize, 5.5f), settings));

                // 非最后一页：强制分页（页眉/页脚自动继承到新页）
                if (page < pageCount - 1)
                    col.Item().PageBreak();
            }
        });
    }

    /// <summary>
    /// 基本信息：置顶「证明书编号（左）/ 签发日期（右对齐）」行 → 「基本信息」标题条 →
    /// 其余 4 字段（客户名称/产品标准/产品名称/交货状态）由 basicDefs 驱动渲染
    /// （过滤 Visible、按 ColumnIndex 升序、按 ColumnWeight 设列宽、Label 取配置显示名）。
    /// </summary>
    private static void ComposeBasicInfo(IContainer container, Certificate cert, IReadOnlyDictionary<string, string> settings,
        IReadOnlyList<CertificatePrintColumnDef> basicDefs)
    {
        var baseFont = GetFloat(settings, CertificatePrintKeys.PageFontSize, 9);
        var values = BuildBasicInfoValues(cert);
        var visible = basicDefs.Where(d => d.Visible).OrderBy(d => d.ColumnIndex).ToList();

        container.Column(col =>
        {
            // 置顶行：证明书编号（左 2 列）/ 签发日期（右 2 列右对齐），左右结构「字段：值」
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    for (int i = 0; i < 4; i++) cd.RelativeColumn(1);
                });
                table.Cell().ColumnSpan(2).Padding(3).Column(c => ComposeFieldInline(c, "证明书编号", "Certificate No.", cert.CertificateNo, baseFont, settings));
                var issueDate = cert.IssueDate == default ? "-" : cert.IssueDate.ToString("yyyy-MM-dd");
                table.Cell().ColumnSpan(2).Padding(3).Column(c => ComposeFieldInline(c, "签发日期", "Issue Date", issueDate, baseFont, settings, rightAligned: true));
            });

            // 标题条：基本信息（位于证明书编号之下）
            col.Item().PaddingTop(2).Background(Colors.White)
                .Border(0.3f).BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(1).PaddingHorizontal(6)
                .Text(t =>
                {
                    t.Span("基本信息").FontSize(GetFloat(settings, CertificatePrintKeys.SectionTitleFontSize, baseFont + 1)).Bold();
                    t.Span("  ").FontSize(GetFloat(settings, CertificatePrintKeys.SectionTitleFontSize, baseFont + 1) - 1).FontColor(Colors.Black);
                    t.Span("Basic Info").FontSize(GetFloat(settings, CertificatePrintKeys.SectionTitleFontSize, baseFont + 1) - 1).FontColor(Colors.Black);
                });

            // 4 字段行：客户名称/产品标准/产品名称/交货状态（标签小字在上、值在下，按配置列）
            if (visible.Count > 0)
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        foreach (var d in visible) cd.RelativeColumn(Math.Max(1, d.ColumnWeight));
                    });
                    foreach (var d in visible)
                    {
                        var value = values.TryGetValue(d.Key, out var v) ? v : "-";
                        table.Cell().Padding(3).Column(c => ComposeField(c, d.Label, d.LabelEn, value, baseFont, settings));
                    }
                });
            }
        });
    }

    /// <summary>标签行：中文标签 + 英文标签（中文右侧，小一号灰色）同一行</summary>
    private static void ComposeField(ColumnDescriptor column, string label, string? labelEn, string value, float baseFont, IReadOnlyDictionary<string, string> settings)
    {
        var labelFont = GetFloat(settings, CertificatePrintKeys.BasicInfoLabelFontSize, baseFont - 2);
        column.Item().Text(t =>
        {
            t.Span(label).FontSize(labelFont).FontColor(Colors.Black);
            if (!string.IsNullOrEmpty(labelEn))
            {
                t.Span("  ").FontSize(labelFont - 1).FontColor(Colors.Black);
                t.Span(labelEn).FontSize(labelFont - 1).FontColor(Colors.Black);
            }
        });
        column.Item().PaddingTop(1).Text(value).FontSize(GetFloat(settings, CertificatePrintKeys.BasicInfoValueFontSize, baseFont));
    }

    /// <summary>左右结构字段：字段名（+英文）+ 冒号 + 值（同一行，值可换行）。
    /// rightAligned=true 时（右侧栏）内容整体靠右：左侧弹性占位、值用 AutoItem 不撑满。</summary>
    private static void ComposeFieldInline(ColumnDescriptor column, string label, string labelEn, string value, float baseFont, IReadOnlyDictionary<string, string> settings, bool rightAligned = false)
    {
        var labelFont = GetFloat(settings, CertificatePrintKeys.BasicInfoLabelFontSize, baseFont - 2);
        var valueFont = GetFloat(settings, CertificatePrintKeys.BasicInfoValueFontSize, baseFont);
        column.Item().Row(row =>
        {
            if (rightAligned) row.RelativeItem(); // 右侧栏：左侧弹性占位，内容整体靠右
            row.AutoItem().Text(t =>
            {
                t.Span(label).FontSize(labelFont).FontColor(Colors.Black);
                if (!string.IsNullOrEmpty(labelEn))
                {
                    t.Span(" ").FontSize(labelFont - 1).FontColor(Colors.Black);
                    t.Span(labelEn).FontSize(labelFont - 1).FontColor(Colors.Black);
                }
            });
            row.AutoItem().Text("：").FontSize(labelFont).FontColor(Colors.Black);
            if (rightAligned)
                row.AutoItem().PaddingLeft(2).Text(value).FontSize(valueFont);
            else
                row.RelativeItem().PaddingLeft(2).Text(value).FontSize(valueFont);
        });
    }

    /// <summary>
    /// 横向明细表：标题条 + 表头行（跨页重复）+ 数据行（行=子项、列=字段，白底黑字）。
    /// 列渲染受 defs 驱动：# 固定首列略窄，其余列过滤 Visible、按 ColumnIndex 升序、按 ColumnWeight 设宽。
    /// </summary>
    private static void ComposeDetailTable(IContainer container, string title, string titleEn,
        IReadOnlyList<CertificatePrintColumnDef> defs,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        float dataFontSize, IReadOnlyDictionary<string, string> settings)
    {
        var baseFont = GetFloat(settings, CertificatePrintKeys.PageFontSize, 9);
        var visible = defs.Where(d => d.Visible).OrderBy(d => d.ColumnIndex).ToList();

        container.Column(col =>
        {
            // 标题条：中文 + 英文（中文右侧，小一号灰色）同一行
            col.Item().Background(Colors.White)
                .Border(0.3f).BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(1).PaddingHorizontal(6)
                .Text(t =>
                {
                    var sectionFont = GetFloat(settings, CertificatePrintKeys.SectionTitleFontSize, baseFont + 1);
                    t.Span(title).FontSize(sectionFont).Bold();
                    if (!string.IsNullOrEmpty(titleEn))
                    {
                        t.Span("  ").FontSize(sectionFont - 1).FontColor(Colors.Black);
                        t.Span(titleEn).FontSize(sectionFont - 1).FontColor(Colors.Black);
                    }
                });

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(0.6f); // # 固定列略窄
                    foreach (var d in visible) cd.RelativeColumn(Math.Max(1, d.ColumnWeight));
                });

                var headerFont = dataFontSize + GetFloat(settings, CertificatePrintKeys.TableHeaderFontSizeDelta, 0.5f);
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("#").FontSize(headerFont).Bold().AlignCenter();
                    foreach (var d in visible)
                        header.Cell().Element(HeaderCellStyle).Column(hc =>
                        {
                            hc.Item().Text(d.Label).FontSize(headerFont).Bold().AlignCenter();
                            if (!string.IsNullOrEmpty(d.LabelEn))
                                hc.Item().PaddingTop(0.5f).Text(d.LabelEn).FontSize(headerFont - 1f).AlignCenter().FontColor(Colors.Black);
                        });
                });

                for (int ri = 0; ri < rows.Count; ri++)
                {
                    var background = Colors.White;
                    var row = rows[ri];
                    table.Cell().Element(c => DataCellStyle(c, background)).Text(row["#"]).FontSize(dataFontSize).AlignCenter();
                    foreach (var d in visible)
                    {
                        // 数据值「合格」字样右侧追加英文（中英对照显示）
                        var value = AppendQualifiedEn(row.TryGetValue(d.Key, out var v) ? v : "-");
                        table.Cell().Element(c => DataCellStyle(c, background)).Text(value).FontSize(dataFontSize).AlignCenter();
                    }
                }
            });
        });
    }

    // ========== 明细表行数据（行=子项，列=字段 Key → 值；# 为固定序号列） ==========

    private static List<IReadOnlyDictionary<string, string>> BuildWarehouseRows(IReadOnlyList<CertificateItem> pageItems)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();
        foreach (var i in pageItems)
        {
            rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["#"] = i.SeqNo.ToString(),
                ["ProductionBatchNo"] = i.ProductionBatchNo ?? "-",
                ["HeatNo"] = i.HeatNo ?? "-",
                ["SteelGrade"] = i.SteelGrade ?? "-",
                ["Specification"] = i.Specification ?? "-",
                ["LengthDesc"] = i.LengthDesc ?? "-",
                ["Quantity"] = i.Quantity?.ToString() ?? "-",
                ["Meters"] = i.Meters?.ToString("G29") ?? "-",
                ["Weight"] = i.Weight?.ToString("G29") ?? "-",
            });
        }
        // 数据行补足 RowsPerPage（保留边框、内容与序号为空）
        return PadRows(rows, WarehouseKeys, RowsPerPage);
    }

    /// <summary>
    /// 化学成分表：首行「标准值」（元素列标"标准值"，C~W 各元素填标准范围值，未命中显示 -），
    /// 其后每个子项一行（元素列留空，C~W 填实测值）。
    /// </summary>
    private static List<IReadOnlyDictionary<string, string>> BuildChemRows(IReadOnlyDictionary<string, string>? stdValues, IReadOnlyList<CertificateItem> pageItems)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();

        // 标准值行（每页重复，位于该页所有数据行之前）
        // 元素列窄，英文放不下右侧 → 中文上行 + 英文（Std. Value）下行小字，与表头 LabelEn 第二行模式一致
        var stdRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["#"] = "-", ["Element"] = "标准值\nStd. Value" };
        foreach (var key in ChemElementKeys)
            stdRow[key] = stdValues != null && stdValues.TryGetValue(key, out var v) ? v : "-";
        rows.Add(stdRow);

        // 数据行
        foreach (var i in pageItems)
        {
            rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["#"] = i.SeqNo.ToString(),
                ["Element"] = string.Empty,
                ["C"] = Fmt(i.ChemC), ["Si"] = Fmt(i.ChemSi), ["Mn"] = Fmt(i.ChemMn), ["P"] = Fmt(i.ChemP), ["S"] = Fmt(i.ChemS),
                ["Ni"] = Fmt(i.ChemNi), ["Cr"] = Fmt(i.ChemCr), ["Mo"] = Fmt(i.ChemMo), ["Cu"] = Fmt(i.ChemCu), ["N"] = Fmt(i.ChemN),
                ["Nb"] = Fmt(i.ChemNb), ["Ti"] = Fmt(i.ChemTi), ["Fe"] = Fmt(i.ChemFe), ["Al"] = Fmt(i.ChemAl), ["W"] = Fmt(i.ChemW),
            });
        }

        // 数据行补足 RowsPerPage（标准值行不计入，总行数 = RowsPerPage + 1）
        return PadRows(rows, ChemKeys, RowsPerPage + 1);
    }

    /// <summary>
    /// 检验检测表（成品检验 + 理化检测合并）：首行「标准值」（成品检验 9 值 + 理化检测 11 值），
    /// 其后每个子项一行（成品检验 Insp* 实测 + 理化检测成对字段 "/" 合并，如 600/610）。
    /// </summary>
    private static List<IReadOnlyDictionary<string, string>> BuildInspectionRows(List<string> inspStdRow, List<string> physStdRow, IReadOnlyList<CertificateItem> pageItems)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();

        // 标准值行（每页重复，位于该页所有数据行之前）
        var stdRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["#"] = "-" };
        for (int i = 0; i < InspectionKeys.Length; i++)
            stdRow[InspectionKeys[i]] = inspStdRow[i];
        for (int i = 0; i < PhysicalKeys.Length; i++)
            stdRow[PhysicalKeys[i]] = physStdRow[i];
        rows.Add(stdRow);

        // 数据行
        foreach (var i in pageItems)
        {
            rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["#"] = i.SeqNo.ToString(),
                ["Pmi"] = i.InspPMI ?? "-",
                ["Visual"] = i.InspVisual ?? "-",
                ["Dimension"] = i.InspDimension ?? "-",
                ["Endoscopy"] = i.InspEndoscopy ?? "-",
                ["Hydro"] = i.InspHydro ?? "-",
                ["UnderwaterPneumatic"] = i.InspUnderwaterPneumatic ?? "-",
                ["EddyCurrent"] = i.InspEddyCurrent ?? "-",
                ["Ultrasonic"] = i.InspUltrasonic ?? "-",
                ["PortDye"] = i.InspPortDye ?? "-",
                ["TensileStrength"] = MergePair(i.TensileStrength_1, i.TensileStrength_2),
                ["YieldRp02"] = MergePair(i.YieldRp02_1, i.YieldRp02_2),
                ["YieldRp10"] = MergePair(i.YieldRp10_1, i.YieldRp10_2),
                ["Elongation"] = MergePair(i.Elongation_1, i.Elongation_2),
                ["Hardness"] = MergePair(i.Hardness_1, i.Hardness_2),
                ["GrainSize"] = MergePair(i.GrainSize_1, i.GrainSize_2),
                ["Ferrite"] = MergePair(i.FerriteContent_1, i.FerriteContent_2),
                ["Expanding"] = i.FlaringResult ?? "-",
                ["Flattening"] = i.FlatteningResult ?? "-",
                ["Intergranular"] = i.IntergranularResult ?? "-",
                ["Pitting"] = i.PittingResult ?? "-",
            });
        }

        // 数据行补足 RowsPerPage（标准值行不计入，总行数 = RowsPerPage + 1）
        return PadRows(rows, InspectionKeys.Concat(PhysicalKeys), RowsPerPage + 1);
    }

    /// <summary>将行列表补齐到 targetCount 行：不足部分补占位空行（保留边框、内容与序号为空）</summary>
    private static List<IReadOnlyDictionary<string, string>> PadRows(List<IReadOnlyDictionary<string, string>> rows, IEnumerable<string> keys, int targetCount)
    {
        while (rows.Count < targetCount)
        {
            var blank = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in keys) blank[k] = string.Empty;
            blank["#"] = string.Empty;
            rows.Add(blank);
        }
        return rows;
    }

    /// <summary>
    /// 解析标准牌号化学成分：取首子项牌号 → 标准牌号映射（PlantGrade 或 StandardGrade 匹配）→
    /// 按产品标准前缀消歧（GB→国数、AS→美数、其他→取首个）→ 标准化学成分表按（标准牌号+类别）取值。
    /// 任一环节未命中返回 null（标准值行为空行）。
    /// </summary>
    private static IReadOnlyDictionary<string, string>? ResolveStandardValues(Certificate cert,
        IReadOnlyList<StandardGradeMapping>? gradeMappings, IReadOnlyList<GradeChemicalComposition>? chemicalCompositions)
    {
        if (gradeMappings == null || chemicalCompositions == null || cert.Items.Count == 0) return null;

        var grade = cert.Items[0].SteelGrade;
        if (string.IsNullOrWhiteSpace(grade)) return null;

        // 候选：PlantGrade 或 StandardGrade 与牌号一致的标准牌号+类别
        var candidates = gradeMappings
            .Where(m => string.Equals(m.PlantGrade, grade, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(m.StandardGrade, grade, StringComparison.OrdinalIgnoreCase))
            .Select(m => (m.StandardGrade, m.StandardGradeCategory))
            .ToList();
        if (candidates.Count == 0) return null;

        // 产品标准前缀消歧：GB→国数、AS→美数、其他（EN 等）无歧义取首个
        var prefix = cert.ProductStandard?.Trim();
        if (!string.IsNullOrEmpty(prefix) && prefix.StartsWith("GB", StringComparison.OrdinalIgnoreCase))
            candidates = candidates.Where(c => c.StandardGradeCategory?.StartsWith("国数") == true).ToList();
        else if (!string.IsNullOrEmpty(prefix) && prefix.StartsWith("AS", StringComparison.OrdinalIgnoreCase))
            candidates = candidates.Where(c => c.StandardGradeCategory?.StartsWith("美数") == true).ToList();
        if (candidates.Count == 0) return null;

        var target = candidates.First();
        var chem = chemicalCompositions.FirstOrDefault(c =>
            string.Equals(c.StandardGrade, target.StandardGrade, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.StandardGradeCategory, target.StandardGradeCategory, StringComparison.OrdinalIgnoreCase));
        if (chem == null) return null;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["C"] = chem.Carbon ?? "-",
            ["Si"] = chem.Silicon ?? "-",
            ["Mn"] = chem.Manganese ?? "-",
            ["P"] = chem.Phosphorus ?? "-",
            ["S"] = chem.Sulfur ?? "-",
            ["Ni"] = chem.Nickel ?? "-",
            ["Cr"] = chem.Chromium ?? "-",
            ["Mo"] = chem.Molybdenum ?? "-",
            ["Cu"] = chem.Copper ?? "-",
            ["N"] = chem.Nitrogen ?? "-",
            ["Nb"] = chem.Niobium ?? "-",
            ["Ti"] = chem.Titanium ?? "-",
            ["Fe"] = chem.Iron ?? "-",
            ["Al"] = chem.Aluminum ?? "-",
            ["W"] = chem.Tungsten ?? "-",
        };
    }

    /// <summary>
    /// 解析成品检验标准值行（9 列，与 InspectionKeys 顺序一一对应）：
    /// 取子标准速览——标准号与产品标准去空白归一匹配；子标准速览仅「水压→HydrostaticTest / 涡流→EddyCurrent / 超声波→UltrasonicTest」
    /// 3 列能对应，其余 6 列（PMI/表检/尺寸/内窥/水下气压/端口着色）无对应字段留空。
    /// </summary>
    private static List<string> BuildInspectionStdRow(Certificate cert, IReadOnlyList<SubStandardQuickView>? subStandardQuickViews)
    {
        var quick = FindQuickView(cert, subStandardQuickViews);

        var row = new List<string>();
        // PMI / 表检 / 尺寸 / 内窥：无对应字段，留空
        row.AddRange(Enumerable.Repeat("-", 4));
        row.Add(quick != null ? Nz(quick.HydrostaticTest) : "-");   // 水压
        row.Add("-");                                                // 水下气压：无对应字段
        row.Add(quick != null ? Nz(quick.EddyCurrent) : "-");       // 涡流
        row.Add(quick != null ? Nz(quick.UltrasonicTest) : "-");    // 超声波
        row.Add("-");                                                // 端口着色：无对应字段
        return row;
    }

    /// <summary>
    /// 解析理化检测标准值行（11 列，与 PhysicalKeys 顺序一一对应）：
    /// 前 5 列（抗拉强度/屈服0.2/屈服1.0/延伸率/硬度）取牌号物理性能——与化学成分共用匹配链
    /// （首子项牌号 → 标准牌号映射 → 产品标准前缀消歧 GB→国数/AS→美数/其他取首个 → 性能表），硬度取洛氏/维氏/布氏第一个非空；
    /// 后 6 列（晶粒度/铁素体/扩口/压扁/晶间腐蚀/点蚀）取子标准速览——标准号与产品标准去空白归一匹配。
    /// 任一数据源未命中仅置空对应单元格，不阻断其余列。
    /// </summary>
    private static List<string> BuildPhysicalStdRow(Certificate cert,
        IReadOnlyList<StandardGradeMapping>? gradeMappings,
        IReadOnlyList<GradePhysicalProperty>? gradePhysicalProperties,
        IReadOnlyList<SubStandardQuickView>? subStandardQuickViews)
    {
        var row = new List<string>();

        // ===== 前 5 列：牌号物理性能 =====
        var grade = cert.Items.Count > 0 ? cert.Items[0].SteelGrade : null;
        GradePhysicalProperty? phys = null;
        if (gradeMappings != null && gradePhysicalProperties != null && !string.IsNullOrWhiteSpace(grade))
        {
            var candidates = gradeMappings
                .Where(m => string.Equals(m.PlantGrade, grade, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(m.StandardGrade, grade, StringComparison.OrdinalIgnoreCase))
                .Select(m => (m.StandardGrade, m.StandardGradeCategory))
                .ToList();
            if (candidates.Count > 0)
            {
                var prefix = cert.ProductStandard?.Trim();
                if (!string.IsNullOrEmpty(prefix) && prefix.StartsWith("GB", StringComparison.OrdinalIgnoreCase))
                    candidates = candidates.Where(c => c.StandardGradeCategory?.StartsWith("国数") == true).ToList();
                else if (!string.IsNullOrEmpty(prefix) && prefix.StartsWith("AS", StringComparison.OrdinalIgnoreCase))
                    candidates = candidates.Where(c => c.StandardGradeCategory?.StartsWith("美数") == true).ToList();
                if (candidates.Count > 0)
                {
                    var target = candidates.First();
                    phys = gradePhysicalProperties.FirstOrDefault(p =>
                        string.Equals(p.StandardGrade, target.StandardGrade, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.StandardGradeCategory, target.StandardGradeCategory, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        if (phys != null)
        {
            row.Add(Nz(phys.TensileStrength));
            row.Add(Nz(phys.YieldStrength02));
            row.Add(Nz(phys.YieldStrength10));
            row.Add(Nz(phys.Elongation));
            // 硬度：按 洛氏(HRB/HRC) → 维氏(HV) → 布氏(HB) 顺序取第一个非空
            row.Add(FirstNonEmpty(phys.HardnessRockwell, phys.HardnessVickers, phys.HardnessBrinell));
        }
        else
        {
            row.AddRange(Enumerable.Repeat("-", 5));
        }

        // ===== 后 6 列：子标准速览（标准号去空白归一匹配） =====
        var quick = FindQuickView(cert, subStandardQuickViews);

        if (quick != null)
        {
            row.Add(Nz(quick.GrainSize));
            row.Add(Nz(quick.FerriteContent));
            row.Add(Nz(quick.ExpandingTest));
            row.Add(Nz(quick.FlatteningTest));
            row.Add(Nz(quick.IntergranularCorrosion));
            row.Add(Nz(quick.PittingCorrosion));
        }
        else
        {
            row.AddRange(Enumerable.Repeat("-", 6));
        }

        return row;
    }

    /// <summary>
    /// 子标准速览匹配：标准号与产品标准去掉所有空白后不区分大小写比较
    /// （真库存在 GB/T14976-2025 无空格 与 GB/T 14976-2025 有空格 的差异，需归一）。
    /// </summary>
    private static SubStandardQuickView? FindQuickView(Certificate cert, IReadOnlyList<SubStandardQuickView>? subStandardQuickViews)
    {
        if (subStandardQuickViews == null || string.IsNullOrWhiteSpace(cert.ProductStandard)) return null;
        var prodStd = cert.ProductStandard!.Replace(" ", "").Replace("\t", "");
        return subStandardQuickViews.FirstOrDefault(q =>
            string.Equals(q.StandardNo?.Replace(" ", "").Replace("\t", ""), prodStd, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>空值归一为 "-"（子标准速览空值以 "-" 占位时原样返回）</summary>
    private static string Nz(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    /// <summary>按顺序取第一个非空字符串，全空返回 "-"</summary>
    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
            if (!string.IsNullOrWhiteSpace(v)) return v;
        return "-";
    }

    // ========== 基本信息字段 ==========

    /// <summary>基本信息 4 字段值（FieldKey → 值，与 CertificatePrintColumnDef.Key 锚点一致）</summary>
    private static Dictionary<string, string> BuildBasicInfoValues(Certificate cert)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CustomerName"] = cert.CustomerName ?? "-",
            ["ProductStandard"] = cert.ProductStandard ?? "-",
            ["ProductName"] = cert.ProductName ?? "-",
            ["DeliveryStatus"] = DeliveryStatusText(cert.DeliveryStatus),
        };
    }

    // ========== 页脚（约整页 1/5 行高） ==========

    private static void ComposeFooter(IContainer container, Certificate cert, IReadOnlyDictionary<string, string> settings)
    {
        // 页脚（装饰槽 After，不可分页）：上部说明文字一行（居中），下部三块（左备注 / 中盖章中英 / 右签发人两行），
        // 块间以竖线分隔区分区域。⚠️ QuestPDF 页脚装饰槽不得无界/固定大高度 + Extend（触发 Decoration 冲突）：
        //    故用自然高度 + 适度 Padding 撑出视觉高度，不设 Height/MinHeight/Extend。
        var remark = string.IsNullOrWhiteSpace(cert.Remark) ? string.Empty : cert.Remark;
        var footerTextFont = GetFloat(settings, CertificatePrintKeys.FooterTextFontSize, 8);

        container.Column(col =>
        {
            // 第 1 行：对本质量证明书的说明（居中）（页眉下方已有横线，页脚顶部不再加隔离线）
            col.Item().PaddingTop(10).AlignCenter().Text(GetString(settings, CertificatePrintKeys.FooterStatement, string.Empty))
                .FontSize(GetFloat(settings, CertificatePrintKeys.FooterStatementFontSize, 8));

            // 第 2 行：三块（备注左 / 盖章中 / 签发人右），块间竖线分隔
            col.Item().PaddingTop(24).PaddingBottom(6).Row(row =>
            {
                // 左：备注（= 证明书表中备注字段数据，左上位置）
                row.RelativeItem().Column(rc =>
                {
                    rc.Item().Text(GetString(settings, CertificatePrintKeys.FooterRemark, "备注：")).FontSize(footerTextFont).Bold();
                    rc.Item().PaddingTop(2).Text(remark).FontSize(footerTextFont);
                });

                // 中：质量检验专用章（中文 + 英文两行居中）
                row.RelativeItem().AlignCenter().Column(cc =>
                {
                    cc.Item().Text(GetString(settings, CertificatePrintKeys.SealText, string.Empty)).FontSize(footerTextFont).Bold();
                    var sealEn = GetString(settings, CertificatePrintKeys.SealTextEn, string.Empty);
                    if (!string.IsNullOrEmpty(sealEn))
                        cc.Item().PaddingTop(2).Text(sealEn).FontSize(footerTextFont - 1).FontColor(Colors.Black);
                });

                // 右：签发人两行（检验员 / 签发工程师），块内靠左
                row.RelativeItem().AlignLeft().Column(sc =>
                {
                    sc.Item().Text(GetString(settings, CertificatePrintKeys.InspectorText, "检验员：________________")).FontSize(footerTextFont);
                    // 检验员/工程师两行适当隔开，留出签字空间
                    sc.Item().PaddingTop(14).Text(GetString(settings, CertificatePrintKeys.SignerText, "工程师：________________")).FontSize(footerTextFont);
                });
            });
        });
    }

    // ========== 单元格样式 ==========

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.White)
            .PaddingVertical(1).PaddingHorizontal(2)
            .AlignMiddle();
    }

    private static IContainer DataCellStyle(IContainer container, string background)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .Background(background)
            .PaddingVertical(1).PaddingHorizontal(2)
            .AlignMiddle();
    }

    // ========== 辅助 ==========

    private static string Fmt(decimal? value) => value?.ToString("G29") ?? "-";

    /// <summary>合并成对检验值（如抗拉强度₁/抗拉强度₂）为 "值1/值2"；单侧有值只显单值；两侧皆空显 "-"</summary>
    private static string MergePair(string? a, string? b)
    {
        var x = string.IsNullOrWhiteSpace(a) ? null : a;
        var y = string.IsNullOrWhiteSpace(b) ? null : b;
        if (x == null && y == null) return "-";
        if (x == null) return y!;
        if (y == null) return x;
        return $"{x}/{y}";
    }

    private static string MergePair(decimal? a, decimal? b) => MergePair(a?.ToString("G29"), b?.ToString("G29"));

    private static string DeliveryStatusText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        // 交货状态：中文 +（枚举英文名）右侧，中英对照显示
        if (Enum.TryParse<DeliveryState>(value, out var ds))
        {
            var cn = EnumHelper.GetDisplayName(ds);
            return string.IsNullOrWhiteSpace(cn) ? value : $"{cn} {value}";
        }
        return value;
    }

    /// <summary>「合格」字样数据值在中文右侧追加英文（检验检测数据值中英对照显示）</summary>
    private static string AppendQualifiedEn(string value)
        => value == "合格" ? "合格 Qualified" : value;

    /// <summary>配置字符串取值：settings 中缺项或空白值回退默认（页眉/页脚文案/字体族）</summary>
    private static string GetString(IReadOnlyDictionary<string, string>? settings, string key, string defaultValue)
        => settings != null && settings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : defaultValue;

    /// <summary>配置数字解析：settings 中缺项或非法值回退默认（字号非法不阻断打印）</summary>
    private static float GetFloat(IReadOnlyDictionary<string, string>? settings, string key, float defaultValue)
        => settings != null && settings.TryGetValue(key, out var v) && float.TryParse(v, out var f) ? f : defaultValue;
}
