// 文件路径: MES.Tools/Program.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MES.Data;
using MES.Tools;
using MES.Tools.Models;

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║         MES 数据导入工具 v1.0                            ║");
Console.WriteLine("║         不锈钢无缝钢管 MES 系统                          ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var connectionString = configuration.GetConnectionString("Default");
var excelFolder = configuration["Import:ExcelFolder"] ?? @"C:\ExcelData";
var skipExisting = configuration["Import:SkipExistingOrders"] == "true";

Console.WriteLine($"📁 Excel 文件夹: {excelFolder}");
Console.WriteLine($"🔗 数据库: {connectionString?.Split(';').FirstOrDefault()}");
Console.WriteLine($"⏭️ 跳过已存在订单: {(skipExisting ? "是" : "否")}");
Console.WriteLine();

// ========== 修改：使用新的文件名 ==========
var requiredFiles = new Dictionary<string, string>
{
    { "产品标准.xlsx", "ProductStandard" },
    { "牌号对照.xlsx", "GradeMapping" },
    { "客户档案.xlsx", "Customer" },
    { "销售单.xlsx", "SalesOrder" },
    { "销售项次.xlsx", "OrderItem" },
    { "技术要求.xlsx", "ProductRequirement" }
};

Console.WriteLine("📋 检查 Excel 文件:");
foreach (var file in requiredFiles.Keys)
{
    var fullPath = Path.Combine(excelFolder, file);
    Console.WriteLine($"   {(File.Exists(fullPath) ? "✅" : "❌")} {file}");
}
Console.WriteLine();

if (!Directory.Exists(excelFolder))
{
    Console.WriteLine($"❌ 文件夹不存在: {excelFolder}");
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    return;
}

var services = new ServiceCollection();
services.AddDbContext<AppDbContext>(options => 
    options.UseSqlServer(connectionString));
services.AddScoped(sp => new ExcelImportService(
    sp.GetRequiredService<AppDbContext>(), 
    skipExisting,
    excelFolder));  // 传入文件夹路径

var serviceProvider = services.BuildServiceProvider();
using var scope = serviceProvider.CreateScope();
var importService = scope.ServiceProvider.GetRequiredService<ExcelImportService>();

try
{
    Console.WriteLine("🚀 开始导入数据...\n");
    var result = await importService.ImportAllAsync();
    
    Console.WriteLine();
    result.PrintSummary();
    
    if (result.Failed > 0)
    {
        Console.WriteLine("\n⚠️ 错误详情:");
        foreach (var log in result.Logs.Where(l => l.Level == ImportLogLevel.Error))
        {
            Console.WriteLine($"   {log.Message}");
        }
    }
    
    // 输出各表统计
    Console.WriteLine("\n📊 各表统计:");
    foreach (var section in result.SectionResults)
    {
        Console.WriteLine($"   {section.Key}: 新增 {section.Value.Inserted}, 跳过 {section.Value.Skipped}, 失败 {section.Value.Failed}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ 异常: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

Console.WriteLine("\n按任意键退出...");
Console.ReadKey();