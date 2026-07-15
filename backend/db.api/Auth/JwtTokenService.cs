using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using db.Service.DTOs.Users;
using Microsoft.IdentityModel.Tokens;

namespace db.api.Auth;

// Reads Jwt:Key/Issuer/Audience/ExpiryHours from configuration and mints a
// signed JWT. Lives in db.api (not db.Service) because token issuance needs
// IConfiguration and web-specific JWT types — keeping it here means
// db.Service.csproj never needs a JWT/ASP.NET Core package reference.
public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(UserResponseDto user)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = jwtSection["Key"]!;
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var expiryHours = double.Parse(jwtSection["ExpiryHours"]!);

        var claims = new[]
        {
            // The long ClaimTypes URIs (not the short "sub"/"role" names) so
            // [Authorize(Roles = "...")] and User.GetUserId()/GetRole() work
            // without any extra inbound-claim-mapping configuration.
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expiresAtUtc = DateTime.UtcNow.AddHours(expiryHours);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
