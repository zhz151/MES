namespace MES.Tools.Models;

public enum ImportLogLevel
{
    Info,
    Success,
    Warning,
    Error
}

public class ImportLogEntry
{
    public string Message { get; set; } = string.Empty;
    public ImportLogLevel Level { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class ImportResult
{
    public string Section { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<ImportLogEntry> Logs { get; set; } = new();
    public Dictionary<string, ImportResult> SectionResults { get; set; } = new();

    public void Log(string message, ImportLogLevel level = ImportLogLevel.Info)
    {
        Logs.Add(new ImportLogEntry { Message = message, Level = level });
        var prefix = level switch
        {
            ImportLogLevel.Success => "✅",
            ImportLogLevel.Warning => "⚠️",
            ImportLogLevel.Error => "❌",
            _ => "ℹ️"
        };
        Console.WriteLine($"{prefix} [{Section}] {message}");
    }

    public void Merge(ImportResult other)
    {
        if (!string.IsNullOrEmpty(other.Section))
        {
            SectionResults[other.Section] = other;
        }
        
        Inserted += other.Inserted;
        Updated += other.Updated;
        Skipped += other.Skipped;
        Failed += other.Failed;
        Logs.AddRange(other.Logs);
        if (!other.Success) Success = false;
    }

    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"📊 导入汇总: {Section}");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine($"  新增: {Inserted}");
        Console.WriteLine($"  更新: {Updated}");
        Console.WriteLine($"  跳过: {Skipped}");
        Console.WriteLine($"  失败: {Failed}");
        Console.WriteLine($"  状态: {(Success ? "✅ 成功" : "❌ 失败")}");
        Console.WriteLine(new string('=', 60));
    }
}