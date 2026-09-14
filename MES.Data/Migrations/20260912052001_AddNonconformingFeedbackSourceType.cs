using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNonconformingFeedbackSourceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SequenceNumber",
                table: "NonconformingFeedback",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "SectionName",
                table: "NonconformingFeedback",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "InspectionItem",
                table: "NonconformingFeedback",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // 存量行回填「生产工段」：旧数据只有生产工段/过程检验两种录入场景，
            // 且存量反馈均由生产环节上报（真库仅 1 行），故以 ProductionSection 作列默认值一次性回填。
            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "NonconformingFeedback",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "ProductionSection");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InspectionItem",
                table: "NonconformingFeedback");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "NonconformingFeedback");

            migrationBuilder.AlterColumn<int>(
                name: "SequenceNumber",
                table: "NonconformingFeedback",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SectionName",
                table: "NonconformingFeedback",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
