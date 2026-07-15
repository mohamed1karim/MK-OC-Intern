// Shape of data to update an existing product (PUT /api/products/{id}).
// Quantity is intentionally NOT here: the spec says all stock changes must
// happen through Orders (In/Out), so editing a product can never touch
// stock directly — that would let stock drift out of sync with order history.
//
// No ActingUserId/Role here — editing a product is Admin/SuperAdmin only,
// enforced via [Authorize(Roles = ...)] on the controller, with the acting
// user's id/role read from their validated JWT.
using System.ComponentModel.DataAnnotations;

namespace db.Service.DTOs.Products;

public class UpdateProductDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative.")]
    public decimal Price { get; set; }
}
