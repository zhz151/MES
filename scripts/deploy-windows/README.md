# MES 部署到腾讯云 Windows Server 2022 —— 小白操作向导

> 目标：让系统跑在 `https://zhz.js.cn`，手机能装 App(PWA) + 摄像头扫码报工。
> 服务器：腾讯云 Windows Server 2022（4核 / 4GB 内存 / **60GB 系统盘 C:**，试验期暂不加数据盘），公网 IP `111.231.10.18`，域名 `zhz.js.cn`（已解析）。
> **数据库：服务器已预装 SQL Server 2022 Express（命名实例 `SQLEXPRESS`）**，直接沿用即可，无需再装 SQL。Express 自带限制（内存约 1.4GB、库≤10GB、无 SQL Agent），试验期够用，脚本已按此适配（备份改用 Windows 计划任务）。
> 目录约定：应用放 `C:\mes`、SQL 数据/备份放 `C:\MSSQL`（都在 60GB 系统盘上）。

这套脚本共 `00-07` 步。**按下面顺序做，别跳步**。标「服务器」的在腾讯云服务器上做，标「开发机」的在你写代码的电脑上做。

> **第 0 步：先把这个 `deploy-windows` 文件夹整个拷到服务器**（RDP 直接拖），放到 `C:\mes\deploy\`（下面的命令都在 `C:\mes\deploy` 里执行）。

---

## 0. 先下载这些（下载放哪都行，后续会用到）

| 软件 | 去哪下载 | 用途 |
|---|---|---|
| .NET 8（ASP.NET Core 运行时） | https://dotnet.microsoft.com/download/dotnet/8.0 → **ASP.NET Core Runtime 8.x** Windows x64 | API 运行 |
| SQL Server 2022 | ✅ 服务器已预装 Express（SQLEXPRESS），一般不需要下载/安装 | 数据库 |
| Nginx for Windows | https://nginx.org/en/download.html → **Windows** 最新 stable（≥1.25） | 网页入口 |
| NSSM | https://nssm.cc/download（zip） | 把程序变成系统服务 |
| SSL 证书 | 腾讯云控制台 → SSL 证书 → 申请免费 DV | HTTPS |

> nginx 解压后把**内容**拷到 `C:\mes\nginx\`（nginx.exe 直接在 C:\mes\nginx 下面）。
> nssm 解压后把 `nssm.exe` 放到 `C:\mes\tools\nssm\nssm.exe`。
> **证书**申请 `zhz.js.cn` 免费 DV 证书 → 下载类型选 **Nginx** → 得到 `.crt` 和 `.key` → 放到 `C:\mes\nginx\conf\ssl\`。

---

## 1. 第一次部署：编辑参数 00-params.ps1

在服务器上打开 `C:\mes\deploy\00-params.ps1`，**把末尾三处 `REPLACE_ME...` 改成强密码/随机串**（Jwt 密钥、初始管理员密码、Hangfire 口令）。这是唯一要你手填的地方。

> 别用默认值上线！改完保存。中文路径照抄即可。

## 2. 预检（在服务器上以管理员运行 01）

```powershell
cd C:\mes\deploy   # 脚本目录
powershell -ExecutionPolicy Bypass -File .\01-prepare.ps1
```

它会逐项检查：C 盘剩余空间、.NET 8、nginx、nssm、证书是否就位。有缺就按提示补，然后重跑，直到全绿。

> 试验期所有东西都放 60GB 系统盘，**磁盘空间要盯着点**（备份只留 5 天，见第 10 步）。

## 3. 确认 SQL（一般已预装，无需重装）

先确认机器上的 SQL 状态（管理员 PowerShell）：

```powershell
Get-Service | Where-Object { $_.Name -like 'MSSQL*' } | Format-Table Name, Status -AutoSize
sqlcmd -S localhost\SQLEXPRESS -E -Q "SELECT @@VERSION"
```

- ✅ **若能看到 `MSSQL$SQLEXPRESS ... Running`，且上面那条能返回版本号** → 已装好可用，**跳过本步，直接进第 4 步**。
- ⚠️ 这台目标服务器正是这种情况（预装 SQL 2022 Express / 实例 `SQLEXPRESS`）。
- ❌ 只有在**完全没有 SQL**（连 `sqlcmd` 都提示找不到服务器）时才需要装：图形向导 → 全新安装 → 只勾**数据库引擎服务** → 实例默认 → **身份验证模式选「Windows 身份验证模式」** → 装完。命名实例解析若报「找不到实例」，先 `Start-Service SQLBrowser`。

> 说明：本套脚本按 **Express + 命名实例 `SQLEXPRESS`** 写好（连接串、建库、授权都已适配）；Express 会把内存自动封顶在 ~1.4GB，库上限 10GB，试验期没问题。

## 4. 建空数据库（必须先建！）→ 运行 02

**在服务器上以管理员**运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\02-init-db.ps1
```

