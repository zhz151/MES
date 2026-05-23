using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddValidInputQuestionToBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ValidInputQuestion",
                table: "ProductionBatch",
                type: "bit",
                nullable: true);

            // 已有数据均设为正常 (false)
            migrationBuilder.Sql("UPDATE [ProductionBatch] SET [ValidInputQuestion] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidInputQuestion",
                table: "ProductionBatch");
        }
    }
}
