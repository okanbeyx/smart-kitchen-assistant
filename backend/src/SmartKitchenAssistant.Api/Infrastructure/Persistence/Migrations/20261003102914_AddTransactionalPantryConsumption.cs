using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartKitchenAssistant.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionalPantryConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockConsumptions",
                schema: "pantry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<byte[]>(type: "varbinary(512)", maxLength: 512, nullable: false),
                    IdempotencyKey = table.Column<byte[]>(type: "varbinary(128)", maxLength: 128, nullable: false),
                    RecipeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockConsumptions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "recipes",
                        principalTable: "Recipes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockConsumptionItems",
                schema: "pantry",
                columns: table => new
                {
                    StockConsumptionId = table.Column<long>(type: "bigint", nullable: false),
                    IngredientId = table.Column<long>(type: "bigint", nullable: false),
                    ConsumedNormalizedQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    QuantityDimension = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockConsumptionItems", x => new { x.StockConsumptionId, x.IngredientId });
                    table.CheckConstraint("CK_StockConsumptionItems_ConsumedNormalizedQuantity_Positive", "[ConsumedNormalizedQuantity] > 0");
                    table.CheckConstraint("CK_StockConsumptionItems_QuantityDimension", "[QuantityDimension] IN (N'Mass', N'Volume', N'Count')");
                    table.ForeignKey(
                        name: "FK_StockConsumptionItems_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalSchema: "catalog",
                        principalTable: "Ingredients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockConsumptionItems_StockConsumptions_StockConsumptionId",
                        column: x => x.StockConsumptionId,
                        principalSchema: "pantry",
                        principalTable: "StockConsumptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockConsumptionItems_IngredientId",
                schema: "pantry",
                table: "StockConsumptionItems",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StockConsumptions_RecipeId",
                schema: "pantry",
                table: "StockConsumptions",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_StockConsumptions_UserId_IdempotencyKey",
                schema: "pantry",
                table: "StockConsumptions",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockConsumptionItems",
                schema: "pantry");

            migrationBuilder.DropTable(
                name: "StockConsumptions",
                schema: "pantry");
        }
    }
}
