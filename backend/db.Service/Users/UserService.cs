// This class holds the actual business logic for Users: talking to the
// database via AppDbcontext, checking business rules (like "username must be
// unique"), and converting between entities and DTOs. Controllers never do
// any of this themselves — they just call these methods.
using db.Context;
using db.Context.Model;
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

    public UserService(AppDbcontext context)
    {
        _context = context;
    }

    // READ (list): fetch every user, map each one to a response DTO so the
    // Password field never leaves this layer.
    public async Task<List<UserResponseDto>> GetAllAsync()
    {
        var users = await _context.users.ToListAsync();
        return users.Select(UserResponseDto.FromEntity).ToList();
    }

    // READ (single): look up by primary key. FindAsync returns null if no
    // row matches — we turn that into a NotFoundException, which the
    // middleware converts into a 404 response for us.
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
            Password = dto.Password,
            Role = UserRole.User
        };

        _context.users.Add(user);
        await _context.SaveChangesAsync();

        return UserResponseDto.FromEntity(user);
    }

    // UPDATE: find the existing row, make sure the new values don't collide
    // with a *different* user, then overwrite the fields and save. Role is
    // deliberately untouched here — this endpoint has no authorization
    // check on who's calling it, so allowing a role change here would let
    // anyone grant themselves Admin/SuperAdmin through a plain profile edit.
    public async Task<UserResponseDto> UpdateAsync(int id, UpdateUserDto dto)
    {
        var user = await _context.users.FindAsync(id);
        if (user is null)
        {
            throw new NotFoundException($"User with id {id} was not found.");
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
        user.Password = dto.Password;

        // No explicit "Update" call needed — EF Core is already tracking
        // this entity (we fetched it with FindAsync), so it notices the
        // property changes automatically and SaveChangesAsync writes them.
        await _context.SaveChangesAsync();

        return UserResponseDto.FromEntity(user);
    }

    // DELETE: Admin/SuperAdmin only. Refuses to delete an Admin or
    // SuperAdmin target directly — they must be demoted to User first (via
    // DemoteToUserAsync), same protective reasoning as Promote/Demote.
    public async Task DeleteAsync(int id, AdminActionDto dto)
    {
        var user = await _context.users.FindAsync(id);
        if (user is null)
        {
            throw new NotFoundException($"User with id {id} was not found.");
        }

        await EnsureActingAdminOrHigherAsync(dto);

        if (user.Role != UserRole.User)
        {
            throw new ConflictException(
                $"Cannot delete: this user is {user.Role}. Demote them to User first.");
        }

        // The database refuses this at the FK level too (Order.CreatedByUser/
        // ProcessedByUser are Restrict, not Cascade), but checking here first
        // gives a clean 409 with a clear message instead of a raw 500 from an
        // unhandled DbUpdateException.
        var hasOrderHistory = await _context.orders
            .AnyAsync(o => o.CreatedByUserId == id || o.ProcessedByUserId == id);
        if (hasOrderHistory)
        {
            throw new ConflictException(
                "Cannot delete: this user has order history (they created or processed at least one order).");
        }

        _context.users.Remove(user);
        await _context.SaveChangesAsync();
    }

    // LOGIN: simple placeholder check — direct Username/Password match
    // against the Users table. No hashing yet (matches how CreateAsync
    // currently stores Password as plain text); this gets replaced by real
    // hashed-password verification once the Auth bonus is built.
    public async Task<UserResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _context.users
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.Password == dto.Password);

        if (user is null)
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
    public async Task<UserResponseDto> PromoteToAdminAsync(int id, AdminActionDto dto)
    {
        var target = await _context.users.FindAsync(id);
        if (target is null)
        {
            throw new NotFoundException($"User with id {id} was not found.");
        }

        await EnsureActingAdminOrHigherAsync(dto);

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
    public async Task<UserResponseDto> DemoteToUserAsync(int id, AdminActionDto dto)
    {
        var target = await _context.users.FindAsync(id);
        if (target is null)
        {
            throw new NotFoundException($"User with id {id} was not found.");
        }

        await EnsureActingAdminOrHigherAsync(dto);

        if (target.Role == UserRole.SuperAdmin)
        {
            throw new ConflictException("A SuperAdmin cannot be demoted.");
        }

        target.Role = UserRole.User;
        await _context.SaveChangesAsync();

        return UserResponseDto.FromEntity(target);
    }

    // Shared by PromoteToAdminAsync/DemoteToUserAsync/DeleteAsync: confirm
    // the acting user is real and at least an Admin (Admin or SuperAdmin —
    // SuperAdmin has every Admin power plus more). Same placeholder role
    // check used by OrderService's Confirm/Complete/Cancel — not real
    // security, since any client can just claim a role until the Auth bonus
    // adds real login.
    private async Task EnsureActingAdminOrHigherAsync(AdminActionDto dto)
    {
        var actingUser = await _context.users.FindAsync(dto.ActingUserId);
        if (actingUser is null)
        {
            throw new NotFoundException($"User with id {dto.ActingUserId} was not found.");
        }

        if (!Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var role))
        {
            throw new ArgumentException($"'{dto.Role}' is not a valid role. Use 'User', 'Admin', or 'SuperAdmin'.");
        }

        if (role != UserRole.Admin && role != UserRole.SuperAdmin)
        {
            throw new ForbiddenException("Only an Admin can perform this action on another user.");
        }
    }
}
