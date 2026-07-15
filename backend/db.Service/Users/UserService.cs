// This class holds the actual business logic for Users: talking to the
// database via AppDbcontext, checking business rules (like "username must be
// unique"), and converting between entities and DTOs. Controllers never do
// any of this themselves — they just call these methods.
using db.Context;
using db.Context.Model;
using db.Service.Auth;
using db.Service.DTOs.Users;
using db.Service.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace db.Service.Users;

public class UserService : IUserService
{
    // AppDbcontext is "injected" here — ASP.NET Core's dependency injection
    // container creates it and hands it to us automatically, because
    // Program.cs registered it earlier with builder.Services.AddDbContext.
    private readonly AppDbcontext _context;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(AppDbcontext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    // READ (list): fetch every user, map each one to a response DTO so the
    // Password field never leaves this layer. Soft-deleted users are hidden
    // by default — pass includeDeleted (Admin/SuperAdmin views only) to see
    // them too, e.g. for a "show deleted" toggle.
    public async Task<List<UserResponseDto>> GetAllAsync(bool includeDeleted = false)
    {
        var query = _context.users.AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(u => !u.IsDeleted);
        }

        var users = await query.ToListAsync();
        return users.Select(UserResponseDto.FromEntity).ToList();
    }

    // READ (single): look up by primary key, regardless of deleted status —
    // callers like the "view this person's order history" page need to
    // resolve a deleted user's username too, since their historical orders
    // still reference them. FindAsync returns null if no row matches — we
    // turn that into a NotFoundException, which the middleware converts into
    // a 404 response for us.
    public async Task<UserResponseDto> GetByIdAsync(int id)
    {
        var user = await _context.users.FindAsync(id);
        if (user is null)
        {
            throw new NotFoundException($"User with id {id} was not found.");
        }

        return UserResponseDto.FromEntity(user);
    }

    // CREATE: validate business rules, build a new User entity from the
    // incoming DTO, save it, then return it as a response DTO. Role is
    // always User here — never taken from the client — so nobody can
    // self-grant Admin/SuperAdmin at signup. The only way to become Admin
    // is through the separate, admin-gated PromoteToAdminAsync.
    public async Task<UserResponseDto> CreateAsync(CreateUserDto dto)
    {
        // Business rule: no two users can share a Username or Email. The
        // database also enforces this via a unique index (belt and
        // suspenders), but checking here first lets us return a clean 409
        // with a helpful message instead of a raw database error.
        var alreadyExists = await _context.users
            .AnyAsync(u => u.Username == dto.Username || u.Email == dto.Email);
        if (alreadyExists)
        {
            throw new ConflictException("A user with that username or email already exists.");
        }

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            Password = _passwordHasher.Hash(dto.Password),
            Role = UserRole.User
        };

        _context.users.Add(user);
        await _context.SaveChangesAsync();

