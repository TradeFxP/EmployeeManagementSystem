Build started...
Build succeeded.
START TRANSACTION;
ALTER TABLE "ColumnPermissions" ADD "CanClearTasks" boolean NOT NULL DEFAULT FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260228070913_AddCanClearTasksOnly', '10.0.1');

COMMIT;

START TRANSACTION;
ALTER TABLE "AspNetUsers" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260301161311_FixMissingProperties', '10.0.1');

COMMIT;


