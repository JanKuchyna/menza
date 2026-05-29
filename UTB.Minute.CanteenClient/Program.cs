using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Security.Claims;
using UTB.Minute.CanteenClient;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// OIDC authentication against Keycloak
var keycloakUrl = (builder.Configuration.GetConnectionString("keycloak") ?? "http://localhost:8080").TrimEnd('/');
var realm = builder.Configuration["Keycloak:Realm"] ?? "menza";

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = $"{keycloakUrl}/realms/{realm}";
    options.ClientId = builder.Configuration["Keycloak:CanteenClientId"] ?? "canteen-client";
    options.ClientSecret = builder.Configuration["Keycloak:CanteenClientSecret"] ?? "canteen-client-secret";
    options.ResponseType = "code";
    options.RequireHttpsMetadata = false;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.Events = new OpenIdConnectEvents
    {
        OnTicketReceived = ctx =>
        {
            // Store the access token as a claim so Blazor components can use it via AuthenticationStateProvider
            var accessToken = ctx.Properties?.GetTokenValue("access_token");
            if (!string.IsNullOrEmpty(accessToken))
                ctx.Principal?.Identities.First().AddClaim(new Claim("access_token", accessToken));
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

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

// Trigger OIDC login challenge – navigated to from the NotAuthorized template in App.razor
app.MapGet("/login", (string? returnUrl) =>
    Results.Challenge(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = returnUrl ?? "/"
        },
        [OpenIdConnectDefaults.AuthenticationScheme]
    )).AllowAnonymous();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

public partial class Program { }
