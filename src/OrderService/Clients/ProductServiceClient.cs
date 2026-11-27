using System.Net;

namespace OrderService.Clients;

public record ProductDto(int Id, string Name, decimal Price, int StockQuantity);

public class ProductServiceClient
{
    private readonly HttpClient _http;

    public ProductServiceClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ProductDto?> GetProductAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/products/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>(ct);
    }
}