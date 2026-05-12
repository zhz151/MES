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
    public string Strategy { get; set; } = "skip";
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
