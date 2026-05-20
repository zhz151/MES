using OfficeOpenXml;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var pgPath = @"E:\MES项目\上传下载资料\工序组.xlsx";
var targetPath = @"E:\MES项目\上传下载资料\过程检验.xlsx";

// 15个工段名称
var sectionFields = new[] { "冷轧拔", "油管断", "去油", "固溶", "矫直", "断切", "测壁厚", "酸洗", "外抛光", "内修磨", "外点磨", "检验", "打焊头", "润滑", "入库" };

// ===== 1. 读取工序组表，建立 (批次号, 工段名称) → 工序组序号 索引 =====
Console.Error.WriteLine("读取工序组.xlsx...");
var sectionIndex = new Dictionary<(string batchNo, string sectionName), string>(/*不区分大小写比较*/);
int pgRows = 0;

using (var pkg = new ExcelPackage(new FileInfo(pgPath)))
{
    var ws = pkg.Workbook.Worksheets[0];
    if (ws.Dimension == null) { Console.Error.WriteLine("工序组表为空"); return; }

    var headers = new List<string>();
    for (int c = 1; c <= ws.Dimension.Columns; c++)
        headers.Add(ws.Cells[1, c].Text.Trim());

    var batchKey = headers.FirstOrDefault(h => h.Contains("批次")) ?? "";
    var seqKey = headers.FirstOrDefault(h => h.Contains("组内序号") || h.Contains("序号")) ?? "";

    for (int r = 2; r <= ws.Dimension.Rows; r++)
    {
        var batchNo = (ws.Cells[r, headers.IndexOf(batchKey) + 1].Text ?? "").Trim();
        var seqNo = (ws.Cells[r, headers.IndexOf(seqKey) + 1].Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(batchNo) || string.IsNullOrWhiteSpace(seqNo)) continue;
        pgRows++;

        foreach (var sf in sectionFields)
        {
            var colIdx = headers.IndexOf(sf);
            if (colIdx < 0) continue;
            var val = (ws.Cells[r, colIdx + 1].Text ?? "").Trim();
            if (!string.IsNullOrEmpty(val))
            {
                var key = (batchNo, sf);
                if (!sectionIndex.ContainsKey(key))
                    sectionIndex[key] = val;  // 存工段在组内的顺序值（冷轧拔=14等）
            }
        }
    }
}
Console.Error.WriteLine($"工序组: {pgRows} 行, 工段索引: {sectionIndex.Count} 个");

// ===== 2. 打开过程检验表，写入工序序号 =====
Console.Error.WriteLine($"打开 {targetPath}...");

using (var pkg = new ExcelPackage(new FileInfo(targetPath)))
{
    var ws = pkg.Workbook.Worksheets[0];
    if (ws.Dimension == null) { Console.Error.WriteLine("目标表为空"); return; }

    var headers = new List<string>();
    for (int c = 1; c <= ws.Dimension.Columns; c++)
        headers.Add(ws.Cells[1, c].Text.Trim());
    Console.Error.WriteLine($"列名: {string.Join(", ", headers)}");

    var batchCol = headers.IndexOf("批次号") + 1;
    var sectionCol = headers.IndexOf("工段名称") + 1;
    var resultCol = headers.IndexOf("工序序号") + 1;  // 兼容旧版列名
    if (resultCol <= 0) resultCol = headers.IndexOf("组内序号") + 1;  // 新版列名

    if (batchCol <= 0 || sectionCol <= 0 || resultCol <= 0)
    {
        Console.Error.WriteLine($"找不到必需列。batchCol={batchCol}, sectionCol={sectionCol}, resultCol={resultCol}");
        return;
    }

    int matched = 0, notFound = 0, totalRows = ws.Dimension.Rows - 1;

    for (int r = 2; r <= ws.Dimension.Rows; r++)
    {
        var batchNo = (ws.Cells[r, batchCol].Text ?? "").Trim();
        var sectionName = (ws.Cells[r, sectionCol].Text ?? "").Trim();

        if (string.IsNullOrWhiteSpace(batchNo) || string.IsNullOrWhiteSpace(sectionName))
        {
            notFound++;
            continue;
        }

        var key = (batchNo, sectionName);
        if (sectionIndex.TryGetValue(key, out var seqNo))
        {
            ws.Cells[r, resultCol].Value = seqNo;
            matched++;
        }
        else
        {
            notFound++;
        }
    }

    // 打印前5行验证
    Console.Error.WriteLine("\n前5行验证:");
    for (int r = 2; r <= Math.Min(6, ws.Dimension.Rows); r++)
    {
        var batchNo = (ws.Cells[r, batchCol].Text ?? "").Trim();
        var sectionName = (ws.Cells[r, sectionCol].Text ?? "").Trim();
        var resultVal = (ws.Cells[r, resultCol].Text ?? "").Trim();
        Console.Error.WriteLine($"  {batchNo} | {sectionName} | 工序序号={resultVal}");
    }

    // 表头从"工序序号"改为"组内序号"（与模板统一）
    ws.Cells[1, resultCol].Value = "组内序号";

    pkg.Save();
    Console.Error.WriteLine($"\n完成! 匹配={matched}, 未匹配={notFound}, 总行={totalRows}");
    Console.Error.WriteLine($"文件已保存: {targetPath}");
}
