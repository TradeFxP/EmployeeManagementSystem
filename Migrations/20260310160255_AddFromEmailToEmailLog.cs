using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserRoles.Migrations
{
    /// <inheritdoc />
    public partial class AddFromEmailToEmailLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FromEmail",
                table: "EmailLogs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaskId",
                table: "EmailLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_TaskId",
                table: "EmailLogs",
                column: "TaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailLogs_TaskItems_TaskId",
                table: "EmailLogs",
                column: "TaskId",
                principalTable: "TaskItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailLogs_TaskItems_TaskId",
                table: "EmailLogs");

            migrationBuilder.DropIndex(
                name: "IX_EmailLogs_TaskId",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "FromEmail",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "EmailLogs");
        }
    }
}
