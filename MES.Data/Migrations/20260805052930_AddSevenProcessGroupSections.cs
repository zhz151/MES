using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSevenProcessGroupSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BrightAnnealing",
                table: "SemiPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmulsionWash",
                table: "SemiPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Extra1",
                table: "SemiPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Extra2",
                table: "SemiPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Packing",
                table: "SemiPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShotBlasting",
                table: "SemiPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Welding",
                table: "SemiPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BrightAnnealing",
                table: "ProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmulsionWash",
                table: "ProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Extra1",
                table: "ProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Extra2",
                table: "ProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Packing",
                table: "ProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShotBlasting",
                table: "ProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Welding",
                table: "ProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BrightAnnealing",
                table: "PiercingPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmulsionWash",
                table: "PiercingPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Extra1",
                table: "PiercingPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Extra2",
                table: "PiercingPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Packing",
                table: "PiercingPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShotBlasting",
                table: "PiercingPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Welding",
                table: "PiercingPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BrightAnnealing",
                table: "InventoryPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmulsionWash",
                table: "InventoryPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Extra1",
                table: "InventoryPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Extra2",
                table: "InventoryPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Packing",
                table: "InventoryPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShotBlasting",
                table: "InventoryPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Welding",
                table: "InventoryPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BrightAnnealing",
                table: "InProcessReworkPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmulsionWash",
                table: "InProcessReworkPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Extra1",
                table: "InProcessReworkPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Extra2",
                table: "InProcessReworkPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Packing",
                table: "InProcessReworkPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShotBlasting",
                table: "InProcessReworkPlanProcessGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Welding",
                table: "InProcessReworkPlanProcessGroup",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrightAnnealing",
                table: "SemiPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "EmulsionWash",
                table: "SemiPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Extra1",
                table: "SemiPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Extra2",
                table: "SemiPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Packing",
                table: "SemiPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "ShotBlasting",
                table: "SemiPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Welding",
                table: "SemiPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "BrightAnnealing",
                table: "ProcessGroup");

            migrationBuilder.DropColumn(
                name: "EmulsionWash",
                table: "ProcessGroup");

            migrationBuilder.DropColumn(
                name: "Extra1",
                table: "ProcessGroup");

            migrationBuilder.DropColumn(
                name: "Extra2",
                table: "ProcessGroup");

            migrationBuilder.DropColumn(
                name: "Packing",
                table: "ProcessGroup");

            migrationBuilder.DropColumn(
                name: "ShotBlasting",
                table: "ProcessGroup");

            migrationBuilder.DropColumn(
                name: "Welding",
                table: "ProcessGroup");

            migrationBuilder.DropColumn(
                name: "BrightAnnealing",
                table: "PiercingPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "EmulsionWash",
                table: "PiercingPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Extra1",
                table: "PiercingPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Extra2",
                table: "PiercingPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Packing",
                table: "PiercingPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "ShotBlasting",
                table: "PiercingPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Welding",
                table: "PiercingPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "BrightAnnealing",
                table: "InventoryPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "EmulsionWash",
                table: "InventoryPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Extra1",
                table: "InventoryPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Extra2",
                table: "InventoryPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Packing",
                table: "InventoryPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "ShotBlasting",
                table: "InventoryPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Welding",
                table: "InventoryPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "BrightAnnealing",
                table: "InProcessReworkPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "EmulsionWash",
                table: "InProcessReworkPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Extra1",
                table: "InProcessReworkPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Extra2",
                table: "InProcessReworkPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Packing",
                table: "InProcessReworkPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "ShotBlasting",
                table: "InProcessReworkPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "Welding",
                table: "InProcessReworkPlanProcessGroup");
        }
    }
}
