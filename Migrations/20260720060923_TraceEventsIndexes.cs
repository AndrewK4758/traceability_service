using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraceabilityService.Migrations
{
    /// <inheritdoc />
    public partial class TraceEventsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TraceEvents_CorrelationId",
                table: "TraceEvents");

            migrationBuilder.DropIndex(
                name: "IX_TraceEvents_JobId",
                table: "TraceEvents");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_TenantId_AppId_CorrelationId",
                table: "TraceEvents",
                columns: new[] { "TenantId", "AppId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_TenantId_AppId_JobId",
                table: "TraceEvents",
                columns: new[] { "TenantId", "AppId", "JobId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TraceEvents_TenantId_AppId_CorrelationId",
                table: "TraceEvents");

            migrationBuilder.DropIndex(
                name: "IX_TraceEvents_TenantId_AppId_JobId",
                table: "TraceEvents");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_CorrelationId",
                table: "TraceEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_JobId",
                table: "TraceEvents",
                column: "JobId");
        }
    }
}
