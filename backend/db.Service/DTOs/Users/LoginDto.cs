// Shape of data sent to POST /api/auth/login. This is the simple
// placeholder login: plain-text username/password check against the Users
// table, no hashing, no token returned. The real "Bonus — Authentication"
// (hashed passwords + JWT) replaces this later — the URL shape stays the
// same so the frontend won't need to change where it calls.
using System.ComponentModel.DataAnnotations;

namespace db.Service.DTOs.Users;

public class LoginDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
