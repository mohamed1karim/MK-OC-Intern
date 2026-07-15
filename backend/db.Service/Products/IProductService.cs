// Describes what the product-related business logic can do. Same pattern as
// IUserService: the controller depends on this interface, not the concrete
// ProductService class.
using db.Service.DTOs.Products;

namespace db.Service.Products;

public interface IProductService
{
    // "name" is optional — pass null/empty to get every product, or a
    // partial name to search (spec: "Search products by name").
    // includeDeleted defaults false so ordinary Products-list views never
    // see soft-deleted products; pass true for an admin "show deleted" toggle.
    Task<List<ProductResponseDto>> GetAllAsync(string? name, bool includeDeleted = false);

    Task<ProductResponseDto> GetByIdAsync(int id);

    // actingUserId/actingRole come from the caller's validated JWT (set by
    // the controller from the ClaimsPrincipal), not from the request body.
    Task<ProductResponseDto> CreateAsync(CreateProductDto dto, int actingUserId, string actingRole);
    Task<ProductResponseDto> UpdateAsync(int id, UpdateProductDto dto, int actingUserId, string actingRole);

    // Admin/SuperAdmin only. Soft delete (see ProductService for why).
    Task DeleteAsync(int id, int actingUserId, string actingRole);

    // Admin/SuperAdmin only. Reverses a soft delete.
    Task<ProductResponseDto> RestoreAsync(int id, int actingUserId, string actingRole);
}
