namespace OrderDemo.Core.DTOs;

public record CustomerDto(int Id, string FirstName, string LastName, string Email);
public record ProductDto(int Id, string Name, decimal Price);
public record OrderLineDto(int Quantity, decimal Price, ProductDto Product);

public record OrderDto(
    int                       Id,
    string                    OrderNumber,
    DateTime                  OrderDate,
    decimal                   Total,
    CustomerDto               Customer,
    IEnumerable<OrderLineDto> Lines);

public record PagedResult<T>(
    IEnumerable<T> Items,
    int            TotalCount,
    int            Page,
    int            PageSize,
    int            TotalPages);

public record OrderStatsDto(
    int       TotalOrders,
    decimal   TotalValue,
    decimal   AverageOrderValue,
    DateTime? From,
    DateTime? To);

public record TopCustomerDto(
    int     CustomerId,
    string  FirstName,
    string  LastName,
    string  Email,
    int     OrderCount,
    decimal TotalSpend);

public record OrderCursor(DateTime LastOrderDate, int LastOrderId);

public record CursorPagedResult<T>(
    IEnumerable<T> Items,
    string?        NextCursor,
    bool           HasMore);
