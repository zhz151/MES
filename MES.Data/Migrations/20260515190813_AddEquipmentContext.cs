using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipmentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipmentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModelNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TechnicalParams = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InstallationDate = table.Column<DateTime>(type: "date", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RelatedSection = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NeedInspection = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    InspectionPerson = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InspectionCycleDays = table.Column<int>(type: "int", nullable: false, defaultValue: 7),
                    LastInspectionDate = table.Column<DateTime>(type: "date", nullable: true),
                    NextInspectionDate = table.Column<DateTime>(type: "date", nullable: true),
                    InspectionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    NeedMaintenance = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaintPerson = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaintCycleDays = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    LastMaintDate = table.Column<DateTime>(type: "date", nullable: true),
                    NextMaintDate = table.Column<DateTime>(type: "date", nullable: true),
                    MaintStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    UsageType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Primary"),
                    RunningStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InspectionRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "date", nullable: false),
                    ActualDate = table.Column<DateTime>(type: "date", nullable: true),
                    Inspector = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ChecklistResults = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionRecord_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaintOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    MaintType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Monthly"),
                    ScheduledDate = table.Column<DateTime>(type: "date", nullable: false),
                    ActualDate = table.Column<DateTime>(type: "date", nullable: true),
                    Executor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ChecklistResults = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceOrder_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepairOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepairOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    FaultDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FaultType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    RepairStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ReportPerson = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReportTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RepairPerson = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RepairStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RepairEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RepairContent = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SparePartUsed = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DowntimeHours = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VerifyPerson = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VerifyTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifyComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairOrder_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_InspectionStatus",
                table: "Equipment",
                column: "InspectionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_LifecycleStatus",
                table: "Equipment",
                column: "LifecycleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Location",
                table: "Equipment",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_MaintStatus",
                table: "Equipment",
                column: "MaintStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Name",
                table: "Equipment",
                column: "EquipmentName");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_NeedInspection",
                table: "Equipment",
                column: "NeedInspection");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_NeedMaintenance",
                table: "Equipment",
                column: "NeedMaintenance");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_RelatedSection",
                table: "Equipment",
                column: "RelatedSection");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_RunningStatus",
                table: "Equipment",
                column: "RunningStatus");

            migrationBuilder.CreateIndex(
                name: "UK_Equipment_Code",
                table: "Equipment",
                column: "EquipmentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecord_EquipmentId",
                table: "InspectionRecord",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecord_ScheduledDate",
                table: "InspectionRecord",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecord_Status",
                table: "InspectionRecord",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UK_InspectionRecord_No",
                table: "InspectionRecord",
                column: "RecordNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrder_EquipmentId",
                table: "MaintenanceOrder",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrder_ScheduledDate",
                table: "MaintenanceOrder",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrder_Status",
                table: "MaintenanceOrder",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UK_MaintenanceOrder_No",
                table: "MaintenanceOrder",
                column: "MaintOrderNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrder_EquipmentId",
                table: "RepairOrder",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrder_ReportTime",
                table: "RepairOrder",
                column: "ReportTime");

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrder_Status",
                table: "RepairOrder",
                column: "RepairStatus");

            migrationBuilder.CreateIndex(
                name: "UK_RepairOrder_No",
                table: "RepairOrder",
                column: "RepairOrderNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionRecord");

            migrationBuilder.DropTable(
                name: "MaintenanceOrder");

            migrationBuilder.DropTable(
                name: "RepairOrder");

            migrationBuilder.DropTable(
                name: "Equipment");
        }
    }
}
