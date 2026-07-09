// DTO = "Data Transfer Object". Instead of returning our User *entity*
// straight from the API, the service returns this shape instead. Two reasons:
//   1. It never includes Password — entities must never leak sensitive
//      fields to the outside world.
//   2. It keeps the API's public shape stable even if internal entity
//      details change later.
// Per the project spec, "DTO mapping" is a Service-layer responsibility, so
// DTOs live here in db.Service rather than in db.api.

using db.Context.Model;

namespace db.Service.DTOs.Users;

/// <summary>What the API sends back when returning a user (no password!).</summary>
public class UserResponseDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // A small helper that builds this DTO from a User entity, so there's one
    // obvious place doing the User -> DTO conversion instead of repeating the
    // same mapping code in every service method.
    public static UserResponseDto FromEntity(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
            CreatedAt = user.CreatedAt
        };
    }
}
