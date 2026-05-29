using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace UTB.Minute.AdminClient;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthenticationStateProvider _authStateProvider;

    public AuthTokenHandler(AuthenticationStateProvider authStateProvider)
    {
        _authStateProvider = authStateProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var state = await _authStateProvider.GetAuthenticationStateAsync();
        var token = state.User.FindFirstValue("access_token");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}
