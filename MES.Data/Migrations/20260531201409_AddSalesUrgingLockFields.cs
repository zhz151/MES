using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesUrgingLockFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedArrivalDate",
                table: "SalesUrging",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLockConfirmed",
                table: "SalesUrging",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMainNoMaterialComplete",
                table: "SalesUrging",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedArrivalDate",
                table: "SalesUrging");

            migrationBuilder.DropColumn(
                name: "IsLockConfirmed",
                table: "SalesUrging");

            migrationBuilder.DropColumn(
                name: "IsMainNoMaterialComplete",
                table: "SalesUrging");
        }
    }
}
