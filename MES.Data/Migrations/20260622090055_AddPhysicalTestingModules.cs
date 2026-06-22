using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalTestingModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Remark",
                table: "GrainSizeTest");

            migrationBuilder.AddColumn<string>(
                name: "Magnification",
                table: "GrainSizeTest",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // 6张表已在之前应用迁移时创建，用 IF NOT EXISTS 避免重复创建
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FlaringTest')
BEGIN
    CREATE TABLE [FlaringTest] (
        [Id] int NOT NULL IDENTITY,
        [InspectionDate] datetime2 NOT NULL,
        [Inspector] nvarchar(50) NOT NULL,
        [FurnaceNo] nvarchar(50) NOT NULL,
        [Grade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [SampleNo] int NULL,
        [SampleSize] nvarchar(50) NULL,
        [InspectionStandard] nvarchar(100) NULL,
        [MandrelTaper] nvarchar(50) NULL,
        [FlaredDiameter] decimal(18,6) NULL,
        [FlaringRate] decimal(18,6) NULL,
        [Observation] nvarchar(200) NULL,
        [Judgment] nvarchar(50) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_FlaringTest] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FlatteningTest')
BEGIN
    CREATE TABLE [FlatteningTest] (
        [Id] int NOT NULL IDENTITY,
        [InspectionDate] datetime2 NOT NULL,
        [Inspector] nvarchar(50) NOT NULL,
        [FurnaceNo] nvarchar(50) NOT NULL,
        [Grade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [SampleNo] int NULL,
        [SampleSize] nvarchar(50) NULL,
        [InspectionStandard] nvarchar(100) NULL,
        [FlatteningGap] decimal(18,6) NULL,
        [Observation] nvarchar(200) NULL,
        [Judgment] nvarchar(50) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_FlatteningTest] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'IntergranularCorrosionTest')
BEGIN
    CREATE TABLE [IntergranularCorrosionTest] (
        [Id] int NOT NULL IDENTITY,
        [InspectionDate] datetime2 NOT NULL,
        [Inspector] nvarchar(50) NOT NULL,
        [FurnaceNo] nvarchar(50) NOT NULL,
        [Grade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [SampleNo] int NULL,
        [SampleSize] nvarchar(50) NULL,
        [InspectionStandard] nvarchar(100) NULL,
        [SensitizationTemperature] nvarchar(50) NULL,
        [SensitizationDuration] nvarchar(50) NULL,
        [CorrosionSolution] nvarchar(100) NULL,
        [CorrosionTime] nvarchar(50) NULL,
        [BendDegree] nvarchar(50) NULL,
        [Magnification] nvarchar(50) NULL,
        [ObservationResult] nvarchar(200) NULL,
        [Judgment] nvarchar(50) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_IntergranularCorrosionTest] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MetallographicTest')
BEGIN
    CREATE TABLE [MetallographicTest] (
        [Id] int NOT NULL IDENTITY,
        [InspectionDate] datetime2 NOT NULL,
        [Inspector] nvarchar(50) NOT NULL,
        [FurnaceNo] nvarchar(50) NOT NULL,
        [Grade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [SampleNo] int NULL,
        [SampleSize] nvarchar(50) NULL,
        [InspectionStandard] nvarchar(100) NULL,
        [EtchingMethod] nvarchar(100) NULL,
        [ElectrolyticVoltage] nvarchar(50) NULL,
        [ElectrolyticTime] nvarchar(50) NULL,
        [Magnification] nvarchar(50) NULL,
        [FerriteContent] decimal(18,6) NULL,
        [Judgment] nvarchar(50) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_MetallographicTest] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PittingCorrosionTest')
BEGIN
    CREATE TABLE [PittingCorrosionTest] (
        [Id] int NOT NULL IDENTITY,
        [InspectionDate] datetime2 NOT NULL,
        [Inspector] nvarchar(50) NOT NULL,
        [FurnaceNo] nvarchar(50) NOT NULL,
        [Grade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [SampleNo] int NULL,
        [SampleSize] nvarchar(50) NULL,
        [InspectionStandard] nvarchar(100) NULL,
        [PolishingGrade] nvarchar(100) NULL,
        [RawWeight] decimal(18,6) NULL,
        [CorrosionSolution] nvarchar(100) NULL,
        [CorrosionTemperature] nvarchar(50) NULL,
        [CorrosionTime] nvarchar(50) NULL,
        [FinalWeight] decimal(18,6) NULL,
        [CorrosionRate] decimal(18,6) NULL,
        [MaxPitDepth] decimal(18,6) NULL,
        [Judgment] nvarchar(50) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_PittingCorrosionTest] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TensileTest')
BEGIN
    CREATE TABLE [TensileTest] (
        [Id] int NOT NULL IDENTITY,
        [InspectionDate] datetime2 NOT NULL,
        [Inspector] nvarchar(50) NOT NULL,
        [FurnaceNo] nvarchar(50) NOT NULL,
        [Grade] nvarchar(50) NOT NULL,
        [Specification] nvarchar(100) NOT NULL,
        [SampleNo] int NULL,
        [SampleSize] nvarchar(50) NULL,
        [InspectionStandard] nvarchar(100) NULL,
        [OriginalGaugeLength] decimal(18,6) NULL,
        [FinalGaugeLength] decimal(18,6) NULL,
        [TensileStrength] decimal(18,6) NULL,
        [YieldStrengthRp02] decimal(18,6) NULL,
        [YieldStrengthRp1] decimal(18,6) NULL,
        [Elongation] decimal(18,6) NULL,
        [Judgment] nvarchar(50) NULL,
        [CreatedTime] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedTime] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_TensileTest] PRIMARY KEY ([Id])
    );
END");

            // 索引也加 IF NOT EXISTS 保护
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FlaringTest_FurnaceNo') CREATE INDEX [IX_FlaringTest_FurnaceNo] ON [FlaringTest] ([FurnaceNo])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FlaringTest_Grade') CREATE INDEX [IX_FlaringTest_Grade] ON [FlaringTest] ([Grade])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FlaringTest_InspectionDate') CREATE INDEX [IX_FlaringTest_InspectionDate] ON [FlaringTest] ([InspectionDate])");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FlatteningTest_FurnaceNo') CREATE INDEX [IX_FlatteningTest_FurnaceNo] ON [FlatteningTest] ([FurnaceNo])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FlatteningTest_Grade') CREATE INDEX [IX_FlatteningTest_Grade] ON [FlatteningTest] ([Grade])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FlatteningTest_InspectionDate') CREATE INDEX [IX_FlatteningTest_InspectionDate] ON [FlatteningTest] ([InspectionDate])");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_IntergranularCorrosionTest_FurnaceNo') CREATE INDEX [IX_IntergranularCorrosionTest_FurnaceNo] ON [IntergranularCorrosionTest] ([FurnaceNo])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_IntergranularCorrosionTest_Grade') CREATE INDEX [IX_IntergranularCorrosionTest_Grade] ON [IntergranularCorrosionTest] ([Grade])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_IntergranularCorrosionTest_InspectionDate') CREATE INDEX [IX_IntergranularCorrosionTest_InspectionDate] ON [IntergranularCorrosionTest] ([InspectionDate])");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MetallographicTest_FurnaceNo') CREATE INDEX [IX_MetallographicTest_FurnaceNo] ON [MetallographicTest] ([FurnaceNo])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MetallographicTest_Grade') CREATE INDEX [IX_MetallographicTest_Grade] ON [MetallographicTest] ([Grade])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MetallographicTest_InspectionDate') CREATE INDEX [IX_MetallographicTest_InspectionDate] ON [MetallographicTest] ([InspectionDate])");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PittingCorrosionTest_FurnaceNo') CREATE INDEX [IX_PittingCorrosionTest_FurnaceNo] ON [PittingCorrosionTest] ([FurnaceNo])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PittingCorrosionTest_Grade') CREATE INDEX [IX_PittingCorrosionTest_Grade] ON [PittingCorrosionTest] ([Grade])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PittingCorrosionTest_InspectionDate') CREATE INDEX [IX_PittingCorrosionTest_InspectionDate] ON [PittingCorrosionTest] ([InspectionDate])");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TensileTest_FurnaceNo') CREATE INDEX [IX_TensileTest_FurnaceNo] ON [TensileTest] ([FurnaceNo])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TensileTest_Grade') CREATE INDEX [IX_TensileTest_Grade] ON [TensileTest] ([Grade])");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TensileTest_InspectionDate') CREATE INDEX [IX_TensileTest_InspectionDate] ON [TensileTest] ([InspectionDate])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlaringTest");

            migrationBuilder.DropTable(
                name: "FlatteningTest");

            migrationBuilder.DropTable(
                name: "IntergranularCorrosionTest");

            migrationBuilder.DropTable(
                name: "MetallographicTest");

            migrationBuilder.DropTable(
                name: "PittingCorrosionTest");

            migrationBuilder.DropTable(
                name: "TensileTest");

            migrationBuilder.DropColumn(
                name: "Magnification",
                table: "GrainSizeTest");

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "GrainSizeTest",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
