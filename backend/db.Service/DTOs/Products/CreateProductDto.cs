// Shape of data required to create a new product (POST /api/products).
// Quantity here is the *initial* stock count when the product is first
// added — after this, the spec requires all stock changes to happen through
// Orders, not by editing a Product directly (see UpdateProductDto).
//
// ActingUserId/Role are the same placeholder pattern as AdminActionDto
// (Users' Promote/Demote) — introducing new products is Admin/SuperAdmin
// only, checked in ProductService.CreateAsync.
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

    [Required]
    public int ActingUserId { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;
}
