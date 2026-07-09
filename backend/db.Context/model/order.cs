// This file defines the "Order" entity: the header record for a stock
// movement. An Order doesn't hold Products directly — it holds a list of
// OrderItem lines (see orderitem.cs), and each line points at one Product.

namespace db.Context.Model;

/// <summary>
/// A stock movement request: either goods arriving (In) or leaving (Out).
/// Spec fields: Id, Type (In/Out), Status, OrderDate, TotalPrice, and who
/// created / changed it.
/// </summary>
public class Order
{
    public int Id { get; set; }

    // In = stock arriving, increases each product's Quantity.
    // Out = stock leaving, decreases each product's Quantity.
    public OrderType Type { get; set; }

    // Every new order starts Pending. An Admin later moves it to Completed
    // or Cancelled — both are final, the status never changes again after that.
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    // Sum of (Quantity * UnitPrice) across all this order's OrderItems.
    // Calculated and stored by the service layer when the order is created.
    public decimal TotalPrice { get; set; }

    // The user who created this order (every order is created by a User).
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    // The admin who later completed or cancelled this order. Null while the
    // order is still Pending — only gets set the moment an Admin acts on it.
    public int? ProcessedByUserId { get; set; }
    public User? ProcessedByUser { get; set; }

    // The products (and quantities) this order covers. One Order has many
    // OrderItems — the actual foreign-key relationship gets configured later
    // in AppDbContext; this is just the C# shape of that relationship.
    public List<OrderItem> OrderItems { get; set; } = new();
}

/// <summary>Whether stock is arriving or leaving in this order.</summary>
public enum OrderType
{
    In,
    Out
}

/// <summary>
/// Where an order is in its lifecycle: Pending -> Confirmed -> Completed or
/// Cancelled. Completed and Cancelled are both terminal; Complete/Cancel are
/// only allowed once an order has been Confirmed by an Admin (not directly
/// from Pending).
/// </summary>
public enum OrderStatus
{
    Pending,
    Confirmed,
    Completed,
    Cancelled
}
