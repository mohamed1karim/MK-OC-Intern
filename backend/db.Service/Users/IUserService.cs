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
    // includeDeleted defaults false so ordinary Users-list views never see
    // soft-deleted accounts; pass true for an admin "show deleted" toggle.
    Task<List<UserResponseDto>> GetAllAsync(bool includeDeleted = false);
    Task<UserResponseDto> GetByIdAsync(int id);
    Task<UserResponseDto> CreateAsync(CreateUserDto dto);

    // actingUserId/actingRole come from the caller's validated JWT (set by
    // the controller from the ClaimsPrincipal), not from the request body —
    // caller must be editing their own account, or be Admin/SuperAdmin.
    Task<UserResponseDto> UpdateAsync(int id, UpdateUserDto dto, int actingUserId, string actingRole);

    // Admin/SuperAdmin only (enforced via actingRole, derived from the JWT).
    // Soft-deletes a user (flips IsDeleted; the row stays, so historical
    // order references keep resolving) — but only if the target is a plain
    // User; an Admin/SuperAdmin target must be demoted first (see
    // DemoteToUserAsync). No return value needed — either it succeeds, or it
    // throws NotFoundException/ConflictException.
    Task DeleteAsync(int id, int actingUserId, string actingRole);

    // Admin/SuperAdmin only. Reverses a soft delete.
    Task<UserResponseDto> RestoreAsync(int id, int actingUserId, string actingRole);

    // Verifies Username/Password (hashed, with a just-in-time upgrade path
    // for the legacy plaintext passwords seeded before hashing existed).
    // Throws UnauthorizedException if they don't match.
    Task<UserResponseDto> LoginAsync(LoginDto dto);

    // Admin/SuperAdmin only. Not in the original spec — added so an Admin
    // can grant/revoke admin rights. SuperAdmin can never be granted or
    // revoked through either of these — see UserRole's doc comment.
    Task<UserResponseDto> PromoteToAdminAsync(int id, int actingUserId, string actingRole);
    Task<UserResponseDto> DemoteToUserAsync(int id, int actingUserId, string actingRole);
}
