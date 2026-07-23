using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraceabilityService.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "traceability");

            migrationBuilder.CreateTable(
                name: "JobStates",
                schema: "traceability",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    AppId = table.Column<string>(type: "text", nullable: false),
                    JobId = table.Column<string>(type: "text", nullable: false),
                    OriginalRequestBytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobStates", x => new { x.TenantId, x.AppId, x.JobId });
                });

            migrationBuilder.CreateTable(
                name: "TraceEvents",
                schema: "traceability",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "text", nullable: false),
                    JobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ChunkId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AppId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraceEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobStates_Status",
                schema: "traceability",
                table: "JobStates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_TenantId_AppId_CorrelationId",
                schema: "traceability",
                table: "TraceEvents",
                columns: new[] { "TenantId", "AppId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_TenantId_AppId_JobId",
                schema: "traceability",
                table: "TraceEvents",
                columns: new[] { "TenantId", "AppId", "JobId" });

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_Timestamp",
                schema: "traceability",
                table: "TraceEvents",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobStates",
                schema: "traceability");

            migrationBuilder.DropTable(
                name: "TraceEvents",
                schema: "traceability");
        }
    }
}
