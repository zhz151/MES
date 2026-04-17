<#
.SYNOPSIS
    导出 MES 项目完整快照，包括所有代码文件、配置文件、项目文件
.DESCRIPTION
    扫描项目目录，将所有必要文件导出到一个快照文件中，
    支持在其他电脑上完全重建相同的项目状态
.PARAMETER ProjectPath
    项目根目录路径（包含 .sln 文件的目录）
.PARAMETER OutputFile
    输出快照文件路径
.EXAMPLE
    .\Export-MESProject.ps1
    .\Export-MESProject.ps1 -ProjectPath "E:\MES项目\MES" -OutputFile "MES-Snapshot.txt"
#>

param(
    [string]$ProjectPath = (Get-Location),
    [string]$OutputFile = (Join-Path (Get-Location) "MES-Project-Snapshot.txt")
)

# 查找 .sln 文件来确定项目根目录
$slnFile = Get-ChildItem -Path $ProjectPath -Filter "*.sln" -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
if ($slnFile) {
    $ProjectPath = $slnFile.DirectoryName
    Write-Host "找到解决方案: $($slnFile.Name)" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   MES 项目快照导出脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "项目路径: $ProjectPath" -ForegroundColor Yellow
Write-Host "输出文件: $OutputFile" -ForegroundColor Yellow
Write-Host ""

# ============================================================
# 定义要包含的文件类型（完整重建所需）
# ============================================================
$includeExtensions = @(
    # 解决方案和项目文件
    "*.sln", "*.csproj", "*.props", "*.targets",
    
    # C# 代码
    "*.cs",
    
    # Blazor 前端
    "*.razor", "*.razor.css",
    
    # 前端资源
    "*.html", "*.css", "*.js", "*.json",
    
    # 配置文件
    "appsettings.json", "appsettings.Development.json",
    
    # 其他
    "*.md", "*.txt", ".gitignore", ".editorconfig"
)

# 定义要排除的目录
$excludeDirs = @(
    "bin", "obj", "Migrations", ".vs", ".git", 
    "node_modules", "wwwroot/lib", "wwwroot/_framework",
    "publish", "Release", "Debug", "logs"
)

# 定义要排除的文件（不需要重建的）
$excludeFiles = @(
    "*.cache", "*.dll", "*.exe", "*.pdb", "*.user",
    "*.suo", "*.lock.json", "*.css.gz", "*.js.gz"
)

# ============================================================
# 收集文件
# ============================================================
Write-Host "正在扫描项目文件..." -ForegroundColor Yellow

$allFiles = @()
foreach ($ext in $includeExtensions) {
    $files = Get-ChildItem -Path $ProjectPath -Filter $ext -Recurse -File -ErrorAction SilentlyContinue
    $allFiles += $files
}

# 过滤排除目录和文件
$filteredFiles = $allFiles | Where-Object {
    $path = $_.FullName
    $exclude = $false
    
    # 检查排除目录
    foreach ($dir in $excludeDirs) {
        if ($path -match "\\$dir\\") {
            $exclude = $true
            break
        }
    }
    
    # 检查排除文件
    foreach ($pattern in $excludeFiles) {
        if ($_.Name -like $pattern) {
            $exclude = $true
            break
        }
    }
    
    -not $exclude
}

# 去重并按路径排序
$filteredFiles = $filteredFiles | Sort-Object FullName -Unique

Write-Host "找到 $($filteredFiles.Count) 个文件" -ForegroundColor Green

# ============================================================
# 收集 NuGet 包信息
# ============================================================
$packagesInfo = @()
$csprojFiles = Get-ChildItem -Path $ProjectPath -Filter "*.csproj" -Recurse -File -ErrorAction SilentlyContinue
foreach ($csproj in $csprojFiles) {
    $content = Get-Content $csproj.FullName -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
    if ($content) {
        # 提取 PackageReference - 修复正则表达式
        $pattern = '<PackageReference Include="([^"]+)" Version="([^"]+)"'
        $matches = [regex]::Matches($content, $pattern)
        foreach ($match in $matches) {
            $packagesInfo += [PSCustomObject]@{
                Project = $csproj.Directory.Name
                Package = $match.Groups[1].Value
                Version = $match.Groups[2].Value
            }
        }
    }
}

# ============================================================
# 生成快照内容
# ============================================================
Write-Host "正在生成快照..." -ForegroundColor Yellow

$output = @"
========================================
   MES 项目完整快照
========================================
生成时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
项目路径: $ProjectPath
解决方案: $(if($slnFile){$slnFile.Name}else{"未找到"})
文件总数: $($filteredFiles.Count)

========================================
   重建说明
========================================

此快照包含重建项目所需的所有文件。

重建步骤:
1. 运行 Restore-MESProject.ps1 恢复所有文件
2. 运行 dotnet restore 恢复 NuGet 包
3. 运行 dotnet build 编译项目
4. 如需数据库，运行 dotnet ef database update

========================================
   NuGet 包清单
========================================

"@

if ($packagesInfo.Count -gt 0) {
    $output += "`n项目 | 包名 | 版本`n"
    $output += "-----|------|-----`n"
    foreach ($pkg in $packagesInfo) {
        $output += "$($pkg.Project) | $($pkg.Package) | $($pkg.Version)`n"
    }
} else {
    $output += "未检测到 NuGet 包`n"
}

$output += @"

========================================
   文件清单
========================================

"@

# 添加文件清单
foreach ($file in $filteredFiles) {
    $relativePath = $file.FullName.Replace($ProjectPath, "").TrimStart('\')
    $output += "  $relativePath`n"
}

# ============================================================
# 添加每个文件的内容
# ============================================================
foreach ($file in $filteredFiles) {
    $relativePath = $file.FullName.Replace($ProjectPath, "").TrimStart('\')
    
    $output += "`n"
    $output += ("=" * 70) + "`n"
    $output += "FILE: $relativePath`n"
    $output += ("=" * 70) + "`n"
    $output += "`n"
    
    try {
        $content = Get-Content $file.FullName -Raw -Encoding UTF8 -ErrorAction Stop
        $output += $content
        if (-not $content.EndsWith("`n")) { $output += "`n" }
    }
    catch {
        $output += "[ERROR: $($_.Exception.Message)]`n"
    }
}

# 写入文件
$output | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   快照导出完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "输出文件: $OutputFile" -ForegroundColor White
$size = [math]::Round((Get-Item $OutputFile).Length / 1KB, 2)
Write-Host "文件大小: $size KB" -ForegroundColor White
Write-Host "文件数量: $($filteredFiles.Count)" -ForegroundColor White
Write-Host "NuGet 包: $($packagesInfo.Count)" -ForegroundColor White
Write-Host ""
Write-Host "后续步骤:" -ForegroundColor Yellow
Write-Host "1. 将快照文件复制到目标电脑" -ForegroundColor White
Write-Host "2. 运行 Restore-MESProject.ps1 恢复项目" -ForegroundColor White
Write-Host ""