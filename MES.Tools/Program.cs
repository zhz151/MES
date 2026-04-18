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

string[] requiredFiles = {
    "1产品标准列表.xlsx",
    "1牌号对照表.xlsx",
    "2销售员及往来单位.xlsx",
    "3销售订单聚合根.xlsx",
    "4销售订单实体.xlsx"
};

Console.WriteLine("📋 检查 Excel 文件:");
foreach (var file in requiredFiles)
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
    skipExisting));

var serviceProvider = services.BuildServiceProvider();
using var scope = serviceProvider.CreateScope();
var importService = scope.ServiceProvider.GetRequiredService<ExcelImportService>();

try
{
    Console.WriteLine("🚀 开始导入数据...\n");
    var result = await importService.ImportAllAsync(excelFolder);
    
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