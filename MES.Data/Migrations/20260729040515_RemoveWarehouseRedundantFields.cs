using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWarehouseRedundantFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InboundDate",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "InboundSource",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "ProductionBatch");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InboundDate",
                table: "ProductionBatch",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundSource",
                table: "ProductionBatch",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "ProductionBatch",
                type: "int",
                nullable: true);
        }
    }
}
