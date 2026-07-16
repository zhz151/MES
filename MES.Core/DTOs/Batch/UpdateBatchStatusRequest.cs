using System.ComponentModel.DataAnnotations;

using MES.Core.Enums;

namespace MES.Core.DTOs.Batch;

/// <summary>
/// 更新批次状态请求
/// </summary>
public class UpdateBatchStatusRequest
{
    [Required(ErrorMessage = "状态不能为空")]
    public BatchStatus Status { get; set; }

    [Required(ErrorMessage = "RowVersion不能为空")]
    public byte[] RowVersion { get; set; } = null!;
}
