// What the API sends back when returning a product. Unlike User, nothing on
// Product is sensitive, so this mirrors the entity closely — but we still
// keep a separate DTO (rather than returning the entity directly) so the
// API's public shape doesn't change if the entity's internals ever do.
using db.Context.Model;

namespace db.Service.DTOs.Products;

public class ProductResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    // AI-expanded version of Description — shown on the product's detail
    // page, while list views keep showing the short Description above.
    public string LongDescription { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    // Null for products created before this field existed, or if the
    // creator's username isn't loaded.
    public string? CreatedByUsername { get; set; }
    public bool IsDeleted { get; set; }

    // Requires product.CreatedByUser to already be loaded (via .Include in
    // the service) if CreatedByUserId is set — this is a pure mapping
    // method, it never queries the database itself.
    public static ProductResponseDto FromEntity(Product product)
    {
        return new ProductResponseDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            LongDescription = product.LongDescription,
            Price = product.Price,
            Quantity = product.Quantity,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            CreatedByUserId = product.CreatedByUserId,
            CreatedByUsername = product.CreatedByUser?.Username,
            IsDeleted = product.IsDeleted
        };
    }
}
