IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(max) NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415130748_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260415130748_InitialCreate', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415172512_AddUserProperties'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'FullName');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [AspNetUsers] ALTER COLUMN [FullName] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415172512_AddUserProperties'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415172512_AddUserProperties'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LastLoginAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415172512_AddUserProperties'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260415172512_AddUserProperties', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419145557_RemoveWorkOrderIdColumn'
)
BEGIN
                    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrderItem_WorkOrderId')
                    BEGIN
                        DROP INDEX [IX_OrderItem_WorkOrderId] ON [OrderItem];
                    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419145557_RemoveWorkOrderIdColumn'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderItem]') AND [c].[name] = N'WorkOrderId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [OrderItem] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [OrderItem] DROP COLUMN [WorkOrderId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419145557_RemoveWorkOrderIdColumn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419145557_RemoveWorkOrderIdColumn', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419151638_RemoveStandardIdColumn'
)
BEGIN
                    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ProductRequirement_ProductionStandard_StandardId')
                    BEGIN
                        ALTER TABLE [ProductRequirement] DROP CONSTRAINT [FK_ProductRequirement_ProductionStandard_StandardId];
                    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419151638_RemoveStandardIdColumn'
)
BEGIN
                    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProductRequirement_StandardId')
                    BEGIN
                        DROP INDEX [IX_ProductRequirement_StandardId] ON [ProductRequirement];
                    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419151638_RemoveStandardIdColumn'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductRequirement]') AND [c].[name] = N'StandardId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ProductRequirement] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [ProductRequirement] DROP COLUMN [StandardId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419151638_RemoveStandardIdColumn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419151638_RemoveStandardIdColumn', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419154826_RemoveProductRequirementNavigationFromProductionStandard'
)
BEGIN
                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductRequirement') AND name = 'ProductionStandardId')
                    BEGIN
                        ALTER TABLE [ProductRequirement] DROP COLUMN [ProductionStandardId];
                    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419154826_RemoveProductRequirementNavigationFromProductionStandard'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419154826_RemoveProductRequirementNavigationFromProductionStandard', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094826_AddWorkOrder'
)
BEGIN
    CREATE TABLE [WorkOrder] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderNo] nvarchar(50) NOT NULL,
        [SalesOrderNo] nvarchar(50) NOT NULL,
        [ProductionMainNo] nvarchar(50) NOT NULL,
        [ProductionSubNo] nvarchar(50) NULL,
        [OrderItemIds] nvarchar(500) NOT NULL,
        [Status] int NOT NULL DEFAULT 0,
        [RowVersion] rowversion NOT NULL,
        [SignDate] datetime NOT NULL,
        [Salesman] nvarchar(50) NOT NULL,
        [EndCustomer] nvarchar(200) NULL,
        [DeliveryDate] datetime NOT NULL,
        [DelayPenalty] bit NOT NULL,
        [MaterialName] nvarchar(20) NOT NULL,
        [SettlementMethod] nvarchar(20) NOT NULL,
        [StandardCode] nvarchar(50) NOT NULL,
        [DeliveryState] nvarchar(50) NOT NULL,
        [PlantGrade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(50) NOT NULL,
        [OuterDiameterMinus] decimal(18,3) NOT NULL DEFAULT 0.0,
        [OuterDiameterPlus] decimal(18,3) NOT NULL DEFAULT 0.0,
        [WallThicknessMinus] decimal(18,3) NOT NULL DEFAULT 0.0,
        [WallThicknessPlus] decimal(18,3) NOT NULL DEFAULT 0.0,
        [LengthStatus] nvarchar(20) NOT NULL,
        [MinLength] decimal(18,2) NULL,
        [MaxLength] decimal(18,2) NULL,
        [TotalQuantity] int NOT NULL DEFAULT 0,
        [TotalMeters] decimal(18,2) NOT NULL DEFAULT 0.0,
        [TotalWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [TotalItemCount] int NOT NULL DEFAULT 0,
        [ItemDetails] nvarchar(max) NULL,
        [TechnicalRequirements] nvarchar(20) NOT NULL DEFAULT N'Normal',
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_WorkOrder] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094826_AddWorkOrder'
)
BEGIN
    CREATE INDEX [IX_WorkOrder_DeliveryDate] ON [WorkOrder] ([DeliveryDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094826_AddWorkOrder'
)
BEGIN
    CREATE INDEX [IX_WorkOrder_MaterialName] ON [WorkOrder] ([MaterialName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094826_AddWorkOrder'
)
BEGIN
    CREATE INDEX [IX_WorkOrder_SalesOrderNo] ON [WorkOrder] ([SalesOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094826_AddWorkOrder'
)
BEGIN
    CREATE INDEX [IX_WorkOrder_Specification] ON [WorkOrder] ([Specification]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094826_AddWorkOrder'
)
BEGIN
    CREATE INDEX [IX_WorkOrder_Status] ON [WorkOrder] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094826_AddWorkOrder'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UK_WorkOrder_MainSub] ON [WorkOrder] ([SalesOrderNo], [ProductionMainNo], [ProductionSubNo]) WHERE [ProductionSubNo] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094826_AddWorkOrder'
)
BEGIN
    CREATE UNIQUE INDEX [UK_WorkOrder_WorkOrderNo] ON [WorkOrder] ([WorkOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094826_AddWorkOrder'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260422094826_AddWorkOrder', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423163440_AddOrderChangeNotification'
)
BEGIN
    CREATE TABLE [OrderChangeNotification] (
        [Id] int NOT NULL IDENTITY,
        [OrderNumber] nvarchar(50) NOT NULL,
        [ChangeType] int NOT NULL,
        [WorkOrderCount] int NOT NULL DEFAULT 0,
        [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_OrderChangeNotification] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423163440_AddOrderChangeNotification'
)
BEGIN
    CREATE INDEX [IX_OrderChangeNotification_CreatedTime] ON [OrderChangeNotification] ([CreatedTime]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423163440_AddOrderChangeNotification'
)
BEGIN
    CREATE INDEX [IX_OrderChangeNotification_IsRead] ON [OrderChangeNotification] ([IsRead]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423163440_AddOrderChangeNotification'
)
BEGIN
    CREATE INDEX [IX_OrderChangeNotification_OrderNumber] ON [OrderChangeNotification] ([OrderNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423163440_AddOrderChangeNotification'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260423163440_AddOrderChangeNotification', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423195319_ReplaceOrderItemUniqueIndexWithFiltered'
)
BEGIN
    DROP INDEX [UK_OrderItem_Sequence] ON [OrderItem];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423195319_ReplaceOrderItemUniqueIndexWithFiltered'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UK_OrderItem_Sequence_Active] ON [OrderItem] ([SalesOrderId], [Sequence]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423195319_ReplaceOrderItemUniqueIndexWithFiltered'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260423195319_ReplaceOrderItemUniqueIndexWithFiltered', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423204006_AddLastItemChangeTimeToSalesOrder'
)
BEGIN
    ALTER TABLE [SalesOrder] ADD [LastItemChangeTime] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423204006_AddLastItemChangeTimeToSalesOrder'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260423204006_AddLastItemChangeTimeToSalesOrder', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427160930_AddMissingColumnsToOrderItem'
)
BEGIN
    ALTER TABLE [OrderItem] ADD [OuterDiameterMinus] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427160930_AddMissingColumnsToOrderItem'
)
BEGIN
    ALTER TABLE [OrderItem] ADD [OuterDiameterPlus] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427160930_AddMissingColumnsToOrderItem'
)
BEGIN
    ALTER TABLE [OrderItem] ADD [WallThicknessMinus] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427160930_AddMissingColumnsToOrderItem'
)
BEGIN
    ALTER TABLE [OrderItem] ADD [WallThicknessPlus] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427160930_AddMissingColumnsToOrderItem'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260427160930_AddMissingColumnsToOrderItem', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427172414_AddRefreshToken'
)
BEGIN
    CREATE TABLE [RefreshToken] (
        [Id] int NOT NULL IDENTITY,
        [Token] nvarchar(200) NOT NULL,
        [UserId] nvarchar(100) NOT NULL,
        [Expires] datetime2 NOT NULL,
        [IsRevoked] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_RefreshToken] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427172414_AddRefreshToken'
)
BEGIN
    CREATE INDEX [IX_RefreshToken_Expires] ON [RefreshToken] ([Expires]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427172414_AddRefreshToken'
)
BEGIN
    CREATE INDEX [IX_RefreshToken_UserId] ON [RefreshToken] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427172414_AddRefreshToken'
)
BEGIN
    CREATE UNIQUE INDEX [UK_RefreshToken_Token] ON [RefreshToken] ([Token]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427172414_AddRefreshToken'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260427172414_AddRefreshToken', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427173333_ConvertWorkOrderEnums'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260427173333_ConvertWorkOrderEnums', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427202346_FixStatusColumnsToString'
)
BEGIN
    DROP INDEX [IX_WorkOrder_Status] ON [WorkOrder];
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'Status');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [WorkOrder] ALTER COLUMN [Status] nvarchar(20) NOT NULL;
    ALTER TABLE [WorkOrder] ADD DEFAULT N'NotGenerated' FOR [Status];
    CREATE INDEX [IX_WorkOrder_Status] ON [WorkOrder] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427202346_FixStatusColumnsToString'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderChangeNotification]') AND [c].[name] = N'ChangeType');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [OrderChangeNotification] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [OrderChangeNotification] ALTER COLUMN [ChangeType] nvarchar(20) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427202346_FixStatusColumnsToString'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260427202346_FixStatusColumnsToString', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428095504_AddMaterialPlanSupport'
)
BEGIN
    ALTER TABLE [WorkOrder] ADD [MaterialPlanRate] decimal(5,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428095504_AddMaterialPlanSupport'
)
BEGIN
    ALTER TABLE [WorkOrder] ADD [MaterialPlanStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428095504_AddMaterialPlanSupport'
)
BEGIN
    CREATE TABLE [PurchaseFinishedPlan] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [PlanDate] date NOT NULL,
        [ProductType] nvarchar(20) NOT NULL,
        [RequiredPiece] int NULL,
        [RequiredWeight] decimal(18,3) NOT NULL,
        [RequiredDate] date NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_PurchaseFinishedPlan] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428095504_AddMaterialPlanSupport'
)
BEGIN
    CREATE TABLE [PurchaseSemiPlan] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [PlanDate] date NOT NULL,
        [AdjustedWallThickness] decimal(18,3) NOT NULL,
        [YieldRate] decimal(5,2) NOT NULL,
        [InputMultiple] int NOT NULL DEFAULT 1,
        [QualifiedRate] decimal(5,2) NOT NULL,
        [Density] decimal(18,4) NULL,
        [UnitWeight] decimal(18,3) NULL,
        [RawUnitWeight] decimal(18,3) NULL,
        [RequiredPieces] int NULL,
        [RequiredWeight] decimal(18,3) NOT NULL,
        [RawMaterialType] nvarchar(20) NOT NULL,
        [RawMaterialSpec] nvarchar(100) NOT NULL,
        [RequiredDate] date NULL,
        [ProcessPlan] nvarchar(max) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_PurchaseSemiPlan] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428095504_AddMaterialPlanSupport'
)
BEGIN
    CREATE INDEX [IX_PurchaseFinishedPlan_WorkOrderId] ON [PurchaseFinishedPlan] ([WorkOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428095504_AddMaterialPlanSupport'
)
BEGIN
    CREATE INDEX [IX_PurchaseSemiPlan_WorkOrderId] ON [PurchaseSemiPlan] ([WorkOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428095504_AddMaterialPlanSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428095504_AddMaterialPlanSupport', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428174004_UpdateMaterialPlanStatusEnum'
)
BEGIN
    UPDATE WorkOrder SET MaterialPlanStatus = 3 WHERE MaterialPlanStatus = 2
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428174004_UpdateMaterialPlanStatusEnum'
)
BEGIN
    UPDATE WorkOrder SET MaterialPlanStatus = 4 WHERE MaterialPlanStatus = 3
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428174004_UpdateMaterialPlanStatusEnum'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428174004_UpdateMaterialPlanStatusEnum', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE TABLE [InventoryBatch] (
        [Id] int NOT NULL IDENTITY,
        [BatchNo] nvarchar(50) NOT NULL,
        [WarehouseId] int NOT NULL,
        [MaterialType] nvarchar(30) NOT NULL,
        [PlantGrade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [InboundSource] nvarchar(20) NOT NULL,
        [SourceName] nvarchar(200) NOT NULL,
        [InboundDate] datetime NOT NULL,
        [RelatedNo] nvarchar(50) NULL,
        [HeatNo] nvarchar(50) NULL,
        [ProductionBatchNo] nvarchar(50) NULL,
        [LengthStatus] nvarchar(20) NULL,
        [MinLength] decimal(18,2) NULL,
        [MaxLength] decimal(18,2) NULL,
        [InitialQuantity] int NOT NULL DEFAULT 0,
        [InitialWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [UnitWeight] decimal(18,3) NULL,
        [Meters] decimal(18,2) NULL,
        [RemainingQuantity] int NOT NULL DEFAULT 0,
        [RemainingWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [ActualSpecification] nvarchar(100) NULL,
        [ActualOuterDiameter] decimal(18,3) NULL,
        [ActualWallThickness] decimal(18,3) NULL,
        [SurfaceCondition] nvarchar(50) NULL,
        [LocationArea] nvarchar(50) NULL,
        [LocationRack] nvarchar(50) NULL,
        [IsFrozen] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Remark] nvarchar(500) NULL,
        [IsMixedPackage] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PackageNo] nvarchar(50) NULL,
        [DefectReason] nvarchar(200) NULL,
        [LiabilityType] nvarchar(50) NULL,
        [OriginalSupplier] nvarchar(200) NULL,
        [TagNo] nvarchar(50) NULL,
        [DefectRemark] nvarchar(500) NULL,
        [IsLinkedToWorkOrder] bit NOT NULL DEFAULT CAST(0 AS bit),
        [WorkOrderNo] nvarchar(50) NULL,
        [SalesOrderNo] nvarchar(50) NULL,
        [OrderItemIds] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_InventoryBatch] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE TABLE [InventoryBatchDeleteLog] (
        [Id] bigint NOT NULL IDENTITY,
        [BatchNo] nvarchar(50) NOT NULL,
        [Operator] nvarchar(50) NOT NULL,
        [DeletedTime] datetime NOT NULL,
        [BatchData] nvarchar(max) NOT NULL,
        [Reason] nvarchar(500) NULL,
        CONSTRAINT [PK_InventoryBatchDeleteLog] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE TABLE [Notification] (
        [Id] int NOT NULL IDENTITY,
        [NotificationType] nvarchar(30) NOT NULL,
        [TargetId] int NULL,
        [Title] nvarchar(200) NOT NULL,
        [Content] nvarchar(500) NOT NULL,
        [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Receiver] nvarchar(50) NOT NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Notification] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE TABLE [OutboundRecord] (
        [Id] bigint NOT NULL IDENTITY,
        [InventoryBatchId] int NOT NULL,
        [OutboundType] nvarchar(30) NOT NULL,
        [TargetCompany] nvarchar(200) NULL,
        [RelatedNo] nvarchar(50) NULL,
        [OutboundQuantity] int NOT NULL DEFAULT 0,
        [OutboundWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [OutboundDate] datetime NOT NULL,
        [Operator] nvarchar(50) NOT NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_OutboundRecord] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE TABLE [Warehouse] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Warehouse] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE INDEX [IX_InventoryBatch_MaterialType] ON [InventoryBatch] ([MaterialType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE INDEX [IX_InventoryBatch_PlantGrade] ON [InventoryBatch] ([PlantGrade]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE INDEX [IX_InventoryBatch_ProductionBatchNo] ON [InventoryBatch] ([ProductionBatchNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_InventoryBatch_RemainingWeight] ON [InventoryBatch] ([RemainingWeight]) WHERE [RemainingWeight] > 0 AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE INDEX [IX_InventoryBatch_SalesOrderNo] ON [InventoryBatch] ([SalesOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE INDEX [IX_InventoryBatch_WarehouseId] ON [InventoryBatch] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE INDEX [IX_InventoryBatch_WorkOrderNo] ON [InventoryBatch] ([WorkOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_InventoryBatch_BatchNo] ON [InventoryBatch] ([BatchNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE INDEX [IX_OutboundRecord_InventoryBatchId] ON [OutboundRecord] ([InventoryBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE INDEX [IX_OutboundRecord_OutboundDate] ON [OutboundRecord] ([OutboundDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE INDEX [IX_OutboundRecord_RelatedNo] ON [OutboundRecord] ([RelatedNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_Warehouse_Code] ON [Warehouse] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428200128_AddWarehouseContext'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428200128_AddWarehouseContext', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    DROP INDEX [IX_OutboundRecord_RelatedNo] ON [OutboundRecord];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutboundRecord]') AND [c].[name] = N'RelatedNo');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [OutboundRecord] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [OutboundRecord] DROP COLUMN [RelatedNo];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'IsFrozen');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [InventoryBatch] DROP COLUMN [IsFrozen];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'IsMixedPackage');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [InventoryBatch] DROP COLUMN [IsMixedPackage];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'PackageNo');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [InventoryBatch] DROP COLUMN [PackageNo];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'RelatedNo');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [InventoryBatch] DROP COLUMN [RelatedNo];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    CREATE TABLE [InventoryPlan] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [PlanDate] date NOT NULL,
        [InventoryBatchId] int NOT NULL,
        [BatchNo] nvarchar(50) NOT NULL,
        [PlantGrade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [InputMultiple] int NOT NULL DEFAULT 1,
        [UsageMode] nvarchar(10) NOT NULL DEFAULT N'All',
        [UsedQuantity] int NULL,
        [UsedWeight] decimal(18,3) NOT NULL,
        [RequiredDate] date NULL,
        [PlanStatus] nvarchar(20) NOT NULL DEFAULT N'Planned',
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_InventoryPlan] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    CREATE INDEX [IX_InventoryPlan_InventoryBatchId] ON [InventoryPlan] ([InventoryBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    CREATE INDEX [IX_InventoryPlan_PlanStatus] ON [InventoryPlan] ([PlanStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    CREATE INDEX [IX_InventoryPlan_WorkOrderId] ON [InventoryPlan] ([WorkOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430082416_AddInventoryPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260430082416_AddInventoryPlan', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430140048_AddReworkPlan'
)
BEGIN
    ALTER TABLE [InventoryPlan] ADD [ProcessPlan] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430140048_AddReworkPlan'
)
BEGIN
    ALTER TABLE [InventoryPlan] ADD [ReworkType] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430140048_AddReworkPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260430140048_AddReworkPlan', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501134935_CrossContextRefactorV2'
)
BEGIN
    DROP INDEX [IX_InventoryPlan_InventoryBatchId] ON [InventoryPlan];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501134935_CrossContextRefactorV2'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryPlan]') AND [c].[name] = N'InventoryBatchId');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [InventoryPlan] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [InventoryPlan] DROP COLUMN [InventoryBatchId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501134935_CrossContextRefactorV2'
)
BEGIN
    ALTER TABLE [PurchaseSemiPlan] ADD [PurchaseOrderNo] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501134935_CrossContextRefactorV2'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [PurchaseOrderNo] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501134935_CrossContextRefactorV2'
)
BEGIN
    ALTER TABLE [InventoryPlan] ADD [InventoryBatchNo] nvarchar(50) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501134935_CrossContextRefactorV2'
)
BEGIN
    ALTER TABLE [InventoryBatch] ADD [PurchaseOrderNo] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501134935_CrossContextRefactorV2'
)
BEGIN
    ALTER TABLE [InventoryBatch] ADD [SubcontractOrderNo] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501134935_CrossContextRefactorV2'
)
BEGIN
    CREATE INDEX [IX_InventoryPlan_InventoryBatchNo] ON [InventoryPlan] ([InventoryBatchNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501134935_CrossContextRefactorV2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260501134935_CrossContextRefactorV2', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501151522_SimplifySourceOrderNo'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseSemiPlan]') AND [c].[name] = N'PurchaseOrderNo');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseSemiPlan] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [PurchaseSemiPlan] DROP COLUMN [PurchaseOrderNo];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501151522_SimplifySourceOrderNo'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseFinishedPlan]') AND [c].[name] = N'PurchaseOrderNo');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseFinishedPlan] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [PurchaseFinishedPlan] DROP COLUMN [PurchaseOrderNo];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501151522_SimplifySourceOrderNo'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'PurchaseOrderNo');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [InventoryBatch] DROP COLUMN [PurchaseOrderNo];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501151522_SimplifySourceOrderNo'
)
BEGIN
    EXEC sp_rename N'[InventoryBatch].[SubcontractOrderNo]', N'SourceOrderNo', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501151522_SimplifySourceOrderNo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260501151522_SimplifySourceOrderNo', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE TABLE [Material] (
        [Id] int NOT NULL IDENTITY,
        [MaterialCategory] nvarchar(30) NOT NULL,
        [PlantGrade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Material] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE TABLE [PurchaseOrder] (
        [Id] int NOT NULL IDENTITY,
        [OrderNo] nvarchar(20) NOT NULL,
        [SupplierId] int NOT NULL,
        [OrderDate] date NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Open',
        [ManualStatus] nvarchar(20) NULL,
        [MaterialCategory] nvarchar(30) NOT NULL,
        [PlantGrade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [UnitWeight] decimal(18,3) NULL,
        [Quantity] int NULL,
        [Weight] decimal(18,3) NOT NULL,
        [RequiredDate] date NOT NULL,
        [UnitPrice] decimal(18,4) NULL,
        [TotalAmount] decimal(18,2) NULL,
        [LastArrivalDate] date NULL,
        [ReceivedQuantity] int NOT NULL DEFAULT 0,
        [ReceivedWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [SourceWorkOrderNo] nvarchar(50) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_PurchaseOrder] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE TABLE [SubcontractOrder] (
        [Id] int NOT NULL IDENTITY,
        [OrderNo] nvarchar(20) NOT NULL,
        [SupplierId] int NOT NULL,
        [OrderDate] date NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Sent',
        [ManualStatus] nvarchar(20) NULL,
        [OutMaterialCategory] nvarchar(30) NOT NULL,
        [OutPlantGrade] nvarchar(50) NOT NULL,
        [OutSpecification] nvarchar(100) NOT NULL,
        [OutQuantity] int NOT NULL,
        [OutWeight] decimal(18,3) NOT NULL,
        [ReturnDeadline] date NULL,
        [InQuantity] int NULL,
        [InWeight] decimal(18,3) NULL,
        [SourceWorkOrderNo] nvarchar(50) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_SubcontractOrder] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE TABLE [SupplierProfile] (
        [Id] int NOT NULL IDENTITY,
        [SupplierName] nvarchar(200) NOT NULL,
        [ContactPerson] nvarchar(50) NULL,
        [ContactPhone] nvarchar(50) NULL,
        [Address] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_SupplierProfile] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE TABLE [SubcontractReturnItem] (
        [Id] int NOT NULL IDENTITY,
        [SubcontractOrderId] int NOT NULL,
        [Sequence] int NOT NULL,
        [ProcessType] nvarchar(30) NOT NULL,
        [MaterialCategory] nvarchar(30) NOT NULL,
        [ProcessSpecification] nvarchar(100) NOT NULL,
        [ProcessStatusRemark] nvarchar(500) NULL,
        [ProcessUnitPrice] decimal(18,4) NULL,
        [ProcessTotalAmount] decimal(18,2) NULL,
        [SourceWorkOrderNo] nvarchar(50) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_SubcontractReturnItem] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubcontractReturnItem_SubcontractOrder_SubcontractOrderId] FOREIGN KEY ([SubcontractOrderId]) REFERENCES [SubcontractOrder] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE INDEX [IX_Material_Category] ON [Material] ([MaterialCategory]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE INDEX [IX_Material_IsActive] ON [Material] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UK_Material_Combo] ON [Material] ([MaterialCategory], [PlantGrade], [Specification]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrder_RequiredDate] ON [PurchaseOrder] ([RequiredDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrder_SourceWO] ON [PurchaseOrder] ([SourceWorkOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrder_Status] ON [PurchaseOrder] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrder_SupplierId] ON [PurchaseOrder] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE UNIQUE INDEX [UK_PurchaseOrder_OrderNo] ON [PurchaseOrder] ([OrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE INDEX [IX_SubcontractOrder_Status] ON [SubcontractOrder] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE INDEX [IX_SubcontractOrder_SupplierId] ON [SubcontractOrder] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE UNIQUE INDEX [UK_SubcontractOrder_OrderNo] ON [SubcontractOrder] ([OrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE INDEX [IX_ReturnItem_OrderId] ON [SubcontractReturnItem] ([SubcontractOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ReturnItem_Seq] ON [SubcontractReturnItem] ([SubcontractOrderId], [Sequence]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501204032_MaterialContextInit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260501204032_MaterialContextInit', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504090728_AddPurchaseSemiPlanNewFields'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutboundRecord]') AND [c].[name] = N'IsDeleted');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [OutboundRecord] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [OutboundRecord] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504090728_AddPurchaseSemiPlanNewFields'
)
BEGIN
    ALTER TABLE [PurchaseSemiPlan] ADD [PlantGrade] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504090728_AddPurchaseSemiPlanNewFields'
)
BEGIN
    ALTER TABLE [PurchaseSemiPlan] ADD [RequiredUnitWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504090728_AddPurchaseSemiPlanNewFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504090728_AddPurchaseSemiPlanNewFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504092935_MakePurchaseSemiPlanFieldsRequired'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseSemiPlan]') AND [c].[name] = N'RequiredDate');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseSemiPlan] DROP CONSTRAINT [' + @var15 + '];');
    EXEC(N'UPDATE [PurchaseSemiPlan] SET [RequiredDate] = ''0001-01-01'' WHERE [RequiredDate] IS NULL');
    ALTER TABLE [PurchaseSemiPlan] ALTER COLUMN [RequiredDate] date NOT NULL;
    ALTER TABLE [PurchaseSemiPlan] ADD DEFAULT '0001-01-01' FOR [RequiredDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504092935_MakePurchaseSemiPlanFieldsRequired'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseSemiPlan]') AND [c].[name] = N'PlantGrade');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseSemiPlan] DROP CONSTRAINT [' + @var16 + '];');
    EXEC(N'UPDATE [PurchaseSemiPlan] SET [PlantGrade] = N'''' WHERE [PlantGrade] IS NULL');
    ALTER TABLE [PurchaseSemiPlan] ALTER COLUMN [PlantGrade] nvarchar(100) NOT NULL;
    ALTER TABLE [PurchaseSemiPlan] ADD DEFAULT N'' FOR [PlantGrade];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504092935_MakePurchaseSemiPlanFieldsRequired'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504092935_MakePurchaseSemiPlanFieldsRequired', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [DeliveryState] nvarchar(50) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [LengthStatus] nvarchar(20) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [MaxLength] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [MinLength] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [OuterDiameterNegative] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [OuterDiameterPositive] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [PlantGrade] nvarchar(50) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [Specification] nvarchar(50) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [WallThicknessNegative] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [WallThicknessPositive] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504111814_AddPurchaseFinishedPlanWorkOrderFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504113233_AddInventoryPlanMaterialTypeLocation'
)
BEGIN
    ALTER TABLE [InventoryPlan] ADD [LocationArea] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504113233_AddInventoryPlanMaterialTypeLocation'
)
BEGIN
    ALTER TABLE [InventoryPlan] ADD [LocationRack] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504113233_AddInventoryPlanMaterialTypeLocation'
)
BEGIN
    ALTER TABLE [InventoryPlan] ADD [MaterialType] nvarchar(50) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504113233_AddInventoryPlanMaterialTypeLocation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504113233_AddInventoryPlanMaterialTypeLocation', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504183547_AddMaterialAndSupplierCode'
)
BEGIN
    ALTER TABLE [SupplierProfile] ADD [SupplierCode] nvarchar(6) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504183547_AddMaterialAndSupplierCode'
)
BEGIN
    ALTER TABLE [Material] ADD [MaterialCode] nvarchar(6) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504183547_AddMaterialAndSupplierCode'
)
BEGIN
                    UPDATE m
                    SET m.MaterialCode = t.NewCode
                    FROM Material m
                    INNER JOIN (
                        SELECT Id, CONCAT('MA', RIGHT('0000' + CAST(ROW_NUMBER() OVER(ORDER BY Id) AS NVARCHAR(4)), 4)) AS NewCode
                        FROM Material
                    ) t ON m.Id = t.Id
                    WHERE m.MaterialCode = '';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504183547_AddMaterialAndSupplierCode'
)
BEGIN
                    UPDATE s
                    SET s.SupplierCode = t.NewCode
                    FROM SupplierProfile s
                    INNER JOIN (
                        SELECT Id, CONCAT('SU', RIGHT('0000' + CAST(ROW_NUMBER() OVER(ORDER BY Id) AS NVARCHAR(4)), 4)) AS NewCode
                        FROM SupplierProfile
                    ) t ON s.Id = t.Id
                    WHERE s.SupplierCode = '';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504183547_AddMaterialAndSupplierCode'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UK_Supplier_Code] ON [SupplierProfile] ([SupplierCode]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504183547_AddMaterialAndSupplierCode'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UK_Material_Code] ON [Material] ([MaterialCode]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504183547_AddMaterialAndSupplierCode'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504183547_AddMaterialAndSupplierCode', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504203613_AddPurchaseOrderPlanFields'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [DeliveryState] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504203613_AddPurchaseOrderPlanFields'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [LengthStatus] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504203613_AddPurchaseOrderPlanFields'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [MaxLength] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504203613_AddPurchaseOrderPlanFields'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [MinLength] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504203613_AddPurchaseOrderPlanFields'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [OuterDiameterNegative] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504203613_AddPurchaseOrderPlanFields'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [OuterDiameterPositive] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504203613_AddPurchaseOrderPlanFields'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [PlanType] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504203613_AddPurchaseOrderPlanFields'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [WallThicknessNegative] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504203613_AddPurchaseOrderPlanFields'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [WallThicknessPositive] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504203613_AddPurchaseOrderPlanFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504203613_AddPurchaseOrderPlanFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505054634_RemovePurchaseOrderPlanFields'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'DeliveryState');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [DeliveryState];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505054634_RemovePurchaseOrderPlanFields'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'LengthStatus');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [LengthStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505054634_RemovePurchaseOrderPlanFields'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'MaxLength');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [MaxLength];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505054634_RemovePurchaseOrderPlanFields'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'MinLength');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [MinLength];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505054634_RemovePurchaseOrderPlanFields'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'OuterDiameterNegative');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [OuterDiameterNegative];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505054634_RemovePurchaseOrderPlanFields'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'OuterDiameterPositive');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [OuterDiameterPositive];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505054634_RemovePurchaseOrderPlanFields'
)
BEGIN
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'PlanType');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var23 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [PlanType];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505054634_RemovePurchaseOrderPlanFields'
)
BEGIN
    DECLARE @var24 sysname;
    SELECT @var24 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'WallThicknessNegative');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var24 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [WallThicknessNegative];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505054634_RemovePurchaseOrderPlanFields'
)
BEGIN
    DECLARE @var25 sysname;
    SELECT @var25 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'WallThicknessPositive');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var25 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [WallThicknessPositive];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505054634_RemovePurchaseOrderPlanFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505054634_RemovePurchaseOrderPlanFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505063411_AddSupplierMaterialCategory'
)
BEGIN
    ALTER TABLE [SupplierProfile] ADD [MaterialCategory] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505063411_AddSupplierMaterialCategory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505063411_AddSupplierMaterialCategory', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DROP INDEX [UK_Supplier_Code] ON [SupplierProfile];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DROP INDEX [UK_OrderItem_Sequence_Active] ON [OrderItem];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DROP INDEX [UK_Material_Code] ON [Material];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DROP INDEX [UK_Material_Combo] ON [Material];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DROP INDEX [IX_InventoryBatch_RemainingWeight] ON [InventoryBatch];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var26 sysname;
    SELECT @var26 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'IsDeleted');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var26 + '];');
    ALTER TABLE [WorkOrder] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var27 sysname;
    SELECT @var27 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Warehouse]') AND [c].[name] = N'IsDeleted');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [Warehouse] DROP CONSTRAINT [' + @var27 + '];');
    ALTER TABLE [Warehouse] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var28 sysname;
    SELECT @var28 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SupplierProfile]') AND [c].[name] = N'IsDeleted');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [SupplierProfile] DROP CONSTRAINT [' + @var28 + '];');
    ALTER TABLE [SupplierProfile] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var29 sysname;
    SELECT @var29 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractReturnItem]') AND [c].[name] = N'IsDeleted');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractReturnItem] DROP CONSTRAINT [' + @var29 + '];');
    ALTER TABLE [SubcontractReturnItem] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var30 sysname;
    SELECT @var30 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractOrder]') AND [c].[name] = N'IsDeleted');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @var30 + '];');
    ALTER TABLE [SubcontractOrder] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var31 sysname;
    SELECT @var31 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StandardGradeMapping]') AND [c].[name] = N'IsDeleted');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [StandardGradeMapping] DROP CONSTRAINT [' + @var31 + '];');
    ALTER TABLE [StandardGradeMapping] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var32 sysname;
    SELECT @var32 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesOrder]') AND [c].[name] = N'IsDeleted');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [SalesOrder] DROP CONSTRAINT [' + @var32 + '];');
    ALTER TABLE [SalesOrder] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var33 sysname;
    SELECT @var33 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RefreshToken]') AND [c].[name] = N'IsDeleted');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [RefreshToken] DROP CONSTRAINT [' + @var33 + '];');
    ALTER TABLE [RefreshToken] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var34 sysname;
    SELECT @var34 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseSemiPlan]') AND [c].[name] = N'IsDeleted');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseSemiPlan] DROP CONSTRAINT [' + @var34 + '];');
    ALTER TABLE [PurchaseSemiPlan] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var35 sysname;
    SELECT @var35 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'IsDeleted');
    IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var35 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var36 sysname;
    SELECT @var36 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseFinishedPlan]') AND [c].[name] = N'IsDeleted');
    IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseFinishedPlan] DROP CONSTRAINT [' + @var36 + '];');
    ALTER TABLE [PurchaseFinishedPlan] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var37 sysname;
    SELECT @var37 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductRequirement]') AND [c].[name] = N'IsDeleted');
    IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [ProductRequirement] DROP CONSTRAINT [' + @var37 + '];');
    ALTER TABLE [ProductRequirement] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var38 sysname;
    SELECT @var38 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionStandard]') AND [c].[name] = N'IsDeleted');
    IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [ProductionStandard] DROP CONSTRAINT [' + @var38 + '];');
    ALTER TABLE [ProductionStandard] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var39 sysname;
    SELECT @var39 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderItem]') AND [c].[name] = N'IsDeleted');
    IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [OrderItem] DROP CONSTRAINT [' + @var39 + '];');
    ALTER TABLE [OrderItem] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var40 sysname;
    SELECT @var40 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderChangeNotification]') AND [c].[name] = N'IsDeleted');
    IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [OrderChangeNotification] DROP CONSTRAINT [' + @var40 + '];');
    ALTER TABLE [OrderChangeNotification] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var41 sysname;
    SELECT @var41 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Material]') AND [c].[name] = N'IsDeleted');
    IF @var41 IS NOT NULL EXEC(N'ALTER TABLE [Material] DROP CONSTRAINT [' + @var41 + '];');
    ALTER TABLE [Material] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var42 sysname;
    SELECT @var42 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryPlan]') AND [c].[name] = N'IsDeleted');
    IF @var42 IS NOT NULL EXEC(N'ALTER TABLE [InventoryPlan] DROP CONSTRAINT [' + @var42 + '];');
    ALTER TABLE [InventoryPlan] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var43 sysname;
    SELECT @var43 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'IsDeleted');
    IF @var43 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var43 + '];');
    ALTER TABLE [InventoryBatch] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    DECLARE @var44 sysname;
    SELECT @var44 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfile]') AND [c].[name] = N'IsDeleted');
    IF @var44 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfile] DROP CONSTRAINT [' + @var44 + '];');
    ALTER TABLE [CustomerProfile] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    CREATE UNIQUE INDEX [UK_Supplier_Code] ON [SupplierProfile] ([SupplierCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    CREATE UNIQUE INDEX [UK_OrderItem_Sequence_Active] ON [OrderItem] ([SalesOrderId], [Sequence]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    CREATE UNIQUE INDEX [UK_Material_Code] ON [Material] ([MaterialCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    CREATE UNIQUE INDEX [UK_Material_Combo] ON [Material] ([MaterialCategory], [PlantGrade], [Specification]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_InventoryBatch_RemainingWeight] ON [InventoryBatch] ([RemainingWeight]) WHERE [RemainingWeight] > 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505125529_RemoveIsDeletedColumn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505125529_RemoveIsDeletedColumn', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505151003_UpdateSubcontractOrderFields'
)
BEGIN
                    DECLARE @cn NVARCHAR(200);
                    SELECT @cn = d.name FROM sys.default_constraints d
                    JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
                    WHERE d.parent_object_id = OBJECT_ID(N'[SubcontractOrder]') AND c.name = 'SourceWorkOrderNo';
                    IF @cn IS NOT NULL EXEC('ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @cn + ']');
                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractOrder]') AND name = 'SourceWorkOrderNo')
                        ALTER TABLE [SubcontractOrder] DROP COLUMN [SourceWorkOrderNo];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505151003_UpdateSubcontractOrderFields'
)
BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractReturnItem]') AND name = 'SourceWorkOrderNo')
                    BEGIN
                        ALTER TABLE [SubcontractReturnItem] ADD [SourceWorkOrderNo] nvarchar(50) NULL;
                    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505151003_UpdateSubcontractOrderFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505151003_UpdateSubcontractOrderFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505161934_AddSubcontractReturnItemMissingFields'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [PlantGrade] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505161934_AddSubcontractReturnItemMissingFields'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [Remark] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505161934_AddSubcontractReturnItemMissingFields'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [RequiredQuantity] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505161934_AddSubcontractReturnItemMissingFields'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [RequiredWeight] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505161934_AddSubcontractReturnItemMissingFields'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [UnitWeight] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505161934_AddSubcontractReturnItemMissingFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505161934_AddSubcontractReturnItemMissingFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505164611_AddFurnaceNumberToSubcontractOrder'
)
BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractOrder]') AND name = 'FurnaceNumber')
                    BEGIN
                        ALTER TABLE [SubcontractOrder] ADD [FurnaceNumber] nvarchar(50) NULL;
                    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505164611_AddFurnaceNumberToSubcontractOrder'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505164611_AddFurnaceNumberToSubcontractOrder', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505173016_MoveProcessTypeToSubcontractOrder'
)
BEGIN
    DECLARE @var45 sysname;
    SELECT @var45 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractReturnItem]') AND [c].[name] = N'ProcessType');
    IF @var45 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractReturnItem] DROP CONSTRAINT [' + @var45 + '];');
    ALTER TABLE [SubcontractReturnItem] DROP COLUMN [ProcessType];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505173016_MoveProcessTypeToSubcontractOrder'
)
BEGIN
    ALTER TABLE [SubcontractOrder] ADD [ProcessType] nvarchar(30) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505173016_MoveProcessTypeToSubcontractOrder'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505173016_MoveProcessTypeToSubcontractOrder', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506072912_AddOutboundSourceOrderNo'
)
BEGIN
    DECLARE @var46 sysname;
    SELECT @var46 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutboundRecord]') AND [c].[name] = N'Operator');
    IF @var46 IS NOT NULL EXEC(N'ALTER TABLE [OutboundRecord] DROP CONSTRAINT [' + @var46 + '];');
    ALTER TABLE [OutboundRecord] DROP COLUMN [Operator];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506072912_AddOutboundSourceOrderNo'
)
BEGIN
    ALTER TABLE [OutboundRecord] ADD [SourceOrderNo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506072912_AddOutboundSourceOrderNo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260506072912_AddOutboundSourceOrderNo', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506211607_RenameManualStatusToIsForceCompleted'
)
BEGIN
    DECLARE @var47 sysname;
    SELECT @var47 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractOrder]') AND [c].[name] = N'ManualStatus');
    IF @var47 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @var47 + '];');
    ALTER TABLE [SubcontractOrder] DROP COLUMN [ManualStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506211607_RenameManualStatusToIsForceCompleted'
)
BEGIN
    DECLARE @var48 sysname;
    SELECT @var48 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'ManualStatus');
    IF @var48 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var48 + '];');
    ALTER TABLE [PurchaseOrder] DROP COLUMN [ManualStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506211607_RenameManualStatusToIsForceCompleted'
)
BEGIN
    ALTER TABLE [SubcontractOrder] ADD [IsForceCompleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506211607_RenameManualStatusToIsForceCompleted'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [IsForceCompleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506211607_RenameManualStatusToIsForceCompleted'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260506211607_RenameManualStatusToIsForceCompleted', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507153100_AddBatchContext'
)
BEGIN
    CREATE TABLE [ProductionBatch] (
        [Id] int NOT NULL IDENTITY,
        [BatchNo] nvarchar(50) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'None',
        [TagNo] nvarchar(50) NULL,
        [IsForceCompleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [QualityRemark] nvarchar(500) NULL,
        [SolutionParams] nvarchar(500) NULL,
        [CurrentExecDate] datetime NULL,
        [CurrentGroupName] nvarchar(50) NULL,
        [CurrentSectionName] nvarchar(50) NULL,
        [CurrentEquipmentName] nvarchar(100) NULL,
        [CurrentOutsource] nvarchar(200) NULL,
        [NextSectionName] nvarchar(50) NULL,
        [Remark] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        [WorkOrderNo] nvarchar(50) NOT NULL,
        [SalesOrderNo] nvarchar(50) NOT NULL,
        [ProductionMainNo] nvarchar(50) NOT NULL,
        [ProductionSubNo] nvarchar(50) NULL,
        [OrderItemIds] nvarchar(500) NOT NULL,
        [SignDate] datetime NOT NULL,
        [Salesman] nvarchar(50) NOT NULL,
        [EndCustomer] nvarchar(200) NULL,
        [DeliveryDate] datetime NOT NULL,
        [DelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
        [MaterialName] nvarchar(20) NOT NULL,
        [SettlementMethod] nvarchar(20) NOT NULL,
        [StandardCode] nvarchar(50) NOT NULL,
        [DeliveryState] nvarchar(50) NOT NULL,
        [PlantGrade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [OuterDiameterNegative] decimal(18,3) NOT NULL DEFAULT 0.0,
        [OuterDiameterPositive] decimal(18,3) NOT NULL DEFAULT 0.0,
        [WallThicknessNegative] decimal(18,3) NOT NULL DEFAULT 0.0,
        [WallThicknessPositive] decimal(18,3) NOT NULL DEFAULT 0.0,
        [LengthStatus] nvarchar(20) NOT NULL,
        [MinLength] decimal(18,2) NULL,
        [MaxLength] decimal(18,2) NULL,
        [TotalQuantity] int NOT NULL DEFAULT 0,
        [TotalMeters] decimal(18,2) NOT NULL DEFAULT 0.0,
        [TotalWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [TotalItemCount] int NOT NULL DEFAULT 0,
        [ItemDetails] nvarchar(max) NULL,
        [TechnicalRequirements] nvarchar(20) NOT NULL,
        [SourceBatchNo] nvarchar(50) NULL,
        [WarehouseId] int NULL,
        [SourceMaterialType] nvarchar(30) NULL,
        [InboundSource] nvarchar(20) NULL,
        [SourceName] nvarchar(200) NULL,
        [InboundDate] datetime NULL,
        [SourceHeatNo] nvarchar(50) NULL,
        [InputQuantity] int NULL,
        [InputWeight] decimal(18,3) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ProductionBatch] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507153100_AddBatchContext'
)
BEGIN
    CREATE TABLE [ProcessGroup] (
        [Id] int NOT NULL IDENTITY,
        [ProductionBatchId] int NOT NULL,
        [SequenceNumber] int NOT NULL,
        [ProcessName] nvarchar(50) NOT NULL,
        [ManufacturingSpec] nvarchar(100) NULL,
        [ManufacturingLength] nvarchar(100) NULL,
        [CuttingTreatment] nvarchar(200) NULL,
        [Remark] nvarchar(500) NULL,
        [ColdRollDraw] int NULL,
        [OilPipeCut] int NULL,
        [Degrease] int NULL,
        [Solution] int NULL,
        [Straighten] int NULL,
        [Cut] int NULL,
        [ThicknessMeasure] int NULL,
        [Pickle] int NULL,
        [OuterPolish] int NULL,
        [InnerGrinding] int NULL,
        [OuterSpotGrinding] int NULL,
        [Inspection] int NULL,
        [WeldingHead] int NULL,
        [Lubrication] int NULL,
        [Warehouse] int NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ProcessGroup] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProcessGroup_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507153100_AddBatchContext'
)
BEGIN
    CREATE INDEX [IX_ProcessGroup_BatchId] ON [ProcessGroup] ([ProductionBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507153100_AddBatchContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ProcessGroup_Seq] ON [ProcessGroup] ([ProductionBatchId], [SequenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507153100_AddBatchContext'
)
BEGIN
    CREATE INDEX [IX_ProductionBatch_SalesOrderNo] ON [ProductionBatch] ([SalesOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507153100_AddBatchContext'
)
BEGIN
    CREATE INDEX [IX_ProductionBatch_Status] ON [ProductionBatch] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507153100_AddBatchContext'
)
BEGIN
    CREATE INDEX [IX_ProductionBatch_TagNo] ON [ProductionBatch] ([TagNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507153100_AddBatchContext'
)
BEGIN
    CREATE INDEX [IX_ProductionBatch_WorkOrderNo] ON [ProductionBatch] ([WorkOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507153100_AddBatchContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ProductionBatch_BatchNo] ON [ProductionBatch] ([BatchNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507153100_AddBatchContext'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260507153100_AddBatchContext', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507204240_AddProductionTypeAndRatioToBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [ProductionRatio] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507204240_AddProductionTypeAndRatioToBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [ProductionType] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507204240_AddProductionTypeAndRatioToBatch'
)
BEGIN
    ALTER TABLE [ProcessGroup] ADD [OuterDiameterTolerance] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507204240_AddProductionTypeAndRatioToBatch'
)
BEGIN
    ALTER TABLE [ProcessGroup] ADD [WallThicknessTolerance] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507204240_AddProductionTypeAndRatioToBatch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260507204240_AddProductionTypeAndRatioToBatch', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507234454_AddSourceWarehouseFieldsToBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [SourceLengthStatus] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507234454_AddSourceWarehouseFieldsToBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [SourcePlantGrade] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507234454_AddSourceWarehouseFieldsToBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [SourceSpecification] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507234454_AddSourceWarehouseFieldsToBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [SourceUnitWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507234454_AddSourceWarehouseFieldsToBatch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260507234454_AddSourceWarehouseFieldsToBatch', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DECLARE @var49 sysname;
    SELECT @var49 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'SignDate');
    IF @var49 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var49 + '];');
    ALTER TABLE [WorkOrder] ALTER COLUMN [SignDate] datetime2 NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DROP INDEX [IX_WorkOrder_DeliveryDate] ON [WorkOrder];
    DECLARE @var50 sysname;
    SELECT @var50 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'DeliveryDate');
    IF @var50 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var50 + '];');
    ALTER TABLE [WorkOrder] ALTER COLUMN [DeliveryDate] datetime2 NOT NULL;
    CREATE INDEX [IX_WorkOrder_DeliveryDate] ON [WorkOrder] ([DeliveryDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DROP INDEX [IX_SalesOrder_SignDate] ON [SalesOrder];
    DECLARE @var51 sysname;
    SELECT @var51 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesOrder]') AND [c].[name] = N'SignDate');
    IF @var51 IS NOT NULL EXEC(N'ALTER TABLE [SalesOrder] DROP CONSTRAINT [' + @var51 + '];');
    ALTER TABLE [SalesOrder] ALTER COLUMN [SignDate] datetime2 NOT NULL;
    CREATE INDEX [IX_SalesOrder_SignDate] ON [SalesOrder] ([SignDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DECLARE @var52 sysname;
    SELECT @var52 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'SignDate');
    IF @var52 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var52 + '];');
    ALTER TABLE [ProductionBatch] ALTER COLUMN [SignDate] datetime2 NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DECLARE @var53 sysname;
    SELECT @var53 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'InboundDate');
    IF @var53 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var53 + '];');
    ALTER TABLE [ProductionBatch] ALTER COLUMN [InboundDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DECLARE @var54 sysname;
    SELECT @var54 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'DeliveryDate');
    IF @var54 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var54 + '];');
    ALTER TABLE [ProductionBatch] ALTER COLUMN [DeliveryDate] datetime2 NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DECLARE @var55 sysname;
    SELECT @var55 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'CurrentExecDate');
    IF @var55 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var55 + '];');
    ALTER TABLE [ProductionBatch] ALTER COLUMN [CurrentExecDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DROP INDEX [IX_OutboundRecord_OutboundDate] ON [OutboundRecord];
    DECLARE @var56 sysname;
    SELECT @var56 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutboundRecord]') AND [c].[name] = N'OutboundDate');
    IF @var56 IS NOT NULL EXEC(N'ALTER TABLE [OutboundRecord] DROP CONSTRAINT [' + @var56 + '];');
    ALTER TABLE [OutboundRecord] ALTER COLUMN [OutboundDate] datetime2 NOT NULL;
    CREATE INDEX [IX_OutboundRecord_OutboundDate] ON [OutboundRecord] ([OutboundDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DECLARE @var57 sysname;
    SELECT @var57 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderItem]') AND [c].[name] = N'DeliveryDate');
    IF @var57 IS NOT NULL EXEC(N'ALTER TABLE [OrderItem] DROP CONSTRAINT [' + @var57 + '];');
    ALTER TABLE [OrderItem] ALTER COLUMN [DeliveryDate] datetime2 NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DECLARE @var58 sysname;
    SELECT @var58 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatchDeleteLog]') AND [c].[name] = N'DeletedTime');
    IF @var58 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatchDeleteLog] DROP CONSTRAINT [' + @var58 + '];');
    ALTER TABLE [InventoryBatchDeleteLog] ALTER COLUMN [DeletedTime] datetime2 NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    DECLARE @var59 sysname;
    SELECT @var59 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'InboundDate');
    IF @var59 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var59 + '];');
    ALTER TABLE [InventoryBatch] ALTER COLUMN [InboundDate] datetime2 NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE TABLE [MaterialReceiveCheck] (
        [Id] int NOT NULL IDENTITY,
        [ProductionBatchId] int NOT NULL,
        [ReceiveDate] datetime2 NOT NULL,
        [ReceivedQuantity] int NULL,
        [ReceivedWeight] decimal(18,3) NULL,
        [Shift] nvarchar(10) NULL,
        [Checker] nvarchar(50) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MaterialReceiveCheck] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaterialReceiveCheck_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE TABLE [ProductionRecord] (
        [Id] int NOT NULL IDENTITY,
        [ProductionBatchId] int NOT NULL,
        [ProcessGroupId] int NOT NULL,
        [ProcessName] nvarchar(50) NOT NULL,
        [ManufacturingSpec] nvarchar(100) NULL,
        [SectionName] nvarchar(50) NOT NULL,
        [SequenceNumber] int NOT NULL,
        [ExecDate] datetime2 NOT NULL,
        [EquipmentName] nvarchar(100) NULL,
        [Operator] nvarchar(50) NULL,
        [Shift] nvarchar(10) NULL,
        [Quantity] int NULL,
        [Weight] decimal(18,3) NULL,
        [DefectQuantity] int NULL,
        [DefectWeight] decimal(18,3) NULL,
        [IsFinished] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CuttingRate] decimal(5,2) NULL,
        [FinishedCutLength] decimal(18,2) NULL,
        [PostCutQuantity] int NULL,
        [TagNo] nvarchar(50) NULL,
        [PlantGrade] nvarchar(50) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ProductionRecord] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionRecord_ProcessGroup_ProcessGroupId] FOREIGN KEY ([ProcessGroupId]) REFERENCES [ProcessGroup] ([Id]),
        CONSTRAINT [FK_ProductionRecord_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE TABLE [SectionOutsource] (
        [Id] int NOT NULL IDENTITY,
        [ProductionBatchId] int NOT NULL,
        [ProcessGroupId] int NOT NULL,
        [ProcessName] nvarchar(50) NOT NULL,
        [ManufacturingSpec] nvarchar(100) NULL,
        [SectionName] nvarchar(50) NOT NULL,
        [SequenceNumber] int NOT NULL,
        [OutsourceVendor] nvarchar(100) NOT NULL,
        [SendOutDate] datetime2 NOT NULL,
        [SendQuantity] int NULL,
        [SendWeight] decimal(18,3) NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'待回收',
        [TagNo] nvarchar(50) NULL,
        [PlantGrade] nvarchar(50) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_SectionOutsource] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SectionOutsource_ProcessGroup_ProcessGroupId] FOREIGN KEY ([ProcessGroupId]) REFERENCES [ProcessGroup] ([Id]),
        CONSTRAINT [FK_SectionOutsource_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE TABLE [OutsourceRecovery] (
        [Id] int NOT NULL IDENTITY,
        [SectionOutsourceId] int NOT NULL,
        [RecoveryDate] datetime2 NOT NULL,
        [RecoveryQuantity] int NULL,
        [RecoveryWeight] decimal(18,3) NULL,
        [IsQualified] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_OutsourceRecovery] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OutsourceRecovery_SectionOutsource_SectionOutsourceId] FOREIGN KEY ([SectionOutsourceId]) REFERENCES [SectionOutsource] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_MaterialReceiveCheck_BatchId] ON [MaterialReceiveCheck] ([ProductionBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE INDEX [IX_OutsourceRecovery_OutsourceId] ON [OutsourceRecovery] ([SectionOutsourceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE INDEX [IX_ProductionRecord_BatchId] ON [ProductionRecord] ([ProductionBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE INDEX [IX_ProductionRecord_ProcessGroupId] ON [ProductionRecord] ([ProcessGroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ProductionRecord_Section] ON [ProductionRecord] ([ProductionBatchId], [ProcessGroupId], [SectionName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE INDEX [IX_SectionOutsource_BatchId] ON [SectionOutsource] ([ProductionBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE INDEX [IX_SectionOutsource_ProcessGroupId] ON [SectionOutsource] ([ProcessGroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_SectionOutsource_Section] ON [SectionOutsource] ([ProductionBatchId], [ProcessGroupId], [SectionName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508182550_AddProductionRecordContext'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260508182550_AddProductionRecordContext', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508210655_AddCuttingMultipleAndUnprocessedFields'
)
BEGIN
    EXEC sp_rename N'[ProductionRecord].[CuttingRate]', N'CuttingMultiple', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508210655_AddCuttingMultipleAndUnprocessedFields'
)
BEGIN
    ALTER TABLE [OutsourceRecovery] ADD [UnprocessedQuantity] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508210655_AddCuttingMultipleAndUnprocessedFields'
)
BEGIN
    ALTER TABLE [OutsourceRecovery] ADD [UnprocessedWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508210655_AddCuttingMultipleAndUnprocessedFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260508210655_AddCuttingMultipleAndUnprocessedFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509200158_FixOrderItemIdsUseSequence'
)
BEGIN
    -- ============================================================
    -- 1. 修复 WorkOrder 表
    -- ============================================================
    DECLARE @Id INT, @OrderItemIds NVARCHAR(MAX), @NewValue NVARCHAR(MAX);
    DECLARE WO_CURSOR CURSOR LOCAL FAST_FORWARD FOR
    SELECT Id, OrderItemIds FROM [WorkOrder] WHERE OrderItemIds IS NOT NULL AND OrderItemIds != N'';
    OPEN WO_CURSOR;
    FETCH NEXT FROM WO_CURSOR INTO @Id, @OrderItemIds;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @NewValue = ISNULL(STUFF((
            SELECT N',' + CAST(oi.Sequence AS NVARCHAR(10))
            FROM STRING_SPLIT(@OrderItemIds, N',') ss
            INNER JOIN [OrderItem] oi ON oi.Id = TRY_CAST(ss.value AS INT)
            ORDER BY oi.Sequence
            FOR XML PATH(N''), TYPE
        ).value(N'.', N'NVARCHAR(MAX)'), 1, 1, N''), N'');
        UPDATE [WorkOrder] SET OrderItemIds = @NewValue WHERE Id = @Id;
        FETCH NEXT FROM WO_CURSOR INTO @Id, @OrderItemIds;
    END;
    CLOSE WO_CURSOR;
    DEALLOCATE WO_CURSOR;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509200158_FixOrderItemIdsUseSequence'
)
BEGIN
    -- ============================================================
    -- 2. 修复 InventoryBatch 表
    -- ============================================================
    DECLARE @Id INT, @OrderItemIds NVARCHAR(MAX), @NewValue NVARCHAR(MAX);
    DECLARE IB_CURSOR CURSOR LOCAL FAST_FORWARD FOR
    SELECT Id, OrderItemIds FROM [InventoryBatch] WHERE OrderItemIds IS NOT NULL AND OrderItemIds != N'';
    OPEN IB_CURSOR;
    FETCH NEXT FROM IB_CURSOR INTO @Id, @OrderItemIds;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @NewValue = ISNULL(STUFF((
            SELECT N',' + CAST(oi.Sequence AS NVARCHAR(10))
            FROM STRING_SPLIT(@OrderItemIds, N',') ss
            INNER JOIN [OrderItem] oi ON oi.Id = TRY_CAST(ss.value AS INT)
            ORDER BY oi.Sequence
            FOR XML PATH(N''), TYPE
        ).value(N'.', N'NVARCHAR(MAX)'), 1, 1, N''), N'');
        UPDATE [InventoryBatch] SET OrderItemIds = @NewValue WHERE Id = @Id;
        FETCH NEXT FROM IB_CURSOR INTO @Id, @OrderItemIds;
    END;
    CLOSE IB_CURSOR;
    DEALLOCATE IB_CURSOR;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509200158_FixOrderItemIdsUseSequence'
)
BEGIN
    -- ============================================================
    -- 3. 修复 ProductionBatch 表
    -- ============================================================
    DECLARE @Id INT, @OrderItemIds NVARCHAR(MAX), @NewValue NVARCHAR(MAX);
    DECLARE PB_CURSOR CURSOR LOCAL FAST_FORWARD FOR
    SELECT Id, OrderItemIds FROM [ProductionBatch] WHERE OrderItemIds IS NOT NULL AND OrderItemIds != N'';
    OPEN PB_CURSOR;
    FETCH NEXT FROM PB_CURSOR INTO @Id, @OrderItemIds;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @NewValue = ISNULL(STUFF((
            SELECT N',' + CAST(oi.Sequence AS NVARCHAR(10))
            FROM STRING_SPLIT(@OrderItemIds, N',') ss
            INNER JOIN [OrderItem] oi ON oi.Id = TRY_CAST(ss.value AS INT)
            ORDER BY oi.Sequence
            FOR XML PATH(N''), TYPE
        ).value(N'.', N'NVARCHAR(MAX)'), 1, 1, N''), N'');
        UPDATE [ProductionBatch] SET OrderItemIds = @NewValue WHERE Id = @Id;
        FETCH NEXT FROM PB_CURSOR INTO @Id, @OrderItemIds;
    END;
    CLOSE PB_CURSOR;
    DEALLOCATE PB_CURSOR;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509200158_FixOrderItemIdsUseSequence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260509200158_FixOrderItemIdsUseSequence', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510155012_AddOutsourceSpecReturnDateIsUrgent'
)
BEGIN
    ALTER TABLE [SectionOutsource] ADD [ExpectedReturnDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510155012_AddOutsourceSpecReturnDateIsUrgent'
)
BEGIN
    ALTER TABLE [SectionOutsource] ADD [IsUrgent] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510155012_AddOutsourceSpecReturnDateIsUrgent'
)
BEGIN
    ALTER TABLE [SectionOutsource] ADD [OutsourceSpec] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510155012_AddOutsourceSpecReturnDateIsUrgent'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260510155012_AddOutsourceSpecReturnDateIsUrgent', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510191220_AddCurrentSpecAndCorrespondingSpec'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [CorrespondingSpec] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510191220_AddCurrentSpecAndCorrespondingSpec'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [CurrentSpec] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510191220_AddCurrentSpecAndCorrespondingSpec'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260510191220_AddCurrentSpecAndCorrespondingSpec', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511171630_FixProductionRecordAndSectionOutsourceSequenceNumber'
)
BEGIN
    UPDATE pr
    SET pr.SequenceNumber =
        CASE pr.SectionName
            WHEN N'冷轧拔' THEN ISNULL(pg.ColdRollDraw, 0)
            WHEN N'油管断' THEN ISNULL(pg.OilPipeCut, 0)
            WHEN N'去油'   THEN ISNULL(pg.Degrease, 0)
            WHEN N'固溶'   THEN ISNULL(pg.Solution, 0)
            WHEN N'矫直'   THEN ISNULL(pg.Straighten, 0)
            WHEN N'断切'   THEN ISNULL(pg.Cut, 0)
            WHEN N'测壁厚' THEN ISNULL(pg.ThicknessMeasure, 0)
            WHEN N'酸洗'   THEN ISNULL(pg.Pickle, 0)
            WHEN N'外抛光' THEN ISNULL(pg.OuterPolish, 0)
            WHEN N'内修磨' THEN ISNULL(pg.InnerGrinding, 0)
            WHEN N'外点磨' THEN ISNULL(pg.OuterSpotGrinding, 0)
            WHEN N'检验'   THEN ISNULL(pg.Inspection, 0)
            WHEN N'打焊头' THEN ISNULL(pg.WeldingHead, 0)
            WHEN N'润滑'   THEN ISNULL(pg.Lubrication, 0)
            WHEN N'入库'   THEN ISNULL(pg.Warehouse, 0)
            ELSE 0
        END
    FROM [ProductionRecord] pr
    INNER JOIN [ProcessGroup] pg ON pr.ProcessGroupId = pg.Id;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511171630_FixProductionRecordAndSectionOutsourceSequenceNumber'
)
BEGIN
    UPDATE so
    SET so.SequenceNumber =
        CASE so.SectionName
            WHEN N'冷轧拔' THEN ISNULL(pg.ColdRollDraw, 0)
            WHEN N'油管断' THEN ISNULL(pg.OilPipeCut, 0)
            WHEN N'去油'   THEN ISNULL(pg.Degrease, 0)
            WHEN N'固溶'   THEN ISNULL(pg.Solution, 0)
            WHEN N'矫直'   THEN ISNULL(pg.Straighten, 0)
            WHEN N'断切'   THEN ISNULL(pg.Cut, 0)
            WHEN N'测壁厚' THEN ISNULL(pg.ThicknessMeasure, 0)
            WHEN N'酸洗'   THEN ISNULL(pg.Pickle, 0)
            WHEN N'外抛光' THEN ISNULL(pg.OuterPolish, 0)
            WHEN N'内修磨' THEN ISNULL(pg.InnerGrinding, 0)
            WHEN N'外点磨' THEN ISNULL(pg.OuterSpotGrinding, 0)
            WHEN N'检验'   THEN ISNULL(pg.Inspection, 0)
            WHEN N'打焊头' THEN ISNULL(pg.WeldingHead, 0)
            WHEN N'润滑'   THEN ISNULL(pg.Lubrication, 0)
            WHEN N'入库'   THEN ISNULL(pg.Warehouse, 0)
            ELSE 0
        END
    FROM [SectionOutsource] so
    INNER JOIN [ProcessGroup] pg ON so.ProcessGroupId = pg.Id;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511171630_FixProductionRecordAndSectionOutsourceSequenceNumber'
)
BEGIN
    DECLARE @var60 sysname;
    SELECT @var60 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutsourceRecovery]') AND [c].[name] = N'IsQualified');
    IF @var60 IS NOT NULL EXEC(N'ALTER TABLE [OutsourceRecovery] DROP CONSTRAINT [' + @var60 + '];');
    ALTER TABLE [OutsourceRecovery] DROP COLUMN [IsQualified];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511171630_FixProductionRecordAndSectionOutsourceSequenceNumber'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260511171630_FixProductionRecordAndSectionOutsourceSequenceNumber', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var61 sysname;
    SELECT @var61 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'UpdatedBy');
    IF @var61 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var61 + '];');
    ALTER TABLE [WorkOrder] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var62 sysname;
    SELECT @var62 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrder]') AND [c].[name] = N'CreatedBy');
    IF @var62 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrder] DROP CONSTRAINT [' + @var62 + '];');
    ALTER TABLE [WorkOrder] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var63 sysname;
    SELECT @var63 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Warehouse]') AND [c].[name] = N'UpdatedBy');
    IF @var63 IS NOT NULL EXEC(N'ALTER TABLE [Warehouse] DROP CONSTRAINT [' + @var63 + '];');
    ALTER TABLE [Warehouse] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var64 sysname;
    SELECT @var64 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Warehouse]') AND [c].[name] = N'CreatedBy');
    IF @var64 IS NOT NULL EXEC(N'ALTER TABLE [Warehouse] DROP CONSTRAINT [' + @var64 + '];');
    ALTER TABLE [Warehouse] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var65 sysname;
    SELECT @var65 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SupplierProfile]') AND [c].[name] = N'UpdatedBy');
    IF @var65 IS NOT NULL EXEC(N'ALTER TABLE [SupplierProfile] DROP CONSTRAINT [' + @var65 + '];');
    ALTER TABLE [SupplierProfile] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var66 sysname;
    SELECT @var66 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SupplierProfile]') AND [c].[name] = N'CreatedBy');
    IF @var66 IS NOT NULL EXEC(N'ALTER TABLE [SupplierProfile] DROP CONSTRAINT [' + @var66 + '];');
    ALTER TABLE [SupplierProfile] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var67 sysname;
    SELECT @var67 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractReturnItem]') AND [c].[name] = N'UpdatedBy');
    IF @var67 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractReturnItem] DROP CONSTRAINT [' + @var67 + '];');
    ALTER TABLE [SubcontractReturnItem] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var68 sysname;
    SELECT @var68 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractReturnItem]') AND [c].[name] = N'CreatedBy');
    IF @var68 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractReturnItem] DROP CONSTRAINT [' + @var68 + '];');
    ALTER TABLE [SubcontractReturnItem] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var69 sysname;
    SELECT @var69 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractOrder]') AND [c].[name] = N'UpdatedBy');
    IF @var69 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @var69 + '];');
    ALTER TABLE [SubcontractOrder] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var70 sysname;
    SELECT @var70 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubcontractOrder]') AND [c].[name] = N'CreatedBy');
    IF @var70 IS NOT NULL EXEC(N'ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @var70 + '];');
    ALTER TABLE [SubcontractOrder] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var71 sysname;
    SELECT @var71 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StandardGradeMapping]') AND [c].[name] = N'UpdatedBy');
    IF @var71 IS NOT NULL EXEC(N'ALTER TABLE [StandardGradeMapping] DROP CONSTRAINT [' + @var71 + '];');
    ALTER TABLE [StandardGradeMapping] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var72 sysname;
    SELECT @var72 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StandardGradeMapping]') AND [c].[name] = N'CreatedBy');
    IF @var72 IS NOT NULL EXEC(N'ALTER TABLE [StandardGradeMapping] DROP CONSTRAINT [' + @var72 + '];');
    ALTER TABLE [StandardGradeMapping] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var73 sysname;
    SELECT @var73 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SectionOutsource]') AND [c].[name] = N'UpdatedBy');
    IF @var73 IS NOT NULL EXEC(N'ALTER TABLE [SectionOutsource] DROP CONSTRAINT [' + @var73 + '];');
    ALTER TABLE [SectionOutsource] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var74 sysname;
    SELECT @var74 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SectionOutsource]') AND [c].[name] = N'Status');
    IF @var74 IS NOT NULL EXEC(N'ALTER TABLE [SectionOutsource] DROP CONSTRAINT [' + @var74 + '];');
    ALTER TABLE [SectionOutsource] ADD DEFAULT N'PendingRecovery' FOR [Status];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var75 sysname;
    SELECT @var75 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SectionOutsource]') AND [c].[name] = N'CreatedBy');
    IF @var75 IS NOT NULL EXEC(N'ALTER TABLE [SectionOutsource] DROP CONSTRAINT [' + @var75 + '];');
    ALTER TABLE [SectionOutsource] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var76 sysname;
    SELECT @var76 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesOrder]') AND [c].[name] = N'UpdatedBy');
    IF @var76 IS NOT NULL EXEC(N'ALTER TABLE [SalesOrder] DROP CONSTRAINT [' + @var76 + '];');
    ALTER TABLE [SalesOrder] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var77 sysname;
    SELECT @var77 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesOrder]') AND [c].[name] = N'CreatedBy');
    IF @var77 IS NOT NULL EXEC(N'ALTER TABLE [SalesOrder] DROP CONSTRAINT [' + @var77 + '];');
    ALTER TABLE [SalesOrder] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var78 sysname;
    SELECT @var78 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RefreshToken]') AND [c].[name] = N'UpdatedBy');
    IF @var78 IS NOT NULL EXEC(N'ALTER TABLE [RefreshToken] DROP CONSTRAINT [' + @var78 + '];');
    ALTER TABLE [RefreshToken] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var79 sysname;
    SELECT @var79 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RefreshToken]') AND [c].[name] = N'CreatedBy');
    IF @var79 IS NOT NULL EXEC(N'ALTER TABLE [RefreshToken] DROP CONSTRAINT [' + @var79 + '];');
    ALTER TABLE [RefreshToken] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var80 sysname;
    SELECT @var80 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseSemiPlan]') AND [c].[name] = N'UpdatedBy');
    IF @var80 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseSemiPlan] DROP CONSTRAINT [' + @var80 + '];');
    ALTER TABLE [PurchaseSemiPlan] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var81 sysname;
    SELECT @var81 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseSemiPlan]') AND [c].[name] = N'CreatedBy');
    IF @var81 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseSemiPlan] DROP CONSTRAINT [' + @var81 + '];');
    ALTER TABLE [PurchaseSemiPlan] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var82 sysname;
    SELECT @var82 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'UpdatedBy');
    IF @var82 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var82 + '];');
    ALTER TABLE [PurchaseOrder] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var83 sysname;
    SELECT @var83 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseOrder]') AND [c].[name] = N'CreatedBy');
    IF @var83 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseOrder] DROP CONSTRAINT [' + @var83 + '];');
    ALTER TABLE [PurchaseOrder] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var84 sysname;
    SELECT @var84 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseFinishedPlan]') AND [c].[name] = N'UpdatedBy');
    IF @var84 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseFinishedPlan] DROP CONSTRAINT [' + @var84 + '];');
    ALTER TABLE [PurchaseFinishedPlan] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var85 sysname;
    SELECT @var85 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseFinishedPlan]') AND [c].[name] = N'CreatedBy');
    IF @var85 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseFinishedPlan] DROP CONSTRAINT [' + @var85 + '];');
    ALTER TABLE [PurchaseFinishedPlan] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var86 sysname;
    SELECT @var86 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductRequirement]') AND [c].[name] = N'UpdatedBy');
    IF @var86 IS NOT NULL EXEC(N'ALTER TABLE [ProductRequirement] DROP CONSTRAINT [' + @var86 + '];');
    ALTER TABLE [ProductRequirement] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var87 sysname;
    SELECT @var87 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductRequirement]') AND [c].[name] = N'CreatedBy');
    IF @var87 IS NOT NULL EXEC(N'ALTER TABLE [ProductRequirement] DROP CONSTRAINT [' + @var87 + '];');
    ALTER TABLE [ProductRequirement] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var88 sysname;
    SELECT @var88 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionStandard]') AND [c].[name] = N'UpdatedBy');
    IF @var88 IS NOT NULL EXEC(N'ALTER TABLE [ProductionStandard] DROP CONSTRAINT [' + @var88 + '];');
    ALTER TABLE [ProductionStandard] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var89 sysname;
    SELECT @var89 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionStandard]') AND [c].[name] = N'CreatedBy');
    IF @var89 IS NOT NULL EXEC(N'ALTER TABLE [ProductionStandard] DROP CONSTRAINT [' + @var89 + '];');
    ALTER TABLE [ProductionStandard] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var90 sysname;
    SELECT @var90 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionRecord]') AND [c].[name] = N'UpdatedBy');
    IF @var90 IS NOT NULL EXEC(N'ALTER TABLE [ProductionRecord] DROP CONSTRAINT [' + @var90 + '];');
    ALTER TABLE [ProductionRecord] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var91 sysname;
    SELECT @var91 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionRecord]') AND [c].[name] = N'CreatedBy');
    IF @var91 IS NOT NULL EXEC(N'ALTER TABLE [ProductionRecord] DROP CONSTRAINT [' + @var91 + '];');
    ALTER TABLE [ProductionRecord] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var92 sysname;
    SELECT @var92 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'UpdatedBy');
    IF @var92 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var92 + '];');
    ALTER TABLE [ProductionBatch] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var93 sysname;
    SELECT @var93 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'CreatedBy');
    IF @var93 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var93 + '];');
    ALTER TABLE [ProductionBatch] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var94 sysname;
    SELECT @var94 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessGroup]') AND [c].[name] = N'UpdatedBy');
    IF @var94 IS NOT NULL EXEC(N'ALTER TABLE [ProcessGroup] DROP CONSTRAINT [' + @var94 + '];');
    ALTER TABLE [ProcessGroup] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var95 sysname;
    SELECT @var95 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessGroup]') AND [c].[name] = N'CreatedBy');
    IF @var95 IS NOT NULL EXEC(N'ALTER TABLE [ProcessGroup] DROP CONSTRAINT [' + @var95 + '];');
    ALTER TABLE [ProcessGroup] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var96 sysname;
    SELECT @var96 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutsourceRecovery]') AND [c].[name] = N'UpdatedBy');
    IF @var96 IS NOT NULL EXEC(N'ALTER TABLE [OutsourceRecovery] DROP CONSTRAINT [' + @var96 + '];');
    ALTER TABLE [OutsourceRecovery] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var97 sysname;
    SELECT @var97 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OutsourceRecovery]') AND [c].[name] = N'CreatedBy');
    IF @var97 IS NOT NULL EXEC(N'ALTER TABLE [OutsourceRecovery] DROP CONSTRAINT [' + @var97 + '];');
    ALTER TABLE [OutsourceRecovery] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var98 sysname;
    SELECT @var98 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderItem]') AND [c].[name] = N'UpdatedBy');
    IF @var98 IS NOT NULL EXEC(N'ALTER TABLE [OrderItem] DROP CONSTRAINT [' + @var98 + '];');
    ALTER TABLE [OrderItem] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var99 sysname;
    SELECT @var99 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderItem]') AND [c].[name] = N'CreatedBy');
    IF @var99 IS NOT NULL EXEC(N'ALTER TABLE [OrderItem] DROP CONSTRAINT [' + @var99 + '];');
    ALTER TABLE [OrderItem] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var100 sysname;
    SELECT @var100 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderChangeNotification]') AND [c].[name] = N'UpdatedBy');
    IF @var100 IS NOT NULL EXEC(N'ALTER TABLE [OrderChangeNotification] DROP CONSTRAINT [' + @var100 + '];');
    ALTER TABLE [OrderChangeNotification] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var101 sysname;
    SELECT @var101 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderChangeNotification]') AND [c].[name] = N'CreatedBy');
    IF @var101 IS NOT NULL EXEC(N'ALTER TABLE [OrderChangeNotification] DROP CONSTRAINT [' + @var101 + '];');
    ALTER TABLE [OrderChangeNotification] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var102 sysname;
    SELECT @var102 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaterialReceiveCheck]') AND [c].[name] = N'UpdatedBy');
    IF @var102 IS NOT NULL EXEC(N'ALTER TABLE [MaterialReceiveCheck] DROP CONSTRAINT [' + @var102 + '];');
    ALTER TABLE [MaterialReceiveCheck] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var103 sysname;
    SELECT @var103 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaterialReceiveCheck]') AND [c].[name] = N'CreatedBy');
    IF @var103 IS NOT NULL EXEC(N'ALTER TABLE [MaterialReceiveCheck] DROP CONSTRAINT [' + @var103 + '];');
    ALTER TABLE [MaterialReceiveCheck] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var104 sysname;
    SELECT @var104 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Material]') AND [c].[name] = N'UpdatedBy');
    IF @var104 IS NOT NULL EXEC(N'ALTER TABLE [Material] DROP CONSTRAINT [' + @var104 + '];');
    ALTER TABLE [Material] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var105 sysname;
    SELECT @var105 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Material]') AND [c].[name] = N'CreatedBy');
    IF @var105 IS NOT NULL EXEC(N'ALTER TABLE [Material] DROP CONSTRAINT [' + @var105 + '];');
    ALTER TABLE [Material] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var106 sysname;
    SELECT @var106 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryPlan]') AND [c].[name] = N'UpdatedBy');
    IF @var106 IS NOT NULL EXEC(N'ALTER TABLE [InventoryPlan] DROP CONSTRAINT [' + @var106 + '];');
    ALTER TABLE [InventoryPlan] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var107 sysname;
    SELECT @var107 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryPlan]') AND [c].[name] = N'CreatedBy');
    IF @var107 IS NOT NULL EXEC(N'ALTER TABLE [InventoryPlan] DROP CONSTRAINT [' + @var107 + '];');
    ALTER TABLE [InventoryPlan] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var108 sysname;
    SELECT @var108 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'UpdatedBy');
    IF @var108 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var108 + '];');
    ALTER TABLE [InventoryBatch] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var109 sysname;
    SELECT @var109 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryBatch]') AND [c].[name] = N'CreatedBy');
    IF @var109 IS NOT NULL EXEC(N'ALTER TABLE [InventoryBatch] DROP CONSTRAINT [' + @var109 + '];');
    ALTER TABLE [InventoryBatch] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var110 sysname;
    SELECT @var110 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfile]') AND [c].[name] = N'UpdatedBy');
    IF @var110 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfile] DROP CONSTRAINT [' + @var110 + '];');
    ALTER TABLE [CustomerProfile] ALTER COLUMN [UpdatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DECLARE @var111 sysname;
    SELECT @var111 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfile]') AND [c].[name] = N'CreatedBy');
    IF @var111 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfile] DROP CONSTRAINT [' + @var111 + '];');
    ALTER TABLE [CustomerProfile] ALTER COLUMN [CreatedBy] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    ALTER TABLE [InventoryBatch] ADD CONSTRAINT [FK_InventoryBatch_Warehouse_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouse] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    ALTER TABLE [OutboundRecord] ADD CONSTRAINT [FK_OutboundRecord_InventoryBatch_InventoryBatchId] FOREIGN KEY ([InventoryBatchId]) REFERENCES [InventoryBatch] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DELETE FROM PurchaseSemiPlan WHERE WorkOrderId NOT IN (SELECT Id FROM WorkOrder)
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    DELETE FROM PurchaseFinishedPlan WHERE WorkOrderId NOT IN (SELECT Id FROM WorkOrder)
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD CONSTRAINT [FK_PurchaseFinishedPlan_WorkOrder_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrder] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD CONSTRAINT [FK_PurchaseOrder_SupplierProfile_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [SupplierProfile] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    ALTER TABLE [PurchaseSemiPlan] ADD CONSTRAINT [FK_PurchaseSemiPlan_WorkOrder_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrder] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    ALTER TABLE [SubcontractOrder] ADD CONSTRAINT [FK_SubcontractOrder_SupplierProfile_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [SupplierProfile] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511192139_AddMissingForeignKeys'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260511192139_AddMissingForeignKeys', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511195143_FixSectionOutsourceStatusInProgress'
)
BEGIN
    UPDATE SectionOutsource SET Status = 'InProgress' WHERE Status = N'在轧'
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511195143_FixSectionOutsourceStatusInProgress'
)
BEGIN
    UPDATE SectionOutsource SET Status = 'PendingRecovery' WHERE Status = N'待回收'
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511195143_FixSectionOutsourceStatusInProgress'
)
BEGIN
    UPDATE SectionOutsource SET Status = 'Recovered' WHERE Status = N'已回收'
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511195143_FixSectionOutsourceStatusInProgress'
)
BEGIN
    UPDATE SectionOutsource SET Status = 'PendingRecovery' WHERE Status NOT IN ('PendingRecovery', 'Recovered', 'InProgress')
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511195143_FixSectionOutsourceStatusInProgress'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260511195143_FixSectionOutsourceStatusInProgress', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512171128_AddBatchOperationLog'
)
BEGIN
    CREATE TABLE [BatchOperationLog] (
        [Id] int NOT NULL IDENTITY,
        [ProductionBatchId] int NOT NULL,
        [OperationType] nvarchar(20) NOT NULL,
        [Detail] nvarchar(2000) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_BatchOperationLog] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BatchOperationLog_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512171128_AddBatchOperationLog'
)
BEGIN
    CREATE INDEX [IX_BatchOperationLog_BatchId] ON [BatchOperationLog] ([ProductionBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512171128_AddBatchOperationLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512171128_AddBatchOperationLog', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513174640_AddCurrentValidFieldsAndCancelledStatus'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [CurrentValidQty] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513174640_AddCurrentValidFieldsAndCancelledStatus'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [CurrentValidWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513174640_AddCurrentValidFieldsAndCancelledStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513174640_AddCurrentValidFieldsAndCancelledStatus', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513201920_AddProcessInspection'
)
BEGIN
    CREATE TABLE [ProcessInspection] (
        [Id] int NOT NULL IDENTITY,
        [ProductionBatchId] int NOT NULL,
        [ProcessGroupId] int NOT NULL,
        [ProcessName] nvarchar(50) NOT NULL,
        [ManufacturingSpec] nvarchar(100) NULL,
        [SectionName] nvarchar(50) NOT NULL,
        [SequenceNumber] int NOT NULL,
        [InspectionDate] datetime2 NOT NULL,
        [EquipmentName] nvarchar(100) NULL,
        [Inspector] nvarchar(50) NULL,
        [Shift] nvarchar(10) NULL,
        [Quantity] int NULL,
        [Weight] decimal(18,3) NULL,
        [InspectionItem] nvarchar(100) NULL,
        [QualifiedQuantity] int NULL,
        [QualifiedWeight] decimal(18,3) NULL,
        [DefectReworkQuantity] int NULL,
        [DefectWarehouseQuantity] int NULL,
        [DefectScrapQuantity] int NULL,
        [DefectDescription] nvarchar(500) NULL,
        [SourceUnit] nvarchar(200) NULL,
        [TagNo] nvarchar(50) NULL,
        [PlantGrade] nvarchar(50) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_ProcessInspection] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProcessInspection_ProcessGroup_ProcessGroupId] FOREIGN KEY ([ProcessGroupId]) REFERENCES [ProcessGroup] ([Id]),
        CONSTRAINT [FK_ProcessInspection_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513201920_AddProcessInspection'
)
BEGIN
    CREATE INDEX [IX_ProcessInspection_BatchId] ON [ProcessInspection] ([ProductionBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513201920_AddProcessInspection'
)
BEGIN
    CREATE INDEX [IX_ProcessInspection_ProcessGroupId] ON [ProcessInspection] ([ProcessGroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513201920_AddProcessInspection'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513201920_AddProcessInspection', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513211449_RemoveDefectQuantityDefectWeight'
)
BEGIN
    DECLARE @var112 sysname;
    SELECT @var112 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionRecord]') AND [c].[name] = N'DefectQuantity');
    IF @var112 IS NOT NULL EXEC(N'ALTER TABLE [ProductionRecord] DROP CONSTRAINT [' + @var112 + '];');
    ALTER TABLE [ProductionRecord] DROP COLUMN [DefectQuantity];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513211449_RemoveDefectQuantityDefectWeight'
)
BEGIN
    DECLARE @var113 sysname;
    SELECT @var113 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionRecord]') AND [c].[name] = N'DefectWeight');
    IF @var113 IS NOT NULL EXEC(N'ALTER TABLE [ProductionRecord] DROP CONSTRAINT [' + @var113 + '];');
    ALTER TABLE [ProductionRecord] DROP COLUMN [DefectWeight];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513211449_RemoveDefectQuantityDefectWeight'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513211449_RemoveDefectQuantityDefectWeight', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514084213_AddChemicalComposition'
)
BEGIN
    CREATE TABLE [ChemicalComposition] (
        [Id] int NOT NULL IDENTITY,
        [PlantGrade] nvarchar(50) NOT NULL,
        [Carbon] nvarchar(100) NULL,
        [Silicon] nvarchar(100) NULL,
        [Manganese] nvarchar(100) NULL,
        [Phosphorus] nvarchar(100) NULL,
        [Sulfur] nvarchar(100) NULL,
        [Nickel] nvarchar(100) NULL,
        [Chromium] nvarchar(100) NULL,
        [Molybdenum] nvarchar(100) NULL,
        [Copper] nvarchar(100) NULL,
        [Nitrogen] nvarchar(100) NULL,
        [Niobium] nvarchar(100) NULL,
        [Titanium] nvarchar(100) NULL,
        [Iron] nvarchar(100) NULL,
        [Aluminum] nvarchar(100) NULL,
        [Tungsten] nvarchar(100) NULL,
        [PREN] nvarchar(100) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_ChemicalComposition] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514084213_AddChemicalComposition'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ChemicalComposition_PlantGrade] ON [ChemicalComposition] ([PlantGrade]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514084213_AddChemicalComposition'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260514084213_AddChemicalComposition', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514104815_AddFurnaceRegistration'
)
BEGIN
    CREATE TABLE [FurnaceRegistration] (
        [Id] int NOT NULL IDENTITY,
        [IncomingDate] date NOT NULL,
        [RawMaterialUnit] nvarchar(100) NOT NULL,
        [RawMaterialType] nvarchar(50) NOT NULL,
        [RegisteredGrade] nvarchar(100) NOT NULL,
        [RelatedPlantGrade] nvarchar(100) NULL,
        [FurnaceNumber] nvarchar(100) NOT NULL,
        [Specification] nvarchar(100) NULL,
        [Quantity] int NULL,
        [Weight] decimal(18,3) NULL,
        [Carbon] decimal(18,6) NULL,
        [Silicon] decimal(18,6) NULL,
        [Manganese] decimal(18,6) NULL,
        [Phosphorus] decimal(18,6) NULL,
        [Sulfur] decimal(18,6) NULL,
        [Nickel] decimal(18,6) NULL,
        [Chromium] decimal(18,6) NULL,
        [Molybdenum] decimal(18,6) NULL,
        [Copper] decimal(18,6) NULL,
        [Nitrogen] decimal(18,6) NULL,
        [Niobium] decimal(18,6) NULL,
        [Titanium] decimal(18,6) NULL,
        [Iron] decimal(18,6) NULL,
        [Aluminum] decimal(18,6) NULL,
        [Tungsten] decimal(18,6) NULL,
        [PREN] decimal(18,6) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_FurnaceRegistration] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514104815_AddFurnaceRegistration'
)
BEGIN
    CREATE UNIQUE INDEX [UK_FurnaceRegistration_FurnaceNumber] ON [FurnaceRegistration] ([FurnaceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514104815_AddFurnaceRegistration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260514104815_AddFurnaceRegistration', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var114 sysname;
    SELECT @var114 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Tungsten');
    IF @var114 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var114 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Tungsten] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var115 sysname;
    SELECT @var115 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Titanium');
    IF @var115 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var115 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Titanium] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var116 sysname;
    SELECT @var116 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Sulfur');
    IF @var116 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var116 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Sulfur] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var117 sysname;
    SELECT @var117 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Silicon');
    IF @var117 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var117 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Silicon] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var118 sysname;
    SELECT @var118 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Phosphorus');
    IF @var118 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var118 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Phosphorus] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var119 sysname;
    SELECT @var119 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Nitrogen');
    IF @var119 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var119 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Nitrogen] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var120 sysname;
    SELECT @var120 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Niobium');
    IF @var120 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var120 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Niobium] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var121 sysname;
    SELECT @var121 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Nickel');
    IF @var121 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var121 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Nickel] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var122 sysname;
    SELECT @var122 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Molybdenum');
    IF @var122 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var122 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Molybdenum] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var123 sysname;
    SELECT @var123 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Manganese');
    IF @var123 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var123 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Manganese] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var124 sysname;
    SELECT @var124 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Iron');
    IF @var124 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var124 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Iron] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var125 sysname;
    SELECT @var125 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Copper');
    IF @var125 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var125 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Copper] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var126 sysname;
    SELECT @var126 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Chromium');
    IF @var126 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var126 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Chromium] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var127 sysname;
    SELECT @var127 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Carbon');
    IF @var127 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var127 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Carbon] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    DECLARE @var128 sysname;
    SELECT @var128 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FurnaceRegistration]') AND [c].[name] = N'Aluminum');
    IF @var128 IS NOT NULL EXEC(N'ALTER TABLE [FurnaceRegistration] DROP CONSTRAINT [' + @var128 + '];');
    ALTER TABLE [FurnaceRegistration] ALTER COLUMN [Aluminum] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514111304_ChangeChemicalElementPrecisionTo3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260514111304_ChangeChemicalElementPrecisionTo3', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514112147_RemoveFurnaceNumberUniqueIndex'
)
BEGIN
    DROP INDEX [UK_FurnaceRegistration_FurnaceNumber] ON [FurnaceRegistration];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514112147_RemoveFurnaceNumberUniqueIndex'
)
BEGIN
    CREATE INDEX [IX_FurnaceRegistration_FurnaceNumber] ON [FurnaceRegistration] ([FurnaceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514112147_RemoveFurnaceNumberUniqueIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260514112147_RemoveFurnaceNumberUniqueIndex', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514120447_AddChemicalValidationRule'
)
BEGIN
    CREATE TABLE [ChemicalValidationRule] (
        [Id] int NOT NULL IDENTITY,
        [PlantGrade] nvarchar(100) NOT NULL,
        [CMin] nvarchar(50) NULL,
        [CMax] nvarchar(50) NULL,
        [SiMin] nvarchar(50) NULL,
        [SiMax] nvarchar(50) NULL,
        [MnMin] nvarchar(50) NULL,
        [MnMax] nvarchar(50) NULL,
        [PMin] nvarchar(50) NULL,
        [PMax] nvarchar(50) NULL,
        [SMin] nvarchar(50) NULL,
        [SMax] nvarchar(50) NULL,
        [NiMin] nvarchar(50) NULL,
        [NiMax] nvarchar(50) NULL,
        [CrMin] nvarchar(50) NULL,
        [CrMax] nvarchar(50) NULL,
        [MoMin] nvarchar(50) NULL,
        [MoMax] nvarchar(50) NULL,
        [CuMin] nvarchar(50) NULL,
        [CuMax] nvarchar(50) NULL,
        [NMin] nvarchar(50) NULL,
        [NMax] nvarchar(50) NULL,
        [NbMin] nvarchar(50) NULL,
        [NbMax] nvarchar(50) NULL,
        [TiMin] nvarchar(50) NULL,
        [TiMax] nvarchar(50) NULL,
        [FeMin] nvarchar(50) NULL,
        [FeMax] nvarchar(50) NULL,
        [AlMin] nvarchar(50) NULL,
        [AlMax] nvarchar(50) NULL,
        [WMin] nvarchar(50) NULL,
        [WMax] nvarchar(50) NULL,
        [PRENMin] nvarchar(50) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_ChemicalValidationRule] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514120447_AddChemicalValidationRule'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ChemicalValidationRule_PlantGrade] ON [ChemicalValidationRule] ([PlantGrade]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514120447_AddChemicalValidationRule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260514120447_AddChemicalValidationRule', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514163558_AddFinalInspection'
)
BEGIN
    CREATE TABLE [FinalInspection] (
        [Id] int NOT NULL IDENTITY,
        [InspectionItem] nvarchar(20) NOT NULL,
        [InspectionDate] datetime2 NOT NULL,
        [BatchNo] nvarchar(50) NOT NULL,
        [ProductionBatchId] int NOT NULL,
        [MaterialName] nvarchar(50) NULL,
        [TagNo] nvarchar(50) NULL,
        [WorkOrderNo] nvarchar(50) NULL,
        [SalesOrderNo] nvarchar(50) NULL,
        [SourceUnit] nvarchar(200) NULL,
        [FurnaceNo] nvarchar(50) NULL,
        [PlantGrade] nvarchar(50) NULL,
        [Specification] nvarchar(100) NULL,
        [FixedLength] nvarchar(50) NULL,
        [EquipmentName] nvarchar(100) NULL,
        [Shift] nvarchar(10) NULL,
        [Operator] nvarchar(50) NULL,
        [Quantity] int NULL,
        [Weight] decimal(18,3) NULL,
        [QualifiedQuantity] int NULL,
        [QualifiedWeight] decimal(18,3) NULL,
        [DefectReworkQuantity] int NULL,
        [DefectWarehouseQuantity] int NULL,
        [DefectScrapQuantity] int NULL,
        [DefectDescription] nvarchar(500) NULL,
        [OuterDiameterRange] nvarchar(100) NULL,
        [WallThicknessRange] nvarchar(100) NULL,
        [LengthAllowanceRange] nvarchar(100) NULL,
        [Pressure] decimal(18,3) NULL,
        [HoldTime] int NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_FinalInspection] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FinalInspection_ProductionBatch_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatch] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514163558_AddFinalInspection'
)
BEGIN
    CREATE INDEX [IX_FinalInspection_BatchNo] ON [FinalInspection] ([BatchNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514163558_AddFinalInspection'
)
BEGIN
    CREATE INDEX [IX_FinalInspection_InspectionDate] ON [FinalInspection] ([InspectionDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514163558_AddFinalInspection'
)
BEGIN
    CREATE INDEX [IX_FinalInspection_InspectionItem] ON [FinalInspection] ([InspectionItem]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514163558_AddFinalInspection'
)
BEGIN
    CREATE INDEX [IX_FinalInspection_ProductionBatchId] ON [FinalInspection] ([ProductionBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514163558_AddFinalInspection'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260514163558_AddFinalInspection', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514201533_AddRoundBarPiercingPlan'
)
BEGIN
    CREATE TABLE [RoundBarPiercingPlan] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [PlanDate] date NOT NULL,
        [AdjustedWallThickness] decimal(18,3) NOT NULL,
        [YieldRate] decimal(5,2) NOT NULL,
        [InputMultiple] int NOT NULL DEFAULT 1,
        [QualifiedRate] decimal(5,2) NOT NULL,
        [Density] decimal(18,4) NULL,
        [UnitWeight] decimal(18,3) NULL,
        [RawUnitWeight] decimal(18,3) NULL,
        [PlantGrade] nvarchar(100) NOT NULL,
        [RawMaterialType] nvarchar(20) NOT NULL,
        [RoundBarSpec] nvarchar(100) NOT NULL,
        [PiercingSpec] nvarchar(100) NOT NULL,
        [RequiredUnitWeight] decimal(18,3) NULL,
        [RequiredPieces] int NULL,
        [RequiredWeight] decimal(18,3) NOT NULL,
        [RequiredDate] date NOT NULL,
        [ProcessPlan] nvarchar(max) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_RoundBarPiercingPlan] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoundBarPiercingPlan_WorkOrder_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrder] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514201533_AddRoundBarPiercingPlan'
)
BEGIN
    CREATE INDEX [IX_RoundBarPiercingPlan_WorkOrderId] ON [RoundBarPiercingPlan] ([WorkOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514201533_AddRoundBarPiercingPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260514201533_AddRoundBarPiercingPlan', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE TABLE [Equipment] (
        [Id] int NOT NULL IDENTITY,
        [EquipmentCode] nvarchar(50) NOT NULL,
        [EquipmentName] nvarchar(200) NOT NULL,
        [ModelNumber] nvarchar(100) NULL,
        [TechnicalParams] nvarchar(500) NULL,
        [Manufacturer] nvarchar(200) NULL,
        [InstallationDate] date NULL,
        [Remark] nvarchar(500) NULL,
        [Location] nvarchar(100) NULL,
        [RelatedSection] nvarchar(100) NULL,
        [NeedInspection] bit NOT NULL DEFAULT CAST(0 AS bit),
        [InspectionPerson] nvarchar(50) NULL,
        [InspectionCycleDays] int NOT NULL DEFAULT 7,
        [LastInspectionDate] date NULL,
        [NextInspectionDate] date NULL,
        [InspectionStatus] nvarchar(20) NOT NULL DEFAULT N'Normal',
        [NeedMaintenance] bit NOT NULL DEFAULT CAST(0 AS bit),
        [MaintPerson] nvarchar(50) NULL,
        [MaintCycleDays] int NOT NULL DEFAULT 30,
        [LastMaintDate] date NULL,
        [NextMaintDate] date NULL,
        [MaintStatus] nvarchar(20) NOT NULL DEFAULT N'Normal',
        [LifecycleStatus] nvarchar(20) NOT NULL DEFAULT N'Active',
        [UsageType] nvarchar(20) NOT NULL DEFAULT N'Primary',
        [RunningStatus] nvarchar(20) NOT NULL DEFAULT N'Normal',
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Equipment] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE TABLE [InspectionRecord] (
        [Id] int NOT NULL IDENTITY,
        [RecordNo] nvarchar(50) NOT NULL,
        [EquipmentId] int NOT NULL,
        [ScheduledDate] date NOT NULL,
        [ActualDate] date NULL,
        [Inspector] nvarchar(50) NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [ChecklistResults] nvarchar(max) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_InspectionRecord] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InspectionRecord_Equipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE TABLE [MaintenanceOrder] (
        [Id] int NOT NULL IDENTITY,
        [MaintOrderNo] nvarchar(50) NOT NULL,
        [EquipmentId] int NOT NULL,
        [MaintType] nvarchar(20) NOT NULL DEFAULT N'Monthly',
        [ScheduledDate] date NOT NULL,
        [ActualDate] date NULL,
        [Executor] nvarchar(50) NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [ChecklistResults] nvarchar(max) NULL,
        [Remark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_MaintenanceOrder] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaintenanceOrder_Equipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE TABLE [RepairOrder] (
        [Id] int NOT NULL IDENTITY,
        [RepairOrderNo] nvarchar(50) NOT NULL,
        [EquipmentId] int NOT NULL,
        [FaultDescription] nvarchar(500) NOT NULL,
        [FaultType] nvarchar(50) NULL,
        [Priority] nvarchar(20) NOT NULL DEFAULT N'Normal',
        [RepairStatus] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [ReportPerson] nvarchar(50) NOT NULL,
        [ReportTime] datetime2 NOT NULL,
        [RepairPerson] nvarchar(50) NULL,
        [RepairStartTime] datetime2 NULL,
        [RepairEndTime] datetime2 NULL,
        [RepairContent] nvarchar(1000) NULL,
        [SparePartUsed] nvarchar(500) NULL,
        [DowntimeHours] decimal(18,2) NULL,
        [VerifyPerson] nvarchar(50) NULL,
        [VerifyTime] datetime2 NULL,
        [VerifyComment] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_RepairOrder] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RepairOrder_Equipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_Equipment_InspectionStatus] ON [Equipment] ([InspectionStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_Equipment_LifecycleStatus] ON [Equipment] ([LifecycleStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_Equipment_Location] ON [Equipment] ([Location]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_Equipment_MaintStatus] ON [Equipment] ([MaintStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_Equipment_Name] ON [Equipment] ([EquipmentName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_Equipment_NeedInspection] ON [Equipment] ([NeedInspection]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_Equipment_NeedMaintenance] ON [Equipment] ([NeedMaintenance]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_Equipment_RelatedSection] ON [Equipment] ([RelatedSection]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_Equipment_RunningStatus] ON [Equipment] ([RunningStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_Equipment_Code] ON [Equipment] ([EquipmentCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_InspectionRecord_EquipmentId] ON [InspectionRecord] ([EquipmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_InspectionRecord_ScheduledDate] ON [InspectionRecord] ([ScheduledDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_InspectionRecord_Status] ON [InspectionRecord] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_InspectionRecord_No] ON [InspectionRecord] ([RecordNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_MaintenanceOrder_EquipmentId] ON [MaintenanceOrder] ([EquipmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_MaintenanceOrder_ScheduledDate] ON [MaintenanceOrder] ([ScheduledDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_MaintenanceOrder_Status] ON [MaintenanceOrder] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_MaintenanceOrder_No] ON [MaintenanceOrder] ([MaintOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_RepairOrder_EquipmentId] ON [RepairOrder] ([EquipmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_RepairOrder_ReportTime] ON [RepairOrder] ([ReportTime]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE INDEX [IX_RepairOrder_Status] ON [RepairOrder] ([RepairStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_RepairOrder_No] ON [RepairOrder] ([RepairOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515190813_AddEquipmentContext'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515190813_AddEquipmentContext', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515221728_RefactorEquipmentContext'
)
BEGIN
    DROP INDEX [IX_Equipment_InspectionStatus] ON [Equipment];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515221728_RefactorEquipmentContext'
)
BEGIN
    DROP INDEX [IX_Equipment_MaintStatus] ON [Equipment];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515221728_RefactorEquipmentContext'
)
BEGIN
    DROP INDEX [IX_Equipment_RunningStatus] ON [Equipment];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515221728_RefactorEquipmentContext'
)
BEGIN
    DECLARE @var129 sysname;
    SELECT @var129 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Equipment]') AND [c].[name] = N'InspectionStatus');
    IF @var129 IS NOT NULL EXEC(N'ALTER TABLE [Equipment] DROP CONSTRAINT [' + @var129 + '];');
    ALTER TABLE [Equipment] DROP COLUMN [InspectionStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515221728_RefactorEquipmentContext'
)
BEGIN
    DECLARE @var130 sysname;
    SELECT @var130 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Equipment]') AND [c].[name] = N'MaintStatus');
    IF @var130 IS NOT NULL EXEC(N'ALTER TABLE [Equipment] DROP CONSTRAINT [' + @var130 + '];');
    ALTER TABLE [Equipment] DROP COLUMN [MaintStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515221728_RefactorEquipmentContext'
)
BEGIN
    DECLARE @var131 sysname;
    SELECT @var131 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Equipment]') AND [c].[name] = N'RunningStatus');
    IF @var131 IS NOT NULL EXEC(N'ALTER TABLE [Equipment] DROP CONSTRAINT [' + @var131 + '];');
    ALTER TABLE [Equipment] DROP COLUMN [RunningStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515221728_RefactorEquipmentContext'
)
BEGIN
    EXEC sp_rename N'[Equipment].[NextMaintDate]', N'CurrentMaintStartDate', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515221728_RefactorEquipmentContext'
)
BEGIN
    EXEC sp_rename N'[Equipment].[NextInspectionDate]', N'CurrentInspectionStartDate', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515221728_RefactorEquipmentContext'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515221728_RefactorEquipmentContext', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DROP INDEX [IX_MaintenanceOrder_ScheduledDate] ON [MaintenanceOrder];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DROP INDEX [IX_MaintenanceOrder_Status] ON [MaintenanceOrder];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DROP INDEX [IX_InspectionRecord_ScheduledDate] ON [InspectionRecord];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DROP INDEX [IX_InspectionRecord_Status] ON [InspectionRecord];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var132 sysname;
    SELECT @var132 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RepairOrder]') AND [c].[name] = N'DowntimeHours');
    IF @var132 IS NOT NULL EXEC(N'ALTER TABLE [RepairOrder] DROP CONSTRAINT [' + @var132 + '];');
    ALTER TABLE [RepairOrder] DROP COLUMN [DowntimeHours];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var133 sysname;
    SELECT @var133 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RepairOrder]') AND [c].[name] = N'VerifyComment');
    IF @var133 IS NOT NULL EXEC(N'ALTER TABLE [RepairOrder] DROP CONSTRAINT [' + @var133 + '];');
    ALTER TABLE [RepairOrder] DROP COLUMN [VerifyComment];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var134 sysname;
    SELECT @var134 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RepairOrder]') AND [c].[name] = N'VerifyPerson');
    IF @var134 IS NOT NULL EXEC(N'ALTER TABLE [RepairOrder] DROP CONSTRAINT [' + @var134 + '];');
    ALTER TABLE [RepairOrder] DROP COLUMN [VerifyPerson];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var135 sysname;
    SELECT @var135 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RepairOrder]') AND [c].[name] = N'VerifyTime');
    IF @var135 IS NOT NULL EXEC(N'ALTER TABLE [RepairOrder] DROP CONSTRAINT [' + @var135 + '];');
    ALTER TABLE [RepairOrder] DROP COLUMN [VerifyTime];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var136 sysname;
    SELECT @var136 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceOrder]') AND [c].[name] = N'ChecklistResults');
    IF @var136 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceOrder] DROP CONSTRAINT [' + @var136 + '];');
    ALTER TABLE [MaintenanceOrder] DROP COLUMN [ChecklistResults];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var137 sysname;
    SELECT @var137 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceOrder]') AND [c].[name] = N'MaintType');
    IF @var137 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceOrder] DROP CONSTRAINT [' + @var137 + '];');
    ALTER TABLE [MaintenanceOrder] DROP COLUMN [MaintType];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var138 sysname;
    SELECT @var138 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceOrder]') AND [c].[name] = N'ScheduledDate');
    IF @var138 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceOrder] DROP CONSTRAINT [' + @var138 + '];');
    ALTER TABLE [MaintenanceOrder] DROP COLUMN [ScheduledDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var139 sysname;
    SELECT @var139 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceOrder]') AND [c].[name] = N'Status');
    IF @var139 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceOrder] DROP CONSTRAINT [' + @var139 + '];');
    ALTER TABLE [MaintenanceOrder] DROP COLUMN [Status];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var140 sysname;
    SELECT @var140 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InspectionRecord]') AND [c].[name] = N'ChecklistResults');
    IF @var140 IS NOT NULL EXEC(N'ALTER TABLE [InspectionRecord] DROP CONSTRAINT [' + @var140 + '];');
    ALTER TABLE [InspectionRecord] DROP COLUMN [ChecklistResults];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var141 sysname;
    SELECT @var141 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InspectionRecord]') AND [c].[name] = N'ScheduledDate');
    IF @var141 IS NOT NULL EXEC(N'ALTER TABLE [InspectionRecord] DROP CONSTRAINT [' + @var141 + '];');
    ALTER TABLE [InspectionRecord] DROP COLUMN [ScheduledDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    DECLARE @var142 sysname;
    SELECT @var142 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InspectionRecord]') AND [c].[name] = N'Status');
    IF @var142 IS NOT NULL EXEC(N'ALTER TABLE [InspectionRecord] DROP CONSTRAINT [' + @var142 + '];');
    ALTER TABLE [InspectionRecord] DROP COLUMN [Status];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    ALTER TABLE [Equipment] ADD [LastRepairDate] date NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515230645_SimplifyEquipmentContext'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515230645_SimplifyEquipmentContext', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515235429_AddExecutionSummaryFields'
)
BEGIN
    ALTER TABLE [MaintenanceOrder] ADD [ExecutionSummary] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515235429_AddExecutionSummaryFields'
)
BEGIN
    ALTER TABLE [InspectionRecord] ADD [ExecutionSummary] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515235429_AddExecutionSummaryFields'
)
BEGIN
    DROP INDEX [IX_Equipment_Location] ON [Equipment];
    DECLARE @var143 sysname;
    SELECT @var143 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Equipment]') AND [c].[name] = N'Location');
    IF @var143 IS NOT NULL EXEC(N'ALTER TABLE [Equipment] DROP CONSTRAINT [' + @var143 + '];');
    EXEC(N'UPDATE [Equipment] SET [Location] = N'''' WHERE [Location] IS NULL');
    ALTER TABLE [Equipment] ALTER COLUMN [Location] nvarchar(100) NOT NULL;
    ALTER TABLE [Equipment] ADD DEFAULT N'' FOR [Location];
    CREATE INDEX [IX_Equipment_Location] ON [Equipment] ([Location]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515235429_AddExecutionSummaryFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515235429_AddExecutionSummaryFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516211553_AddWorkOrderExecutionSummary'
)
BEGIN
    CREATE TABLE [WorkOrderExecutionSummary] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [WorkOrderNo] nvarchar(50) NOT NULL,
        [Salesman] nvarchar(50) NOT NULL,
        [CustomerName] nvarchar(200) NOT NULL,
        [SignDate] datetime2 NOT NULL,
        [DeliveryDate] datetime2 NOT NULL,
        [DelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
        [SettlementMethod] nvarchar(20) NOT NULL,
        [SalesOrderNo] nvarchar(50) NOT NULL,
        [ProductionMainNo] nvarchar(50) NOT NULL,
        [ProductionSubNo] nvarchar(50) NULL,
        [MaterialName] nvarchar(50) NOT NULL,
        [DeliveryState] nvarchar(50) NOT NULL,
        [PlantGrade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [LengthStatus] nvarchar(20) NOT NULL,
        [MinLength] decimal(18,2) NULL,
        [MaxLength] decimal(18,2) NULL,
        [TotalItemCount] int NOT NULL DEFAULT 0,
        [TotalQuantity] int NOT NULL DEFAULT 0,
        [TotalMeters] decimal(18,2) NOT NULL DEFAULT 0.0,
        [TotalWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [LatestPlanDate] date NULL,
        [MaterialPlanRate] decimal(5,2) NOT NULL DEFAULT 0.0,
        [MaterialPlanStatus] int NOT NULL DEFAULT 0,
        [MainNoMaterialPlanRate] decimal(5,2) NOT NULL DEFAULT 0.0,
        [MainNoMaterialPlanStatus] int NOT NULL DEFAULT 0,
        [InputStartDate] date NULL,
        [InputEndDate] date NULL,
        [TotalBatchCount] int NOT NULL DEFAULT 0,
        [InputQuantity] int NOT NULL DEFAULT 0,
        [InputWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [TheoreticalOutputQty] decimal(18,3) NOT NULL DEFAULT 0.0,
        [TheoreticalOutputWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [InputOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
        [InputStatus] int NOT NULL DEFAULT 0,
        [MainNoInputOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
        [MainNoInputStatus] int NOT NULL DEFAULT 0,
        [ValidBatchCount] int NOT NULL DEFAULT 0,
        [ValidInputQuantity] int NOT NULL DEFAULT 0,
        [ValidInputWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [ValidOutputQty] decimal(18,3) NOT NULL DEFAULT 0.0,
        [ValidOutputWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [ValidInputOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
        [ValidInputStatus] int NOT NULL DEFAULT 0,
        [LastRefreshTime] datetime2 NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_WorkOrderExecutionSummary] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516211553_AddWorkOrderExecutionSummary'
)
BEGIN
    CREATE INDEX [IX_WES_InputStatus] ON [WorkOrderExecutionSummary] ([InputStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516211553_AddWorkOrderExecutionSummary'
)
BEGIN
    CREATE INDEX [IX_WES_ProductionMainNo] ON [WorkOrderExecutionSummary] ([ProductionMainNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516211553_AddWorkOrderExecutionSummary'
)
BEGIN
    CREATE INDEX [IX_WES_SalesOrderNo] ON [WorkOrderExecutionSummary] ([SalesOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516211553_AddWorkOrderExecutionSummary'
)
BEGIN
    CREATE INDEX [IX_WES_WorkOrderNo] ON [WorkOrderExecutionSummary] ([WorkOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516211553_AddWorkOrderExecutionSummary'
)
BEGIN
    CREATE UNIQUE INDEX [UK_WES_WorkOrderId] ON [WorkOrderExecutionSummary] ([WorkOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516211553_AddWorkOrderExecutionSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516211553_AddWorkOrderExecutionSummary', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516223231_AddManufacturingItemToProductionBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [ManufacturingItem] nvarchar(30) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516223231_AddManufacturingItemToProductionBatch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516223231_AddManufacturingItemToProductionBatch', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517011900_AddProductionRecordDataSource'
)
BEGIN
    ALTER TABLE [ProductionRecord] ADD [DataSource] nvarchar(10) NULL DEFAULT N'MANUAL';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517011900_AddProductionRecordDataSource'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260517011900_AddProductionRecordDataSource', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517144122_AddMainNoValidInputOutputRatio'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [MainNoValidInputOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517144122_AddMainNoValidInputOutputRatio'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [MainNoValidInputStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517144122_AddMainNoValidInputOutputRatio'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260517144122_AddMainNoValidInputOutputRatio', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517172335_AddProcessGroupManufacturingMultiple'
)
BEGIN
    ALTER TABLE [ProcessGroup] ADD [ManufacturingMultiple] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517172335_AddProcessGroupManufacturingMultiple'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260517172335_AddProcessGroupManufacturingMultiple', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517183955_MakeProductionRatioNonNullable'
)
BEGIN
    UPDATE [ProductionBatch] SET [ProductionRatio] = 0 WHERE [ProductionRatio] IS NULL
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517183955_MakeProductionRatioNonNullable'
)
BEGIN
    DECLARE @var144 sysname;
    SELECT @var144 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'ProductionRatio');
    IF @var144 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var144 + '];');
    EXEC(N'UPDATE [ProductionBatch] SET [ProductionRatio] = 0 WHERE [ProductionRatio] IS NULL');
    ALTER TABLE [ProductionBatch] ALTER COLUMN [ProductionRatio] int NOT NULL;
    ALTER TABLE [ProductionBatch] ADD DEFAULT 0 FOR [ProductionRatio];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517183955_MakeProductionRatioNonNullable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260517183955_MakeProductionRatioNonNullable', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519195606_DropProductionRecordUK'
)
BEGIN
    DROP INDEX [UK_ProductionRecord_Section] ON [ProductionRecord];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519195606_DropProductionRecordUK'
)
BEGIN
    CREATE INDEX [IX_ProductionRecord_Section] ON [ProductionRecord] ([ProductionBatchId], [ProcessGroupId], [SectionName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519195606_DropProductionRecordUK'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519195606_DropProductionRecordUK', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519202738_DropSectionOutsourceUK'
)
BEGIN
    DROP INDEX [UK_SectionOutsource_Section] ON [SectionOutsource];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519202738_DropSectionOutsourceUK'
)
BEGIN
    CREATE INDEX [IX_SectionOutsource_Section] ON [SectionOutsource] ([ProductionBatchId], [ProcessGroupId], [SectionName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519202738_DropSectionOutsourceUK'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519202738_DropSectionOutsourceUK', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519234641_AddQualifiedConcessionFields'
)
BEGIN
    ALTER TABLE [ProcessInspection] ADD [ConcessionRemark] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519234641_AddQualifiedConcessionFields'
)
BEGIN
    ALTER TABLE [ProcessInspection] ADD [QualifiedConcessionQuantity] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519234641_AddQualifiedConcessionFields'
)
BEGIN
    ALTER TABLE [FinalInspection] ADD [ConcessionRemark] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519234641_AddQualifiedConcessionFields'
)
BEGIN
    ALTER TABLE [FinalInspection] ADD [QualifiedConcessionQuantity] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519234641_AddQualifiedConcessionFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519234641_AddQualifiedConcessionFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521022534_AddOrderListSummary'
)
BEGIN
    CREATE TABLE [OrderListSummary] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [OrderNumber] nvarchar(50) NOT NULL,
        [SignDate] datetime2 NOT NULL,
        [CustomerName] nvarchar(200) NOT NULL,
        [Salesman] nvarchar(50) NOT NULL,
        [EndCustomer] nvarchar(200) NULL,
        [DeliveryStart] date NULL,
        [DeliveryEnd] date NULL,
        [HasDelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
        [TotalContractWeight] int NOT NULL DEFAULT 0,
        [ItemCount] int NOT NULL DEFAULT 0,
        [HasTechReqCount] int NOT NULL DEFAULT 0,
        [Status] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [LastChangeDate] datetime2 NULL,
        [FirstOrderItemId] int NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_OrderListSummary] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521022534_AddOrderListSummary'
)
BEGIN
    CREATE INDEX [IX_OLS_CustomerName] ON [OrderListSummary] ([CustomerName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521022534_AddOrderListSummary'
)
BEGIN
    CREATE INDEX [IX_OLS_DeliveryEnd] ON [OrderListSummary] ([DeliveryEnd]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521022534_AddOrderListSummary'
)
BEGIN
    CREATE INDEX [IX_OLS_OrderNumber] ON [OrderListSummary] ([OrderNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521022534_AddOrderListSummary'
)
BEGIN
    CREATE INDEX [IX_OLS_SignDate] ON [OrderListSummary] ([SignDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521022534_AddOrderListSummary'
)
BEGIN
    CREATE INDEX [IX_OLS_Status] ON [OrderListSummary] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521022534_AddOrderListSummary'
)
BEGIN
    CREATE UNIQUE INDEX [UK_OLS_OrderId] ON [OrderListSummary] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521022534_AddOrderListSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260521022534_AddOrderListSummary', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE TABLE [WorkOrderListSummary] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [WorkOrderNo] nvarchar(50) NOT NULL,
        [SalesOrderNo] nvarchar(50) NOT NULL,
        [ProductionMainNo] nvarchar(50) NOT NULL,
        [ProductionSubNo] nvarchar(50) NULL,
        [OrderItemIds] nvarchar(500) NULL,
        [SignDate] datetime2 NOT NULL,
        [Salesman] nvarchar(50) NOT NULL,
        [EndCustomer] nvarchar(200) NULL,
        [DeliveryDate] datetime2 NOT NULL,
        [DelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
        [SettlementMethod] nvarchar(20) NOT NULL,
        [MaterialName] nvarchar(20) NOT NULL,
        [StandardCode] nvarchar(100) NULL,
        [DeliveryState] nvarchar(50) NOT NULL,
        [PlantGrade] nvarchar(100) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [OuterDiameterNegative] decimal(18,2) NULL,
        [OuterDiameterPositive] decimal(18,2) NULL,
        [WallThicknessNegative] decimal(18,2) NULL,
        [WallThicknessPositive] decimal(18,2) NULL,
        [LengthStatus] nvarchar(20) NOT NULL,
        [MinLength] decimal(18,2) NULL,
        [MaxLength] decimal(18,2) NULL,
        [TotalQuantity] int NOT NULL DEFAULT 0,
        [TotalMeters] decimal(18,2) NOT NULL DEFAULT 0.0,
        [TotalWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [TotalItemCount] int NOT NULL DEFAULT 0,
        [ItemDetails] nvarchar(max) NULL,
        [TechnicalRequirements] nvarchar(20) NOT NULL DEFAULT N'Normal',
        [Status] int NOT NULL DEFAULT 0,
        [CreatedTime] datetimeoffset NOT NULL,
        [LatestPlanDate] date NULL,
        [MaterialPlanRate] decimal(5,2) NOT NULL DEFAULT 0.0,
        [MaterialPlanStatus] int NOT NULL DEFAULT 0,
        [SemiPlanTotalWeight] decimal(18,3) NULL,
        [SemiPlanTotalPieces] int NULL,
        [FinishedPlanTotalWeight] decimal(18,3) NULL,
        [FinishedPlanTotalPieces] int NULL,
        [InventoryPlanTotalWeight] decimal(18,3) NULL,
        [InventoryPlanTotalPieces] int NULL,
        [ReworkPlanTotalWeight] decimal(18,3) NULL,
        [ReworkPlanTotalPieces] int NULL,
        [PiercingPlanTotalWeight] decimal(18,3) NULL,
        [PiercingPlanTotalPieces] int NULL,
        [MainNoMaterialPlanRate] decimal(5,2) NOT NULL DEFAULT 0.0,
        [MainNoMaterialPlanStatus] int NOT NULL DEFAULT 0,
        [OrderMaterialPlanStatus] int NOT NULL DEFAULT 0,
        [RowVersion] rowversion NULL,
        [LastRefreshTime] datetime2 NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_WorkOrderListSummary] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE TABLE [WorkOrderStatusSummary] (
        [Id] int NOT NULL IDENTITY,
        [SalesOrderId] int NOT NULL,
        [OrderNumber] nvarchar(50) NOT NULL,
        [SignDate] datetime2 NOT NULL,
        [CustomerName] nvarchar(200) NOT NULL,
        [Salesman] nvarchar(50) NOT NULL,
        [EndCustomer] nvarchar(200) NULL,
        [DeliveryStart] date NULL,
        [DeliveryEnd] date NULL,
        [HasDelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
        [TotalContractWeight] int NOT NULL DEFAULT 0,
        [ItemCount] int NOT NULL DEFAULT 0,
        [WorkOrderCount] int NOT NULL DEFAULT 0,
        [WorkOrderStatus] nvarchar(20) NOT NULL DEFAULT N'NotGenerated',
        [HasWorkOrder] bit NOT NULL DEFAULT CAST(0 AS bit),
        [WorkOrderId] int NULL,
        [RowVersion] rowversion NULL,
        [LastChangeDate] datetime2 NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_WorkOrderStatusSummary] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOLS_LatestPlanDate] ON [WorkOrderListSummary] ([LatestPlanDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOLS_MainNoMaterialPlanStatus] ON [WorkOrderListSummary] ([MainNoMaterialPlanStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOLS_MaterialPlanStatus] ON [WorkOrderListSummary] ([MaterialPlanStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOLS_OrderMaterialPlanStatus] ON [WorkOrderListSummary] ([OrderMaterialPlanStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOLS_ProductionMainNo] ON [WorkOrderListSummary] ([ProductionMainNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOLS_SalesOrderNo] ON [WorkOrderListSummary] ([SalesOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOLS_Status] ON [WorkOrderListSummary] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOLS_WorkOrderNo] ON [WorkOrderListSummary] ([WorkOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE UNIQUE INDEX [UK_WOLS_WorkOrderId] ON [WorkOrderListSummary] ([WorkOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOSS_CustomerName] ON [WorkOrderStatusSummary] ([CustomerName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOSS_OrderNumber] ON [WorkOrderStatusSummary] ([OrderNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOSS_SignDate] ON [WorkOrderStatusSummary] ([SignDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE INDEX [IX_WOSS_WorkOrderStatus] ON [WorkOrderStatusSummary] ([WorkOrderStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    CREATE UNIQUE INDEX [UK_WOSS_SalesOrderId] ON [WorkOrderStatusSummary] ([SalesOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521104953_AddWorkOrderReadModels'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260521104953_AddWorkOrderReadModels', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521112129_FixPlanRatePrecision'
)
BEGIN
    DECLARE @var145 sysname;
    SELECT @var145 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderListSummary]') AND [c].[name] = N'MaterialPlanRate');
    IF @var145 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderListSummary] DROP CONSTRAINT [' + @var145 + '];');
    ALTER TABLE [WorkOrderListSummary] ALTER COLUMN [MaterialPlanRate] decimal(7,2) NOT NULL;
    ALTER TABLE [WorkOrderListSummary] ADD DEFAULT 0.0 FOR [MaterialPlanRate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521112129_FixPlanRatePrecision'
)
BEGIN
    DECLARE @var146 sysname;
    SELECT @var146 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderListSummary]') AND [c].[name] = N'MainNoMaterialPlanRate');
    IF @var146 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderListSummary] DROP CONSTRAINT [' + @var146 + '];');
    ALTER TABLE [WorkOrderListSummary] ALTER COLUMN [MainNoMaterialPlanRate] decimal(7,2) NOT NULL;
    ALTER TABLE [WorkOrderListSummary] ADD DEFAULT 0.0 FOR [MainNoMaterialPlanRate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521112129_FixPlanRatePrecision'
)
BEGIN
    DECLARE @var147 sysname;
    SELECT @var147 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderExecutionSummary]') AND [c].[name] = N'MaterialPlanRate');
    IF @var147 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderExecutionSummary] DROP CONSTRAINT [' + @var147 + '];');
    ALTER TABLE [WorkOrderExecutionSummary] ALTER COLUMN [MaterialPlanRate] decimal(7,2) NOT NULL;
    ALTER TABLE [WorkOrderExecutionSummary] ADD DEFAULT 0.0 FOR [MaterialPlanRate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521112129_FixPlanRatePrecision'
)
BEGIN
    DECLARE @var148 sysname;
    SELECT @var148 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderExecutionSummary]') AND [c].[name] = N'MainNoMaterialPlanRate');
    IF @var148 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderExecutionSummary] DROP CONSTRAINT [' + @var148 + '];');
    ALTER TABLE [WorkOrderExecutionSummary] ALTER COLUMN [MainNoMaterialPlanRate] decimal(7,2) NOT NULL;
    ALTER TABLE [WorkOrderExecutionSummary] ADD DEFAULT 0.0 FOR [MainNoMaterialPlanRate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521112129_FixPlanRatePrecision'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260521112129_FixPlanRatePrecision', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260522202809_AddValidInputQuestionToBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [ValidInputQuestion] bit NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260522202809_AddValidInputQuestionToBatch'
)
BEGIN
    UPDATE [ProductionBatch] SET [ValidInputQuestion] = 0
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260522202809_AddValidInputQuestionToBatch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260522202809_AddValidInputQuestionToBatch', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162153_AddDataSourceToAllModules'
)
BEGIN
    ALTER TABLE [SectionOutsource] ADD [DataSource] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162153_AddDataSourceToAllModules'
)
BEGIN
    ALTER TABLE [ProcessInspection] ADD [DataSource] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162153_AddDataSourceToAllModules'
)
BEGIN
    ALTER TABLE [OutsourceRecovery] ADD [DataSource] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162153_AddDataSourceToAllModules'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [DataSource] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162153_AddDataSourceToAllModules'
)
BEGIN
    ALTER TABLE [FinalInspection] ADD [DataSource] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162153_AddDataSourceToAllModules'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523162153_AddDataSourceToAllModules', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523190936_AddCurrentSectionCompletedToBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [CurrentSectionCompleted] bit NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523190936_AddCurrentSectionCompletedToBatch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523190936_AddCurrentSectionCompletedToBatch', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524220441_AddPurchaseFinishedPlanInputMultiple'
)
BEGIN
    ALTER TABLE [PurchaseOrder] ADD [InputMultiple] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524220441_AddPurchaseFinishedPlanInputMultiple'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [InputMultiple] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524220441_AddPurchaseFinishedPlanInputMultiple'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524220441_AddPurchaseFinishedPlanInputMultiple', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524230940_AddSubcontractReturnItemInputMultiple'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [InputMultiple] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524230940_AddSubcontractReturnItemInputMultiple'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524230940_AddSubcontractReturnItemInputMultiple', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525010648_AddWorkOrderExecutionG5PurchaseOrderFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingOutsourceFinishQty] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525010648_AddWorkOrderExecutionG5PurchaseOrderFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingOutsourceFinishWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525010648_AddWorkOrderExecutionG5PurchaseOrderFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingRoughTubeQty] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525010648_AddWorkOrderExecutionG5PurchaseOrderFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingRoughTubeWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525010648_AddWorkOrderExecutionG5PurchaseOrderFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [TheoreticalFinishQty] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525010648_AddWorkOrderExecutionG5PurchaseOrderFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [TheoreticalFinishWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525010648_AddWorkOrderExecutionG5PurchaseOrderFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525010648_AddWorkOrderExecutionG5PurchaseOrderFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525133205_AddSubcontractReturnItemProcessFields'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [IsForceCompleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525133205_AddSubcontractReturnItemProcessFields'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [ProcessStatus] nvarchar(20) NOT NULL DEFAULT N'Pending';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525133205_AddSubcontractReturnItemProcessFields'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [ReturnedQuantity] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525133205_AddSubcontractReturnItemProcessFields'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [ReturnedWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525133205_AddSubcontractReturnItemProcessFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525133205_AddSubcontractReturnItemProcessFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525145104_AddWorkOrderExecutionGroup6ReworkFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ReworkBatchCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525145104_AddWorkOrderExecutionGroup6ReworkFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ReworkInputEndDate] date NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525145104_AddWorkOrderExecutionGroup6ReworkFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ReworkInputQuantity] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525145104_AddWorkOrderExecutionGroup6ReworkFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ReworkInputWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525145104_AddWorkOrderExecutionGroup6ReworkFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ReworkTheoreticalOutputQty] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525145104_AddWorkOrderExecutionGroup6ReworkFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ReworkTheoreticalOutputWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525145104_AddWorkOrderExecutionGroup6ReworkFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525145104_AddWorkOrderExecutionGroup6ReworkFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525152212_AddWorkOrderExecutionGroup7FlowFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [FlowOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525152212_AddWorkOrderExecutionGroup7FlowFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [FlowStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525152212_AddWorkOrderExecutionGroup7FlowFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [MainNoFlowOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525152212_AddWorkOrderExecutionGroup7FlowFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [MainNoFlowStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525152212_AddWorkOrderExecutionGroup7FlowFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525152212_AddWorkOrderExecutionGroup7FlowFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [DefectiveOutputQty] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [DefectiveOutputWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [DefectiveRatio] decimal(8,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [DefectiveRawQty] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [DefectiveRawWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [GeneralDefectRatio] decimal(8,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [GeneralDefectWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [InspectionDefectQty] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [InspectionDefectRatio] decimal(8,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [InspectionDefectWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ScrapRatio] decimal(8,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ScrapWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [SeriousDefectRatio] decimal(8,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [SeriousDefectWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525171309_AddWorkOrderExecutionG8G9G10DefectFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525175507_AddWorkOrderExecutionG11WarehousingFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [MainNoWarehousingStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525175507_AddWorkOrderExecutionG11WarehousingFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [OrderWarehousingStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525175507_AddWorkOrderExecutionG11WarehousingFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [WarehousingEndDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525175507_AddWorkOrderExecutionG11WarehousingFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [WarehousingStartDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525175507_AddWorkOrderExecutionG11WarehousingFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [WarehousingTotalQty] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525175507_AddWorkOrderExecutionG11WarehousingFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [WarehousingTotalWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525175507_AddWorkOrderExecutionG11WarehousingFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [WoWarehousingStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525175507_AddWorkOrderExecutionG11WarehousingFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525175507_AddWorkOrderExecutionG11WarehousingFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525181132_AddWorkOrderExecutionG9DateFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [InspectionEndDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525181132_AddWorkOrderExecutionG9DateFields'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [InspectionStartDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525181132_AddWorkOrderExecutionG9DateFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525181132_AddWorkOrderExecutionG9DateFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525183735_RemoveG4DeadFields'
)
BEGIN
    DECLARE @var149 sysname;
    SELECT @var149 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderExecutionSummary]') AND [c].[name] = N'MainNoValidInputOutputRatio');
    IF @var149 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderExecutionSummary] DROP CONSTRAINT [' + @var149 + '];');
    ALTER TABLE [WorkOrderExecutionSummary] DROP COLUMN [MainNoValidInputOutputRatio];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525183735_RemoveG4DeadFields'
)
BEGIN
    DECLARE @var150 sysname;
    SELECT @var150 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderExecutionSummary]') AND [c].[name] = N'MainNoValidInputStatus');
    IF @var150 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderExecutionSummary] DROP CONSTRAINT [' + @var150 + '];');
    ALTER TABLE [WorkOrderExecutionSummary] DROP COLUMN [MainNoValidInputStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525183735_RemoveG4DeadFields'
)
BEGIN
    DECLARE @var151 sysname;
    SELECT @var151 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderExecutionSummary]') AND [c].[name] = N'ValidInputOutputRatio');
    IF @var151 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderExecutionSummary] DROP CONSTRAINT [' + @var151 + '];');
    ALTER TABLE [WorkOrderExecutionSummary] DROP COLUMN [ValidInputOutputRatio];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525183735_RemoveG4DeadFields'
)
BEGIN
    DECLARE @var152 sysname;
    SELECT @var152 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderExecutionSummary]') AND [c].[name] = N'ValidInputStatus');
    IF @var152 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderExecutionSummary] DROP CONSTRAINT [' + @var152 + '];');
    ALTER TABLE [WorkOrderExecutionSummary] DROP COLUMN [ValidInputStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525183735_RemoveG4DeadFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525183735_RemoveG4DeadFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525203256_AddG7BatchCountsAndG12ScheduleStage'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [FlowIncompleteBatchCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525203256_AddG7BatchCountsAndG12ScheduleStage'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [FlowTotalBatchCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525203256_AddG7BatchCountsAndG12ScheduleStage'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ScheduleStage] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525203256_AddG7BatchCountsAndG12ScheduleStage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525203256_AddG7BatchCountsAndG12ScheduleStage', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525220048_AddRemainingWorkDaysToBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [RemainingWorkDays] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525220048_AddRemainingWorkDaysToBatch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525220048_AddRemainingWorkDaysToBatch', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526180415_AddSteelPropertyToGradeMapping'
)
BEGIN
    ALTER TABLE [StandardGradeMapping] ADD [SteelProperty] nvarchar(20) NOT NULL DEFAULT N'镍基合金';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526180415_AddSteelPropertyToGradeMapping'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260526180415_AddSteelPropertyToGradeMapping', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526185535_AddTotalWorkDaysToBatch'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [TotalWorkDays] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526185535_AddTotalWorkDaysToBatch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260526185535_AddTotalWorkDaysToBatch', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526200037_AddStandardProcessCycle'
)
BEGIN
    CREATE TABLE [StandardProcessCycle] (
        [Id] int NOT NULL IDENTITY,
        [PlantGrade] nvarchar(50) NOT NULL,
        [RawMaterialType] nvarchar(50) NOT NULL,
        [RawSpec] nvarchar(100) NOT NULL,
        [ProductSpec] nvarchar(100) NOT NULL,
        [DeliveryState] nvarchar(50) NOT NULL,
        [StandardCycleDays] int NOT NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_StandardProcessCycle] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526200037_AddStandardProcessCycle'
)
BEGIN
    CREATE INDEX [IX_StandardProcessCycle_PlantGrade] ON [StandardProcessCycle] ([PlantGrade]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526200037_AddStandardProcessCycle'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260526200037_AddStandardProcessCycle', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526205019_AddStandardCycleToMaterialPlans'
)
BEGIN
    ALTER TABLE [RoundBarPiercingPlan] ADD [StandardCycle] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526205019_AddStandardCycleToMaterialPlans'
)
BEGIN
    ALTER TABLE [PurchaseSemiPlan] ADD [StandardCycle] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526205019_AddStandardCycleToMaterialPlans'
)
BEGIN
    ALTER TABLE [PurchaseFinishedPlan] ADD [StandardCycle] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526205019_AddStandardCycleToMaterialPlans'
)
BEGIN
    ALTER TABLE [InventoryPlan] ADD [StandardCycle] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526205019_AddStandardCycleToMaterialPlans'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260526205019_AddStandardCycleToMaterialPlans', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526215926_AddMaxStandardCycleToSummary'
)
BEGIN
    ALTER TABLE [WorkOrderListSummary] ADD [MaxStandardCycle] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526215926_AddMaxStandardCycleToSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260526215926_AddMaxStandardCycleToSummary', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    DECLARE @var153 sysname;
    SELECT @var153 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaterialReceiveCheck]') AND [c].[name] = N'ReceivedQuantity');
    IF @var153 IS NOT NULL EXEC(N'ALTER TABLE [MaterialReceiveCheck] DROP CONSTRAINT [' + @var153 + '];');
    ALTER TABLE [MaterialReceiveCheck] DROP COLUMN [ReceivedQuantity];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    DECLARE @var154 sysname;
    SELECT @var154 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaterialReceiveCheck]') AND [c].[name] = N'ReceivedWeight');
    IF @var154 IS NOT NULL EXEC(N'ALTER TABLE [MaterialReceiveCheck] DROP CONSTRAINT [' + @var154 + '];');
    ALTER TABLE [MaterialReceiveCheck] DROP COLUMN [ReceivedWeight];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [IsClosed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    DECLARE @var155 sysname;
    SELECT @var155 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaterialReceiveCheck]') AND [c].[name] = N'DataSource');
    IF @var155 IS NOT NULL EXEC(N'ALTER TABLE [MaterialReceiveCheck] DROP CONSTRAINT [' + @var155 + '];');
    ALTER TABLE [MaterialReceiveCheck] ALTER COLUMN [DataSource] nvarchar(10) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [BatchNo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [FurnaceNo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [IsForceCompleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [MaterialName] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [PlantGrade] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [SalesOrderNo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [SourceUnit] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [Specification] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [TagNo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [WorkOrderNo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527031606_UpdateMaterialReceiveCheckRemoveQtyWeight', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527043059_RenameMaterialNameToManufacturingItem'
)
BEGIN
    EXEC sp_rename N'[MaterialReceiveCheck].[MaterialName]', N'ManufacturingItem', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527043059_RenameMaterialNameToManufacturingItem'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527043059_RenameMaterialNameToManufacturingItem', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527151703_AddProductionTypeToMaterialReceiveCheckAndFinalInspection'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [ProductionType] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527151703_AddProductionTypeToMaterialReceiveCheckAndFinalInspection'
)
BEGIN
    ALTER TABLE [FinalInspection] ADD [ProductionType] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527151703_AddProductionTypeToMaterialReceiveCheckAndFinalInspection'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527151703_AddProductionTypeToMaterialReceiveCheckAndFinalInspection', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527185805_AddProcessCycleToExecutionSummary'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ProcessCycle] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527185805_AddProcessCycleToExecutionSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527185805_AddProcessCycleToExecutionSummary', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527190726_AddFlowMaxRemainingWorkDaysToExecutionSummary'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [FlowMaxRemainingWorkDays] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527190726_AddFlowMaxRemainingWorkDaysToExecutionSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527190726_AddFlowMaxRemainingWorkDaysToExecutionSummary', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527194522_AddG12TotalRemainingWorkDaysAndUrgencyLevel'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [TotalRemainingWorkDays] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527194522_AddG12TotalRemainingWorkDaysAndUrgencyLevel'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [UrgencyLevel] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527194522_AddG12TotalRemainingWorkDaysAndUrgencyLevel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527194522_AddG12TotalRemainingWorkDaysAndUrgencyLevel', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527202300_AddEstimatedCompletionDateAndDaysDiffToG12'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527202300_AddEstimatedCompletionDateAndDaysDiffToG12', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [DaysDiffFromDelivery] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [EstimatedProcessCompletionDate] date NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    CREATE TABLE [InventoryPlanProcessGroup] (
        [Id] int NOT NULL IDENTITY,
        [InventoryPlanId] int NOT NULL,
        [SequenceNumber] int NOT NULL,
        [ProcessName] nvarchar(50) NOT NULL,
        [ManufacturingSpec] nvarchar(100) NULL,
        [OuterDiameterTolerance] nvarchar(50) NULL,
        [WallThicknessTolerance] nvarchar(50) NULL,
        [ManufacturingLength] nvarchar(100) NULL,
        [CuttingTreatment] nvarchar(200) NULL,
        [ManufacturingMultiple] int NOT NULL,
        [Remark] nvarchar(500) NULL,
        [ColdRollDraw] int NULL,
        [OilPipeCut] int NULL,
        [Degrease] int NULL,
        [Solution] int NULL,
        [Straighten] int NULL,
        [Cut] int NULL,
        [ThicknessMeasure] int NULL,
        [Pickle] int NULL,
        [OuterPolish] int NULL,
        [InnerGrinding] int NULL,
        [OuterSpotGrinding] int NULL,
        [Inspection] int NULL,
        [WeldingHead] int NULL,
        [Lubrication] int NULL,
        [Warehouse] int NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_InventoryPlanProcessGroup] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryPlanProcessGroup_InventoryPlan_InventoryPlanId] FOREIGN KEY ([InventoryPlanId]) REFERENCES [InventoryPlan] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    CREATE TABLE [PiercingPlanProcessGroup] (
        [Id] int NOT NULL IDENTITY,
        [RoundBarPiercingPlanId] int NOT NULL,
        [SequenceNumber] int NOT NULL,
        [ProcessName] nvarchar(50) NOT NULL,
        [ManufacturingSpec] nvarchar(100) NULL,
        [OuterDiameterTolerance] nvarchar(50) NULL,
        [WallThicknessTolerance] nvarchar(50) NULL,
        [ManufacturingLength] nvarchar(100) NULL,
        [CuttingTreatment] nvarchar(200) NULL,
        [ManufacturingMultiple] int NOT NULL,
        [Remark] nvarchar(500) NULL,
        [ColdRollDraw] int NULL,
        [OilPipeCut] int NULL,
        [Degrease] int NULL,
        [Solution] int NULL,
        [Straighten] int NULL,
        [Cut] int NULL,
        [ThicknessMeasure] int NULL,
        [Pickle] int NULL,
        [OuterPolish] int NULL,
        [InnerGrinding] int NULL,
        [OuterSpotGrinding] int NULL,
        [Inspection] int NULL,
        [WeldingHead] int NULL,
        [Lubrication] int NULL,
        [Warehouse] int NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_PiercingPlanProcessGroup] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PiercingPlanProcessGroup_RoundBarPiercingPlan_RoundBarPiercingPlanId] FOREIGN KEY ([RoundBarPiercingPlanId]) REFERENCES [RoundBarPiercingPlan] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    CREATE TABLE [SemiPlanProcessGroup] (
        [Id] int NOT NULL IDENTITY,
        [PurchaseSemiPlanId] int NOT NULL,
        [SequenceNumber] int NOT NULL,
        [ProcessName] nvarchar(50) NOT NULL,
        [ManufacturingSpec] nvarchar(100) NULL,
        [OuterDiameterTolerance] nvarchar(50) NULL,
        [WallThicknessTolerance] nvarchar(50) NULL,
        [ManufacturingLength] nvarchar(100) NULL,
        [CuttingTreatment] nvarchar(200) NULL,
        [ManufacturingMultiple] int NOT NULL,
        [Remark] nvarchar(500) NULL,
        [ColdRollDraw] int NULL,
        [OilPipeCut] int NULL,
        [Degrease] int NULL,
        [Solution] int NULL,
        [Straighten] int NULL,
        [Cut] int NULL,
        [ThicknessMeasure] int NULL,
        [Pickle] int NULL,
        [OuterPolish] int NULL,
        [InnerGrinding] int NULL,
        [OuterSpotGrinding] int NULL,
        [Inspection] int NULL,
        [WeldingHead] int NULL,
        [Lubrication] int NULL,
        [Warehouse] int NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_SemiPlanProcessGroup] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SemiPlanProcessGroup_PurchaseSemiPlan_PurchaseSemiPlanId] FOREIGN KEY ([PurchaseSemiPlanId]) REFERENCES [PurchaseSemiPlan] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    CREATE INDEX [IX_InventoryPlanProcessGroup_PlanId] ON [InventoryPlanProcessGroup] ([InventoryPlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    CREATE UNIQUE INDEX [UK_InventoryPlanProcessGroup_Seq] ON [InventoryPlanProcessGroup] ([InventoryPlanId], [SequenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    CREATE INDEX [IX_PiercingPlanProcessGroup_PlanId] ON [PiercingPlanProcessGroup] ([RoundBarPiercingPlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    CREATE UNIQUE INDEX [UK_PiercingPlanProcessGroup_Seq] ON [PiercingPlanProcessGroup] ([RoundBarPiercingPlanId], [SequenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    CREATE INDEX [IX_SemiPlanProcessGroup_PlanId] ON [SemiPlanProcessGroup] ([PurchaseSemiPlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    CREATE UNIQUE INDEX [UK_SemiPlanProcessGroup_Seq] ON [SemiPlanProcessGroup] ([PurchaseSemiPlanId], [SequenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527232013_AddProcessGroupTemplateAndPlanProcessGroups', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528233232_RemoveProcessPlanFromMaterialPlans'
)
BEGIN
    DECLARE @var156 sysname;
    SELECT @var156 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RoundBarPiercingPlan]') AND [c].[name] = N'ProcessPlan');
    IF @var156 IS NOT NULL EXEC(N'ALTER TABLE [RoundBarPiercingPlan] DROP CONSTRAINT [' + @var156 + '];');
    ALTER TABLE [RoundBarPiercingPlan] DROP COLUMN [ProcessPlan];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528233232_RemoveProcessPlanFromMaterialPlans'
)
BEGIN
    DECLARE @var157 sysname;
    SELECT @var157 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseSemiPlan]') AND [c].[name] = N'ProcessPlan');
    IF @var157 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseSemiPlan] DROP CONSTRAINT [' + @var157 + '];');
    ALTER TABLE [PurchaseSemiPlan] DROP COLUMN [ProcessPlan];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528233232_RemoveProcessPlanFromMaterialPlans'
)
BEGIN
    DECLARE @var158 sysname;
    SELECT @var158 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryPlan]') AND [c].[name] = N'ProcessPlan');
    IF @var158 IS NOT NULL EXEC(N'ALTER TABLE [InventoryPlan] DROP CONSTRAINT [' + @var158 + '];');
    ALTER TABLE [InventoryPlan] DROP COLUMN [ProcessPlan];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528233232_RemoveProcessPlanFromMaterialPlans'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260528233232_RemoveProcessPlanFromMaterialPlans', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260529024024_AddRawMaterialLockRemarkToExecutionSummary'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [RawMaterialLockRemark] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260529024024_AddRawMaterialLockRemarkToExecutionSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260529024024_AddRawMaterialLockRemarkToExecutionSummary', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260529041541_AddSchedulingContext'
)
BEGIN
    CREATE TABLE [RawMaterialLockPlanAndExecution] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [WorkOrderNo] nvarchar(50) NOT NULL,
        [Salesman] nvarchar(50) NOT NULL,
        [CustomerName] nvarchar(200) NOT NULL,
        [SignDate] datetime2 NOT NULL,
        [DeliveryDate] datetime2 NOT NULL,
        [DelayPenalty] bit NOT NULL DEFAULT CAST(0 AS bit),
        [SettlementMethod] nvarchar(20) NOT NULL,
        [SalesOrderNo] nvarchar(50) NOT NULL,
        [ProductionMainNo] nvarchar(50) NOT NULL,
        [ProductionSubNo] nvarchar(50) NULL,
        [MaterialName] nvarchar(50) NOT NULL,
        [DeliveryState] nvarchar(50) NOT NULL,
        [PlantGrade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [LengthStatus] nvarchar(20) NOT NULL,
        [MinLength] decimal(18,3) NULL,
        [MaxLength] decimal(18,3) NULL,
        [TotalItemCount] int NOT NULL DEFAULT 0,
        [TotalQuantity] int NOT NULL DEFAULT 0,
        [TotalMeters] decimal(18,3) NOT NULL DEFAULT 0.0,
        [TotalWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [LatestPlanDate] datetime2 NULL,
        [MaterialPlanRate] decimal(8,2) NOT NULL DEFAULT 0.0,
        [MaterialPlanStatus] int NOT NULL DEFAULT 0,
        [MainNoMaterialPlanRate] decimal(8,2) NOT NULL DEFAULT 0.0,
        [MainNoMaterialPlanStatus] int NOT NULL DEFAULT 0,
        [ProcessCycle] int NOT NULL DEFAULT 0,
        [PendingRoughTubeQty] int NOT NULL DEFAULT 0,
        [PendingRoughTubeWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [PendingOutsourceFinishQty] int NOT NULL DEFAULT 0,
        [PendingOutsourceFinishWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [TheoreticalFinishQty] decimal(18,3) NOT NULL DEFAULT 0.0,
        [TheoreticalFinishWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [InputStartDate] datetime2 NULL,
        [InputEndDate] datetime2 NULL,
        [TotalBatchCount] int NOT NULL DEFAULT 0,
        [InputQuantity] int NOT NULL DEFAULT 0,
        [InputWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [TheoreticalOutputQty] decimal(18,3) NOT NULL DEFAULT 0.0,
        [TheoreticalOutputWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [InputOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
        [InputStatus] int NOT NULL DEFAULT 0,
        [MainNoInputOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
        [MainNoInputStatus] int NOT NULL DEFAULT 0,
        [FlowOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
        [FlowStatus] int NOT NULL DEFAULT 0,
        [MainNoFlowOutputRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
        [MainNoFlowStatus] int NOT NULL DEFAULT 0,
        [FlowTotalBatchCount] int NOT NULL DEFAULT 0,
        [FlowIncompleteBatchCount] int NOT NULL DEFAULT 0,
        [FlowMaxRemainingWorkDays] int NOT NULL DEFAULT 0,
        [GeneralDefectWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [GeneralDefectRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
        [SeriousDefectWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [SeriousDefectRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
        [ScrapWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
        [ScrapRatio] decimal(8,2) NOT NULL DEFAULT 0.0,
        [ScheduleStage] int NOT NULL DEFAULT 0,
        [TotalRemainingWorkDays] int NULL,
        [UrgencyLevel] nvarchar(20) NULL,
        [EstimatedProcessCompletionDate] date NULL,
        [DaysDiffFromDelivery] int NULL,
        [RawMaterialLockRemark] nvarchar(20) NULL,
        [SalesUrging] bit NOT NULL DEFAULT CAST(0 AS bit),
        [UrgingRemark] nvarchar(500) NULL,
        [CurrentScheduleStage] int NULL,
        [CurrentRawMaterialLockRemark] nvarchar(20) NULL,
        [IsExecuted] bit NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_RawMaterialLockPlanAndExecution] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260529041541_AddSchedulingContext'
)
BEGIN
    CREATE TABLE [SalesUrging] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [IsSalesUrging] bit NOT NULL DEFAULT CAST(0 AS bit),
        [UrgingRemark] nvarchar(500) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_SalesUrging] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260529041541_AddSchedulingContext'
)
BEGIN
    CREATE INDEX [IX_RMLPAE_ScheduleStage] ON [RawMaterialLockPlanAndExecution] ([ScheduleStage]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260529041541_AddSchedulingContext'
)
BEGIN
    CREATE INDEX [IX_RMLPAE_WorkOrderNo] ON [RawMaterialLockPlanAndExecution] ([WorkOrderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260529041541_AddSchedulingContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_RMLPAE_WorkOrderId] ON [RawMaterialLockPlanAndExecution] ([WorkOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260529041541_AddSchedulingContext'
)
BEGIN
    CREATE UNIQUE INDEX [UK_SU_WorkOrderId] ON [SalesUrging] ([WorkOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260529041541_AddSchedulingContext'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260529041541_AddSchedulingContext', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260531201409_AddSalesUrgingLockFields'
)
BEGIN
    ALTER TABLE [SalesUrging] ADD [EstimatedArrivalDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260531201409_AddSalesUrgingLockFields'
)
BEGIN
    ALTER TABLE [SalesUrging] ADD [IsLockConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260531201409_AddSalesUrgingLockFields'
)
BEGIN
    ALTER TABLE [SalesUrging] ADD [IsMainNoMaterialComplete] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260531201409_AddSalesUrgingLockFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260531201409_AddSalesUrgingLockFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260531211647_AddHasAbnormality'
)
BEGIN
    ALTER TABLE [RawMaterialLockPlanAndExecution] ADD [HasAbnormality] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260531211647_AddHasAbnormality'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260531211647_AddHasAbnormality', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601172059_AddProcessInspectionBatchNo'
)
BEGIN
    ALTER TABLE [ProcessInspection] ADD [BatchNo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601172059_AddProcessInspectionBatchNo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260601172059_AddProcessInspectionBatchNo', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601174809_AddBatchNoFieldsForDataImport'
)
BEGIN
    ALTER TABLE [SubcontractReturnItem] ADD [OrderNo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601174809_AddBatchNoFieldsForDataImport'
)
BEGIN
    ALTER TABLE [ProductRequirement] ADD [ItemSequence] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601174809_AddBatchNoFieldsForDataImport'
)
BEGIN
    ALTER TABLE [ProductRequirement] ADD [OrderNo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601174809_AddBatchNoFieldsForDataImport'
)
BEGIN
    ALTER TABLE [ProcessGroup] ADD [BatchNo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601174809_AddBatchNoFieldsForDataImport'
)
BEGIN
    ALTER TABLE [OrderItem] ADD [OrderNumber] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601174809_AddBatchNoFieldsForDataImport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260601174809_AddBatchNoFieldsForDataImport', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601183650_AddBatchNextProcess'
)
BEGIN
    DECLARE @var159 sysname;
    SELECT @var159 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductionBatch]') AND [c].[name] = N'CorrespondingSpec');
    IF @var159 IS NOT NULL EXEC(N'ALTER TABLE [ProductionBatch] DROP CONSTRAINT [' + @var159 + '];');
    ALTER TABLE [ProductionBatch] ALTER COLUMN [CorrespondingSpec] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601183650_AddBatchNextProcess'
)
BEGIN
    ALTER TABLE [ProductionBatch] ADD [NextProcess] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601183650_AddBatchNextProcess'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260601183650_AddBatchNextProcess', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602004535_AddStandardWorkDays'
)
BEGIN
    CREATE TABLE [StandardWorkDays] (
        [Id] int NOT NULL IDENTITY,
        [SectionName] nvarchar(50) NOT NULL,
        [PlantGradePrefix] nvarchar(50) NULL,
        [StandardDays] float NOT NULL,
        [Remark] nvarchar(200) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_StandardWorkDays] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602004535_AddStandardWorkDays'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UK_SWD_SectionName_PlantGradePrefix] ON [StandardWorkDays] ([SectionName], [PlantGradePrefix]) WHERE [PlantGradePrefix] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602004535_AddStandardWorkDays'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260602004535_AddStandardWorkDays', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602011723_AddStandardWorkDayDeliveryStates'
)
BEGIN
    CREATE TABLE [StandardWorkDayDeliveryStates] (
        [Id] int NOT NULL IDENTITY,
        [DeliveryState] nvarchar(100) NOT NULL,
        [ExtraDays] float NOT NULL,
        [PlantGradePrefix] nvarchar(50) NULL,
        [Remark] nvarchar(200) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_StandardWorkDayDeliveryStates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602011723_AddStandardWorkDayDeliveryStates'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UK_SWDDS_DeliveryState_PlantGradePrefix] ON [StandardWorkDayDeliveryStates] ([DeliveryState], [PlantGradePrefix]) WHERE [PlantGradePrefix] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602011723_AddStandardWorkDayDeliveryStates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260602011723_AddStandardWorkDayDeliveryStates', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602050229_AddConfigParameterTable'
)
BEGIN
    CREATE TABLE [ConfigParameters] (
        [Id] int NOT NULL IDENTITY,
        [Category] nvarchar(50) NOT NULL,
        [ParamKey] nvarchar(100) NOT NULL,
        [ParamValue] decimal(18,4) NOT NULL,
        [Remark] nvarchar(200) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_ConfigParameters] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602050229_AddConfigParameterTable'
)
BEGIN
    CREATE UNIQUE INDEX [UK_CP_Category_ParamKey] ON [ConfigParameters] ([Category], [ParamKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602050229_AddConfigParameterTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260602050229_AddConfigParameterTable', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602184642_AddSectionFlowAnalysisTables'
)
BEGIN
    CREATE TABLE [SectionFlowCategorySettings] (
        [Id] int NOT NULL IDENTITY,
        [CategoryCode] nvarchar(10) NOT NULL,
        [CategoryName] nvarchar(50) NOT NULL,
        [DailyProductionTarget] decimal(18,2) NULL,
        [LowerLimitDays] decimal(18,2) NULL,
        [UpperLimitDays] decimal(18,2) NULL,
        [Remark] nvarchar(200) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_SectionFlowCategorySettings] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602184642_AddSectionFlowAnalysisTables'
)
BEGIN
    CREATE TABLE [SectionFlowCategoryItems] (
        [Id] int NOT NULL IDENTITY,
        [SettingId] int NOT NULL,
        [ProcessGroupName] nvarchar(100) NOT NULL,
        [SectionName] nvarchar(50) NOT NULL,
        [Coefficient] decimal(18,4) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_SectionFlowCategoryItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SectionFlowCategoryItems_SectionFlowCategorySettings_SettingId] FOREIGN KEY ([SettingId]) REFERENCES [SectionFlowCategorySettings] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602184642_AddSectionFlowAnalysisTables'
)
BEGIN
    CREATE UNIQUE INDEX [UK_SFCI_SettingId_ProcessGroupName_SectionName] ON [SectionFlowCategoryItems] ([SettingId], [ProcessGroupName], [SectionName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602184642_AddSectionFlowAnalysisTables'
)
BEGIN
    CREATE UNIQUE INDEX [UK_SFCS_CategoryCode] ON [SectionFlowCategorySettings] ([CategoryCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602184642_AddSectionFlowAnalysisTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260602184642_AddSectionFlowAnalysisTables', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602210321_AddWorkOrderScheduleTable'
)
BEGIN
    CREATE TABLE [WorkOrderSchedules] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [WorkOrderNo] nvarchar(50) NOT NULL,
        [Salesman] nvarchar(50) NOT NULL,
        [CustomerName] nvarchar(200) NOT NULL,
        [SignDate] datetime2 NOT NULL,
        [DeliveryDate] datetime2 NOT NULL,
        [DelayPenalty] bit NOT NULL,
        [SettlementMethod] nvarchar(50) NOT NULL,
        [SalesOrderNo] nvarchar(50) NOT NULL,
        [ProductionMainNo] nvarchar(20) NOT NULL,
        [ProductionSubNo] nvarchar(20) NULL,
        [MaterialName] nvarchar(50) NOT NULL,
        [DeliveryState] nvarchar(100) NOT NULL,
        [PlantGrade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [LengthStatus] nvarchar(20) NOT NULL,
        [TotalItemCount] int NOT NULL,
        [TotalQuantity] int NOT NULL,
        [TotalMeters] decimal(18,2) NOT NULL,
        [TotalWeight] decimal(18,2) NOT NULL,
        [LatestPlanDate] datetime2 NULL,
        [MaterialPlanRate] decimal(18,2) NOT NULL,
        [MaterialPlanStatus] int NOT NULL,
        [MainNoMaterialPlanRate] decimal(18,2) NOT NULL,
        [MainNoMaterialPlanStatus] int NOT NULL,
        [ProcessCycle] int NOT NULL,
        [PendingRoughTubeQty] int NOT NULL,
        [PendingRoughTubeWeight] decimal(18,2) NOT NULL,
        [PendingOutsourceFinishQty] int NOT NULL,
        [PendingOutsourceFinishWeight] decimal(18,2) NOT NULL,
        [TheoreticalFinishQty] decimal(18,2) NOT NULL,
        [TheoreticalFinishWeight] decimal(18,2) NOT NULL,
        [InputStartDate] datetime2 NULL,
        [InputEndDate] datetime2 NULL,
        [TotalBatchCount] int NOT NULL,
        [InputQuantity] int NOT NULL,
        [InputWeight] decimal(18,2) NOT NULL,
        [TheoreticalOutputQty] decimal(18,2) NOT NULL,
        [TheoreticalOutputWeight] decimal(18,2) NOT NULL,
        [InputOutputRatio] decimal(18,2) NOT NULL,
        [InputStatus] int NOT NULL,
        [MainNoInputOutputRatio] decimal(18,2) NOT NULL,
        [MainNoInputStatus] int NOT NULL,
        [FlowOutputRatio] decimal(18,2) NOT NULL,
        [FlowStatus] int NOT NULL,
        [MainNoFlowOutputRatio] decimal(18,2) NOT NULL,
        [MainNoFlowStatus] int NOT NULL,
        [FlowTotalBatchCount] int NOT NULL,
        [FlowIncompleteBatchCount] int NOT NULL,
        [FlowMaxRemainingWorkDays] int NOT NULL,
        [GeneralDefectWeight] decimal(18,2) NOT NULL,
        [GeneralDefectRatio] decimal(18,2) NOT NULL,
        [SeriousDefectWeight] decimal(18,2) NOT NULL,
        [SeriousDefectRatio] decimal(18,2) NOT NULL,
        [ScrapWeight] decimal(18,2) NOT NULL,
        [ScrapRatio] decimal(18,2) NOT NULL,
        [ScheduleStage] int NOT NULL,
        [TotalRemainingWorkDays] int NULL,
        [UrgencyLevel] nvarchar(20) NULL,
        [EstimatedProcessCompletionDate] datetime2 NULL,
        [DaysDiffFromDelivery] int NULL,
        [RawMaterialLockRemark] nvarchar(200) NULL,
        [SalesUrging] bit NOT NULL,
        [UrgingRemark] nvarchar(500) NULL,
        [CurrentScheduleStage] int NULL,
        [CurrentRawMaterialLockRemark] nvarchar(200) NULL,
        [IsExecuted] bit NULL,
        [Priority] nvarchar(20) NULL,
        [PlannedStartDate] datetime2 NULL,
        [PlannedEndDate] datetime2 NULL,
        [ScheduleStatus] nvarchar(20) NULL,
        [UrgencyReason] nvarchar(500) NULL,
        [Remark] nvarchar(500) NULL,
        [HasAbnormality] bit NOT NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_WorkOrderSchedules] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602210321_AddWorkOrderScheduleTable'
)
BEGIN
    CREATE UNIQUE INDEX [UK_WOS_WorkOrderId] ON [WorkOrderSchedules] ([WorkOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602210321_AddWorkOrderScheduleTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260602210321_AddWorkOrderScheduleTable', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602213055_AddIsPreInputToRawMaterialLockPlanAndExecution'
)
BEGIN
    ALTER TABLE [RawMaterialLockPlanAndExecution] ADD [IsPreInput] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602213055_AddIsPreInputToRawMaterialLockPlanAndExecution'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260602213055_AddIsPreInputToRawMaterialLockPlanAndExecution', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602214624_AddIsMainNoMaterialCompleteToRawMaterialLockPlanAndExecution'
)
BEGIN
    ALTER TABLE [RawMaterialLockPlanAndExecution] ADD [IsMainNoMaterialComplete] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602214624_AddIsMainNoMaterialCompleteToRawMaterialLockPlanAndExecution'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260602214624_AddIsMainNoMaterialCompleteToRawMaterialLockPlanAndExecution', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var160 sysname;
    SELECT @var160 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'CurrentRawMaterialLockRemark');
    IF @var160 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var160 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [CurrentRawMaterialLockRemark];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var161 sysname;
    SELECT @var161 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'CurrentScheduleStage');
    IF @var161 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var161 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [CurrentScheduleStage];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var162 sysname;
    SELECT @var162 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'GeneralDefectRatio');
    IF @var162 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var162 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [GeneralDefectRatio];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var163 sysname;
    SELECT @var163 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'GeneralDefectWeight');
    IF @var163 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var163 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [GeneralDefectWeight];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var164 sysname;
    SELECT @var164 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'HasAbnormality');
    IF @var164 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var164 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [HasAbnormality];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var165 sysname;
    SELECT @var165 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'InputEndDate');
    IF @var165 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var165 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [InputEndDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var166 sysname;
    SELECT @var166 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'InputOutputRatio');
    IF @var166 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var166 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [InputOutputRatio];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var167 sysname;
    SELECT @var167 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'InputQuantity');
    IF @var167 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var167 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [InputQuantity];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var168 sysname;
    SELECT @var168 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'InputStartDate');
    IF @var168 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var168 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [InputStartDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var169 sysname;
    SELECT @var169 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'InputStatus');
    IF @var169 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var169 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [InputStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var170 sysname;
    SELECT @var170 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'InputWeight');
    IF @var170 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var170 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [InputWeight];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var171 sysname;
    SELECT @var171 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'IsExecuted');
    IF @var171 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var171 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [IsExecuted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var172 sysname;
    SELECT @var172 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'LatestPlanDate');
    IF @var172 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var172 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [LatestPlanDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var173 sysname;
    SELECT @var173 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'MainNoInputOutputRatio');
    IF @var173 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var173 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [MainNoInputOutputRatio];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var174 sysname;
    SELECT @var174 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'MainNoInputStatus');
    IF @var174 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var174 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [MainNoInputStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var175 sysname;
    SELECT @var175 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'MainNoMaterialPlanRate');
    IF @var175 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var175 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [MainNoMaterialPlanRate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var176 sysname;
    SELECT @var176 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'MainNoMaterialPlanStatus');
    IF @var176 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var176 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [MainNoMaterialPlanStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var177 sysname;
    SELECT @var177 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'MaterialPlanRate');
    IF @var177 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var177 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [MaterialPlanRate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var178 sysname;
    SELECT @var178 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'MaterialPlanStatus');
    IF @var178 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var178 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [MaterialPlanStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var179 sysname;
    SELECT @var179 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'PendingOutsourceFinishQty');
    IF @var179 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var179 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [PendingOutsourceFinishQty];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var180 sysname;
    SELECT @var180 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'PendingOutsourceFinishWeight');
    IF @var180 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var180 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [PendingOutsourceFinishWeight];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var181 sysname;
    SELECT @var181 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'PendingRoughTubeQty');
    IF @var181 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var181 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [PendingRoughTubeQty];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var182 sysname;
    SELECT @var182 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'PendingRoughTubeWeight');
    IF @var182 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var182 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [PendingRoughTubeWeight];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var183 sysname;
    SELECT @var183 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'PlannedEndDate');
    IF @var183 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var183 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [PlannedEndDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var184 sysname;
    SELECT @var184 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'PlannedStartDate');
    IF @var184 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var184 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [PlannedStartDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var185 sysname;
    SELECT @var185 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'Priority');
    IF @var185 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var185 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [Priority];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var186 sysname;
    SELECT @var186 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'ProcessCycle');
    IF @var186 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var186 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [ProcessCycle];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var187 sysname;
    SELECT @var187 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'Remark');
    IF @var187 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var187 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [Remark];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var188 sysname;
    SELECT @var188 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'ScheduleStatus');
    IF @var188 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var188 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [ScheduleStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var189 sysname;
    SELECT @var189 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'ScrapRatio');
    IF @var189 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var189 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [ScrapRatio];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var190 sysname;
    SELECT @var190 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'ScrapWeight');
    IF @var190 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var190 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [ScrapWeight];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var191 sysname;
    SELECT @var191 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'SeriousDefectRatio');
    IF @var191 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var191 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [SeriousDefectRatio];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var192 sysname;
    SELECT @var192 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'SeriousDefectWeight');
    IF @var192 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var192 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [SeriousDefectWeight];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var193 sysname;
    SELECT @var193 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'TheoreticalFinishQty');
    IF @var193 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var193 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [TheoreticalFinishQty];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var194 sysname;
    SELECT @var194 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'TheoreticalFinishWeight');
    IF @var194 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var194 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [TheoreticalFinishWeight];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var195 sysname;
    SELECT @var195 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'TheoreticalOutputQty');
    IF @var195 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var195 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [TheoreticalOutputQty];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var196 sysname;
    SELECT @var196 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'TheoreticalOutputWeight');
    IF @var196 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var196 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [TheoreticalOutputWeight];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var197 sysname;
    SELECT @var197 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'TotalBatchCount');
    IF @var197 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var197 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [TotalBatchCount];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    DECLARE @var198 sysname;
    SELECT @var198 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'UrgencyReason');
    IF @var198 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var198 + '];');
    ALTER TABLE [WorkOrderSchedules] DROP COLUMN [UrgencyReason];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260602235151_RemoveUnusedFieldsFromWorkOrderSchedule', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603000419_AddMinLengthMaxLengthToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [MaxLength] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603000419_AddMinLengthMaxLengthToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [MinLength] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603000419_AddMinLengthMaxLengthToWorkOrderSchedule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603000419_AddMinLengthMaxLengthToWorkOrderSchedule', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603184533_AddMaterialPlanCoverageToExecutionSummary'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [LatestRequiredDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603184533_AddMaterialPlanCoverageToExecutionSummary'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [MaterialPlanCoveredCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603184533_AddMaterialPlanCoverageToExecutionSummary'
)
BEGIN
    ALTER TABLE [RawMaterialLockPlanAndExecution] ADD [LatestRequiredDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603184533_AddMaterialPlanCoverageToExecutionSummary'
)
BEGIN
    ALTER TABLE [RawMaterialLockPlanAndExecution] ADD [MaterialPlanCoveredCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603184533_AddMaterialPlanCoverageToExecutionSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603184533_AddMaterialPlanCoverageToExecutionSummary', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603185905_AddMaterialPlanProportionToExecutionSummary'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [MaterialPlanProportion] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603185905_AddMaterialPlanProportionToExecutionSummary'
)
BEGIN
    ALTER TABLE [RawMaterialLockPlanAndExecution] ADD [MaterialPlanProportion] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603185905_AddMaterialPlanProportionToExecutionSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603185905_AddMaterialPlanProportionToExecutionSummary', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603191829_AddMaterialPlanFieldsToWorkOrderListSummary'
)
BEGIN
    ALTER TABLE [WorkOrderListSummary] ADD [LatestRequiredDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603191829_AddMaterialPlanFieldsToWorkOrderListSummary'
)
BEGIN
    ALTER TABLE [WorkOrderListSummary] ADD [MaterialPlanCoveredCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603191829_AddMaterialPlanFieldsToWorkOrderListSummary'
)
BEGIN
    ALTER TABLE [WorkOrderListSummary] ADD [MaterialPlanProportion] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603191829_AddMaterialPlanFieldsToWorkOrderListSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603191829_AddMaterialPlanFieldsToWorkOrderListSummary', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603203851_RemoveG14Fields'
)
BEGIN
    DECLARE @var199 sysname;
    SELECT @var199 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RawMaterialLockPlanAndExecution]') AND [c].[name] = N'CurrentRawMaterialLockRemark');
    IF @var199 IS NOT NULL EXEC(N'ALTER TABLE [RawMaterialLockPlanAndExecution] DROP CONSTRAINT [' + @var199 + '];');
    ALTER TABLE [RawMaterialLockPlanAndExecution] DROP COLUMN [CurrentRawMaterialLockRemark];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603203851_RemoveG14Fields'
)
BEGIN
    DECLARE @var200 sysname;
    SELECT @var200 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RawMaterialLockPlanAndExecution]') AND [c].[name] = N'CurrentScheduleStage');
    IF @var200 IS NOT NULL EXEC(N'ALTER TABLE [RawMaterialLockPlanAndExecution] DROP CONSTRAINT [' + @var200 + '];');
    ALTER TABLE [RawMaterialLockPlanAndExecution] DROP COLUMN [CurrentScheduleStage];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603203851_RemoveG14Fields'
)
BEGIN
    DECLARE @var201 sysname;
    SELECT @var201 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RawMaterialLockPlanAndExecution]') AND [c].[name] = N'IsExecuted');
    IF @var201 IS NOT NULL EXEC(N'ALTER TABLE [RawMaterialLockPlanAndExecution] DROP CONSTRAINT [' + @var201 + '];');
    ALTER TABLE [RawMaterialLockPlanAndExecution] DROP COLUMN [IsExecuted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603203851_RemoveG14Fields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603203851_RemoveG14Fields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603211641_AddDailyOutputEstimateAndCapacityWorkDays'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [CapacityWorkDays] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603211641_AddDailyOutputEstimateAndCapacityWorkDays'
)
BEGIN
    ALTER TABLE [RawMaterialLockPlanAndExecution] ADD [CapacityWorkDays] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603211641_AddDailyOutputEstimateAndCapacityWorkDays'
)
BEGIN
    CREATE TABLE [DailyOutputEstimates] (
        [Id] int NOT NULL IDENTITY,
        [MinOuterDiameter] decimal(18,2) NOT NULL,
        [DailyOutputTons] decimal(18,2) NOT NULL,
        [Remark] nvarchar(200) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_DailyOutputEstimates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603211641_AddDailyOutputEstimateAndCapacityWorkDays'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603211641_AddDailyOutputEstimateAndCapacityWorkDays', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603224128_AddCapacityWorkDaysToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [CapacityWorkDays] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603224128_AddCapacityWorkDaysToWorkOrderSchedule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603224128_AddCapacityWorkDaysToWorkOrderSchedule', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604032838_RefactorToLeftJoinMode'
)
BEGIN
    DROP TABLE [RawMaterialLockPlanAndExecution];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604032838_RefactorToLeftJoinMode'
)
BEGIN
    CREATE TABLE [RawMaterialLockPreExecution] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [IsPreInput] bit NOT NULL DEFAULT CAST(0 AS bit),
        [BudgetInputDate] date NULL,
        [IsMainNoMaterialComplete] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_RawMaterialLockPreExecution] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604032838_RefactorToLeftJoinMode'
)
BEGIN
    CREATE UNIQUE INDEX [UK_RMLPE_WorkOrderId] ON [RawMaterialLockPreExecution] ([WorkOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604032838_RefactorToLeftJoinMode'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260604032838_RefactorToLeftJoinMode', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604041505_RemoveUnusedFieldsFromSalesUrging'
)
BEGIN
    DECLARE @var202 sysname;
    SELECT @var202 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesUrging]') AND [c].[name] = N'EstimatedArrivalDate');
    IF @var202 IS NOT NULL EXEC(N'ALTER TABLE [SalesUrging] DROP CONSTRAINT [' + @var202 + '];');
    ALTER TABLE [SalesUrging] DROP COLUMN [EstimatedArrivalDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604041505_RemoveUnusedFieldsFromSalesUrging'
)
BEGIN
    DECLARE @var203 sysname;
    SELECT @var203 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesUrging]') AND [c].[name] = N'IsLockConfirmed');
    IF @var203 IS NOT NULL EXEC(N'ALTER TABLE [SalesUrging] DROP CONSTRAINT [' + @var203 + '];');
    ALTER TABLE [SalesUrging] DROP COLUMN [IsLockConfirmed];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604041505_RemoveUnusedFieldsFromSalesUrging'
)
BEGIN
    DECLARE @var204 sysname;
    SELECT @var204 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesUrging]') AND [c].[name] = N'IsMainNoMaterialComplete');
    IF @var204 IS NOT NULL EXEC(N'ALTER TABLE [SalesUrging] DROP CONSTRAINT [' + @var204 + '];');
    ALTER TABLE [SalesUrging] DROP COLUMN [IsMainNoMaterialComplete];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604041505_RemoveUnusedFieldsFromSalesUrging'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260604041505_RemoveUnusedFieldsFromSalesUrging', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [DeformedProcessCompleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingSection20Roll] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingSection30Roll] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingSection50Roll] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingSection60Roll] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingSectionDrawBench] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingSectionRoughTube] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingSectionThreeRoll] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [PendingSectionWarehouseFix] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    ALTER TABLE [WorkOrderExecutionSummary] ADD [ProductionAttentionProcess] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604221212_AddPendingSectionFieldsG14'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260604221212_AddPendingSectionFieldsG14', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605024734_AddBatchDeliveryToSalesUrging'
)
BEGIN
    ALTER TABLE [SalesUrging] ADD [IsBatchDelivery] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605024734_AddBatchDeliveryToSalesUrging'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605024734_AddBatchDeliveryToSalesUrging', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [DeformedProcessCompleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [PendingSection20Roll] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [PendingSection30Roll] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [PendingSection50Roll] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [PendingSection60Roll] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [PendingSectionDrawBench] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [PendingSectionRoughTube] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [PendingSectionThreeRoll] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [PendingSectionWarehouseFix] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [ProductionAttentionProcess] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030206_AddG14FieldsToWorkOrderSchedule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605030206_AddG14FieldsToWorkOrderSchedule', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030700_AddBatchDeliveryToWorkOrderSchedule'
)
BEGIN
    DECLARE @var205 sysname;
    SELECT @var205 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkOrderSchedules]') AND [c].[name] = N'ProductionAttentionProcess');
    IF @var205 IS NOT NULL EXEC(N'ALTER TABLE [WorkOrderSchedules] DROP CONSTRAINT [' + @var205 + '];');
    ALTER TABLE [WorkOrderSchedules] ALTER COLUMN [ProductionAttentionProcess] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030700_AddBatchDeliveryToWorkOrderSchedule'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [IsBatchDelivery] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605030700_AddBatchDeliveryToWorkOrderSchedule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605030700_AddBatchDeliveryToWorkOrderSchedule', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605042906_AddIsPausedToSalesUrging'
)
BEGIN
    ALTER TABLE [SalesUrging] ADD [IsPaused] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605042906_AddIsPausedToSalesUrging'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605042906_AddIsPausedToSalesUrging', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605051230_RenameIsDemandAdjustmentToIsUrging'
)
BEGIN
    EXEC sp_rename N'[OrderDemandAdjustment].[IsDemandAdjustment]', N'IsUrging', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605051230_RenameIsDemandAdjustmentToIsUrging'
)
BEGIN
    EXEC sp_rename N'[WorkOrderSchedules].[IsDemandAdjustment]', N'IsUrging', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605051230_RenameIsDemandAdjustmentToIsUrging'
)
BEGIN
    ALTER TABLE [WorkOrderSchedules] ADD [IsPaused] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605051230_RenameIsDemandAdjustmentToIsUrging'
)
BEGIN
    DECLARE @var206 sysname;
    SELECT @var206 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderDemandAdjustment]') AND [c].[name] = N'IsBatchDelivery');
    IF @var206 IS NOT NULL EXEC(N'ALTER TABLE [OrderDemandAdjustment] DROP CONSTRAINT [' + @var206 + '];');
    ALTER TABLE [OrderDemandAdjustment] ADD DEFAULT CAST(0 AS bit) FOR [IsBatchDelivery];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605051230_RenameIsDemandAdjustmentToIsUrging'
)
BEGIN
    DECLARE @var207 sysname;
    SELECT @var207 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderDemandAdjustment]') AND [c].[name] = N'IsPaused');
    IF @var207 IS NOT NULL EXEC(N'ALTER TABLE [OrderDemandAdjustment] DROP CONSTRAINT [' + @var207 + '];');
    ALTER TABLE [OrderDemandAdjustment] ADD DEFAULT CAST(0 AS bit) FOR [IsPaused];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605051230_RenameIsDemandAdjustmentToIsUrging'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605051230_RenameIsDemandAdjustmentToIsUrging', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606204623_AddLengthStatusAndProductionWeightToMaterialReceiveCheck'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [LengthStatus] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606204623_AddLengthStatusAndProductionWeightToMaterialReceiveCheck'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [ProductionCutQuantity] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606204623_AddLengthStatusAndProductionWeightToMaterialReceiveCheck'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [ProductionWeight] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606204623_AddLengthStatusAndProductionWeightToMaterialReceiveCheck'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260606204623_AddLengthStatusAndProductionWeightToMaterialReceiveCheck', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606222941_AddSalesmanAndDeliveryStateToMaterialReceiveCheck'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [DeliveryState] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606222941_AddSalesmanAndDeliveryStateToMaterialReceiveCheck'
)
BEGIN
    ALTER TABLE [MaterialReceiveCheck] ADD [Salesman] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606222941_AddSalesmanAndDeliveryStateToMaterialReceiveCheck'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260606222941_AddSalesmanAndDeliveryStateToMaterialReceiveCheck', N'8.0.0');
END;
GO

COMMIT;
GO

