using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OrderDemo.Mcp.Services;

namespace OrderDemo.Mcp.Resources;

[McpServerResourceType]
public class OrderResources(ApiClient apiClient)
{
    [McpServerResource(
        UriTemplate = "orders://products/catalogue",
        Name        = "Product Catalogue",
        MimeType    = "application/json")]
    [Description("Complete list of all available products with names and prices. " +
                 "Use this to understand what products exist before analysing order data.")]
    public Task<ResourceContents> GetProductCatalogueAsync()
    {
        var catalogue = new[]
        {
            new { id = 1,  name = "Wireless Keyboard",          price = 49.99m  },
            new { id = 2,  name = "USB-C Hub",                  price = 34.99m  },
            new { id = 3,  name = "Monitor Stand",              price = 59.99m  },
            new { id = 4,  name = "Mechanical Mouse",           price = 79.99m  },
            new { id = 5,  name = "Webcam HD",                  price = 89.99m  },
            new { id = 6,  name = "Noise-Cancelling Headset",   price = 149.99m },
            new { id = 7,  name = "Laptop Stand",               price = 39.99m  },
            new { id = 8,  name = "LED Desk Lamp",              price = 29.99m  },
            new { id = 9,  name = "External SSD 1TB",           price = 109.99m },
            new { id = 10, name = "HDMI Cable 2m",              price = 12.99m  },
            new { id = 11, name = "Ethernet Switch 8-Port",     price = 44.99m  },
            new { id = 12, name = "Ergonomic Chair Cushion",    price = 55.00m  },
            new { id = 13, name = "Cable Management Kit",       price = 19.99m  },
            new { id = 14, name = "Screen Cleaner Kit",         price = 9.99m   },
            new { id = 15, name = "Smart Power Strip",          price = 34.99m  },
            new { id = 16, name = "Bluetooth Speaker",          price = 69.99m  },
            new { id = 17, name = "Drawing Tablet",             price = 129.99m },
            new { id = 18, name = "Portable Charger 20000mAh",  price = 49.99m  },
            new { id = 19, name = "Microphone Boom Arm",        price = 39.99m  },
            new { id = 20, name = "RGB LED Strip",              price = 24.99m  }
        };

        return Task.FromResult<ResourceContents>(new TextResourceContents
        {
            Uri      = "orders://products/catalogue",
            MimeType = "application/json",
            Text     = JsonSerializer.Serialize(catalogue,
                new JsonSerializerOptions { WriteIndented = true })
        });
    }

    [McpServerResource(
        UriTemplate = "orders://schema",
        Name        = "Order Data Schema",
        MimeType    = "text/markdown")]
    [Description("Description of the order data model — entities, fields, and relationships. " +
                 "Read this first to understand the data structure before querying.")]
    public Task<ResourceContents> GetSchemaAsync()
    {
        const string schema = """
            # Order Demo — Data Schema

            ## Customer
            - `id` (int) — unique identifier
            - `firstName` (string)
            - `lastName` (string)
            - `email` (string)

            ## Product
            - `id` (int) — unique identifier
            - `name` (string)
            - `price` (decimal) — unit price in USD

            ## Order
            - `id` (int) — unique identifier
            - `orderNumber` (string) — format: ORD-XXXXXX
            - `orderDate` (datetime) — UTC
            - `total` (decimal) — stored total in USD
            - `customer` (Customer) — embedded customer detail
            - `lines` (OrderLine[]) — array of line items

            ## OrderLine
            - `quantity` (int)
            - `price` (decimal) — unit price at time of order
            - `product` (Product) — embedded product detail

            ## Notes
            - Orders are dated within the last 365 days from the current date
            - Each order has 1–5 line items
            - 10,000 orders total across 1,000 customers and 20 products
            - `order.total` equals sum of `line.price * line.quantity` across all lines

            ## Available Tools
            - `search_orders` — cursor-paginated search with optional lastName and date range filters
            - `get_order_detail` — full detail for a single order by order number
            - `get_order_stats` — aggregate stats (count, revenue, average) for a date range
            - `get_top_customers` — customers ranked by total spend
            - `generate_insights` — AI-generated narrative analysis of order data
            """;

        return Task.FromResult<ResourceContents>(new TextResourceContents
        {
            Uri      = "orders://schema",
            MimeType = "text/markdown",
            Text     = schema
        });
    }

    [McpServerResource(
        UriTemplate = "orders://customers/{id}/summary",
        Name        = "Customer Summary",
        MimeType    = "application/json")]
    [Description("Order history summary for a specific customer. " +
                 "Returns total orders, total spend, and first/last order dates. " +
                 "Replace {id} with the customer's numeric ID.")]
    public async Task<ResourceContents> GetCustomerSummaryAsync(
        [Description("The customer ID to look up")]
        int id)
    {
        var summary = await apiClient.GetCustomerSummaryAsync(id);

        var text = summary is null
            ? JsonSerializer.Serialize(new { error = $"Customer {id} not found." })
            : JsonSerializer.Serialize(summary,
                new JsonSerializerOptions { WriteIndented = true });

        return new TextResourceContents
        {
            Uri      = $"orders://customers/{id}/summary",
            MimeType = "application/json",
            Text     = text
        };
    }
}
