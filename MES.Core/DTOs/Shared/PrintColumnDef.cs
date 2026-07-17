namespace MES.Core.DTOs.Shared;

/// <summary>
/// 打印列定义（Key=属性名, Label=显示名）
/// Width=列宽(px)，null/0=自动等宽(RelativeColumn)
/// </summary>
public class PrintColumnDef
{
    public string Key { get; set; } = "";
    public string Label { set; get; } = "";
    public int? Width { get; set; }
}
