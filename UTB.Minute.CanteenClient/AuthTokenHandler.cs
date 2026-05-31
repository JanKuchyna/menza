using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;

namespace UTB.Minute.CanteenClient;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthTokenHandler(AuthenticationStateProvider authStateProvider, IHttpContextAccessor httpContextAccessor)
    {
        _authStateProvider = authStateProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // SSE endpoint is public and keeps connection open — skip auth header
        if (request.RequestUri?.PathAndQuery.Contains("/events") == true)
            return await base.SendAsync(request, ct);

        string? token = null;

        // Try HttpContext first (works during HTTP requests / prerender)
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
            token = await httpContext.GetTokenAsync("access_token");

        // Fall back to claim stored on the principal (works during Blazor SignalR circuit)
        if (token is null)
        {
            try
            {
                var state = await _authStateProvider.GetAuthenticationStateAsync();
                token = state.User.FindFirstValue("access_token");
            }
            catch { }
        }

        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, ct);
    }
}
