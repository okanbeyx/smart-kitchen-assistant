namespace SmartKitchenAssistant.Api.Infrastructure.Authentication;

public sealed class JwtAuthenticationOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string? Authority { get; set; }

    public string? Audience { get; set; }
}
