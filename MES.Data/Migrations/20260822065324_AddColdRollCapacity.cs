using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddColdRollCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ColdRollCapacity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BilletSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RollingSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsFinished = table.Column<bool>(type: "bit", nullable: false),
                    MachineNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DailyOutput = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    SampleCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColdRollCapacity", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_CRC_Dimensions",
                table: "ColdRollCapacity",
                columns: new[] { "ProcessType", "BilletSpec", "RollingSpec", "IsFinished" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ColdRollCapacity");
        }
    }
}
