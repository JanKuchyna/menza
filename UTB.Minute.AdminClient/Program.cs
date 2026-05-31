using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Security.Claims;
using UTB.Minute.AdminClient;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorPages(options =>
{
    // Protect the Blazor host page – unauthenticated users are challenged before reaching Blazor
    options.Conventions.AuthorizePage("/_Host");
});
builder.Services.AddServerSideBlazor();

// OIDC authentication against Keycloak
var keycloakUrl = (builder.Configuration.GetConnectionString("keycloak") ?? "http://localhost:8080").TrimEnd('/');
var realm = builder.Configuration["Keycloak:Realm"] ?? "menza";

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options => { options.Cookie.Name = "menza.admin"; })
.AddOpenIdConnect(options =>
{
    options.Authority = $"{keycloakUrl}/realms/{realm}";
    options.ClientId = builder.Configuration["Keycloak:AdminClientId"] ?? "admin-client";
    options.ClientSecret = builder.Configuration["Keycloak:AdminClientSecret"] ?? "admin-client-secret";
    options.ResponseType = "code";
    options.RequireHttpsMetadata = false;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.Events = new OpenIdConnectEvents
    {
        OnTicketReceived = ctx =>
        {
            var identity = ctx.Principal?.Identities.First();
            if (identity is null) return Task.CompletedTask;

            // Store the access token as a claim so Blazor components can use it via AuthenticationStateProvider
            var accessToken = ctx.Properties?.GetTokenValue("access_token");
            if (!string.IsNullOrEmpty(accessToken))
            {
                identity.AddClaim(new Claim("access_token", accessToken));

                // Parse realm roles directly from the JWT access token payload
                // (realm_access is not forwarded via the userinfo endpoint)
                try
                {
                    var payload = accessToken.Split('.')[1];
                    var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                    var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("realm_access", out var realmAccess) &&
                        realmAccess.TryGetProperty("roles", out var roles))
                    {
                        foreach (var role in roles.EnumerateArray())
                        {
                            var roleName = role.GetString();
                            if (roleName is not null && !identity.HasClaim(ClaimTypes.Role, roleName))
                                identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                        }
                    }
                }
                catch { }
            }

            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// HttpClient for WebApi with bearer token injection
builder.Services.AddTransient<AuthTokenHandler>();
builder.Services.AddHttpClient("WebApi", client =>
{
    client.BaseAddress = new Uri("http://utb-minute-webapi");
}).AddHttpMessageHandler<AuthTokenHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).AllowAnonymous();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

public partial class Program { }
