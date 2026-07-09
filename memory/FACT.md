# MES 项目持久知识

## StandardRegister（标准号）模块
...

## 日期范围搜索规范（§6.30.2）
- 理化检测等列表页模糊搜索右侧搭配日期起止搜索栏
- MudGrid: 模糊搜索 md="6" + 日期从 md="3" + 日期至 md="3"
- 日期字段用 `MudTextField T="string"`（禁止 MudDatePicker）+ `Placeholder="yyyy-MM-dd"`
- CS: `_dateFrom`/`_dateTo` + `OnDateFromChanged`/`OnDateToChanged` 事件
- PageState Extras 持久化：extras["dateFrom"]/extras["dateTo"]
- 文档 V9.23 / SKILL.md V9.11

## 文档同步规则
- 修改 04_开发规范.md 后必须同步更新 SKILL.md（行8明确注明）
- SKILL.md 位于 `C:\Users\86139\AppData\Roaming\CherryStudio\Data\Skills\mes-code-check\SKILL.md`