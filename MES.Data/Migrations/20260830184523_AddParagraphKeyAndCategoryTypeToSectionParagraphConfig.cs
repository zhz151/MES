using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParagraphKeyAndCategoryTypeToSectionParagraphConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_SPC_ParagraphName",
                table: "SectionParagraphConfigs");

            migrationBuilder.AddColumn<string>(
                name: "CategoryType",
                table: "SectionParagraphConfigs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParagraphKey",
                table: "SectionParagraphConfigs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // 回填存量段落：能映射到 3 类配置的按稳定 Key 回填（保留参数），无法映射的留空由首次同步清理。
            // 冷轧拔=机台组稳定 Key（5060/2030/ThreeRoll/Draw）；普通工段=工段英文 Key（旧"切割"映射到工段"断切" Cut）。
            migrationBuilder.Sql("UPDATE [SectionParagraphConfigs] SET CategoryType = N'Cold',    ParagraphKey = N'5060'     WHERE ParagraphName = N'冷轧5060'");
            migrationBuilder.Sql("UPDATE [SectionParagraphConfigs] SET CategoryType = N'Cold',    ParagraphKey = N'2030'     WHERE ParagraphName = N'冷轧2030'");
            migrationBuilder.Sql("UPDATE [SectionParagraphConfigs] SET CategoryType = N'Cold',    ParagraphKey = N'ThreeRoll' WHERE ParagraphName = N'冷轧三辊'");
            migrationBuilder.Sql("UPDATE [SectionParagraphConfigs] SET CategoryType = N'Cold',    ParagraphKey = N'Draw'      WHERE ParagraphName = N'冷拔'");
            migrationBuilder.Sql("UPDATE [SectionParagraphConfigs] SET CategoryType = N'Section', ParagraphKey = N'Solution'   WHERE ParagraphName = N'固溶'");
            migrationBuilder.Sql("UPDATE [SectionParagraphConfigs] SET CategoryType = N'Section', ParagraphKey = N'Straighten' WHERE ParagraphName = N'矫直'");
            migrationBuilder.Sql("UPDATE [SectionParagraphConfigs] SET CategoryType = N'Section', ParagraphKey = N'Cut'        WHERE ParagraphName = N'切割'");
            migrationBuilder.Sql("UPDATE [SectionParagraphConfigs] SET CategoryType = N'Section', ParagraphKey = N'Cut'        WHERE ParagraphName = N'断切'");
            migrationBuilder.Sql("UPDATE [SectionParagraphConfigs] SET CategoryType = N'Section', ParagraphKey = N'Degrease'   WHERE ParagraphName = N'去油'");
            migrationBuilder.Sql("UPDATE [SectionParagraphConfigs] SET CategoryType = N'Section', ParagraphKey = N'Pickle'     WHERE ParagraphName = N'酸洗'");

            migrationBuilder.CreateIndex(
                name: "UK_SPC_CategoryType_ParagraphKey",
                table: "SectionParagraphConfigs",
                columns: new[] { "CategoryType", "ParagraphKey" },
                unique: true,
                filter: "[CategoryType] IS NOT NULL AND [ParagraphKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_SPC_CategoryType_ParagraphKey",
                table: "SectionParagraphConfigs");

            migrationBuilder.DropColumn(
                name: "CategoryType",
                table: "SectionParagraphConfigs");

            migrationBuilder.DropColumn(
                name: "ParagraphKey",
                table: "SectionParagraphConfigs");

            migrationBuilder.CreateIndex(
                name: "UK_SPC_ParagraphName",
                table: "SectionParagraphConfigs",
                column: "ParagraphName",
                unique: true);
        }
    }
}
