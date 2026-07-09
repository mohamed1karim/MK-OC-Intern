// Shape of data to update an existing product (PUT /api/products/{id}).
// Quantity is intentionally NOT here: the spec says all stock changes must
// happen through Orders (In/Out), so editing a product can never touch
// stock directly — that would let stock drift out of sync with order history.
//
// ActingUserId/Role are the same placeholder pattern as CreateProductDto —
// editing a product is Admin/SuperAdmin only, checked in
// ProductService.UpdateAsync.
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

    [Required]
    public int ActingUserId { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;
}
