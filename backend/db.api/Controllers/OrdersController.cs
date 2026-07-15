// The "front desk" for Orders: /api/orders. Unlike Users/Products, there's
// no PUT here — the spec only calls for Create, List, View, and two special
// status-transition actions (Complete, Cancel). DELETE is added on top of
// the spec for storage/cleanup purposes (see OrderService.DeleteAsync for
// what it actually does to stock).
using db.api.Auth;
using db.Service.DTOs.Orders;
using db.Service.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace db.api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    // GET /api/orders                    -> every order (Admin/SuperAdmin), or
    //                                        just the caller's own (plain User)
    // GET /api/orders?createdByUserId=3   -> only orders that user 3 created
    //                                        (Admin/SuperAdmin only — a plain
    //                                        User is always forced to their
    //                                        own id, see OrderService)
    // GET /api/orders?involvingUserId=5   -> orders where user 5 was either
    //                                        the creator or the processor
    //                                        (Admin/SuperAdmin only)
    [HttpGet]
    public async Task<ActionResult<List<OrderResponseDto>>> GetAll(
        [FromQuery] int? createdByUserId,
        [FromQuery] int? involvingUserId)
    {
        var orders = await _orderService.GetAllAsync(
            createdByUserId, involvingUserId, User.GetUserId(), User.GetRole());
        return Ok(orders);
    }

    // GET /api/orders/5 — a plain User may only view an order they created
    // or processed; Admin/SuperAdmin may view any order.
    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponseDto>> GetById(int id)
    {
        var order = await _orderService.GetByIdAsync(id, User.GetUserId(), User.GetRole());
        return Ok(order);
    }

    // POST /api/orders — the creator is always the caller.
    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> Create(CreateOrderDto dto)
    {
        var created = await _orderService.CreateAsync(dto, User.GetUserId());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // POST /api/orders/5/confirm — Admin/SuperAdmin only.
    [HttpPost("{id}/confirm")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<OrderResponseDto>> Confirm(int id)
    {
        var confirmed = await _orderService.ConfirmAsync(id, User.GetUserId(), User.GetRole());
        return Ok(confirmed);
    }

    // POST /api/orders/5/complete — Admin/SuperAdmin only.
    [HttpPost("{id}/complete")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<OrderResponseDto>> Complete(int id)
    {
        var completed = await _orderService.CompleteAsync(id, User.GetUserId(), User.GetRole());
        return Ok(completed);
    }

    // POST /api/orders/5/cancel — Admin/SuperAdmin only.
    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<OrderResponseDto>> Cancel(int id)
    {
        var cancelled = await _orderService.CancelAsync(id, User.GetUserId(), User.GetRole());
        return Ok(cancelled);
    }

    // DELETE /api/orders/5 — Admin/SuperAdmin only (previously had no check
    // at all).
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _orderService.DeleteAsync(id, User.GetRole());
        return NoContent();
    }
}
