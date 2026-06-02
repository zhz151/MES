using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStandardWorkDayDeliveryStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StandardWorkDayDeliveryStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExtraDays = table.Column<double>(type: "float", nullable: false),
                    PlantGradePrefix = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardWorkDayDeliveryStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_SWDDS_DeliveryState_PlantGradePrefix",
                table: "StandardWorkDayDeliveryStates",
                columns: new[] { "DeliveryState", "PlantGradePrefix" },
                unique: true,
                filter: "[PlantGradePrefix] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StandardWorkDayDeliveryStates");
        }
    }
}
