using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameQualityTestFurnaceNoToBatchNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FurnaceNo",
                table: "TensileTest",
                newName: "BatchNo");

            migrationBuilder.RenameIndex(
                name: "IX_TensileTest_FurnaceNo",
                table: "TensileTest",
                newName: "IX_TensileTest_BatchNo");

            migrationBuilder.RenameColumn(
                name: "FurnaceNo",
                table: "PittingCorrosionTest",
                newName: "BatchNo");

            migrationBuilder.RenameIndex(
                name: "IX_PittingCorrosionTest_FurnaceNo",
                table: "PittingCorrosionTest",
                newName: "IX_PittingCorrosionTest_BatchNo");

            migrationBuilder.RenameColumn(
                name: "FurnaceNo",
                table: "MetallographicTest",
                newName: "BatchNo");

            migrationBuilder.RenameIndex(
                name: "IX_MetallographicTest_FurnaceNo",
                table: "MetallographicTest",
                newName: "IX_MetallographicTest_BatchNo");

            migrationBuilder.RenameColumn(
                name: "FurnaceNo",
                table: "IntergranularCorrosionTest",
                newName: "BatchNo");

            migrationBuilder.RenameIndex(
                name: "IX_IntergranularCorrosionTest_FurnaceNo",
                table: "IntergranularCorrosionTest",
                newName: "IX_IntergranularCorrosionTest_BatchNo");

            migrationBuilder.RenameColumn(
                name: "FurnaceNo",
                table: "HardnessTest",
                newName: "BatchNo");

            migrationBuilder.RenameIndex(
                name: "IX_HardnessTest_FurnaceNo",
                table: "HardnessTest",
                newName: "IX_HardnessTest_BatchNo");

            migrationBuilder.RenameColumn(
                name: "FurnaceNo",
                table: "GrainSizeTest",
                newName: "BatchNo");

            migrationBuilder.RenameIndex(
                name: "IX_GrainSizeTest_FurnaceNo",
                table: "GrainSizeTest",
                newName: "IX_GrainSizeTest_BatchNo");

            migrationBuilder.RenameColumn(
                name: "FurnaceNo",
                table: "FlatteningTest",
                newName: "BatchNo");

            migrationBuilder.RenameIndex(
                name: "IX_FlatteningTest_FurnaceNo",
                table: "FlatteningTest",
                newName: "IX_FlatteningTest_BatchNo");

            migrationBuilder.RenameColumn(
                name: "FurnaceNo",
                table: "FlaringTest",
                newName: "BatchNo");

            migrationBuilder.RenameIndex(
                name: "IX_FlaringTest_FurnaceNo",
                table: "FlaringTest",
                newName: "IX_FlaringTest_BatchNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BatchNo",
                table: "TensileTest",
                newName: "FurnaceNo");

            migrationBuilder.RenameIndex(
                name: "IX_TensileTest_BatchNo",
                table: "TensileTest",
                newName: "IX_TensileTest_FurnaceNo");

            migrationBuilder.RenameColumn(
                name: "BatchNo",
                table: "PittingCorrosionTest",
                newName: "FurnaceNo");

            migrationBuilder.RenameIndex(
                name: "IX_PittingCorrosionTest_BatchNo",
                table: "PittingCorrosionTest",
                newName: "IX_PittingCorrosionTest_FurnaceNo");

            migrationBuilder.RenameColumn(
                name: "BatchNo",
                table: "MetallographicTest",
                newName: "FurnaceNo");

            migrationBuilder.RenameIndex(
                name: "IX_MetallographicTest_BatchNo",
                table: "MetallographicTest",
                newName: "IX_MetallographicTest_FurnaceNo");

            migrationBuilder.RenameColumn(
                name: "BatchNo",
                table: "IntergranularCorrosionTest",
                newName: "FurnaceNo");

            migrationBuilder.RenameIndex(
                name: "IX_IntergranularCorrosionTest_BatchNo",
                table: "IntergranularCorrosionTest",
                newName: "IX_IntergranularCorrosionTest_FurnaceNo");

            migrationBuilder.RenameColumn(
                name: "BatchNo",
                table: "HardnessTest",
                newName: "FurnaceNo");

            migrationBuilder.RenameIndex(
                name: "IX_HardnessTest_BatchNo",
                table: "HardnessTest",
                newName: "IX_HardnessTest_FurnaceNo");

            migrationBuilder.RenameColumn(
                name: "BatchNo",
                table: "GrainSizeTest",
                newName: "FurnaceNo");

            migrationBuilder.RenameIndex(
                name: "IX_GrainSizeTest_BatchNo",
                table: "GrainSizeTest",
                newName: "IX_GrainSizeTest_FurnaceNo");

            migrationBuilder.RenameColumn(
                name: "BatchNo",
                table: "FlatteningTest",
                newName: "FurnaceNo");

            migrationBuilder.RenameIndex(
                name: "IX_FlatteningTest_BatchNo",
                table: "FlatteningTest",
                newName: "IX_FlatteningTest_FurnaceNo");

            migrationBuilder.RenameColumn(
                name: "BatchNo",
                table: "FlaringTest",
                newName: "FurnaceNo");

            migrationBuilder.RenameIndex(
                name: "IX_FlaringTest_BatchNo",
                table: "FlaringTest",
                newName: "IX_FlaringTest_FurnaceNo");
        }
    }
}
