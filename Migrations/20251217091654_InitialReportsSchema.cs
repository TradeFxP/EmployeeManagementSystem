using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserRoles.Migrations
{
    /// <inheritdoc />
    public partial class InitialReportsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /* migrationBuilder.DropForeignKey(
                name: "FK_DailyReports_AspNetUsers_ApplicationUserId",
                table: "DailyReports"); */

            migrationBuilder.RenameColumn(
                name: "AdminComment",
                table: "DailyReports",
                newName: "ReviewerComment");

            migrationBuilder.AlterColumn<string>(
                name: "Task",
                table: "DailyReports",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReviewerComment",
                table: "DailyReports",
                newName: "AdminComment");

            migrationBuilder.AlterColumn<string>(
                name: "Task",
                table: "DailyReports",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            /* migrationBuilder.AddForeignKey(
                name: "FK_DailyReports_AspNetUsers_ApplicationUserId",
                table: "DailyReports",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade); */
        }
    }
}
