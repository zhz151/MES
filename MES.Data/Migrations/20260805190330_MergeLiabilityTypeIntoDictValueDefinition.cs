using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class MergeLiabilityTypeIntoDictValueDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 数据搬移：LiabilityTypeDefinitions → DictValueDefinitions（DictKey=LiabilityTypeKey，责任类别并入字典显示配置）
            migrationBuilder.Sql(@"
INSERT INTO [DictValueDefinitions]
    ([DictKey], [Value], [DisplayName], [DisplayOrder], [IsEnabled], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
SELECT 'LiabilityTypeKey', [LiabilityKey], [LiabilityName], [DisplayOrder], [IsEnabled], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy]
FROM [LiabilityTypeDefinitions]");

            migrationBuilder.DropTable(
                name: "LiabilityTypeDefinitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiabilityTypeDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LiabilityKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LiabilityName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiabilityTypeDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_LTD_LiabilityKey",
                table: "LiabilityTypeDefinitions",
                column: "LiabilityKey",
                unique: true);

            // 还原数据：DictValueDefinitions（DictKey=LiabilityTypeKey）→ LiabilityTypeDefinitions
            migrationBuilder.Sql(@"
INSERT INTO [LiabilityTypeDefinitions]
    ([LiabilityKey], [LiabilityName], [DisplayOrder], [IsEnabled], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
SELECT [Value], [DisplayName], [DisplayOrder], [IsEnabled], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy]
FROM [DictValueDefinitions]
WHERE [DictKey] = 'LiabilityTypeKey'");
        }
    }
}
