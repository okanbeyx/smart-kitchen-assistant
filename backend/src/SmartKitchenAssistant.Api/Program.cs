using Microsoft.AspNetCore.Routing;
using SmartKitchenAssistant.Api.Features.Catalog.Api;
using SmartKitchenAssistant.Api.Features.Catalog.Application;
using SmartKitchenAssistant.Api.Features.Catalog.Infrastructure.Persistence;
using SmartKitchenAssistant.Api.Features.Pantry.Api;
using SmartKitchenAssistant.Api.Features.Pantry.Application;
using SmartKitchenAssistant.Api.Features.Pantry.Infrastructure.Persistence;
using SmartKitchenAssistant.Api.Features.Recipes.Api;
using SmartKitchenAssistant.Api.Features.Recipes.Application;
using SmartKitchenAssistant.Api.Features.Recipes.Infrastructure.Persistence;
using SmartKitchenAssistant.Api.Infrastructure.Authentication;
using SmartKitchenAssistant.Api.Infrastructure.Http;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddScoped<IIngredientReadRepository, IngredientReadRepository>();
builder.Services.AddScoped<IngredientReadService>();
builder.Services.AddScoped<IPantryRepository, PantryRepository>();
builder.Services.AddScoped<PantryService>();
builder.Services.AddScoped<IRecipeReadRepository, RecipeReadRepository>();
builder.Services.AddScoped<RecipeReadService>();
builder.Services.AddScoped<IRecipeSuitabilityRepository, RecipeSuitabilityRepository>();
builder.Services.AddScoped<RecipeSuitabilityCalculator>();
builder.Services.AddScoped<RecipeSuitabilityService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options =>
    options.ThrowOnBadRequest = true);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");
app.MapHealthChecks("/health").AllowAnonymous();
app.MapIngredientEndpoints();
app.MapPantryEndpoints();
app.MapRecipeEndpoints();

app.Run();

public partial class Program;
