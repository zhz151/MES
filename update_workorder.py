# -*- coding: utf-8 -*-
import codecs

path = r"E:\MES项目\MES\MES.Blazor\Pages\WorkOrders\WorkOrderExecution.razor.cs"

with codecs.open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# =========================================================
# TASK 1: Update GroupKey values (two-phase temporary mark)
# =========================================================
# Phase 1: Replace GroupKey = N, with unique temp markers
phase1 = {
    'GroupKey = 1,':  'GroupKey = __G01__,',
    'GroupKey = 2,':  'GroupKey = __G03__,',
    'GroupKey = 3,':  'GroupKey = __G04__,',
    'GroupKey = 4,':  'GroupKey = __G05__,',
    'GroupKey = 5,':  'GroupKey = __G06__,',
    'GroupKey = 6,':  'GroupKey = __G07__,',
    'GroupKey = 7,':  'GroupKey = __G08__,',
    'GroupKey = 8,':  'GroupKey = __G09__,',
    'GroupKey = 9,':  'GroupKey = __G10__,',
    'GroupKey = 11,': 'GroupKey = __G11__,',
    'GroupKey = 12,': 'GroupKey = __G13__,',
    'GroupKey = 13,': 'GroupKey = __G14__,',
    'GroupKey = 14,': 'GroupKey = __G12__,',
    'GroupKey = 15,': 'GroupKey = __G19__,',
    'GroupKey = 16,': 'GroupKey = __G20__,',
    'GroupKey = 17,': 'GroupKey = __G18__,',
    'GroupKey = 18,': 'GroupKey = __G15__,',
    'GroupKey = 19,': 'GroupKey = __G16__,',
    'GroupKey = 20,': 'GroupKey = __G02__,',
    'GroupKey = 21,': 'GroupKey = __G17__,',
}

for old, new in phase1.items():
    assert old in content, f"Phase1: '{old}' not found!"
    content = content.replace(old, new)

print("Phase 1 complete.")

# Phase 2: Replace temp markers with final GroupKey values
phase2 = {
    'GroupKey = __G01__,': 'GroupKey = 1,',
    'GroupKey = __G02__,': 'GroupKey = 2,',
    'GroupKey = __G03__,': 'GroupKey = 3,',
    'GroupKey = __G04__,': 'GroupKey = 4,',
    'GroupKey = __G05__,': 'GroupKey = 5,',
    'GroupKey = __G06__,': 'GroupKey = 6,',
    'GroupKey = __G07__,': 'GroupKey = 7,',
    'GroupKey = __G08__,': 'GroupKey = 8,',
    'GroupKey = __G09__,': 'GroupKey = 9,',
    'GroupKey = __G10__,': 'GroupKey = 10,',
    'GroupKey = __G11__,': 'GroupKey = 11,',
    'GroupKey = __G12__,': 'GroupKey = 12,',
    'GroupKey = __G13__,': 'GroupKey = 13,',
    'GroupKey = __G14__,': 'GroupKey = 14,',
    'GroupKey = __G15__,': 'GroupKey = 15,',
    'GroupKey = __G16__,': 'GroupKey = 16,',
    'GroupKey = __G17__,': 'GroupKey = 17,',
    'GroupKey = __G18__,': 'GroupKey = 18,',
    'GroupKey = __G19__,': 'GroupKey = 19,',
    'GroupKey = __G20__,': 'GroupKey = 20,',
}

for old, new in phase2.items():
    assert old in content, f"Phase2: '{old}' not found!"
    content = content.replace(old, new)

print("Phase 2 complete.")

