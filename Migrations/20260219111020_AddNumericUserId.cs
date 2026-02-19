using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserRoles.Migrations
{
    /// <inheritdoc />
    public partial class AddNumericUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add the column with a temporary default of 0 (not unique yet)
            migrationBuilder.AddColumn<int>(
                name: "NumericId",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Step 2: Back-fill existing users
            // Admin user gets 100; all other users get sequential IDs from 101, ordered by creation date.
            // We detect the Admin by checking AspNetUserRoles + AspNetRoles.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    admin_role_id TEXT;
    counter INT := 100;
    rec RECORD;
BEGIN
    -- Find the Admin role id
    SELECT ""Id"" INTO admin_role_id
    FROM ""AspNetRoles""
    WHERE ""Name"" = 'Admin'
    LIMIT 1;

    -- Admin user first gets 100
    IF admin_role_id IS NOT NULL THEN
        UPDATE ""AspNetUsers"" u
        SET ""NumericId"" = 100
        FROM ""AspNetUserRoles"" ur
        WHERE ur.""UserId"" = u.""Id""
          AND ur.""RoleId"" = admin_role_id;
    END IF;

    -- All remaining users (non-admin) get 101, 102, … ordered to be deterministic
    FOR rec IN
        SELECT u.""Id""
        FROM ""AspNetUsers"" u
        WHERE u.""NumericId"" = 0
        ORDER BY u.""UserName"" ASC
    LOOP
        counter := counter + 1;
        UPDATE ""AspNetUsers""
        SET ""NumericId"" = counter
        WHERE ""Id"" = rec.""Id"";
    END LOOP;
END $$;
");

            // Step 3: Now that every row has a unique NumericId, add the unique index
            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NumericId",
                table: "AspNetUsers",
                column: "NumericId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NumericId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NumericId",
                table: "AspNetUsers");
        }
    }
}
