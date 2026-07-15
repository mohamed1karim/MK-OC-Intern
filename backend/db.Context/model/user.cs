// This file defines the "User" entity: the C# class that Entity Framework Core
// maps directly to the "Users" table in the database. A model/entity class has
// no logic in it — it's purely a description of what one row of data looks like.
// EF Core reads the properties below and turns each one into a table column.

namespace db.Context.Model;

/// <summary>
/// A person who can use the warehouse system.
/// Spec fields: Id, Username, Email, Password, Role, CreatedAt.
/// </summary>
public class User
{
    // Primary key. EF Core treats a property literally named "Id" as the
    // table's primary key, and makes it auto-increment (IDENTITY) by convention
    // — we don't have to configure this manually.
    public int Id { get; set; }

    // The name the user logs in with. Must be unique — enforced later with a
    // unique index in AppDbContext (the "classes" step doesn't set that up yet).
    public string Username { get; set; } = string.Empty;

    // The user's email address. Also must be unique.
    public string Email { get; set; } = string.Empty;

    // Stored as plain text for now. The spec explicitly defers real password
    // hashing to the "Bonus — Authentication" phase, since there's no login
    // logic yet in the base project.
    public string Password { get; set; } = string.Empty;

    // Whether this user is a "User", "Admin", or "SuperAdmin". Admins (and
    // SuperAdmins) get extra powers (e.g. completing/cancelling orders,
    // promoting/demoting other users).
    public UserRole Role { get; set; }

    // Defaults to "now" the instant a new User object is constructed in code,
    // so callers never have to remember to set this themselves.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Soft delete: "deleting" a user just flips this instead of removing the
    // row. A hard delete would violate the Restrict FK on Order.CreatedByUser/
    // ProcessedByUser for anyone with order history — soft delete sidesteps
    // that entirely (the row never disappears, so every historical
    // "requested by"/"accepted by" reference keeps resolving), while still
    // hiding the user from normal Users-list queries.
    public bool IsDeleted { get; set; } = false;
}

/// <summary>
/// The roles a user can have. Using an enum instead of a plain string means
/// a typo like "Admni" can never even compile, let alone get saved to the
/// database — the compiler only allows these three values.
///
/// Every new account always starts as User (enforced in
/// UserService.CreateAsync, regardless of what a client requests) —
/// Admin/SuperAdmin can never be self-granted at signup. Admin is granted
/// only via UserService.PromoteToAdminAsync, and revoked via
/// DemoteToUserAsync. SuperAdmin is a protected tier: it cannot be granted
/// or revoked through the app at all (no promote/demote path reaches it) —
/// it can only be set directly, e.g. by whoever administers the database.
/// </summary>
public enum UserRole
{
    User,
    Admin,
    SuperAdmin
}
