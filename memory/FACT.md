# MES 项目持久知识

## 当前规范版本
- **04_开发规范.md V8.34**（2026-05-31）— 新增 §6.4.4 分页汇总行规范 + §10.7 支数/米数/重量/批次数整数显示规则 + §10.8 百分比 1 位小数显示规则 + B33/B34/B35 检查项

## 分页汇总行规范（§6.4.4）
- 涉及支数/米数/重量/批次数的列表页，必须在 MudTable FooterContent 中添加分页汇总行
- `_summableColumnKeys` 静态 HashSet 定义所有可汇总列 Key
- `ComputePageSums()` 在 LoadDataFromServer 加载 _pageItems 后调用，反射读取类型求和
- int 类型直接 `sum.ToString()`，decimal 类型用 `((int)sum).ToString()`（§10.7 整数显示）
- 样式：`col-footer-cell`（顶部 2px 分隔线 + #FAFAFA 背景）+ `col-footer-sum`（#1565C0 蓝色字体）

## 列表页整数显示规则（§10.7）
- 支数/米数/重量/批次数四类列在列表页中必须显示为整数值
- decimal 属性用 `((int)val).ToString()`，int 属性用 `val.ToString()`
- 百分比/比率列不受此约束，使用 §10.8 规则
- 编辑/详情页保留原始精度

## 列表页百分比显示规则（§10.8）
- 所有百分比/比率列在列表页中必须使用 `ToString("F1")` 显示 1 位小数
- 如 `76.0%`、`12.5%`、`0.0%`，禁止直接插值或 G29 格式
- 仅适用于列表页，编辑/详情页保留原始精度

## SKILL.md 检查项更新
- B33：分页汇总行验证
- B34：整数显示验证
- B35：百分比显示验证
- 纪律五：声明"已完成"前必须执行 B32 + B33 + B34 + B35

## 排序筛选验证规范（§6.4.8）
- Step 2 三角验证：对每一列检查 FilterType + 后端 GetFilterContextsAsync Dictionary + 后端 ApplySorting
- 声明"已完成"的条件：5 步全部打勾，Step 2 无遗漏列
- 核心工作流纪律五：声明"已完成/符合规范"之前必须先执行 B32

## 页面状态持久化规范（§6.25）
- OnInitializedAsync 中状态恢复后必须调用 `if (savedState != null && table != null) await table.ReloadServerData();`
- 原因：Blazor WASM 首次渲染时 MudTable 的 ServerData 可能已用默认值加载，状态恢复后需重新加载
