// What the API sends back for an order, including its line items.
using db.Context.Model;

namespace db.Service.DTOs.Orders;

public class OrderResponseDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalPrice { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public int? ProcessedByUserId { get; set; }
    // Null while still Pending — nobody has acted on it yet.
    public string? ProcessedByUsername { get; set; }
    public List<OrderItemResponseDto> Items { get; set; } = new();

    // Requires order.OrderItems (+ each item's Product), order.CreatedByUser,
    // and order.ProcessedByUser to already be loaded (via .Include in the
    // service) — this is a pure mapping method, it never queries the
    // database itself.
    public static OrderResponseDto FromEntity(Order order)
    {
        return new OrderResponseDto
        {
            Id = order.Id,
            Type = order.Type.ToString(),
            Status = order.Status.ToString(),
            OrderDate = order.OrderDate,
            TotalPrice = order.TotalPrice,
            CreatedByUserId = order.CreatedByUserId,
            CreatedByUsername = order.CreatedByUser.Username,
            ProcessedByUserId = order.ProcessedByUserId,
            ProcessedByUsername = order.ProcessedByUser?.Username,
            Items = order.OrderItems.Select(OrderItemResponseDto.FromEntity).ToList()
        };
    }
}

/// <summary>One line of an order's response — a product, quantity, and price.</summary>
public class OrderItemResponseDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;

    public static OrderItemResponseDto FromEntity(OrderItem item)
    {
        return new OrderItemResponseDto
        {
            ProductId = item.ProductId,
            ProductName = item.Product.Name,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice
        };
    }
}
