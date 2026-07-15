using db.Service.DTOs.Users;

namespace db.api.Auth;

public interface ITokenService
{
    // Mints a signed JWT carrying the user's id/username/role as claims,
    // and returns the UTC instant it expires at (so the response can tell
    // the frontend when to expect a 401 without it having to decode the
    // token itself).
    (string Token, DateTime ExpiresAtUtc) GenerateToken(UserResponseDto user);
}
