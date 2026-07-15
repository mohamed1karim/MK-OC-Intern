// The "front desk" for Products: /api/products. Same shape as
// UsersController — no database access here, only calls to IProductService.
using db.api.Auth;
using db.Service.DTOs.Products;
using db.Service.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace db.api.Controllers;

// [Authorize] at class level: any logged-in role may browse products; the
// admin-only actions below tighten this per-method.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    // GET /api/products                        -> list active products
    // GET /api/products?name=widget             -> search by name (spec requirement)
    // GET /api/products?includeDeleted=true     -> includes soft-deleted products too
    [HttpGet]
    public async Task<ActionResult<List<ProductResponseDto>>> GetAll(
        [FromQuery] string? name,
        [FromQuery] bool includeDeleted = false)
    {
        var products = await _productService.GetAllAsync(name, includeDeleted);
        return Ok(products);
    }

    // GET /api/products/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductResponseDto>> GetById(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        return Ok(product);
    }

    // POST /api/products — Admin/SuperAdmin only.
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ProductResponseDto>> Create(CreateProductDto dto)
    {
        var created = await _productService.CreateAsync(dto, User.GetUserId(), User.GetRole());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /api/products/5 — Admin/SuperAdmin only.
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ProductResponseDto>> Update(int id, UpdateProductDto dto)
    {
        var updated = await _productService.UpdateAsync(id, dto, User.GetUserId(), User.GetRole());
        return Ok(updated);
    }

    // DELETE /api/products/5 — Admin/SuperAdmin only.
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _productService.DeleteAsync(id, User.GetUserId(), User.GetRole());
        return NoContent();
    }

    // POST /api/products/5/restore — Admin/SuperAdmin only, reverses a soft delete.
    [HttpPost("{id}/restore")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ProductResponseDto>> Restore(int id)
    {
        var restored = await _productService.RestoreAsync(id, User.GetUserId(), User.GetRole());
        return Ok(restored);
    }
}
