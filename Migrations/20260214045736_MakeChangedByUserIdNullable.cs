using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserRoles.Migrations
{
    /// <inheritdoc />
    public partial class MakeChangedByUserIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old constraint that restricts deletion
            migrationBuilder.DropForeignKey(
                name: "FK_TaskHistories_AspNetUsers_ChangedByUserId",
                table: "TaskHistories");

            migrationBuilder.AlterColumn<string>(
                name: "ChangedByUserId",
                table: "TaskHistories",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            // Add new constraint with SetNull
            migrationBuilder.AddForeignKey(
                name: "FK_TaskHistories_AspNetUsers_ChangedByUserId",
                table: "TaskHistories",
                column: "ChangedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ChangedByUserId",
                table: "TaskHistories",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
