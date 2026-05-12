using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutsourceSpecReturnDateIsUrgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedReturnDate",
                table: "SectionOutsource",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUrgent",
                table: "SectionOutsource",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OutsourceSpec",
                table: "SectionOutsource",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedReturnDate",
                table: "SectionOutsource");

            migrationBuilder.DropColumn(
                name: "IsUrgent",
                table: "SectionOutsource");

            migrationBuilder.DropColumn(
                name: "OutsourceSpec",
                table: "SectionOutsource");
        }
    }
}
