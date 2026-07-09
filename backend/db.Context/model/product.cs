// This file defines the "Product" entity: a single item type stocked in the
// warehouse. EF Core maps it to a "Products" table. Quantity here is the
// current stock on hand — Order processing (built later) increases/decreases it.

namespace db.Context.Model;

/// <summary>
/// An item the warehouse stocks.
/// Spec fields: Id, Name, Description, Price, Quantity, CreatedAt, UpdatedAt.
/// </summary>
public class Product
{
    public int Id { get; set; }

    // Also what "search products by name" (a required feature) filters on.
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Current unit price. OrderItem snapshots this value into its own
    // UnitPrice at order-creation time, so changing Price later never
    // rewrites the total of past orders.
    public decimal Price { get; set; }

    // Current stock on hand. In orders increase this; Out orders decrease it.
    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // The service layer is responsible for bumping this whenever a Product
    // (or its stock) changes — EF Core does not update this automatically.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Who introduced this product (must be an Admin/SuperAdmin — see
    // ProductService.CreateAsync). Nullable because products created before
    // this field existed have no recorded creator.
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
