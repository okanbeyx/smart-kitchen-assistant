using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace SmartKitchenAssistant.Api.Infrastructure.Authentication;

internal static class AuthenticatedUserIdResolver
{
    internal const string UserIdClaimType = "sub";
    internal const int MaxUserIdLength = 256;

    internal static bool TryResolve(
        ClaimsPrincipal? principal,
        [NotNullWhen(true)] out string? userId)
    {
        userId = null;

        if (principal is null)
        {
            return false;
        }

        var authenticatedIdentities = principal.Identities
            .Where(identity => identity.IsAuthenticated)
            .ToArray();

        if (authenticatedIdentities.Length == 0)
        {
            return false;
        }

        var subjectClaims = authenticatedIdentities
            .SelectMany(identity => identity.FindAll(UserIdClaimType))
            .ToArray();

        if (subjectClaims.Length != 1)
        {
            return false;
        }

        var subject = subjectClaims[0].Value;

        if (string.IsNullOrWhiteSpace(subject) || subject.Length > MaxUserIdLength)
        {
            return false;
        }

        userId = subject;
        return true;
    }
}