会自动：连通 `SQLEXPRESS` → 授权 SQL 服务账户写 `C:\MSSQL` → 锁内存（Express 自动封顶 ~1.4GB，命令只是保险）→ 建空库 `MES`（文件放 `C:\MSSQL\DATA`）。
**这是整套里最容易踩的坑**：必须先把库建出来，后面 API 启动才会自动建表。看到 `database 'MES' ready` 即成功。

## 5. 打包发布 → 在【开发机】运行 03

在你自己写代码的电脑上（装了 .NET SDK）：

```powershell
cd MES项目目录\scripts\deploy-windows
powershell -ExecutionPolicy Bypass -File .\03-publish-local.ps1
```

会自动 `dotnet publish` 前后端 + 把前端接口地址改成同源 + 打成一个 zip，**最后打印 zip 路径**。

把 zip 拷到服务器（RDP 直接拖，或用 pscp），**在服务器上解压到 `C:\mes\`**，得到：

```
C:\mes\api\   ← 里面要有 MES.Api.dll
C:\mes\web\   ← 里面要有 index.html
```

## 6. 注册 API 服务 → 在【服务器】运行 04

```powershell
powershell -ExecutionPolicy Bypass -File .\04-setup-api.ps1
```

脚本会：
- 注册 Windows 服务 `MES.API`（开机自启、崩了自动重启）
- 把所有密码/连接串写进服务环境变量（不落任何文件明文）

**看到提示后**：先**另开一个 PowerShell 窗口**，照它打印的命令前台跑一次 `dotnet MES.Api.dll`。首次启动会自动建全部表 + 初始化 46 个角色和管理员 `Admin`（密码就是你填的 Seed 密码），可能要等 30 秒~2 分钟，看到服务稳定监听、无红色报错即可 `Ctrl+C`。然后回脚本窗口按 `y`，服务自动启动。

> 前台跑是为了一次性看清建库/种子日志，别跳过。

## 7. 配置 Nginx → 在【服务器】运行 05

```powershell
powershell -ExecutionPolicy Bypass -File .\05-setup-nginx.ps1
```

自动生成 `nginx.conf`、`nginx -t` 校验、把 nginx 注册为服务并启动。看到 `nginx is running` 即成功。

## 8. 开端口（重要，很多人漏）

- **腾讯云控制台** → 该服务器 → 防火墙/安全组：放行 **80、443**（来源可先 `0.0.0.0/0`；3389 建议只留你家 IP）
- **服务器内 Windows 防火墙**：给 80/443 加放行入站规则
- 1433、7000 **不要放行**（只本机用）

## 9. 冒烟验证 → 任意能上网的电脑运行 06

```powershell
powershell -ExecutionPolicy Bypass -File .\06-verify.ps1
```

期望输出全 `OK`（首页 200 / manifest 200 / login 400=后端活着 / hangfire 401=有保护）。

然后**用手机**：浏览器打开 `https://zhz.js.cn` → 登录 Admin → 浏览器菜单「添加到主屏幕」装成 App → 扫码报工实测。Hangfire 面板 `https://zhz.js.cn/hangfire`。

## 10. 建每日备份 → 在【服务器】运行 07

Express **没有 SQL Agent**，所以备份用 **Windows 计划任务**代替，一键注册：

```powershell
powershell -ExecutionPolicy Bypass -File .\07-setup-backup.ps1
```

