using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Clients;
using OrderService.Data;
using OrderService.Messaging;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly ProductServiceClient _products;
    private readonly IEventPublisher _events;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(OrderDbContext db, ProductServiceClient products,
        IEventPublisher events, ILogger<OrdersController> logger)
    {
        _db = db;
        _products = products;
        _events = events;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<Order>>> GetAll()
    {
        return await _db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAtUtc).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Order>> GetById(int id)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        return order;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> Place(PlaceOrderRequest request, CancellationToken ct)
    {
        if (request.Quantity <= 0)
            return BadRequest("Quantity must be greater than 0.");

        ProductDto? product;
        try
        {
            product = await _products.GetProductAsync(request.ProductId, ct);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, "Product service is unavailable. Please try again later.");
        }

        if (product is null)
            return NotFound($"Product {request.ProductId} not found.");

        if (product.StockQuantity < request.Quantity)
            return BadRequest($"Only {product.StockQuantity} item(s) in stock.");

        var order = new Order
        {
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = request.Quantity,
            TotalAmount = product.Price * request.Quantity,
            Status = OrderStatus.Placed,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        try
        {
            // CancellationToken.None: order save ho chuka hai, client request cancel ho jaaye tab bhi event bhejna hai
            await _events.PublishAsync(Queues.OrderPlaced,
                new OrderPlacedEvent(order.Id, order.ProductId, order.Quantity, DateTime.UtcNow),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order {OrderId} saved but OrderPlaced event could not be published.", order.Id);
        }

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }
}