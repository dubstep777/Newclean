using System.Net;
using System.Net.Http;
using XUnlock.Models;
using XUnlock.Services;

namespace XUnlock.Tests;

public class CommunityApiClientTests
{
    [Fact]
    public async Task UnauthorizedState_IsAuthenticationRequired()
    {
        using var client = CreateClient(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var result = await client.GetStateAsync(CancellationToken.None);
        Assert.Equal(PermissionState.AuthenticationRequired, result.State);
    }

    [Fact]
    public async Task RateLimitedApply_IsTemporaryError()
    {
        using var client = CreateClient(new HttpResponseMessage((HttpStatusCode)429));
        var result = await client.ApplyAsync(CancellationToken.None);
        Assert.Equal(ApplyState.TemporaryError, result.State);
    }

    [Fact]
    public async Task SuccessfulApply_IsParsed()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"code\":1}")
        };
        using var client = CreateClient(response);
        var result = await client.ApplyAsync(CancellationToken.None);
        Assert.Equal(ApplyState.Successful, result.State);
    }

    private static CommunityApiClient CreateClient(HttpResponseMessage response)
    {
        var handler = new StubHandler(response);
        return new CommunityApiClient(new Logger(), handler);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public StubHandler(HttpResponseMessage response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _response.Headers.Date = DateTimeOffset.UtcNow;
            return Task.FromResult(_response);
        }
    }
}
