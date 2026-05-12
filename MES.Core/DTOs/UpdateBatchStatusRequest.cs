using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

/// <summary>
/// 更新批次状态请求
/// </summary>
public class UpdateBatchStatusRequest
{
    [Required(ErrorMessage = "状态不能为空")]
    public string Status { get; set; } = null!;

    [Required(ErrorMessage = "RowVersion不能为空")]
    public byte[] RowVersion { get; set; } = null!;
}
