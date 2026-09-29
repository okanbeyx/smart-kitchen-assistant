using Microsoft.AspNetCore.Routing;
using SmartKitchenAssistant.Api.Features.Pantry.Api;
using SmartKitchenAssistant.Api.Features.Pantry.Application;
using SmartKitchenAssistant.Api.Features.Pantry.Infrastructure.Persistence;
using SmartKitchenAssistant.Api.Infrastructure.Authentication;
using SmartKitchenAssistant.Api.Infrastructure.Http;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddScoped<IPantryRepository, PantryRepository>();
builder.Services.AddScoped<PantryService>();
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
app.MapPantryEndpoints();

app.Run();

public partial class Program;
