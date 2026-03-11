using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserRoles.Migrations
{
    /// <inheritdoc />
    public partial class PreserveCommunicationLogsOnTaskDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailLogs_TaskItems_TaskId",
                table: "EmailLogs");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailLogs_TaskItems_TaskId",
                table: "EmailLogs",
                column: "TaskId",
                principalTable: "TaskItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailLogs_TaskItems_TaskId",
                table: "EmailLogs");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailLogs_TaskItems_TaskId",
                table: "EmailLogs",
                column: "TaskId",
                principalTable: "TaskItems",
                principalColumn: "Id");
        }
    }
}
