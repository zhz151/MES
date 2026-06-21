using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleOrderItemFromProductionStandard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItem_ProductionStandard_ProductionStandardId",
                table: "OrderItem");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItem_StandardGradeMapping_StandardGrade",
                table: "OrderItem");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_StandardGradeMapping_StandardGrade",
                table: "StandardGradeMapping");

            migrationBuilder.DropIndex(
                name: "IX_OrderItem_ProductStandardId",
                table: "OrderItem");

            migrationBuilder.DropIndex(
                name: "IX_OrderItem_StandardGrade",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "ProductionStandardId",
                table: "OrderItem");

            migrationBuilder.AddColumn<string>(
                name: "StandardNo",
                table: "OrderItem",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandardNo",
                table: "OrderItem");

            migrationBuilder.AddColumn<int>(
                name: "ProductionStandardId",
                table: "OrderItem",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_StandardGradeMapping_StandardGrade",
                table: "StandardGradeMapping",
                column: "StandardGrade");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItem_ProductStandardId",
                table: "OrderItem",
                column: "ProductionStandardId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItem_StandardGrade",
                table: "OrderItem",
                column: "StandardGrade");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItem_ProductionStandard_ProductionStandardId",
                table: "OrderItem",
                column: "ProductionStandardId",
                principalTable: "ProductionStandard",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItem_StandardGradeMapping_StandardGrade",
                table: "OrderItem",
                column: "StandardGrade",
                principalTable: "StandardGradeMapping",
                principalColumn: "StandardGrade",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
