// Business logic for Products: talks to the database via AppDbcontext,
// applies the "no direct quantity edits" rule from UpdateProductDto, and
// maps between entities and DTOs. Same shape as UserService on purpose —
// once you've learned one, you've learned the pattern for all of them.
using db.Context;
using db.Context.Model;
using db.Service.Ai;
using db.Service.DTOs.Products;
using db.Service.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace db.Service.Products;

public class ProductService : IProductService
{
    private readonly AppDbcontext _context;
    private readonly IProductDescriptionEnhancer _descriptionEnhancer;

    public ProductService(AppDbcontext context, IProductDescriptionEnhancer descriptionEnhancer)
    {
        _context = context;
        _descriptionEnhancer = descriptionEnhancer;
    }

    // READ (list + search): if a name is supplied, filter to products whose
    // Name contains it (case-insensitive, since SQL Server's default
    // collation is case-insensitive). This single method covers both
    // "list products" and "search products by name" from the spec.
    // Soft-deleted products are hidden by default — pass includeDeleted
    // (Admin/SuperAdmin views only) to see them too, e.g. for a "show
    // deleted" toggle.
    public async Task<List<ProductResponseDto>> GetAllAsync(string? name, bool includeDeleted = false)
    {
        var query = _context.products.Include(p => p.CreatedByUser).AsQueryable();

        if (!includeDeleted)
        {
            query = query.Where(p => !p.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(p => p.Name.Contains(name));
        }

        var products = await query.ToListAsync();
        return products.Select(ProductResponseDto.FromEntity).ToList();
    }

    public async Task<ProductResponseDto> GetByIdAsync(int id)
    {
        var product = await _context.products
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
        {
            throw new NotFoundException($"Product with id {id} was not found.");
        }

        return ProductResponseDto.FromEntity(product);
    }

    // CREATE: Admin/SuperAdmin only — introducing new products isn't
    // something a regular User can do. Quantity here is the initial stock
    // count. No uniqueness rule on Name — the spec never says two products
    // can't share a name.
    public async Task<ProductResponseDto> CreateAsync(CreateProductDto dto, int actingUserId, string actingRole)
    {
        EnsureActingAdminOrHigher(actingRole, "introduce a new product");

        var creator = await _context.users.FindAsync(actingUserId);
        if (creator is null)
        {
            throw new NotFoundException($"User with id {actingUserId} was not found.");
        }

        // The short description the admin typed is kept as-is (shown in list
        // views); the AI-expanded version is stored separately for the
        // product's detail page. Falls back to the short text on any AI
        // failure (see GroqProductDescriptionEnhancer) — never blocks
        // product creation.
        var longDescription = await _descriptionEnhancer.EnhanceAsync(dto.Name, dto.Description);

        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            LongDescription = longDescription,
            Price = dto.Price,
            Quantity = dto.Quantity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = creator.Id,
            // Set directly (already fetched above) rather than relying on EF
            // Core to load it — this Product isn't coming from a query with
            // .Include, so the navigation property would otherwise be null
            // and ProductResponseDto.FromEntity would silently omit the name.
            CreatedByUser = creator
        };

        _context.products.Add(product);
        await _context.SaveChangesAsync();

        return ProductResponseDto.FromEntity(product);
    }

    // UPDATE: Admin/SuperAdmin only — deliberately does not touch Quantity
    // either (UpdateProductDto has no Quantity field at all), so stock can
    // only ever change through Orders.
    public async Task<ProductResponseDto> UpdateAsync(int id, UpdateProductDto dto, int actingUserId, string actingRole)
    {
        var product = await _context.products.FindAsync(id);
        if (product is null)
        {
            throw new NotFoundException($"Product with id {id} was not found.");
        }

        EnsureActingAdminOrHigher(actingRole, "edit a product");

        // Re-run the same short-to-long expansion Create does, so the
        // detail page's LongDescription never goes stale relative to
        // whatever short Description was just edited.
        var longDescription = await _descriptionEnhancer.EnhanceAsync(dto.Name, dto.Description);

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.LongDescription = longDescription;
        product.Price = dto.Price;

        // The spec calls for an UpdatedAt column; this is the one place
        // (besides future Order processing) responsible for bumping it.
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ProductResponseDto.FromEntity(product);
    }

    // DELETE: Admin/SuperAdmin only. Soft delete — flips IsDeleted instead of
    // removing the row, so it never hits the Restrict FK on
    // OrderItem.Product (a hard delete would fail there for any product with
    // order history — the row now just stays, and every historical order
    // line keeps resolving its product name/price).
    public async Task DeleteAsync(int id, int actingUserId, string actingRole)
    {
        var product = await _context.products.FindAsync(id);
        if (product is null)
        {
            throw new NotFoundException($"Product with id {id} was not found.");
        }

        EnsureActingAdminOrHigher(actingRole, "delete a product");

        if (product.IsDeleted)
        {
            throw new ConflictException("This product is already deleted.");
        }

        product.IsDeleted = true;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    // RESTORE: Admin/SuperAdmin only. Reverses a soft delete.
    public async Task<ProductResponseDto> RestoreAsync(int id, int actingUserId, string actingRole)
    {
        var product = await _context.products
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            throw new NotFoundException($"Product with id {id} was not found.");
        }

        EnsureActingAdminOrHigher(actingRole, "restore a product");

        if (!product.IsDeleted)
        {
            throw new ConflictException("This product is not deleted.");
        }

        product.IsDeleted = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ProductResponseDto.FromEntity(product);
    }

    // Shared by Create/Update/Delete/Restore: confirm the acting role (read
    // straight from the caller's validated JWT, not a client-supplied
    // field) is at least an Admin. The controller's
    // [Authorize(Roles = "Admin,SuperAdmin")] already enforces this before
    // the request reaches here — this is a defense-in-depth check at the
    // service boundary, same pattern used by OrderService/UserService.
    private static void EnsureActingAdminOrHigher(string actingRole, string action)
    {
        if (!Enum.TryParse<UserRole>(actingRole, ignoreCase: true, out var role)
            || (role != UserRole.Admin && role != UserRole.SuperAdmin))
        {
            throw new ForbiddenException($"Only an Admin can {action}.");
        }
    }
}
