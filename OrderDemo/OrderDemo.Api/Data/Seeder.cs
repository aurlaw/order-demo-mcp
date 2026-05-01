using OrderDemo.Api.Models;

namespace OrderDemo.Api.Data;

public static class Seeder
{
    public static void Seed(AppDbContext context)
    {
        if (context.Customers.Any()) return;

        var rng = new Random(42);

        var catalogue = new (string Name, decimal Price)[]
        {
            ("Wireless Keyboard", 49.99m),
            ("USB-C Hub", 34.99m),
            ("Monitor Stand", 59.99m),
            ("Mechanical Mouse", 79.99m),
            ("Webcam HD", 89.99m),
            ("Noise-Cancelling Headset", 149.99m),
            ("Laptop Stand", 39.99m),
            ("LED Desk Lamp", 29.99m),
            ("External SSD 1TB", 109.99m),
            ("HDMI Cable 2m", 12.99m),
            ("Ethernet Switch 8-Port", 44.99m),
            ("Ergonomic Chair Cushion", 55.00m),
            ("Cable Management Kit", 19.99m),
            ("Screen Cleaner Kit", 9.99m),
            ("Smart Power Strip", 34.99m),
            ("Bluetooth Speaker", 69.99m),
            ("Drawing Tablet", 129.99m),
            ("Portable Charger 20000mAh", 49.99m),
            ("Microphone Boom Arm", 39.99m),
            ("RGB LED Strip", 24.99m),
        };

        var products = catalogue.Select((c, i) => new Product
        {
            Id = i + 1,
            Name = c.Name,
            Price = c.Price
        }).ToList();

        string[] firstNames =
        [
            "James", "Mary", "Robert", "Patricia", "John", "Jennifer", "Michael", "Linda",
            "William", "Barbara", "David", "Elizabeth", "Richard", "Susan", "Joseph", "Jessica",
            "Thomas", "Sarah", "Charles", "Karen", "Christopher", "Lisa", "Daniel", "Nancy",
            "Matthew", "Betty", "Anthony", "Margaret", "Mark", "Sandra", "Donald", "Ashley",
            "Steven", "Emily", "Paul", "Dorothy", "Andrew", "Kimberly", "Joshua", "Helen",
            "Kenneth", "Donna", "Kevin", "Carol", "Brian", "Michelle", "George", "Amanda",
            "Timothy", "Melissa"
        ];

        string[] lastNames =
        [
            "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
            "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson",
            "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson",
            "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker",
            "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
            "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell",
            "Carter", "Roberts"
        ];

        var customers = new List<Customer>(1000);
        for (int i = 0; i < 1000; i++)
        {
            var firstName = firstNames[i % firstNames.Length];
            var lastName = lastNames[i % lastNames.Length];
            customers.Add(new Customer
            {
                Id = i + 1,
                FirstName = firstName,
                LastName = lastName,
                Email = $"{firstName.ToLower()}.{lastName.ToLower()}{i + 1}@example.com"
            });
        }

        var orders = new List<Order>(10000);
        var orderLines = new List<OrderLine>(40000);
        var lineId = 1;

        for (int i = 0; i < 10000; i++)
        {
            var customer = customers[rng.Next(customers.Count)];
            var orderDate = DateTime.UtcNow
                .AddDays(-rng.Next(0, 365))
                .AddHours(-rng.Next(0, 24));

            var lineCount = rng.Next(1, 6);
            var lines = new List<OrderLine>(lineCount);

            for (int j = 0; j < lineCount; j++)
            {
                var product = products[rng.Next(products.Count)];
                var quantity = rng.Next(1, 5);
                lines.Add(new OrderLine
                {
                    Id = lineId++,
                    OrderId = i + 1,
                    ProductId = product.Id,
                    Product = product,
                    Quantity = quantity,
                    Price = product.Price
                });
            }

            var total = Math.Round(lines.Sum(l => l.Price * l.Quantity), 2);

            var order = new Order
            {
                Id = i + 1,
                OrderNumber = $"ORD-{i + 1:D6}",
                OrderDate = orderDate,
                CustomerId = customer.Id,
                Customer = customer,
                Total = total,
                Lines = lines
            };

            orders.Add(order);
            orderLines.AddRange(lines);
        }

        context.Products.AddRange(products);
        context.Customers.AddRange(customers);
        context.Orders.AddRange(orders);
        context.OrderLines.AddRange(orderLines);
        context.SaveChanges();
    }
}
