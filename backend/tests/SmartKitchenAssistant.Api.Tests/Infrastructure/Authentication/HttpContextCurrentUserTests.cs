using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartKitchenAssistant.Api.Infrastructure.Authentication;

namespace SmartKitchenAssistant.Api.Tests.Infrastructure.Authentication;

public sealed class HttpContextCurrentUserTests
{
    [Fact]
    public void UserIdReturnsExactSubjectClaim()
    {
        const string subject = " User-CaseSensitive ";
        var currentUser = CreateCurrentUser(isAuthenticated: true, subject);

        var userId = currentUser.UserId;

        Assert.Equal(subject, userId);
    }

    [Fact]
    public void UserIdRejectsUnauthenticatedPrincipal()
    {
        var currentUser = CreateCurrentUser(isAuthenticated: false, "user-1");

        Assert.Throws<InvalidOperationException>(() => currentUser.UserId);
    }

    [Fact]
    public void UserIdRejectsMissingSubjectClaim()
    {
        var currentUser = CreateCurrentUser(isAuthenticated: true);

        Assert.Throws<InvalidOperationException>(() => currentUser.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void UserIdRejectsWhitespaceSubjectClaim(string subject)
    {
        var currentUser = CreateCurrentUser(isAuthenticated: true, subject);

        Assert.Throws<InvalidOperationException>(() => currentUser.UserId);
    }

    [Fact]
    public void UserIdRejectsDuplicateSubjectClaims()
    {
        var currentUser = CreateCurrentUser(
            isAuthenticated: true,
            "user-1",
            "user-2");

        Assert.Throws<InvalidOperationException>(() => currentUser.UserId);
    }

    [Fact]
    public void UserIdRejectsSubjectFromUnauthenticatedIdentity()
    {
        var currentUser = CreateCurrentUser(
            CreateIdentity(isAuthenticated: true),
            CreateIdentity(isAuthenticated: false, "untrusted-user"));

        Assert.Throws<InvalidOperationException>(() => currentUser.UserId);
    }

    [Fact]
    public void UserIdIgnoresSubjectFromUnauthenticatedIdentity()
    {
        const string authenticatedSubject = " Trusted-User ";
        var currentUser = CreateCurrentUser(
            CreateIdentity(isAuthenticated: true, authenticatedSubject),
            CreateIdentity(isAuthenticated: false, "untrusted-user"));

        Assert.Equal(authenticatedSubject, currentUser.UserId);
    }

    [Fact]
    public void UserIdRejectsSubjectsFromMultipleAuthenticatedIdentities()
    {
        var currentUser = CreateCurrentUser(
            CreateIdentity(isAuthenticated: true, "user-1"),
            CreateIdentity(isAuthenticated: true, "user-2"));

        Assert.Throws<InvalidOperationException>(() => currentUser.UserId);
    }

    [Fact]
    public void UserIdAcceptsSubjectAtMaximumLength()
    {
        var subject = new string('a', 256);
        var currentUser = CreateCurrentUser(isAuthenticated: true, subject);

        Assert.Equal(subject, currentUser.UserId);
    }

    [Fact]
    public void UserIdRejectsSubjectAboveMaximumLength()
    {
        var currentUser = CreateCurrentUser(
            isAuthenticated: true,
            new string('a', 257));

        Assert.Throws<InvalidOperationException>(() => currentUser.UserId);
    }

    private static HttpContextCurrentUser CreateCurrentUser(
        bool isAuthenticated,
        params string[] subjectClaims)
    {
        return CreateCurrentUser(CreateIdentity(isAuthenticated, subjectClaims));
    }

    private static HttpContextCurrentUser CreateCurrentUser(
        params ClaimsIdentity[] identities)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identities)
        };
        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        return new HttpContextCurrentUser(accessor);
    }

    private static ClaimsIdentity CreateIdentity(
        bool isAuthenticated,
        params string[] subjectClaims)
    {
        var claims = subjectClaims.Select(subject => new Claim("sub", subject));

        return new ClaimsIdentity(
            claims,
            isAuthenticated ? TestAuthenticationHandler.SchemeName : null);
    }
}
