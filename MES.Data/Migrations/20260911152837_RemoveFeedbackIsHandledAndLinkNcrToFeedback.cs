using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFeedbackIsHandledAndLinkNcrToFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NonconformingFeedback_IsHandled",
                table: "NonconformingFeedback");

            migrationBuilder.DropColumn(
                name: "IsHandled",
                table: "NonconformingFeedback");

            migrationBuilder.AddColumn<int>(
                name: "NonconformingFeedbackId",
                table: "Ncr",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ncr_NonconformingFeedbackId",
                table: "Ncr",
                column: "NonconformingFeedbackId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ncr_NonconformingFeedback_NonconformingFeedbackId",
                table: "Ncr",
                column: "NonconformingFeedbackId",
                principalTable: "NonconformingFeedback",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ncr_NonconformingFeedback_NonconformingFeedbackId",
                table: "Ncr");

            migrationBuilder.DropIndex(
                name: "IX_Ncr_NonconformingFeedbackId",
                table: "Ncr");

            migrationBuilder.DropColumn(
                name: "NonconformingFeedbackId",
                table: "Ncr");

            migrationBuilder.AddColumn<bool>(
                name: "IsHandled",
                table: "NonconformingFeedback",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_NonconformingFeedback_IsHandled",
                table: "NonconformingFeedback",
                column: "IsHandled");
        }
    }
}
