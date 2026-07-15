// What POST /api/auth/login returns: the user's own profile plus a JWT the
// frontend attaches to every subsequent request. The token itself is minted
// in db.api (it needs IConfiguration + web-specific JWT types), but the
// shape of the response is still owned here, matching every other DTO.
namespace db.Service.DTOs.Users;

public class LoginResponseDto
{
    public UserResponseDto User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
