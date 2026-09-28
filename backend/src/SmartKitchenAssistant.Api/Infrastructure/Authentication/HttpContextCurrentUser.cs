using SmartKitchenAssistant.Api.Application.Identity;

namespace SmartKitchenAssistant.Api.Infrastructure.Authentication;

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    public string UserId
    {
        get
        {
            if (AuthenticatedUserIdResolver.TryResolve(
                    httpContextAccessor.HttpContext?.User,
                    out var userId))
            {
                return userId;
            }

            throw new InvalidOperationException(
                "Authenticated user identifier is unavailable.");
        }
    }
}