它会：授权系统账户能连 SQL → 注册计划任务 `MES_Backup_Daily`（每天 02:30 运行 `backup-mes.ps1`）→ **当场试跑一次**备份验证。
- 备份文件：`C:\MSSQL\Backup\MES_FULL_yyyyMMdd.bak`（压缩），自动清理 **5 天前**的
- 备份日志：`C:\mes\logs\backup.log`（可查上次是否成功）
- 手动看/试跑：开始菜单搜「任务计划程序」→ `MES_Backup_Daily` → 右键「运行」

> ⚠️ 试验期只有 60GB 系统盘，备份只留 5 天。**建议每周把 `C:\MSSQL\Backup` 里的 `.bak` 拷到别处（网盘/家里的电脑）一份**；以后加了数据盘，把 `backup-mes.ps1` 里的 `-5` 调回 `-30` 重跑一次 `07-setup-backup.ps1` 即可放宽。
> 证书到期前一个月记得去腾讯云续（一年一续），续完替换 `C:\mes\nginx\conf\ssl` 里的文件后执行：`C:\mes\nginx\nginx.exe -s reload -p C:\mes\nginx\`。

---

## 以后怎么更新（增量发布）

1. 开发机跑 `03-publish-local.ps1` 打新 zip
2. 服务器解压覆盖 `C:\mes\api`、`C:\mes\web`（覆盖前先拷一份到 `C:\mes\publish\<日期>\` 留底）
3. 含数据库变更的新版本：先 `nssm restart MES.API` 让它自动跑迁移，确认日志正常，再覆盖前端
4. `nginx` 不用重启（静态文件即时生效）

## 常见问题

| 现象 | 原因/处理 |
|---|---|
| 启动 API 报数据库连接失败/循环重启 | 库没先建 → 重跑 02；或连接串实例名不对（本机应为 `localhost\SQLEXPRESS`，见 00-params 第 5 步前确认） |
| 手机打不开 / 无锁图标 | 安全组/防火墙没放 80/443；证书没放对位置（跑 05 前确认 ssl 目录有 crt/key） |
| 登录后跳转不停 / 无限 307 | 反向代理转发头没生效——确认用的是本套 05 配置（已带 X-Forwarded-Proto）且 API 是本次改过 `UseForwardedHeaders` 的版本 |
| 上传 Excel 报 413 | `client_max_body_size 100m` 已配；确认用了生成的新 nginx.conf |
| 内存吃满 / 卡死 | Express 内存自动封顶 ~1.4GB，正常不会吃满；真卡了看 `C:\mes\logs`；Hangfire 已在代码限制 1 线程 |
| C 盘快满了 | 试验期全在 60GB 系统盘：删 `C:\MSSQL\Backup` 里过期 .bak、清 `C:\mes\logs` 旧日志；正式推广建议加数据盘 |
| 手机装不了 PWA / 扫码没画面 | 必须 HTTPS（域名证书有效）+ 首次访问需是 https；http 一律不行 |

## 文件清单

| 文件 | 作用 |
|---|---|
| `00-params.ps1` | 唯一要改的：参数 + 三个密码 |
| `01-prepare.ps1` | 预检（C 盘空间/.NET/工具/证书） |
| `02-init-db.ps1` | 锁内存 + 建空库（必须先于 API 启动） |
| `03-publish-local.ps1` | **开发机**发布打包（自动把前端接口改同源） |
| `04-setup-api.ps1` | NSSM 注册 API 服务 + 注入密钥环境变量 |
| `05-setup-nginx.ps1` | 生成 nginx.conf + 注册 nginx 服务 |
| `06-verify.ps1` | 公网冒烟 + 手机清单 |
| `07-setup-backup.ps1` | **Express 用**：一键注册每日备份计划任务（会当场试跑一次） |
| `backup-mes.ps1` | 被计划任务调用：每日压缩备份 + 清理 5 天前（日志 `C:\mes\logs\backup.log`） |
| `07-backup-job.sql` | 仅当以后升级 Standard/Developer（默认实例 + SQL Agent）时用；**Express 不适用** |
| `nginx-mes.conf.template` | nginx 配置模板 |
