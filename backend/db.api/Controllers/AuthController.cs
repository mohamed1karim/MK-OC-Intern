// The "front desk" for logging in: /api/auth. Kept separate from
// UsersController since this is conceptually "auth", not user CRUD — and
// this exact URL shape (/api/auth/login) is what stays in place once the
// real Bonus — Authentication (hashed passwords, JWT) replaces the
// placeholder logic underneath it.
using db.Service.DTOs.Users;
using db.Service.Users;
using Microsoft.AspNetCore.Mvc;

namespace db.api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<UserResponseDto>> Login(LoginDto dto)
    {
        var user = await _userService.LoginAsync(dto);
        return Ok(user);
    }
}
