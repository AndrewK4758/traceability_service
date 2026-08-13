using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraceabilityService.Data.Migrations
{
    /// <inheritdoc />
    public partial class EFCore10Update : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TraceEvents_TenantId_AppId_CorrelationId",
                schema: "traceability",
                table: "TraceEvents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_JobStates",
                schema: "traceability",
                table: "JobStates");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "traceability",
                table: "TraceEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AppId",
                schema: "traceability",
                table: "TraceEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "JobId",
                schema: "traceability",
                table: "JobStates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AppId",
                schema: "traceability",
                table: "JobStates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "traceability",
                table: "JobStates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                schema: "traceability",
                table: "JobStates",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_JobStates",
                schema: "traceability",
                table: "JobStates",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_CorrelationId",
                schema: "traceability",
                table: "TraceEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_JobStates_TenantId_AppId_JobId",
                schema: "traceability",
                table: "JobStates",
                columns: new[] { "TenantId", "AppId", "JobId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TraceEvents_CorrelationId",
                schema: "traceability",
                table: "TraceEvents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_JobStates",
                schema: "traceability",
                table: "JobStates");

            migrationBuilder.DropIndex(
                name: "IX_JobStates_TenantId_AppId_JobId",
                schema: "traceability",
                table: "JobStates");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "traceability",
                table: "JobStates");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "traceability",
                table: "TraceEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "AppId",
                schema: "traceability",
                table: "TraceEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "traceability",
                table: "JobStates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "JobId",
                schema: "traceability",
                table: "JobStates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "AppId",
                schema: "traceability",
                table: "JobStates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AddPrimaryKey(
                name: "PK_JobStates",
                schema: "traceability",
                table: "JobStates",
                columns: new[] { "TenantId", "AppId", "JobId" });

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_TenantId_AppId_CorrelationId",
                schema: "traceability",
                table: "TraceEvents",
                columns: new[] { "TenantId", "AppId", "CorrelationId" });
        }
    }
}
