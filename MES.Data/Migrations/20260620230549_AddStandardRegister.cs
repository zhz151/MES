using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStandardRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StandardRegister",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StandardNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StandardName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RefSpecification = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StandardLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ManufactureMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SteelType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardRegister", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StandardRegisterItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StandardRegisterId = table.Column<int>(type: "int", nullable: false),
                    SeqNo = table.Column<int>(type: "int", nullable: false),
                    InspectionCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InspectionItem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsMandatory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SamplingRequirement = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApplicableRange = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RefStandard = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DetailRequirement = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardRegisterItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandardRegisterItem_StandardRegister_StandardRegisterId",
                        column: x => x.StandardRegisterId,
                        principalTable: "StandardRegister",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UK_StandardRegister_No",
                table: "StandardRegister",
                column: "StandardNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandardRegisterItem_RegisterId",
                table: "StandardRegisterItem",
                column: "StandardRegisterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StandardRegisterItem");

            migrationBuilder.DropTable(
                name: "StandardRegister");
        }
    }
}
