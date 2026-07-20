using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraceabilityService.Migrations
{
    /// <inheritdoc />
    public partial class FirstClassIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_JobStates",
                table: "JobStates");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "JobStates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AppId",
                table: "JobStates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_JobStates",
                table: "JobStates",
                columns: new[] { "TenantId", "AppId", "JobId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_JobStates",
                table: "JobStates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "JobStates");

            migrationBuilder.DropColumn(
                name: "AppId",
                table: "JobStates");

            migrationBuilder.AddPrimaryKey(
                name: "PK_JobStates",
                table: "JobStates",
                column: "JobId");
        }
    }
}
