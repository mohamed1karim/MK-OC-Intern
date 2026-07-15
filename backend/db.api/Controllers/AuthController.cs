// The "front desk" for logging in: /api/auth. Kept separate from
// UsersController since this is conceptually "auth", not user CRUD.
using db.api.Auth;
using db.Service.DTOs.Users;
using db.Service.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace db.api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ITokenService _tokenService;

    public AuthController(IUserService userService, ITokenService tokenService)
    {
        _userService = userService;
        _tokenService = tokenService;
    }

    // POST /api/auth/login — must stay reachable without a token, obviously.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var user = await _userService.LoginAsync(dto);
        var (token, expiresAt) = _tokenService.GenerateToken(user);

        return Ok(new LoginResponseDto { User = user, Token = token, ExpiresAt = expiresAt });
    }
}
