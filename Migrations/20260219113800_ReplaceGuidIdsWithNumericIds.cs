using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserRoles.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceGuidIdsWithNumericIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    CONSTRAINT_REC RECORD;
BEGIN
    -- 1. Create mapping
    CREATE TEMP TABLE id_mapping AS
    SELECT ""Id"" as old_id, ""NumericId""::TEXT as new_id
    FROM ""AspNetUsers"";

    -- 2. Drop all Foreign Keys pointing to AspNetUsers
    FOR CONSTRAINT_REC IN 
        SELECT 
            conname, 
            conrelid::regclass::text as tablename
        FROM pg_constraint 
        WHERE confrelid = '""AspNetUsers""'::regclass
    LOOP
        EXECUTE 'ALTER TABLE ' || CONSTRAINT_REC.tablename || ' DROP CONSTRAINT ""' || CONSTRAINT_REC.conname || '""';
    END LOOP;

    -- 3. Update AspNetUsers itself (including self-references)
    UPDATE ""AspNetUsers"" u SET ""Id"" = m.new_id FROM id_mapping m WHERE u.""Id"" = m.old_id;
    UPDATE ""AspNetUsers"" u SET ""ParentUserId"" = m.new_id FROM id_mapping m WHERE u.""ParentUserId"" = m.old_id;
    UPDATE ""AspNetUsers"" u SET ""ManagerId"" = m.new_id FROM id_mapping m WHERE u.""ManagerId"" = m.old_id;

    -- 4. Update Identity Tables
    UPDATE ""AspNetUserRoles"" t SET ""UserId"" = m.new_id FROM id_mapping m WHERE t.""UserId"" = m.old_id;
    UPDATE ""AspNetUserClaims"" t SET ""UserId"" = m.new_id FROM id_mapping m WHERE t.""UserId"" = m.old_id;
    UPDATE ""AspNetUserLogins"" t SET ""UserId"" = m.new_id FROM id_mapping m WHERE t.""UserId"" = m.old_id;
    UPDATE ""AspNetUserTokens"" t SET ""UserId"" = m.new_id FROM id_mapping m WHERE t.""UserId"" = m.old_id;

    -- 5. Update Application Tables
    UPDATE ""AssignedTasks"" t SET ""AssignedById"" = m.new_id FROM id_mapping m WHERE t.""AssignedById"" = m.old_id;
    UPDATE ""AssignedTasks"" t SET ""AssignedToId"" = m.new_id FROM id_mapping m WHERE t.""AssignedToId"" = m.old_id;
    
    UPDATE ""BoardPermissions"" t SET ""UserId"" = m.new_id FROM id_mapping m WHERE t.""UserId"" = m.old_id;
    
    UPDATE ""DailyReports"" t SET ""ApplicationUserId"" = m.new_id FROM id_mapping m WHERE t.""ApplicationUserId"" = m.old_id;
    
    UPDATE ""EmailLogs"" t SET ""SentByUserId"" = m.new_id FROM id_mapping m WHERE t.""SentByUserId"" = m.old_id;
    
    UPDATE ""Epics"" t SET ""CreatedByUserId"" = m.new_id FROM id_mapping m WHERE t.""CreatedByUserId"" = m.old_id;
    UPDATE ""Features"" t SET ""CreatedByUserId"" = m.new_id FROM id_mapping m WHERE t.""CreatedByUserId"" = m.old_id;
    UPDATE ""Projects"" t SET ""CreatedByUserId"" = m.new_id FROM id_mapping m WHERE t.""CreatedByUserId"" = m.old_id;
    UPDATE ""Stories"" t SET ""CreatedByUserId"" = m.new_id FROM id_mapping m WHERE t.""CreatedByUserId"" = m.old_id;
    
    UPDATE ""TaskCustomFields"" t SET ""CreatedByUserId"" = m.new_id FROM id_mapping m WHERE t.""CreatedByUserId"" = m.old_id;
    
    UPDATE ""TaskHistories"" t SET ""ChangedByUserId"" = m.new_id FROM id_mapping m WHERE t.""ChangedByUserId"" = m.old_id;
    
    UPDATE ""TaskItems"" t SET ""AssignedToUserId"" = m.new_id FROM id_mapping m WHERE t.""AssignedToUserId"" = m.old_id;
    UPDATE ""TaskItems"" t SET ""AssignedByUserId"" = m.new_id FROM id_mapping m WHERE t.""AssignedByUserId"" = m.old_id;
    UPDATE ""TaskItems"" t SET ""CreatedByUserId"" = m.new_id FROM id_mapping m WHERE t.""CreatedByUserId"" = m.old_id;
    UPDATE ""TaskItems"" t SET ""ReviewedByUserId"" = m.new_id FROM id_mapping m WHERE t.""ReviewedByUserId"" = m.old_id;
    UPDATE ""TaskItems"" t SET ""CompletedByUserId"" = m.new_id FROM id_mapping m WHERE t.""CompletedByUserId"" = m.old_id;
    
    UPDATE ""UserTeams"" t SET ""UserId"" = m.new_id FROM id_mapping m WHERE t.""UserId"" = m.old_id;

    -- 5.5 Cleanup Orphans (Records pointing to non-existent users)
    -- This prevents FK violations when re-adding constraints if old data was inconsistent
    DELETE FROM ""AspNetUserRoles"" WHERE ""UserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    DELETE FROM ""AspNetUserClaims"" WHERE ""UserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    DELETE FROM ""AspNetUserLogins"" WHERE ""UserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    DELETE FROM ""AspNetUserTokens"" WHERE ""UserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");

    DELETE FROM ""AssignedTasks"" WHERE ""AssignedById"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    DELETE FROM ""AssignedTasks"" WHERE ""AssignedToId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    
    DELETE FROM ""BoardPermissions"" WHERE ""UserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    
    DELETE FROM ""DailyReports"" WHERE ""ApplicationUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    
    UPDATE ""EmailLogs"" SET ""SentByUserId"" = NULL WHERE ""SentByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    
    UPDATE ""Epics"" SET ""CreatedByUserId"" = NULL WHERE ""CreatedByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    UPDATE ""Features"" SET ""CreatedByUserId"" = NULL WHERE ""CreatedByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    UPDATE ""Projects"" SET ""CreatedByUserId"" = NULL WHERE ""CreatedByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"") OR ""CreatedByUserId"" IS NULL; -- Projects usually required, but we'll try to be safe
    UPDATE ""Stories"" SET ""CreatedByUserId"" = NULL WHERE ""CreatedByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    
    UPDATE ""TaskCustomFields"" SET ""CreatedByUserId"" = NULL WHERE ""CreatedByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    
    DELETE FROM ""TaskHistories"" WHERE ""ChangedByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"") AND ""ChangedByUserId"" IS NOT NULL;
    
    DELETE FROM ""TaskItems"" WHERE ""AssignedToUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    UPDATE ""TaskItems"" SET ""AssignedByUserId"" = NULL WHERE ""AssignedByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    DELETE FROM ""TaskItems"" WHERE ""CreatedByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    UPDATE ""TaskItems"" SET ""ReviewedByUserId"" = NULL WHERE ""ReviewedByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    UPDATE ""TaskItems"" SET ""CompletedByUserId"" = NULL WHERE ""CompletedByUserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");
    
    DELETE FROM ""UserTeams"" WHERE ""UserId"" NOT IN (SELECT ""Id"" FROM ""AspNetUsers"");

    -- 6. Re-add Foreign Keys (Standard EF Naming)
    ALTER TABLE ""AspNetUsers"" ADD CONSTRAINT ""FK_AspNetUsers_AspNetUsers_ParentUserId"" FOREIGN KEY (""ParentUserId"") REFERENCES ""AspNetUsers""(""Id"");
    ALTER TABLE ""AspNetUsers"" ADD CONSTRAINT ""FK_AspNetUsers_AspNetUsers_ManagerId"" FOREIGN KEY (""ManagerId"") REFERENCES ""AspNetUsers""(""Id"");
    
    ALTER TABLE ""AspNetUserRoles"" ADD CONSTRAINT ""FK_AspNetUserRoles_AspNetUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE;
    ALTER TABLE ""AspNetUserClaims"" ADD CONSTRAINT ""FK_AspNetUserClaims_AspNetUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE;
    ALTER TABLE ""AspNetUserLogins"" ADD CONSTRAINT ""FK_AspNetUserLogins_AspNetUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE;
    ALTER TABLE ""AspNetUserTokens"" ADD CONSTRAINT ""FK_AspNetUserTokens_AspNetUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE;

    ALTER TABLE ""AssignedTasks"" ADD CONSTRAINT ""FK_AssignedTasks_AspNetUsers_AssignedById"" FOREIGN KEY (""AssignedById"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE RESTRICT;
    ALTER TABLE ""AssignedTasks"" ADD CONSTRAINT ""FK_AssignedTasks_AspNetUsers_AssignedToId"" FOREIGN KEY (""AssignedToId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE;
    
    ALTER TABLE ""BoardPermissions"" ADD CONSTRAINT ""FK_BoardPermissions_AspNetUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE;
    
    ALTER TABLE ""DailyReports"" ADD CONSTRAINT ""FK_DailyReports_AspNetUsers_ApplicationUserId"" FOREIGN KEY (""ApplicationUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE;
    
    ALTER TABLE ""EmailLogs"" ADD CONSTRAINT ""FK_EmailLogs_AspNetUsers_SentByUserId"" FOREIGN KEY (""SentByUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE SET NULL;
    
    ALTER TABLE ""Epics"" ADD CONSTRAINT ""FK_Epics_AspNetUsers_CreatedByUserId"" FOREIGN KEY (""CreatedByUserId"") REFERENCES ""AspNetUsers""(""Id"");
    ALTER TABLE ""Features"" ADD CONSTRAINT ""FK_Features_AspNetUsers_CreatedByUserId"" FOREIGN KEY (""CreatedByUserId"") REFERENCES ""AspNetUsers""(""Id"");
    ALTER TABLE ""Projects"" ADD CONSTRAINT ""FK_Projects_AspNetUsers_CreatedByUserId"" FOREIGN KEY (""CreatedByUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE;
    ALTER TABLE ""Stories"" ADD CONSTRAINT ""FK_Stories_AspNetUsers_CreatedByUserId"" FOREIGN KEY (""CreatedByUserId"") REFERENCES ""AspNetUsers""(""Id"");
    
    ALTER TABLE ""TaskCustomFields"" ADD CONSTRAINT ""FK_TaskCustomFields_AspNetUsers_CreatedByUserId"" FOREIGN KEY (""CreatedByUserId"") REFERENCES ""AspNetUsers""(""Id"");
    
    ALTER TABLE ""TaskHistories"" ADD CONSTRAINT ""FK_TaskHistories_AspNetUsers_ChangedByUserId"" FOREIGN KEY (""ChangedByUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE RESTRICT;
    
    ALTER TABLE ""TaskItems"" ADD CONSTRAINT ""FK_TaskItems_AspNetUsers_AssignedToUserId"" FOREIGN KEY (""AssignedToUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE;
    ALTER TABLE ""TaskItems"" ADD CONSTRAINT ""FK_TaskItems_AspNetUsers_AssignedByUserId"" FOREIGN KEY (""AssignedByUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE RESTRICT;
    ALTER TABLE ""TaskItems"" ADD CONSTRAINT ""FK_TaskItems_AspNetUsers_CreatedByUserId"" FOREIGN KEY (""CreatedByUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE RESTRICT;
    ALTER TABLE ""TaskItems"" ADD CONSTRAINT ""FK_TaskItems_AspNetUsers_ReviewedByUserId"" FOREIGN KEY (""ReviewedByUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE RESTRICT;
    ALTER TABLE ""TaskItems"" ADD CONSTRAINT ""FK_TaskItems_AspNetUsers_CompletedByUserId"" FOREIGN KEY (""CompletedByUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE RESTRICT;
    
    ALTER TABLE ""UserTeams"" ADD CONSTRAINT ""FK_UserTeams_AspNetUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE;

END $$;
");
            migrationBuilder.DropForeignKey(
                name: "FK_AssignedTasks_AspNetUsers_AssignedById",
                table: "AssignedTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_AspNetUsers_AssignedByUserId",
                table: "TaskItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_AspNetUsers_CreatedByUserId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NumericId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NumericId",
                table: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignedTasks_AspNetUsers_AssignedById",
                table: "AssignedTasks",
                column: "AssignedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_AspNetUsers_AssignedByUserId",
                table: "TaskItems",
                column: "AssignedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_AspNetUsers_CreatedByUserId",
                table: "TaskItems",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssignedTasks_AspNetUsers_AssignedById",
                table: "AssignedTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_AspNetUsers_AssignedByUserId",
                table: "TaskItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_AspNetUsers_CreatedByUserId",
                table: "TaskItems");

            migrationBuilder.AddColumn<int>(
                name: "NumericId",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NumericId",
                table: "AspNetUsers",
                column: "NumericId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignedTasks_AspNetUsers_AssignedById",
                table: "AssignedTasks",
                column: "AssignedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_AspNetUsers_AssignedByUserId",
                table: "TaskItems",
                column: "AssignedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_AspNetUsers_CreatedByUserId",
                table: "TaskItems",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
