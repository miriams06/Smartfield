using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartField.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaximumLocationAccuracy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaximumLocationAccuracyMeters",
                table: "CompanySettings",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "CompanyId",
                keyValue: new Guid("9f0b4a28-864b-4d2f-9ca6-44cf64352d68"),
                column: "MaximumLocationAccuracyMeters",
                value: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximumLocationAccuracyMeters",
                table: "CompanySettings");
        }
    }
}
