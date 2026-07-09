-- ============================================================
-- 20260709_AddCustomerFieldsToSalesOrder.sql
-- 向 SalesOrder 表添加 CustomerName/Salesman/EndCustomer 快照字段
-- 并从 CustomerProfile 回填已有数据
-- ============================================================

-- Step 1: 添加字段（允许为空，回填后再设为非空）
ALTER TABLE [dbo].[SalesOrders] ADD
    [CustomerName] NVARCHAR(200) NULL,
    [Salesman] NVARCHAR(100) NULL,
    [EndCustomer] NVARCHAR(200) NULL;
GO

-- Step 2: 从 CustomerProfile 回填已有订单的客户字段
UPDATE so
SET
    so.[CustomerName] = cp.[CustomerUnit],
    so.[Salesman] = cp.[Salesman],
    so.[EndCustomer] = cp.[EndCustomer]
FROM [dbo].[SalesOrders] so
INNER JOIN [dbo].[CustomerProfiles] cp ON cp.[Id] = so.[CustomerId]
WHERE so.[CustomerName] IS NULL;
GO

-- Step 3: 将 CustomerName/Salesman 设为非空（EndCustomer 允许为空）
ALTER TABLE [dbo].[SalesOrders] ALTER COLUMN [CustomerName] NVARCHAR(200) NOT NULL;
ALTER TABLE [dbo].[SalesOrders] ALTER COLUMN [Salesman] NVARCHAR(100) NOT NULL;
GO
