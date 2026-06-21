# MES 项目持久知识

## docs 扫描完成 (2026-06-21)
- docs/ 共 43 个 md 文件，更新了 5 个：
  - 02_架构设计.md V4.3：订单上下文移除产品标准/牌号对照，新增生产标准上下文模块，实体数 35→48
  - 10_数据导入导出规范.md V1.8：实体数 46→48，EntityOrder 追加 GradeChemicalComposition/GradePhysicalProperty
  - 接口设计/数据工具接口设计.md V6.3：实体数 46→48，新增 GradeChemicalComposition/GradePhysicalProperty，StandardGradeMapping 复合键更新
- 无需变更：订单上下文(3份)、工单上下文(2份)、批次上下文(1份)、质量管理模块详细设计 — 均已正确引用"已删除/已移除"
- 生产标准上下文新增 docs (3份)：模块设计/数据库设计/接口设计 各 1 份 V1.0