# =========================================================
# TASK 2: Update variable comment headers
# =========================================================
task2 = [
    ('// G2: 用料计划',        '// G3: 用料计划'),
    ('// G3: 圆棒穿孔计划',    '// G4: 圆棒穿孔计划'),
    ('// G4: 荒管采购计划',    '// G5: 荒管采购计划'),
    ('// G5: 成品采购计划',    '// G6: 成品采购计划'),
    ('// G6: 库存使用计划',    '// G7: 库存使用计划'),
    ('// G7: 库料改制计划',    '// G8: 库料改制计划'),
    ('// G8: 在产改制计划',    '// G9: 在产改制计划'),
    ('// G9: 在产主工单计划',  '// G10: 在产主工单计划'),
    ('// G14: 有效流转',       '// G12: 有效流转'),
    ('// G12: 合格流转',       '// G13: 合格流转'),
    ('// G18: 成品入库',       '// G15: 成品入库'),
    ('// G19: 实时关注',       '// G16: 实时关注'),
    ('// G21: 在产节点待量',   '// G17: 在产节点待量'),
    ('// G17: 汇总不合格',     '// G18: 汇总不合格'),
    ('// G15: 过程不合格',     '// G19: 过程不合格'),
    ('// G16: 成检不合格',     '// G20: 成检不合格'),
    ('// G8: 工单需求调整',    '// G2: 工单需求调整'),
    ('// G20: 返整执行数据',   '// G14: 返整执行数据'),
    ('// G18: 原始投料',       '// G11: 原始投料'),
    ('// G19: 合格流转',       '// G13: 合格流转'),
    ('// G21: 有效流转',       '// G12: 有效流转'),
    ('// G3: 过程不合格',      '// G19: 过程不合格'),
    ('// G4: 成检不合格',      '// G20: 成检不合格'),
    ('// G5: 汇总不合格',      '// G18: 汇总不合格'),
    ('// G6: 成品入库',        '// G15: 成品入库'),
    ('// G7: 实时关注',        '// G16: 实时关注'),
    ('// G9: 在产节点待量',    '// G17: 在产节点待量'),
]

for old, new in task2:
    if old in content:
        content = content.replace(old, new)
        print(f"  Replaced: {old}")
    else:
        print(f"  WARNING: Not found: {old}")

print("Task 2 complete.")

# =========================================================
# TASK 3: Update assembly order comments (all.AddRange)
# =========================================================
task3 = [
    ('    all.AddRange(g13);   // G20: 工单需求调整', '    all.AddRange(g13);   // G2: 工单需求调整'),
    ('    all.AddRange(g2);    // G2: 用料计划',      '    all.AddRange(g2);    // G3: 用料计划'),
    ('    all.AddRange(g15);   // G3: 圆棒穿孔',      '    all.AddRange(g15);   // G4: 圆棒穿孔'),
    ('    all.AddRange(g16);   // G4: 荒管采购',      '    all.AddRange(g16);   // G5: 荒管采购'),
    ('    all.AddRange(g17);   // G5: 成品采购',      '    all.AddRange(g17);   // G6: 成品采购'),
    ('    all.AddRange(g18);   // G6: 库存使用',      '    all.AddRange(g18);   // G7: 库存使用'),
    ('    all.AddRange(g19);   // G7: 库料改制',      '    all.AddRange(g19);   // G8: 库料改制'),
    ('    all.AddRange(g20);   // G8: 在产改制',      '    all.AddRange(g20);   // G9: 在产改制'),
    ('    all.AddRange(g21);   // G9: 在产主工单',    '    all.AddRange(g21);   // G10: 在产主工单'),
    ('    all.AddRange(g7);    // G14: 有效流转',     '    all.AddRange(g7);    // G12: 有效流转'),
    ('    all.AddRange(g4);    // G12: 合格流转',     '    all.AddRange(g4);    // G13: 合格流转'),
    ('    all.AddRange(g6);    // G13: 返整执行',     '    all.AddRange(g6);    // G14: 返整执行'),
    ('    all.AddRange(g11);   // G18: 成品入库',     '    all.AddRange(g11);   // G15: 成品入库'),
    ('    all.AddRange(g12);   // G19: 实时关注',     '    all.AddRange(g12);   // G16: 实时关注'),
    ('    all.AddRange(g14);   // G21: 在产节点待量',  '    all.AddRange(g14);   // G17: 在产节点待量'),
    ('    all.AddRange(g10);   // G17: 汇总不合格',   '    all.AddRange(g10);   // G18: 汇总不合格'),
    ('    all.AddRange(g8);    // G15: 过程不合格',   '    all.AddRange(g8);    // G19: 过程不合格'),
    ('    all.AddRange(g9);    // G16: 成检不合格',   '    all.AddRange(g9);    // G20: 成检不合格'),
]

for old, new in task3:
    if old in content:
        content = content.replace(old, new)
        print(f"  Replaced: {old}")
    else:
        print(f"  WARNING: Not found: {old}")

print("Task 3 complete.")

# Write the modified content back
with codecs.open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print("File written successfully.")
