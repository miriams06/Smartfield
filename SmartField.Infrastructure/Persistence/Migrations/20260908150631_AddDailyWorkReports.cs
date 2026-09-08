using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartField.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyWorkReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyWorkReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ClockOutAttendanceEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyWorkReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyWorkReports_AttendanceEvents_ClockOutAttendanceEventId",
                        column: x => x.ClockOutAttendanceEventId,
                        principalTable: "AttendanceEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyWorkReports_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyWorkReports_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyWorkReports_ClockOutAttendanceEventId",
                table: "DailyWorkReports",
                column: "ClockOutAttendanceEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyWorkReports_CompanyId_EmployeeId_WorkDate",
                table: "DailyWorkReports",
                columns: new[] { "CompanyId", "EmployeeId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyWorkReports_EmployeeId",
                table: "DailyWorkReports",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyWorkReports");
        }
    }
}
