namespace OrderDemo.Api.Models;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public ICollection<OrderLine> Lines { get; set; } = [];
}
