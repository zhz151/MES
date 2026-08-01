namespace MES.Core.Models;

/// <summary>
/// Excel导入行原始数据
/// </summary>
public class ImportRowData
{
    public int RowNumber { get; set; }
    public Dictionary<string, string> Values { get; set; } = new();
}

/// <summary>
/// 导入预览结果
/// </summary>
public class ImportPreviewResult
{
    public int TotalRows { get; set; }
    public int ValidCount { get; set; }
    public int ErrorCount { get; set; }
    public int DuplicateCount { get; set; }
    /// <summary>将新增的行数（无 ID 或业务键未命中）</summary>
    public int AddCount { get; set; }
    /// <summary>将覆盖的行数（命中已存在记录）</summary>
    public int OverwriteCount { get; set; }
    /// <summary>带 ID 但库中不存在该 ID 的行数</summary>
    public int InvalidIdCount { get; set; }
    public List<ImportRowResult> RowResults { get; set; } = new();
}

/// <summary>
/// 单行预览结果
/// </summary>
public class ImportRowResult
{
    public int RowNumber { get; set; }
    public string? Key { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool IsDuplicate { get; set; }
    public bool IsValid { get; set; }
    /// <summary>该行将被如何处理：新增 / 覆盖 / ID不存在</summary>
    public string RowAction { get; set; } = "新增";
    public ImportRowData? Data { get; set; }
}

/// <summary>
/// 导入执行结果
/// </summary>
public class ImportResult
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public bool HasRolledBack { get; set; }
    public string? RollbackReason { get; set; }
    public List<ImportRowError> Errors { get; set; } = new();
}

/// <summary>
/// 单行导入错误
/// </summary>
public class ImportRowError
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = "";
}
