# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

The default run path is Aspire — it boots all three services, the dashboard, and OTel telemetry from a single command:

```bash
cd OrderDemo/OrderDemo.AppHost
aspire start              # detached; prints dashboard URL with login token
aspire describe           # resource states + URLs
aspire logs <resource>    # api | mcp | web
aspire stop               # tear down
```

When debugging a single service in isolation (no dashboard, no OTel), run it directly:

```bash
cd OrderDemo/OrderDemo.Api && dotnet run     # http://localhost:5000  (Scalar at /scalar)
cd OrderDemo/OrderDemo.Mcp && dotnet run     # http://localhost:5010
cd OrderDemo/OrderDemo.Web && npm run dev    # http://localhost:5173
```

`dotnet build` from any project compiles cleanly. There is no test project — verify via curl, the Scalar UI, or the Aspire dashboard's Traces view.

```bash
# Reset database (re-runs seeder with all users and seed data)
rm OrderDemo/OrderDemo.Api/orderdemo.db
```

## Architecture

Solution layout (`OrderDemo/OrderDemo.sln`):

| Project | Role |
|---------|------|
| `OrderDemo.Core` | Shared DTOs (`OrderDto`, `CursorPagedResult<T>`, `OrderStatsDto`, `TopCustomerDto`, `CustomerSummaryDto`, …) — referenced by Api and Mcp |
| `OrderDemo.Api` | .NET 10 Minimal API + EF Core + JWT auth + Serilog |
| `OrderDemo.Mcp` | MCP server (Streamable HTTP at `/`, optional stdio with `--stdio`) — tools, prompts, resources, sampling |
| `OrderDemo.Web` | Vue 3 + Vite dashboard; Vite proxies `/api/*` → `http://localhost:5000` |
| `OrderDemo.AppHost` | Aspire AppHost — `AppHost.cs` wires `api` (pinned 5000), `mcp` (pinned 5010, `WithReference(api).WaitFor(api)`), `web` (`AddViteApp`, dynamic port) |
| `OrderDemo.ServiceDefaults` | OTel tracing/metrics/logs, service discovery, HTTP resilience, `self` health check — referenced by Api and Mcp via `builder.AddServiceDefaults()` |

### API layer flow

```
Endpoints/*.cs  →  Services/OrderService.cs  →  Data/AppDbContext.cs  →  SQLite
```

- **`Program.cs`** — calls `builder.AddServiceDefaults()` (OTel + discovery + resilience), then wires Serilog, EF Core, ASP.NET Identity, JWT Bearer auth, CORS (allows `localhost:5173`), `CorrelationMiddleware`, `UseSerilogRequestLogging`, health checks, and `MapAuthEndpoints` / `MapOrderEndpoints`.
- **`Endpoints/`** — Minimal API route registration only; no business logic. `AuthEndpoints.cs` (login + JWT) and `OrderEndpoints.cs` (orders + `/customers/{id:int}/summary`).
- **`Services/OrderService.cs`** — all query logic lives here. Receives `AppDbContext` and `ILogger<OrderService>` via primary-constructor injection. Every new query method goes in this class.
- **`Models/`** — EF Core entities: `Customer`, `Order`, `OrderLine`, `Product`.
- **`Data/Seeder.cs`** — seeds 1 000 customers, 20 products, 10 000 orders, and two auth users (`admin` / `mcp`) on first run. Guard `if (context.Customers.Any()) return;` skips on subsequent starts. **Delete `orderdemo.db` to reseed.**
- **DTOs live in `OrderDemo.Core/DTOs/OrderDtos.cs`** (not in the Api project) — shared with Mcp.
- **`Middleware/CorrelationMiddleware.cs`** — reads inbound `X-Correlation-Id`, falls back to `TraceIdentifier`, echoes it on the response, pushes it into Serilog's `LogContext` so every log entry for the request carries `CorrelationId`.
- **`HealthChecks/DatabaseHealthCheck.cs`** — `/health` returns `Healthy` only when customer + order counts are non-zero; `Degraded` when reachable-but-unseeded; `Unhealthy` on exceptions.

