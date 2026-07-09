// Shape of data required to create a new order (POST /api/orders). Note
// there's no Status field here — every new order starts Pending automatically
// (set in the Order entity itself), the client never gets to choose it.
using System.ComponentModel.DataAnnotations;

namespace db.Service.DTOs.Orders;

public class CreateOrderDto
{
    // Sent as the enum's name: "In" or "Out".
    [Required]
    public string Type { get; set; } = string.Empty;

    // No login yet, so the client has to say who's creating this order.
    [Required]
    public int CreatedByUserId { get; set; }

    // Must contain at least one product line — enforced in OrderService,
    // since [MinLength] on a collection doesn't reliably catch an empty
    // list the way it does for strings.
    [Required]
    public List<OrderItemInputDto> Items { get; set; } = new();
}

/// <summary>One line of the incoming order: which product, how many units.</summary>
public class OrderItemInputDto
{
    [Required]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }
}
