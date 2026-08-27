using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Shared;
using MES.Core.Models;

namespace MES.Core.Interfaces.Quality;

/// <summary>
/// 质量证明书服务接口
/// </summary>
public interface ICertificateService
{
    /// <summary>分页查询质保书列表</summary>
    Task<PagedResult<CertificateDto>> GetAllAsync(QueryParams query);

    /// <summary>获取质保书详情（含子项）</summary>
    Task<CertificateDetailDto?> GetByIdAsync(int id);

    /// <summary>创建质保书</summary>
    Task<CertificateDetailDto> CreateAsync(CertificateCreateRequest request);

    /// <summary>更新质保书</summary>
    Task<CertificateDetailDto> UpdateAsync(int id, CertificateUpdateRequest request);

    /// <summary>删除质保书</summary>
    Task DeleteAsync(int id);

    /// <summary>获取筛选上下文（各列的 DISTINCT 值，用于 ExcelFilter）</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>获取下一个质保书编号（如 SO20240714001-02）</summary>
    Task<string> GetNextCertificateNoAsync(string orderNo);

    /// <summary>
    /// 自动填充检验数据 — 根据炉号+生产批号查询化学分析/成品检验/拉伸检验的最新记录
    /// </summary>
    Task<List<CertificateItemDto>> AutoFillInspectionDataAsync(List<AutoFillInspectionItem> items);

    /// <summary>打印 PDF：按 Id 集合查质保书（含子项）渲染质量证明书模板，返回 PDF 字节</summary>
    Task<byte[]> PrintFileAsync(CertificatePrintRequest request);

    /// <summary>打印选中列表（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    Task<byte[]> PrintCertificateListAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns);
}
