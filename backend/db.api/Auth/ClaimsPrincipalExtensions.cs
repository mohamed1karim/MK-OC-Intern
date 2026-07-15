using System.Security.Claims;

namespace db.api.Auth;

// Reads the acting user's id/role straight off the validated JWT attached
// to the request — this is what every controller uses now instead of
// trusting an ActingUserId/Role field the client sent in the request body.
public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string GetRole(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role)!;
}
