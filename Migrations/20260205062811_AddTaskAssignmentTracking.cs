using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserRoles.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskAssignmentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "TaskItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedByUserId",
                table: "TaskItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_AssignedByUserId",
                table: "TaskItems",
                column: "AssignedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_AspNetUsers_AssignedByUserId",
                table: "TaskItems",
                column: "AssignedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_AspNetUsers_AssignedByUserId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_AssignedByUserId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "AssignedByUserId",
                table: "TaskItems");
        }
    }
}
