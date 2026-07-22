using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraceabilityService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobStates",
                columns: table => new
                {
                    JobId = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalRequestBytes = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobStates", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "TraceEvents",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "TEXT", nullable: false),
                    JobId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ChunkId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AppId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraceEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobStates_Status",
                table: "JobStates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_CorrelationId",
                table: "TraceEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_JobId",
                table: "TraceEvents",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_Timestamp",
                table: "TraceEvents",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobStates");

            migrationBuilder.DropTable(
                name: "TraceEvents");
        }
    }
}
