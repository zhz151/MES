using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Certificate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CertificateNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProductStandard = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeliveryStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    Signatory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CertificateItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CertificateId = table.Column<int>(type: "int", nullable: false),
                    SeqNo = table.Column<int>(type: "int", nullable: false),
                    InventoryBatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductionBatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HeatNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SteelGrade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Specification = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LengthDesc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Meters = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ChemC = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemSi = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemMn = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemP = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemS = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemNi = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemCr = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemMo = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemCu = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemN = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemNb = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemTi = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemFe = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemAl = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemW = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ChemPREN = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    InspPMI = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspVisual = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspDimension = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspEndoscopy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspHydro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspUnderwaterPneumatic = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspEddyCurrent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspUltrasonic = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspPortDye = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TensileStrength_1 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    TensileStrength_2 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    YieldRp02_1 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    YieldRp02_2 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    YieldRp10_1 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    YieldRp10_2 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Elongation_1 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Elongation_2 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Hardness_1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Hardness_2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GrainSize_1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GrainSize_2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FerriteContent_1 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    FerriteContent_2 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    FlaringResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FlatteningResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IntergranularResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PittingResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateItem_Certificate_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "Certificate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UK_Certificate_No",
                table: "Certificate",
                column: "CertificateNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateItem_CertificateId",
                table: "CertificateItem",
                column: "CertificateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateItem");

            migrationBuilder.DropTable(
                name: "Certificate");
        }
    }
}
