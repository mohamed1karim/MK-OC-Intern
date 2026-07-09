// Shape of data the client must send to create a new user (POST /api/users).
// The [Required]/[EmailAddress]/[MaxLength] attributes are "data
// annotations" — ASP.NET Core checks them automatically before the
// controller method even runs, and returns a 400 Bad Request with a clear
// message if any of them fail. We never have to write that checking by hand.
//
// No Role field here on purpose: every new account always starts as User
// (enforced in UserService.CreateAsync), so a client can never self-grant
// Admin/SuperAdmin by passing a role at signup. Admin is only ever granted
// afterwards, via the separate admin-gated Promote/Demote endpoints.
using System.ComponentModel.DataAnnotations;

namespace db.Service.DTOs.Users;

public class CreateUserDto
{
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
