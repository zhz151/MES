using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutsourceVendorProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutsourceVendorProfile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsWorkshop = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutsourceVendorProfile", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_OutsourceVendor_Code",
                table: "OutsourceVendorProfile",
                column: "VendorCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_OutsourceVendor_Name_Section",
                table: "OutsourceVendorProfile",
                columns: new[] { "VendorName", "SectionName" },
                unique: true);

            // 存量初始化：从 SectionOutsource 历史委外 (单位 × 工段 × 是否厂内) 去重回填档案。
            // 厂内车间(IsInternal=1) → IsWorkshop=1；编码按名流水生成 WV0001...
            migrationBuilder.Sql(@"
INSERT INTO [OutsourceVendorProfile]
    ([VendorCode], [VendorName], [SectionName], [IsWorkshop], [IsActive],
     [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
SELECT 'WV' + RIGHT('0000' + CAST(ROW_NUMBER() OVER (ORDER BY s.[VendorName], s.[SectionName]) AS varchar(4)), 4),
       s.[VendorName], s.[SectionName], s.[IsWorkshop], 1,
       SYSDATETIMEOFFSET(), 'migration', SYSDATETIMEOFFSET(), 'migration'
FROM (SELECT DISTINCT [OutsourceVendor] AS [VendorName], [SectionName],
             CASE WHEN [IsInternal] = 1 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [IsWorkshop]
      FROM [SectionOutsource]) s;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutsourceVendorProfile");
        }
    }
}
