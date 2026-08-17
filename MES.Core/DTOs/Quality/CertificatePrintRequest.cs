namespace MES.Core.DTOs.Quality;

/// <summary>
/// 质量证明书打印请求：按 Id 集合打印（详情页单张 / 列表页选中或全部）。
/// 空 Ids 由后端判为打印全部（列表页全量打印语义），与工艺卡打印一致。
/// </summary>
public class CertificatePrintRequest
{
    /// <summary>质保书 Id 集合；为空表示打印全部</summary>
    public int[] Ids { get; set; } = Array.Empty<int>();
}
