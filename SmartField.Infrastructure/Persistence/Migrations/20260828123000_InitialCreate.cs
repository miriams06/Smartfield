using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartField.Infrastructure.Persistence.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Companies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Nif = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Companies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_AuditLogs_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CompanySettings",
            columns: table => new
            {
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequireGeolocation = table.Column<bool>(type: "bit", nullable: false),
                GeofenceMode = table.Column<int>(type: "int", nullable: false),
                AllowBreaks = table.Column<bool>(type: "bit", nullable: false),
                AllowProjectSelection = table.Column<bool>(type: "bit", nullable: false),
                RequireProjectSelection = table.Column<bool>(type: "bit", nullable: false),
                DefaultGeofenceRadiusMeters = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CompanySettings", x => x.CompanyId);
                table.ForeignKey(
                    name: "FK_CompanySettings_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ExternalReferences",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SystemName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LocalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExternalEntityId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ExternalCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExternalReferences", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExternalReferences_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "IntegrationOutbox",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                LastAttemptUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntegrationOutbox", x => x.Id);
                table.ForeignKey(
                    name: "FK_IntegrationOutbox_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WorkSites",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                GeofenceRadiusMeters = table.Column<int>(type: "int", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                ExternalSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ErpCostCenterCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkSites", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkSites_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Employees",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                MobilePhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                DefaultWorkSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExternalSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ErpEmployeeCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Employees", x => x.Id);
                table.ForeignKey(
                    name: "FK_Employees_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Employees_WorkSites_DefaultWorkSiteId",
                    column: x => x.DefaultWorkSiteId,
                    principalTable: "WorkSites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ProjectType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Other"),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                WorkSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                ExternalSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ErpProjectCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ErpCostCenterCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
                table.ForeignKey(
                    name: "FK_Projects_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Projects_WorkSites_WorkSiteId",
                    column: x => x.WorkSiteId,
                    principalTable: "WorkSites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AttendanceEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ServerTimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ClientTimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                LocationAccuracyMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                WorkSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsInsideGeofence = table.Column<bool>(type: "bit", nullable: true),
                DistanceFromWorkSiteMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ClientEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_AttendanceEvents_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceEvents_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceEvents_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceEvents_WorkSites_WorkSiteId",
                    column: x => x.WorkSiteId,
                    principalTable: "WorkSites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AttendanceCorrections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttendanceEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginalTimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CorrectedTimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                OriginalEventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                CorrectedEventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                CorrectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceCorrections", x => x.Id);
                table.ForeignKey(
                    name: "FK_AttendanceCorrections_AttendanceEvents_AttendanceEventId",
                    column: x => x.AttendanceEventId,
                    principalTable: "AttendanceEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceCorrections_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.InsertData(
            table: "Companies",
            columns: new[] { "Id", "Code", "CreatedAtUtc", "IsActive", "Name", "Nif", "TimeZone", "UpdatedAtUtc" },
            values: new object[]
            {
                Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68"),
                "SYS-DEMO",
                new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
                true,
                "SmartField Demo",
                string.Empty,
                "Europe/Lisbon",
                null
            });

        migrationBuilder.InsertData(
            table: "CompanySettings",
            columns: new[]
            {
                "CompanyId", "AllowBreaks", "AllowProjectSelection", "CreatedAtUtc",
                "DefaultGeofenceRadiusMeters", "GeofenceMode", "RequireGeolocation",
                "RequireProjectSelection", "UpdatedAtUtc"
            },
            values: new object[]
            {
                Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68"),
                true,
                false,
                new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
                100,
                0,
                false,
                false,
                null
            });

        migrationBuilder.InsertData(
            table: "Employees",
            columns: new[]
            {
                "Id", "CompanyId", "CreatedAtUtc", "DefaultWorkSiteId", "Email",
                "EmployeeNumber", "ErpEmployeeCode", "ExternalId", "ExternalSystem",
                "IsActive", "MobilePhone", "Name", "UpdatedAtUtc"
            },
            values: new object[]
            {
                Guid.Parse("49f8a4ab-9802-46a4-99d7-2bcd6a664ad8"),
                Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68"),
                new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
                null,
                null,
                "FUNC001",
                null,
                null,
                null,
                true,
                null,
                "Funcionário Demo",
                null
            });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceCorrections_AttendanceEventId",
            table: "AttendanceCorrections",
            column: "AttendanceEventId");

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceCorrections_CompanyId",
            table: "AttendanceCorrections",
            column: "CompanyId");

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceEvents_ClientEventId",
            table: "AttendanceEvents",
            column: "ClientEventId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceEvents_CompanyId_EmployeeId_ServerTimestampUtc",
            table: "AttendanceEvents",
            columns: new[] { "CompanyId", "EmployeeId", "ServerTimestampUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceEvents_EmployeeId",
            table: "AttendanceEvents",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceEvents_ProjectId",
            table: "AttendanceEvents",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceEvents_WorkSiteId",
            table: "AttendanceEvents",
            column: "WorkSiteId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_CompanyId_EntityType_EntityId",
            table: "AuditLogs",
            columns: new[] { "CompanyId", "EntityType", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_Companies_Code",
            table: "Companies",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Employees_CompanyId_EmployeeNumber",
            table: "Employees",
            columns: new[] { "CompanyId", "EmployeeNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Employees_DefaultWorkSiteId",
            table: "Employees",
            column: "DefaultWorkSiteId");

        migrationBuilder.CreateIndex(
            name: "IX_ExternalReferences_CompanyId_SystemName_EntityType_LocalEntityId",
            table: "ExternalReferences",
            columns: new[] { "CompanyId", "SystemName", "EntityType", "LocalEntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationOutbox_CompanyId_Status_CreatedAtUtc",
            table: "IntegrationOutbox",
            columns: new[] { "CompanyId", "Status", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Projects_CompanyId_Code",
            table: "Projects",
            columns: new[] { "CompanyId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Projects_WorkSiteId",
            table: "Projects",
            column: "WorkSiteId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkSites_CompanyId_Code",
            table: "WorkSites",
            columns: new[] { "CompanyId", "Code" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AttendanceCorrections");
        migrationBuilder.DropTable(name: "AuditLogs");
        migrationBuilder.DropTable(name: "CompanySettings");
        migrationBuilder.DropTable(name: "ExternalReferences");
        migrationBuilder.DropTable(name: "IntegrationOutbox");
        migrationBuilder.DropTable(name: "AttendanceEvents");
        migrationBuilder.DropTable(name: "Employees");
        migrationBuilder.DropTable(name: "Projects");
        migrationBuilder.DropTable(name: "WorkSites");
        migrationBuilder.DropTable(name: "Companies");
    }
}
