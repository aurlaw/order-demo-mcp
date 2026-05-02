# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# API (Terminal 1)
cd OrderDemo/OrderDemo.Api
dotnet run          # starts on http://localhost:5000
dotnet build        # compile check

# Vue frontend (Terminal 2)
cd OrderDemo/OrderDemo.Web
npm install
npm run dev         # starts on http://localhost:5173
npm run build

# Reset database (re-runs seeder with all users and seed data)
rm OrderDemo/OrderDemo.Api/orderdemo.db
```

There is no test project. Verification is done manually via curl or the Scalar UI at `http://localhost:5000/scalar`.

## Architecture

Monorepo: .NET 10 Minimal API (`OrderDemo.Api`) + Vue 3 SPA (`OrderDemo.Web`).

### API layer flow

```
Endpoints/*.cs  →  Services/OrderService.cs  →  Data/AppDbContext.cs  →  SQLite
```

- **`Program.cs`** — wires DI, EF Core, ASP.NET Identity, JWT Bearer auth, CORS (allows `localhost:5173`), and calls `MapAuthEndpoints` / `MapOrderEndpoints`.
- **`Endpoints/`** — Minimal API route registration only; no business logic. Two files: `AuthEndpoints.cs` (login + JWT issuance) and `OrderEndpoints.cs`.
- **`Services/OrderService.cs`** — all query logic lives here. Receives `AppDbContext` via primary-constructor injection. Every new query method goes in this class.
- **`DTOs/`** — `OrderDtos.cs` holds all order-related records (`OrderDto`, `PagedResult<T>`, `OrderStatsDto`, `TopCustomerDto`). `LoginDtos.cs` holds auth records.
- **`Data/Seeder.cs`** — seeds 1 000 customers, 20 products, 10 000 orders, and two auth users (`admin` / `mcp`) on first run. The guard `if (context.Customers.Any()) return;` skips everything on subsequent starts. **Delete `orderdemo.db` to reseed.**
- **`Models/`** — EF Core entities: `Customer`, `Order`, `OrderLine`, `Product`.

### Auth

All order endpoints require `[RequireAuthorization]`. Obtain a JWT via `POST /auth/login` with `{"username":"admin","password":"Admin@demo1!"}` (or `mcp` / `Mcp@service1!`). Token lifetime is 8 hours.

### Route registration order matters

In `OrderEndpoints.cs`, literal routes (`/orders/stats`, `/orders/top-customers`) are registered **before** the parameterised route (`/orders/{orderNumber}`) to prevent ASP.NET Core from matching literal segments as order number values.

Minimal API endpoints should not use deprecated WithOpenApi()

### EF Core / SQLite note

`GroupBy` with aggregates (`Sum`, `Count`) on SQLite requires breaking into two queries — one to aggregate by ID, a second to hydrate the entity details — to avoid EF Core translation failures. See `GetTopCustomersAsync` in `OrderService.cs` for the established pattern.


### MCP Server

We do not need to use the /mcp endpoint