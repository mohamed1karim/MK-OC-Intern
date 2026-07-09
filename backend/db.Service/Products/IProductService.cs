// Describes what the product-related business logic can do. Same pattern as
// IUserService: the controller depends on this interface, not the concrete
// ProductService class.
using db.Service.DTOs.Products;

namespace db.Service.Products;

public interface IProductService
{
    // "name" is optional — pass null/empty to get every product, or a
    // partial name to search (spec: "Search products by name").
    Task<List<ProductResponseDto>> GetAllAsync(string? name);

    Task<ProductResponseDto> GetByIdAsync(int id);
    Task<ProductResponseDto> CreateAsync(CreateProductDto dto);
    Task<ProductResponseDto> UpdateAsync(int id, UpdateProductDto dto);

    // Admin/SuperAdmin only.
    Task DeleteAsync(int id, ProductActionDto dto);
}
