namespace ProductService.Models
{
    public record CreateProductRequest
    {
        public string Name { get; init; }
        public decimal Price { get; init; }
        public int StockQuantity {  get; init; }

        public CreateProductRequest(string Name, decimal Price, int StockQuantity)
        {
            this.Name = Name;
            this.Price = Price;
            this.StockQuantity = StockQuantity;
        }
    }
}
