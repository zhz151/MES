namespace MES.Core.Constants;

/// <summary>
/// 质量证明书打印配置键（CertificatePrintSetting 配置表 Key），
/// 默认值与 CertificatePrintHelper 打印模板硬编码值保持一致。
/// </summary>
public static class CertificatePrintKeys
{
    // ========== 页眉：企业信息 ==========

    /// <summary>公司名称（页眉左侧，Logo 旁）</summary>
    public const string CompanyName = "CompanyName";

    /// <summary>公司名称（英文，页眉左侧，中文下方第二行）</summary>
    public const string CompanyNameEn = "CompanyNameEn";

    /// <summary>公司地址（页眉右侧）</summary>
    public const string CompanyAddress = "CompanyAddress";

    /// <summary>公司地址（英文，页眉右侧，中文下方第二行）</summary>
    public const string CompanyAddressEn = "CompanyAddressEn";

    /// <summary>联系方式（页眉右侧，公司地址下方）</summary>
    public const string CompanyContact = "CompanyContact";

    /// <summary>Logo 图片路径（相对后端 wwwroot，如 images/certificate-logo.png）</summary>
    public const string CompanyLogoPath = "CompanyLogoPath";

    // ========== 页眉：标题 ==========

    /// <summary>页眉标题（默认「产品质量证明书」）</summary>
    public const string HeaderTitle = "HeaderTitle";

    /// <summary>页眉标题（英文，中文下方第二行）</summary>
    public const string HeaderTitleEn = "HeaderTitleEn";

    // ========== 页脚 ==========

    /// <summary>页脚第 1 行：对本质量证明书的说明文字</summary>
    public const string FooterStatement = "FooterStatement";

    /// <summary>页脚第 2 行左侧：备注说明</summary>
    public const string FooterRemark = "FooterRemark";

    /// <summary>页脚第 2 行中间：盖章区域文字（中文）</summary>
    public const string SealText = "SealText";

    /// <summary>页脚第 2 行中间：盖章区域文字（英文，中文下方第二行）</summary>
    public const string SealTextEn = "SealTextEn";

    /// <summary>页脚第 2 行右侧：签发工程师签字区域文字（第 2 行）</summary>
    public const string SignerText = "SignerText";

    /// <summary>页脚第 2 行右侧：检验员签字区域文字（第 1 行）</summary>
    public const string InspectorText = "InspectorText";

    // ========== 字体/字号 ==========

    /// <summary>正文字体族（页面默认字体）</summary>
    public const string PageFontFamily = "PageFontFamily";

    /// <summary>正文字号（页面默认字号）</summary>
    public const string PageFontSize = "PageFontSize";

    /// <summary>主标题字体族（页眉标题）</summary>
    public const string HeaderFontFamily = "HeaderFontFamily";

    /// <summary>主标题字号（页眉标题）</summary>
    public const string HeaderFontSize = "HeaderFontSize";

    // ========== 页眉：细分子号（默认值 = 原模板硬编码值） ==========

    /// <summary>公司名称字号（页眉左侧 Logo 旁中文）</summary>
    public const string HeaderCompanyNameFontSize = "HeaderCompanyNameFontSize";

    /// <summary>公司名称字号（英文，中文下方第二行）</summary>
    public const string HeaderCompanyNameEnFontSize = "HeaderCompanyNameEnFontSize";

    /// <summary>公司地址字号（页眉右侧中文）</summary>
    public const string HeaderAddressFontSize = "HeaderAddressFontSize";

    /// <summary>公司地址字号（英文，中文下方第二行）</summary>
    public const string HeaderAddressEnFontSize = "HeaderAddressEnFontSize";

    /// <summary>联系方式字号（页眉右侧）</summary>
    public const string HeaderContactFontSize = "HeaderContactFontSize";

    /// <summary>页眉英文标题字号（中文标题下方第二行，原为标题字号-5 派生）</summary>
    public const string HeaderTitleEnFontSize = "HeaderTitleEnFontSize";

    // ========== 内容：细分子号（默认值 = 原模板硬编码/派生值） ==========

    /// <summary>基本信息标签字号（标签小字在值上方）</summary>
    public const string BasicInfoLabelFontSize = "BasicInfoLabelFontSize";

    /// <summary>基本信息值字号</summary>
    public const string BasicInfoValueFontSize = "BasicInfoValueFontSize";

    /// <summary>区块标题条字号（基本信息标题条 + 三张明细表标题条共用）</summary>
    public const string SectionTitleFontSize = "SectionTitleFontSize";

    /// <summary>物料信息表内容字号</summary>
    public const string MaterialTableFontSize = "MaterialTableFontSize";

    /// <summary>化学成分表内容字号（16 列宽表，默认小于正文）</summary>
    public const string ChemistryTableFontSize = "ChemistryTableFontSize";

    /// <summary>检验检测表内容字号（21 列宽表，默认小于正文）</summary>
    public const string InspectionTableFontSize = "InspectionTableFontSize";

    /// <summary>明细表表头相对内容字号增量（表头 = 表内容字号 + 该增量）</summary>
    public const string TableHeaderFontSizeDelta = "TableHeaderFontSizeDelta";

    /// <summary>区块间距（基本信息与三张明细表之间的顶部间距，三处共用）</summary>
    public const string SectionSpacing = "SectionSpacing";

    // ========== 页脚：细分子号（默认值 = 原模板硬编码值） ==========

    /// <summary>页脚第 1 行说明文字字号</summary>
    public const string FooterStatementFontSize = "FooterStatementFontSize";

    /// <summary>页脚第 2 行三栏字号（备注/盖章/签发人）</summary>
    public const string FooterTextFontSize = "FooterTextFontSize";
}
