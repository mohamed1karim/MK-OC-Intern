// This is the "front desk" for Users: the actual web addresses (routes)
// that Swagger and, later, the Angular frontend will call. A controller's
// only job is to handle the HTTP side of things (reading the request,
// picking a status code) — it must never talk to the database directly. All
// real logic lives in IUserService (db.Service/Users/UserService.cs).
using db.Service.DTOs.Users;
using db.Service.Users;
using Microsoft.AspNetCore.Mvc;

namespace db.api.Controllers;

// [ApiController] turns on automatic model validation: if a DTO's
// [Required]/[EmailAddress]/etc. attributes fail, ASP.NET Core returns a 400
// Bad Request before this class's code even runs.
// [Route("api/[controller]")] means this controller's base URL is
// "/api/users" ("[controller]" is replaced with "Users" minus the
// "Controller" suffix).
[ApiController]
[Route("api/[controller]")]
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

    // GET /api/users
    [HttpGet]
    public async Task<ActionResult<List<UserResponseDto>>> GetAll()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }

    // GET /api/users/5
    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponseDto>> GetById(int id)
    {
        // No try/catch needed here: if the user doesn't exist, UserService
        // throws NotFoundException, and ExceptionHandlingMiddleware (wired
        // up in Program.cs) turns that into a 404 automatically.
        var user = await _userService.GetByIdAsync(id);
        return Ok(user);
    }

    // POST /api/users
    [HttpPost]
    public async Task<ActionResult<UserResponseDto>> Create(CreateUserDto dto)
    {
        var created = await _userService.CreateAsync(dto);

        // 201 Created, with a Location header pointing at GET /api/users/{id}
        // for the new user — the standard REST way to respond to a POST.
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /api/users/5
    [HttpPut("{id}")]
    public async Task<ActionResult<UserResponseDto>> Update(int id, UpdateUserDto dto)
    {
        var updated = await _userService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    // DELETE /api/users/5 — Admin/SuperAdmin only, and only targets a plain
    // User (an Admin/SuperAdmin target must be demoted first). Takes a body
    // (ActingUserId/Role) even though it's a DELETE — [ApiController] binds
    // complex types from the body regardless of HTTP verb.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, AdminActionDto dto)
    {
        await _userService.DeleteAsync(id, dto);

        // 204 No Content: the standard REST response for a successful
        // delete — there's no body to return since the resource is gone.
        return NoContent();
    }

    // POST /api/users/5/promote — Admin/SuperAdmin only, grants the Admin role.
    [HttpPost("{id}/promote")]
    public async Task<ActionResult<UserResponseDto>> PromoteToAdmin(int id, AdminActionDto dto)
    {
        var promoted = await _userService.PromoteToAdminAsync(id, dto);
        return Ok(promoted);
    }

    // POST /api/users/5/demote — Admin/SuperAdmin only, reverts to User.
    // SuperAdmin targets are rejected (protected from demotion).
    [HttpPost("{id}/demote")]
    public async Task<ActionResult<UserResponseDto>> DemoteToUser(int id, AdminActionDto dto)
    {
        var demoted = await _userService.DemoteToUserAsync(id, dto);
        return Ok(demoted);
    }
}
