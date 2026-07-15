// This is the "front desk" for Users: the actual web addresses (routes)
// that Swagger and the Angular frontend call. A controller's only job is to
// handle the HTTP side of things (reading the request, picking a status
// code) — it must never talk to the database directly. All real logic lives
// in IUserService (db.Service/Users/UserService.cs).
using db.api.Auth;
using db.Service.DTOs.Users;
using db.Service.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace db.api.Controllers;

// [ApiController] turns on automatic model validation: if a DTO's
// [Required]/[EmailAddress]/etc. attributes fail, ASP.NET Core returns a 400
// Bad Request before this class's code even runs.
// [Route("api/[controller]")] means this controller's base URL is
// "/api/users" ("[controller]" is replaced with "Users" minus the
// "Controller" suffix).
// [Authorize] at class level requires a valid JWT for every action here
// unless overridden per-action ([AllowAnonymous] on Create/signup).
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    // The service is injected here the same way AppDbcontext was injected
    // into UserService — Program.cs registers IUserService -> UserService,
    // and ASP.NET Core supplies it automatically.
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    // GET /api/users                     -> active users only
    // GET /api/users?includeDeleted=true  -> includes soft-deleted users too
    // Admin/SuperAdmin only — the list includes email addresses (PII), and
    // matches how the UI already gates this page.
    [HttpGet]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<List<UserResponseDto>>> GetAll([FromQuery] bool includeDeleted = false)
    {
        var users = await _userService.GetAllAsync(includeDeleted);
        return Ok(users);
    }

    // GET /api/users/5 — Admin/SuperAdmin only, same reasoning as GetAll.
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<UserResponseDto>> GetById(int id)
    {
        // No try/catch needed here: if the user doesn't exist, UserService
        // throws NotFoundException, and ExceptionHandlingMiddleware (wired
        // up in Program.cs) turns that into a 404 automatically.
        var user = await _userService.GetByIdAsync(id);
        return Ok(user);
    }

    // POST /api/users — self-service signup, must stay reachable without a
    // token.
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponseDto>> Create(CreateUserDto dto)
    {
        var created = await _userService.CreateAsync(dto);

        // 201 Created, with a Location header pointing at GET /api/users/{id}
        // for the new user — the standard REST way to respond to a POST.
        // (GetById itself is Admin-only, but CreatedAtAction only builds the
        // URL, it doesn't invoke the action.)
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /api/users/5 — any logged-in user may edit their own account;
    // Admin/SuperAdmin may edit anyone's. Enforced in UserService (the real
    // boundary), using the caller's id/role straight from their JWT.
    [HttpPut("{id}")]
    public async Task<ActionResult<UserResponseDto>> Update(int id, UpdateUserDto dto)
    {
        var updated = await _userService.UpdateAsync(id, dto, User.GetUserId(), User.GetRole());
        return Ok(updated);
    }

    // DELETE /api/users/5 — Admin/SuperAdmin only, and only targets a plain
    // User (an Admin/SuperAdmin target must be demoted first).
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _userService.DeleteAsync(id, User.GetUserId(), User.GetRole());

        // 204 No Content: the standard REST response for a successful
        // delete — there's no body to return since the resource is gone.
        return NoContent();
    }

    // POST /api/users/5/restore — Admin/SuperAdmin only, reverses a soft delete.
    [HttpPost("{id}/restore")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<UserResponseDto>> Restore(int id)
    {
        var restored = await _userService.RestoreAsync(id, User.GetUserId(), User.GetRole());
        return Ok(restored);
    }

    // POST /api/users/5/promote — Admin/SuperAdmin only, grants the Admin role.
    [HttpPost("{id}/promote")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<UserResponseDto>> PromoteToAdmin(int id)
    {
        var promoted = await _userService.PromoteToAdminAsync(id, User.GetUserId(), User.GetRole());
        return Ok(promoted);
    }

    // POST /api/users/5/demote — Admin/SuperAdmin only, reverts to User.
    // SuperAdmin targets are rejected (protected from demotion).
    [HttpPost("{id}/demote")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<UserResponseDto>> DemoteToUser(int id)
    {
        var demoted = await _userService.DemoteToUserAsync(id, User.GetUserId(), User.GetRole());
        return Ok(demoted);
    }
}
