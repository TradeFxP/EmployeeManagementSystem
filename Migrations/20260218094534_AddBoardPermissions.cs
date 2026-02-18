using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UserRoles.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoardPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TeamName = table.Column<string>(type: "text", nullable: false),
                    CanAddColumn = table.Column<bool>(type: "boolean", nullable: false),
                    CanRenameColumn = table.Column<bool>(type: "boolean", nullable: false),
                    CanReorderColumns = table.Column<bool>(type: "boolean", nullable: false),
                    CanDeleteColumn = table.Column<bool>(type: "boolean", nullable: false),
                    CanEditAllFields = table.Column<bool>(type: "boolean", nullable: false),
                    CanDeleteTask = table.Column<bool>(type: "boolean", nullable: false),
                    CanReviewTask = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardPermissions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardPermissions_UserId_TeamName",
                table: "BoardPermissions",
                columns: new[] { "UserId", "TeamName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardPermissions");
        }
    }
}
