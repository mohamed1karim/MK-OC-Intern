// The "front desk" for Orders: /api/orders. Unlike Users/Products, there's
// no PUT here — the spec only calls for Create, List, View, and two special
// status-transition actions (Complete, Cancel). DELETE is added on top of
// the spec for storage/cleanup purposes (see OrderService.DeleteAsync for
// what it actually does to stock).
using db.Service.DTOs.Orders;
using db.Service.Orders;
using Microsoft.AspNetCore.Mvc;

namespace db.api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    // GET /api/orders                    -> every order
    // GET /api/orders?createdByUserId=3   -> only orders that user 3 created
    // GET /api/orders?involvingUserId=5   -> orders where user 5 was either
    //                                        the creator or the processor
    //                                        (used for an Admin/SuperAdmin
    //                                        viewing someone else's history)
    [HttpGet]
    public async Task<ActionResult<List<OrderResponseDto>>> GetAll(
        [FromQuery] int? createdByUserId,
        [FromQuery] int? involvingUserId)
    {
        var orders = await _orderService.GetAllAsync(createdByUserId, involvingUserId);
        return Ok(orders);
    }

    // GET /api/orders/5
    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponseDto>> GetById(int id)
    {
        var order = await _orderService.GetByIdAsync(id);
        return Ok(order);
    }

    // POST /api/orders
    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> Create(CreateOrderDto dto)
    {
        var created = await _orderService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // POST /api/orders/5/confirm
    [HttpPost("{id}/confirm")]
    public async Task<ActionResult<OrderResponseDto>> Confirm(int id, ProcessOrderDto dto)
    {
        var confirmed = await _orderService.ConfirmAsync(id, dto);
        return Ok(confirmed);
    }

    // POST /api/orders/5/complete
    [HttpPost("{id}/complete")]
    public async Task<ActionResult<OrderResponseDto>> Complete(int id, ProcessOrderDto dto)
    {
        var completed = await _orderService.CompleteAsync(id, dto);
        return Ok(completed);
    }

    // POST /api/orders/5/cancel
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<OrderResponseDto>> Cancel(int id, ProcessOrderDto dto)
    {
        var cancelled = await _orderService.CancelAsync(id, dto);
        return Ok(cancelled);
    }

    // DELETE /api/orders/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _orderService.DeleteAsync(id);
        return NoContent();
    }
}
