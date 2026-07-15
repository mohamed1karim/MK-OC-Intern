// Shape of data required to create a new product (POST /api/products).
// Quantity here is the *initial* stock count when the product is first
// added — after this, the spec requires all stock changes to happen through
// Orders, not by editing a Product directly (see UpdateProductDto).
//
// No ActingUserId/Role here — introducing new products is Admin/SuperAdmin
// only, enforced via [Authorize(Roles = ...)] on the controller, with the
// acting user's id/role read from their validated JWT rather than trusted
// from the request body.
using System.ComponentModel.DataAnnotations;

namespace db.Service.DTOs.Products;

public class CreateProductDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
    public int Quantity { get; set; }
}