        return UserResponseDto.FromEntity(user);
    }

    // UPDATE: find the existing row, make sure the new values don't collide
    // with a *different* user, then overwrite the fields and save. Role is
    // deliberately untouched here — this endpoint never accepts a role from
    // the client, so allowing a role change here would let anyone grant
    // themselves Admin/SuperAdmin through a plain profile edit. Caller
    // (from the validated JWT) must be editing their own account, or be
    // Admin/SuperAdmin editing someone else's — enforced here, not just in
    // the controller, since this is the actual security boundary.
    public async Task<UserResponseDto> UpdateAsync(int id, UpdateUserDto dto, int actingUserId, string actingRole)
    {
        var user = await _context.users.FindAsync(id);
        if (user is null)
        {
            throw new NotFoundException($"User with id {id} was not found.");
        }

        var isSelf = actingUserId == id;
        var isAdminOrHigher = Enum.TryParse<UserRole>(actingRole, ignoreCase: true, out var role)
            && (role == UserRole.Admin || role == UserRole.SuperAdmin);
        if (!isSelf && !isAdminOrHigher)
        {
            throw new ForbiddenException("You can only edit your own account.");
        }

        // Note the "u.Id != id" — without it, a user updating their own
        // record with their own unchanged username/email would incorrectly
        // look like a conflict with themselves.
        var conflicting = await _context.users
            .AnyAsync(u => u.Id != id && (u.Username == dto.Username || u.Email == dto.Email));
        if (conflicting)
        {
            throw new ConflictException("A user with that username or email already exists.");
        }

        user.Username = dto.Username;
        user.Email = dto.Email;
        user.Password = _passwordHasher.Hash(dto.Password);

        // No explicit "Update" call needed — EF Core is already tracking
        // this entity (we fetched it with FindAsync), so it notices the
        // property changes automatically and SaveChangesAsync writes them.
        await _context.SaveChangesAsync();

        return UserResponseDto.FromEntity(user);
    }

    // DELETE: Admin/SuperAdmin only. Soft delete — flips IsDeleted instead of
    // removing the row, so it never hits the Restrict FK on Order.
    // CreatedByUser/ProcessedByUser (a hard delete would fail there for
    // anyone with order history). Still refuses to delete an Admin or
    // SuperAdmin target directly — they must be demoted to User first (via
    // DemoteToUserAsync), same protective reasoning as Promote/Demote.
    public async Task DeleteAsync(int id, int actingUserId, string actingRole)
    {
        var user = await _context.users.FindAsync(id);
        if (user is null)
        {
            throw new NotFoundException($"User with id {id} was not found.");
        }

        EnsureActingAdminOrHigher(actingRole);

        if (user.Role != UserRole.User)
        {
            throw new ConflictException(
                $"Cannot delete: this user is {user.Role}. Demote them to User first.");
        }

        if (user.IsDeleted)
        {
            throw new ConflictException("This user is already deleted.");
        }

        user.IsDeleted = true;
        await _context.SaveChangesAsync();
    }

    // RESTORE: Admin/SuperAdmin only. Reverses a soft delete.
    public async Task<UserResponseDto> RestoreAsync(int id, int actingUserId, string actingRole)
    {
        var user = await _context.users.FindAsync(id);
        if (user is null)
        {
            throw new NotFoundException($"User with id {id} was not found.");
        }

        EnsureActingAdminOrHigher(actingRole);

        if (!user.IsDeleted)
        {
            throw new ConflictException("This user is not deleted.");
        }

        user.IsDeleted = false;
        await _context.SaveChangesAsync();

        return UserResponseDto.FromEntity(user);
    }

    // LOGIN: verifies the hashed password. Also carries a one-time,
    // just-in-time migration for the handful of users seeded before hashing
    // existed — if the stored value isn't in our hash format yet, fall back
    // to a direct string comparison, and only upon a successful match
    // (i.e. we've just cryptographically proven the plaintext value was
    // correct) rehash it in place. No row is ever touched otherwise.
    public async Task<UserResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _context.users
            .FirstOrDefaultAsync(u => u.Username == dto.Username && !u.IsDeleted);

        if (user is null)
        {
            throw new UnauthorizedException("Invalid username or password.");
        }

        bool passwordOk;
        if (_passwordHasher.IsHashed(user.Password))
        {
            passwordOk = _passwordHasher.Verify(dto.Password, user.Password);
        }
        else
        {
            passwordOk = user.Password == dto.Password;
            if (passwordOk)
            {
                user.Password = _passwordHasher.Hash(dto.Password);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // The legacy-password upgrade is a nice-to-have, not the
                    // thing being verified — a transient save failure here
                    // must never turn an otherwise-successful login into an
                    // error. It'll simply be retried on their next login.
                }
            }
        }

        if (!passwordOk)
        {
            throw new UnauthorizedException("Invalid username or password.");
        }

        return UserResponseDto.FromEntity(user);
    }

    // PROMOTE: Admin/SuperAdmin only. Grants the Admin role to any user
    // (including one who's already Admin — harmless no-op). Can never
    // result in SuperAdmin — that tier isn't grantable through the app at
    // all, even by a SuperAdmin. Doesn't touch Username/Email/Password,
    // unlike UpdateAsync, which is why this is its own endpoint rather than
    // reusing PUT /api/users/{id} (that requires a Password the acting
    // Admin doesn't have, since it's never returned to the client).
    public async Task<UserResponseDto> PromoteToAdminAsync(int id, int actingUserId, string actingRole)
    {
        var target = await _context.users.FindAsync(id);
        if (target is null)
        {
            throw new NotFoundException($"User with id {id} was not found.");
        }

        EnsureActingAdminOrHigher(actingRole);

        if (target.Role == UserRole.SuperAdmin)
        {
            throw new ConflictException("This user is already a SuperAdmin.");
        }

        target.Role = UserRole.Admin;
        await _context.SaveChangesAsync();

        return UserResponseDto.FromEntity(target);
    }

    // DEMOTE: Admin/SuperAdmin only. Reverts any Admin back to a regular
    // User. SuperAdmin is protected — it can never be demoted by anyone,
    // including another SuperAdmin.
    public async Task<UserResponseDto> DemoteToUserAsync(int id, int actingUserId, string actingRole)
    {
        var target = await _context.users.FindAsync(id);
        if (target is null)
        {
            throw new NotFoundException($"User with id {id} was not found.");
        }

        EnsureActingAdminOrHigher(actingRole);

        if (target.Role == UserRole.SuperAdmin)
        {
            throw new ConflictException("A SuperAdmin cannot be demoted.");
        }

        target.Role = UserRole.User;
        await _context.SaveChangesAsync();

        return UserResponseDto.FromEntity(target);
    }

    // Shared by PromoteToAdminAsync/DemoteToUserAsync/DeleteAsync/
    // RestoreAsync: confirm the acting role (read straight from the
    // caller's validated JWT, not a client-supplied field) is at least an
    // Admin (Admin or SuperAdmin — SuperAdmin has every Admin power plus
    // more). The controller's [Authorize(Roles = "Admin,SuperAdmin")]
    // already enforces this before the request even reaches here — this is
    // a defense-in-depth check at the service boundary, same pattern used
    // by OrderService/ProductService.
    private static void EnsureActingAdminOrHigher(string actingRole)
    {
        if (!Enum.TryParse<UserRole>(actingRole, ignoreCase: true, out var role)
            || (role != UserRole.Admin && role != UserRole.SuperAdmin))
        {
            throw new ForbiddenException("Only an Admin can perform this action on another user.");
        }
    }
}
