// Shape of data the client sends to update an existing user
// (PUT /api/users/{id}). Same fields as create, on purpose — it's a full
// replacement of the editable fields, not a partial patch.
//
// No Role field here on purpose, same reasoning as CreateUserDto: this
// endpoint has no authorization check on who's calling it, so allowing Role
// here would let anyone grant themselves (or anyone else) Admin/SuperAdmin
// via a plain profile edit. Role changes only ever happen through the
// separate admin-gated Promote/Demote endpoints.
using System.ComponentModel.DataAnnotations;

namespace db.Service.DTOs.Users;

public class UpdateUserDto
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
