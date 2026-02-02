using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UserRoles.Migrations
{
    /// <inheritdoc />
    public partial class InitialTeamColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ColumnId",
                table: "TaskItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TeamName",
                table: "TaskItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TeamColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamName = table.Column<string>(type: "text", nullable: false),
                    ColumnName = table.Column<string>(type: "text", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamColumns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ColumnId",
                table: "TaskItems",
                column: "ColumnId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_TeamColumns_ColumnId",
                table: "TaskItems",
                column: "ColumnId",
                principalTable: "TeamColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_TeamColumns_ColumnId",
                table: "TaskItems");

            migrationBuilder.DropTable(
                name: "TeamColumns");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ColumnId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "ColumnId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "TeamName",
                table: "TaskItems");
        }
    }
}
