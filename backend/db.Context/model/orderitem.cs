// This file defines the "OrderItem" entity: a single line within an Order,
// pointing at one Product and how many units of it are being moved, at what
// price. An Order can have many OrderItems (that's how one order covers
// "multiple products", per the spec).

namespace db.Context.Model;

/// <summary>
/// One line of an Order: a Product, a quantity, and the price it was sold/
/// bought at when the order was placed.
/// Spec fields: Id, OrderId, ProductId, Quantity, UnitPrice.
/// </summary>
public class OrderItem
{
    public int Id { get; set; }

    // Which Order this line belongs to (the "many" side of Order → OrderItems).
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    // Which Product this line is for.
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    // How many units of the Product this line moves.
    public int Quantity { get; set; }

    // Snapshot of Product.Price at the moment the order was created — stored
    // here instead of looked up live, so past orders stay correct even if the
    // product's price changes afterwards.
    public decimal UnitPrice { get; set; }
}
