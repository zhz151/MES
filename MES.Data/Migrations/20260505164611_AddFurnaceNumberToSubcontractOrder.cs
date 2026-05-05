using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFurnaceNumberToSubcontractOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractOrder]') AND name = 'FurnaceNumber')
                BEGIN
                    ALTER TABLE [SubcontractOrder] ADD [FurnaceNumber] nvarchar(50) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractOrder]') AND name = 'FurnaceNumber')
                BEGIN
                    ALTER TABLE [SubcontractOrder] DROP COLUMN [FurnaceNumber];
                END
            ");
        }
    }
}
