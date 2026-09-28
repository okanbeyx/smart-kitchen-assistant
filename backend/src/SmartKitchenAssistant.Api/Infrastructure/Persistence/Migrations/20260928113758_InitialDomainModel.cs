using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartKitchenAssistant.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "recipes");

            migrationBuilder.EnsureSchema(
                name: "pantry");

            migrationBuilder.CreateTable(
                name: "Ingredients",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    QuantityDimension = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.CheckConstraint("CK_Ingredients_QuantityDimension", "[QuantityDimension] IN (N'Mass', N'Volume', N'Count')");
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                schema: "recipes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseServings = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.CheckConstraint("CK_Recipes_BaseServings_Positive", "[BaseServings] > 0");
                    table.CheckConstraint("CK_Recipes_Status", "[Status] IN (N'Draft', N'Published')");
                });

            migrationBuilder.CreateTable(
                name: "UserPantryItems",
                schema: "pantry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IngredientId = table.Column<long>(type: "bigint", nullable: false),
                    NormalizedQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    DisplayUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPantryItems", x => x.Id);
                    table.CheckConstraint("CK_UserPantryItems_DisplayUnit", "[DisplayUnit] IN (N'Gram', N'Kilogram', N'Milliliter', N'Liter', N'Each')");
                    table.CheckConstraint("CK_UserPantryItems_NormalizedQuantity_Positive", "[NormalizedQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_UserPantryItems_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalSchema: "catalog",
                        principalTable: "Ingredients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                schema: "recipes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<long>(type: "bigint", nullable: false),
                    IngredientId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    NormalizedQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    DisplayUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.Id);
                    table.CheckConstraint("CK_RecipeIngredients_DisplayUnit", "[DisplayUnit] IS NULL OR [DisplayUnit] IN (N'Gram', N'Kilogram', N'Milliliter', N'Liter', N'Each')");
                    table.CheckConstraint("CK_RecipeIngredients_NormalizedQuantity_Positive", "[NormalizedQuantity] IS NULL OR [NormalizedQuantity] > 0");
                    table.CheckConstraint("CK_RecipeIngredients_QuantityAndUnit_Paired", "([NormalizedQuantity] IS NULL AND [DisplayUnit] IS NULL) OR ([NormalizedQuantity] IS NOT NULL AND [DisplayUnit] IS NOT NULL)");
                    table.CheckConstraint("CK_RecipeIngredients_Sequence_Positive", "[Sequence] > 0");
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalSchema: "catalog",
                        principalTable: "Ingredients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "recipes",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSteps",
                schema: "recipes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Instruction = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TimerSeconds = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSteps", x => x.Id);
                    table.CheckConstraint("CK_RecipeSteps_Sequence_Positive", "[Sequence] > 0");
                    table.CheckConstraint("CK_RecipeSteps_TimerSeconds_Positive", "[TimerSeconds] IS NULL OR [TimerSeconds] > 0");
                    table.ForeignKey(
                        name: "FK_RecipeSteps_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "recipes",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_IngredientId",
                schema: "recipes",
                table: "RecipeIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeId_Sequence",
                schema: "recipes",
                table: "RecipeIngredients",
                columns: new[] { "RecipeId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSteps_RecipeId_Sequence",
                schema: "recipes",
                table: "RecipeSteps",
                columns: new[] { "RecipeId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPantryItems_IngredientId",
                schema: "pantry",
                table: "UserPantryItems",
                column: "IngredientId");

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
            migrationBuilder.DropTable(
                name: "RecipeIngredients",
                schema: "recipes");

            migrationBuilder.DropTable(
                name: "RecipeSteps",
                schema: "recipes");

            migrationBuilder.DropTable(
                name: "UserPantryItems",
                schema: "pantry");

            migrationBuilder.DropTable(
                name: "Recipes",
                schema: "recipes");

            migrationBuilder.DropTable(
                name: "Ingredients",
                schema: "catalog");
        }
    }
}
