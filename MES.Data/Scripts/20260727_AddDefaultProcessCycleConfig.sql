-- 添加默认工艺周期系统参数 (22天)
-- 主号/库料改制无工时默认使用
IF NOT EXISTS (SELECT 1 FROM ConfigParameters WHERE Category = 'DefaultValue' AND ParamKey = 'DefaultProcessCycle')
BEGIN
    INSERT INTO ConfigParameters (Category, CategoryDisplay, Context, ParamKey, ParamValue, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
    VALUES ('DefaultValue', '工单-默认工艺周期', '工单', 'DefaultProcessCycle', 22, '默认工艺周期(天)，主号/库料改制无工时默认使用', GETDATE(), 'System', GETDATE(), 'System');
    PRINT '已添加 DefaultProcessCycle=22';
END
ELSE
BEGIN
    PRINT 'DefaultProcessCycle 已存在，跳过';
END
GO
