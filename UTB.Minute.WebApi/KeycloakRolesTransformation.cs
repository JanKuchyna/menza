using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace UTB.Minute.WebApi;

public class KeycloakRolesTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (realmAccess is not null)
        {
            using var doc = JsonDocument.Parse(realmAccess);
            if (doc.RootElement.TryGetProperty("roles", out var roles))
            {
                foreach (var role in roles.EnumerateArray())
                {
                    var roleName = role.GetString();
                    if (roleName is not null && !identity.HasClaim(ClaimTypes.Role, roleName))
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                }
            }
        }
        return Task.FromResult(principal);
    }
}
