// The "front desk" for Products: /api/products. Same shape as
// UsersController — no database access here, only calls to IProductService.
using db.Service.DTOs.Products;
using db.Service.Products;
using Microsoft.AspNetCore.Mvc;

namespace db.api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    // GET /api/products              -> list all
    // GET /api/products?name=widget  -> search by name (spec requirement)
    [HttpGet]
    public async Task<ActionResult<List<ProductResponseDto>>> GetAll([FromQuery] string? name)
    {
        var products = await _productService.GetAllAsync(name);
        return Ok(products);
    }

    // GET /api/products/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductResponseDto>> GetById(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        return Ok(product);
    }

    // POST /api/products
    [HttpPost]
    public async Task<ActionResult<ProductResponseDto>> Create(CreateProductDto dto)
    {
        var created = await _productService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /api/products/5
    [HttpPut("{id}")]
    public async Task<ActionResult<ProductResponseDto>> Update(int id, UpdateProductDto dto)
    {
        var updated = await _productService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    // DELETE /api/products/5 — Admin/SuperAdmin only. Takes a body
    // (ActingUserId/Role) even though it's a DELETE — [ApiController] binds
    // complex types from the body regardless of HTTP verb.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, ProductActionDto dto)
    {
        await _productService.DeleteAsync(id, dto);
        return NoContent();
    }
}
