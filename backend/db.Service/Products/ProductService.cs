// Business logic for Products: talks to the database via AppDbcontext,
// applies the "no direct quantity edits" rule from UpdateProductDto, and
// maps between entities and DTOs. Same shape as UserService on purpose —
// once you've learned one, you've learned the pattern for all of them.
using db.Context;
using db.Context.Model;
using db.Service.DTOs.Products;
using db.Service.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace db.Service.Products;

public class ProductService : IProductService
{
    private readonly AppDbcontext _context;

    public ProductService(AppDbcontext context)
    {
        _context = context;
    }

    // READ (list + search): if a name is supplied, filter to products whose
    // Name contains it (case-insensitive, since SQL Server's default
    // collation is case-insensitive). This single method covers both
    // "list products" and "search products by name" from the spec.
    public async Task<List<ProductResponseDto>> GetAllAsync(string? name)
    {
        var query = _context.products.Include(p => p.CreatedByUser).AsQueryable();

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
    public async Task<ProductResponseDto> CreateAsync(CreateProductDto dto)
    {
        var creator = await EnsureActingAdminOrHigherAsync(dto.ActingUserId, dto.Role, "introduce a new product");

        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
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
    public async Task<ProductResponseDto> UpdateAsync(int id, UpdateProductDto dto)
    {
        var product = await _context.products.FindAsync(id);
        if (product is null)
        {
            throw new NotFoundException($"Product with id {id} was not found.");
        }

        await EnsureActingAdminOrHigherAsync(dto.ActingUserId, dto.Role, "edit a product");

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.Price = dto.Price;

        // The spec calls for an UpdatedAt column; this is the one place
        // (besides future Order processing) responsible for bumping it.
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ProductResponseDto.FromEntity(product);
    }

    // DELETE: Admin/SuperAdmin only.
    public async Task DeleteAsync(int id, ProductActionDto dto)
    {
        var product = await _context.products.FindAsync(id);
        if (product is null)
        {
            throw new NotFoundException($"Product with id {id} was not found.");
        }

        await EnsureActingAdminOrHigherAsync(dto.ActingUserId, dto.Role, "delete a product");

        // The database refuses this at the FK level too (OrderItem.Product
        // is Restrict, not Cascade), but checking here first gives a clean
        // 409 with a clear message instead of a raw 500 from an unhandled
        // DbUpdateException.
        var hasOrderHistory = await _context.orderItems.AnyAsync(oi => oi.ProductId == id);
        if (hasOrderHistory)
        {
            throw new ConflictException(
                "Cannot delete: this product appears in existing order history.");
        }

        _context.products.Remove(product);
        await _context.SaveChangesAsync();
    }

    // Shared by CreateAsync/DeleteAsync: confirm the acting user is real and
    // at least an Admin (Admin or SuperAdmin). Same placeholder role check
    // used by OrderService/UserService — not real security, since any
    // client can just claim a role until the Auth bonus adds real login.
    // Returns the acting user entity so callers that need it (CreateAsync,
    // for CreatedByUser) don't have to look it up a second time.
    private async Task<User> EnsureActingAdminOrHigherAsync(int actingUserId, string roleString, string action)
    {
        var actingUser = await _context.users.FindAsync(actingUserId);
        if (actingUser is null)
        {
            throw new NotFoundException($"User with id {actingUserId} was not found.");
        }

        if (!Enum.TryParse<UserRole>(roleString, ignoreCase: true, out var role))
        {
            throw new ArgumentException($"'{roleString}' is not a valid role. Use 'User', 'Admin', or 'SuperAdmin'.");
        }

        if (role != UserRole.Admin && role != UserRole.SuperAdmin)
        {
            throw new ForbiddenException($"Only an Admin can {action}.");
        }

        return actingUser;
    }
}