### MCP layer (`OrderDemo.Mcp`)

- **`Services/ApiClient.cs`** — typed HTTP client; calls `EnsureTokenAsync()` + `AttachCorrelationHeader()` before each API call. Reads `ApiClient:BaseUrl` from config; the existing pin to `http://localhost:5000` matches the AppHost's Api endpoint, so no override is needed.
- **`Tools/OrderTools.cs`** — five MCP tools returning `IEnumerable<ContentBlock>` (`search_orders`, `get_order_detail`, `get_order_stats`, `get_top_customers`, `generate_insights` — last one uses sampling).
- **`Prompts/OrderPrompts.cs`** — four prompt templates (`monthly_order_summary`, `top_customers_report`, `order_lookup`, `daily_briefing`).
- **`Resources/OrderResources.cs`** — two static (`orders://products/catalogue`, `orders://schema`) + one templated (`orders://customers/{id}/summary`).
- **`HealthChecks/ApiHealthCheck.cs`** — uses a dedicated unauthenticated `HttpClient` named `"health"`. Maps 2xx → `Healthy`; non-2xx / `HttpRequestException` / `TaskCanceledException` → `Degraded`. Mcp reports `Degraded` (not `Unhealthy`) when the API is down — the process is still running.
- **stdio guard in `Program.cs`** — when launched with `--stdio`, Serilog writes plain text to stderr only (stdout is reserved for the MCP protocol stream); HTTP mode uses the JSON formatter.

### Auth and secrets

All order endpoints require `[RequireAuthorization]`. Obtain a JWT via `POST /auth/login` with `{"username":"admin","password":"Admin@demo1!"}` (or `mcp` / `Mcp@service1!`). Token lifetime is 8 hours.

`Jwt:Secret`, `AdminUser:Password`, `McpUser:Password` (Api) and `ApiClient:Password` (Mcp) live in [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) and only load when `ASPNETCORE_ENVIRONMENT=Development`. The AppHost sets that env var explicitly on both projects — without it Aspire would default to `Production` and login would 401 because Mcp would send placeholder strings as the password. See the README for the per-project `dotnet user-secrets set` commands.

### Route registration order matters

In `OrderEndpoints.cs`, literal routes (`/orders/stats`, `/orders/top-customers`) are registered **before** the parameterised route (`/orders/{orderNumber}`) to prevent ASP.NET Core from matching literal segments as order number values.

Minimal API endpoints should not use deprecated WithOpenApi()

### EF Core / SQLite note

`GroupBy` with aggregates (`Sum`, `Count`) on SQLite requires breaking into two queries — one to aggregate by ID, a second to hydrate the entity details — to avoid EF Core translation failures. See `GetTopCustomersAsync` in `OrderService.cs` for the established pattern.


### MCP transport endpoint

`MapMcp()` registers the Streamable HTTP transport at the **root path** (`/`), not `/mcp`. Initialize with `POST /` and use the returned `Mcp-Session-Id` header on subsequent calls. The `/mcp` path returns 404.

### Aspire constraints

- Both .NET services use `launchProfileName: null` in `AppHost.cs` plus explicit `WithHttpEndpoint(port: …)` — this bypasses each project's `launchSettings.json` so port pinning is deterministic. Adding/renaming a launch profile won't change Aspire behaviour.
- `OrderDemo.Web` uses `AddViteApp` and **must not** receive a `WithHttpEndpoint(...)` call — the Vite integration auto-registers an endpoint with a dynamic port (visible in the dashboard). The Api's CORS only allows `http://localhost:5173`, but Vue calls hit the API server-side through Vite's proxy, so CORS isn't exercised in dev.
- ServiceDefaults' `MapDefaultEndpoints()` would map `/health` again and collide with the Phase 3e custom JSON health writer — it is intentionally **not** called in either service's `Program.cs`. The ServiceDefaults `self` check still appears as one of the entries in the existing `/health` payload.