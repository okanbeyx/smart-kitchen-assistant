using System.Net;
using System.Net.Http.Headers;

namespace SmartKitchenAssistant.Api.Tests;

[Collection(TestWebApplicationFactoryCollection.Name)]
public sealed class AuthenticationBoundaryTests
{
    private readonly HttpClient _client;

    public AuthenticationBoundaryTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRootWithoutCredentialsReturnsUnauthorized()
    {
        using var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRootWithoutSubjectClaimReturnsForbidden()
    {
        using var request = CreateAuthenticatedRequest();
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRootWithWhitespaceSubjectClaimReturnsForbidden()
    {
        using var request = CreateAuthenticatedRequest("   ");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRootWithDuplicateSubjectClaimsReturnsForbidden()
    {
        using var request = CreateAuthenticatedRequest("user-1", "user-2");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRootWithTooLongSubjectClaimReturnsForbidden()
    {
        using var request = CreateAuthenticatedRequest(new string('a', 257));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRootWithValidSubjectClaimReturnsOk()
    {
        using var request = CreateAuthenticatedRequest("user-1");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        params string[] subjectClaims)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Authorization =
            new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);

        if (subjectClaims.Length > 0)
        {
            request.Headers.TryAddWithoutValidation(
                TestAuthenticationHandler.UserIdHeaderName,
                subjectClaims);
        }

        return request;
    }
}
