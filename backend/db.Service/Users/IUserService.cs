// An interface describing *what* the user-related business logic can do,
// without saying *how*. The controller depends on this interface (not the
// concrete UserService class directly) — that's what lets Program.cs swap in
// a different implementation later (e.g. for tests) without touching the
// controller at all. This is the standard ASP.NET Core "dependency
// injection" pattern.
using db.Service.DTOs.Users;

namespace db.Service.Users;

public interface IUserService
{
    Task<List<UserResponseDto>> GetAllAsync();
    Task<UserResponseDto> GetByIdAsync(int id);
    Task<UserResponseDto> CreateAsync(CreateUserDto dto);
    Task<UserResponseDto> UpdateAsync(int id, UpdateUserDto dto);

    // Admin/SuperAdmin only. Removes a user entirely — but only if the
    // target is a plain User; an Admin/SuperAdmin target must be demoted
    // first (see DemoteToUserAsync). No return value needed — either it
    // succeeds, or it throws NotFoundException/ConflictException.
    Task DeleteAsync(int id, AdminActionDto dto);

    // Simple placeholder login: checks Username/Password directly against
    // the Users table. Throws UnauthorizedException if they don't match.
    Task<UserResponseDto> LoginAsync(LoginDto dto);

    // Admin/SuperAdmin only. Not in the original spec — added so an Admin
    // can grant/revoke admin rights. SuperAdmin can never be granted or
    // revoked through either of these — see UserRole's doc comment.
    Task<UserResponseDto> PromoteToAdminAsync(int id, AdminActionDto dto);
    Task<UserResponseDto> DemoteToUserAsync(int id, AdminActionDto dto);
}
