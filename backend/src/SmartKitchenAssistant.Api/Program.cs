using SmartKitchenAssistant.Api.Infrastructure.Authentication;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
