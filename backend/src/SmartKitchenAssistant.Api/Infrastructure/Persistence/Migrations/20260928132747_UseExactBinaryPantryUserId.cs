using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartKitchenAssistant.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseExactBinaryPantryUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPantryItems_UserId_IngredientId",
                schema: "pantry",
                table: "UserPantryItems");

            migrationBuilder.AddColumn<byte[]>(
                name: "UserIdBinary",
                schema: "pantry",
                table: "UserPantryItems",
                type: "varbinary(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [pantry].[UserPantryItems]
                SET [UserIdBinary] = CONVERT(varbinary(512), [UserId]);

                IF EXISTS
                (
                    SELECT 1
                    FROM [pantry].[UserPantryItems]
                    WHERE [UserIdBinary] IS NULL
                       OR DATALENGTH([UserIdBinary]) <> DATALENGTH([UserId])
                )
                BEGIN
                    THROW 50001, 'UserId UTF-16LE conversion failed.', 1;
                END;
                """);

            migrationBuilder.AlterColumn<byte[]>(
                name: "UserIdBinary",
                schema: "pantry",
                table: "UserPantryItems",
                type: "varbinary(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "pantry",
                table: "UserPantryItems");

            migrationBuilder.RenameColumn(
                name: "UserIdBinary",
                schema: "pantry",
                table: "UserPantryItems",
                newName: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPantryItems_UserId_IngredientId",
                schema: "pantry",
                table: "UserPantryItems",
                columns: new[] { "UserId", "IngredientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPantryItems_UserId_IngredientId",
                schema: "pantry",
                table: "UserPantryItems");

            migrationBuilder.AddColumn<string>(
                name: "UserIdText",
                schema: "pantry",
                table: "UserPantryItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT 1
                    FROM [pantry].[UserPantryItems]
                    WHERE DATALENGTH([UserId]) % 2 <> 0
                       OR DATALENGTH([UserId]) > 512
                )
                BEGIN
                    THROW 50002, 'UserId is not a valid UTF-16LE value.', 1;
                END;

                UPDATE [pantry].[UserPantryItems]
                SET [UserIdText] = CONVERT(nvarchar(256), [UserId]);

                IF EXISTS
                (
                    SELECT 1
                    FROM [pantry].[UserPantryItems]
                    WHERE [UserIdText] IS NULL
                       OR DATALENGTH([UserIdText]) <> DATALENGTH([UserId])
                       OR CONVERT(varbinary(512), [UserIdText]) <> [UserId]
                )
                BEGIN
                    THROW 50003, 'UserId UTF-16LE reverse conversion failed.', 1;
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "UserIdText",
                schema: "pantry",
                table: "UserPantryItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "pantry",
                table: "UserPantryItems");

            migrationBuilder.RenameColumn(
                name: "UserIdText",
                schema: "pantry",
                table: "UserPantryItems",
                newName: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPantryItems_UserId_IngredientId",
                schema: "pantry",
                table: "UserPantryItems",
                columns: new[] { "UserId", "IngredientId" },
                unique: true);
        }
    }
}
