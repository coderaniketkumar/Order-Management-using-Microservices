using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductService.Data;
using ProductService.Models;

namespace ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductDbContext _db;

    public ProductsController(ProductDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll()
    {
        return await _db.Products.AsNoTracking().ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();
        return product;
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(CreateProductRequest request)
    {
        var product = new Product
        {
            Name = request.Name,
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }
}