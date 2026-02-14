using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserRoles.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "TaskItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "TaskItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedByUserId",
                table: "TaskItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "TaskItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PreviousColumnId",
                table: "TaskItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "TaskItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewStatus",
                table: "TaskItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "TaskItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "TaskItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_CompletedByUserId",
                table: "TaskItems",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_PreviousColumnId",
                table: "TaskItems",
                column: "PreviousColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ReviewedByUserId",
                table: "TaskItems",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_TeamName_IsArchived",
                table: "TaskItems",
                columns: new[] { "TeamName", "IsArchived" });

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_AspNetUsers_CompletedByUserId",
                table: "TaskItems",
                column: "CompletedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_AspNetUsers_ReviewedByUserId",
                table: "TaskItems",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_TeamColumns_PreviousColumnId",
                table: "TaskItems",
                column: "PreviousColumnId",
                principalTable: "TeamColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_AspNetUsers_CompletedByUserId",
                table: "TaskItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_AspNetUsers_ReviewedByUserId",
                table: "TaskItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_TeamColumns_PreviousColumnId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_CompletedByUserId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_PreviousColumnId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ReviewedByUserId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_TeamName_IsArchived",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "PreviousColumnId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "TaskItems");
        }
    }
}
