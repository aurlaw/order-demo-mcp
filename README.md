# Order Demo

Monorepo: .NET 10 Minimal API + Vue 3 dashboard + MCP server, orchestrated locally with [Aspire](https://aspire.dev).

## First-time setup

### 1. Install the Aspire CLI

```bash
curl -sSL https://aspire.dev/install.sh | bash
```

See [aspire.dev/docs/install](https://aspire.dev/docs/install) for other platforms. Once installed, verify with `aspire doctor` — the dashboard runs over HTTPS, so if the dev certificate is untrusted, run `aspire certs trust`.

### 2. Set User Secrets

Sensitive config — the JWT signing key and seeded user passwords — lives in [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets), not `appsettings.json`. The committed `appsettings.json` files contain placeholder strings (`*** set via user secrets: <key> ***`) that document the shape of the config; the actual values must be set once per dev machine before either project will start cleanly.

```bash
# OrderDemo.Api
cd OrderDemo/OrderDemo.Api
dotnet user-secrets set "Jwt:Secret"          "order-demo-secret-key-min-32-chars-long!"
dotnet user-secrets set "AdminUser:Password"  "Admin@demo1!"
dotnet user-secrets set "McpUser:Password"    "Mcp@service1!"

# OrderDemo.Mcp
cd ../OrderDemo.Mcp
dotnet user-secrets set "ApiClient:Password"  "Mcp@service1!"
```

### 3. Configure Auth0

Auth0 brokers user login for the Vue dashboard and OAuth for the MCP server (Claude.ai Custom Connector). Set this up once in the [Auth0 dashboard](https://manage.auth0.com), then mirror the values into User Secrets and `.env.local`.

**In the Auth0 dashboard, create:**

| Resource | Type | Key settings |
|----------|------|--------------|
| `Order Demo API` | API | Identifier `https://api.orderdemo.dev`, RS256, permission `orders:read` |
| `Order Demo SPA` | Single Page Application | Allowed Callback / Logout / Web Origins: `http://localhost:5173` |
| `Order Demo MCP` | Regular Web Application | Allowed Callback URL: `https://<your-tunnel>.trycloudflare.com/callback` (update on each new tunnel) |
| `Order Demo MCP Management` | Machine to Machine | Authorise against the **Auth0 Management API** with `create:clients`, `delete:clients`, `read:clients` |
| Test user | Database connection | `Username-Password-Authentication` — used to log into the dashboard and Claude.ai connector |

On the API, enable **Allow Skipping User Consent** for development, and ensure the SPA is authorised with the `orders:read` scope.

**Then set the values:**

```bash
# OrderDemo.Api
cd OrderDemo/OrderDemo.Api
dotnet user-secrets set "Auth0:Domain"                  "<tenant>.us.auth0.com"
dotnet user-secrets set "Auth0:Audience"                "https://api.orderdemo.dev"

# OrderDemo.Mcp
cd ../OrderDemo.Mcp
dotnet user-secrets set "Auth0:Domain"                  "<tenant>.us.auth0.com"
dotnet user-secrets set "Auth0:Audience"                "https://api.orderdemo.dev"
dotnet user-secrets set "Auth0:Mcp:ClientId"            "<Order Demo MCP — Client ID>"
dotnet user-secrets set "Auth0:Mcp:ClientSecret"        "<Order Demo MCP — Client Secret>"
dotnet user-secrets set "Auth0:Management:ClientId"     "<MCP Management — Client ID>"
dotnet user-secrets set "Auth0:Management:ClientSecret" "<MCP Management — Client Secret>"
# Auth0:Mcp:CallbackUrl is set later, after starting cloudflared (see MCP section below)
```

```bash
# OrderDemo.Web — create OrderDemo/OrderDemo.Web/.env.local
VITE_AUTH0_DOMAIN=<tenant>.us.auth0.com
VITE_AUTH0_CLIENT_ID=<Order Demo SPA — Client ID>
VITE_AUTH0_AUDIENCE=https://api.orderdemo.dev
```

> Each time cloudflared restarts, the tunnel URL changes — update both the Allowed Callback URL on the `Order Demo MCP` Auth0 app and the `Auth0:Mcp:CallbackUrl` user secret.

## Quick start

```bash
cd OrderDemo/OrderDemo.AppHost
aspire start
```

`aspire start` boots all three services and the Aspire dashboard. The CLI prints a dashboard URL (with login token) — open it to see resources, logs, traces, and metrics.

| Resource | URL                            | Notes                                    |
|----------|--------------------------------|------------------------------------------|
| `api`    | http://localhost:5000          | REST API; `/scalar` for the API explorer |
| `mcp`    | http://localhost:5010          | MCP server (Streamable HTTP)             |
npm run dev
| `web`    | http://localhost:          | Vue 3 dashboard — exact port shown in the Aspire dashboard; proxies `/api/*` to the API |

`aspire stop` shuts everything down. `aspire describe` / `aspire logs <resource>` are useful while it's running.

### Without Aspire (fallback)

Run the services directly when debugging a single project or running without the Aspire CLI:

```bash
# API
cd OrderDemo/OrderDemo.Api
dotnet run
# → http://localhost:5000/scalar

# MCP server
cd OrderDemo/OrderDemo.Mcp
dotnet run
# → http://localhost:5010

# Vue frontend
cd OrderDemo/OrderDemo.Web
npm install
npm run dev
# → http://localhost:5173
```

`OrderDemo/run.sh` starts the Api and Mcp together in this mode (no dashboard, no telemetry).

## MCP (Claude.ai — Custom Connector)

Claude.ai connects to local MCP servers via a public tunnel. This demo uses [cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/) to create one.

### 1. Install cloudflared

**macOS (Homebrew):**
```bash
brew install cloudflared
```

**Direct download:** https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/


### 2. Start the tunnel

In a separate terminal:

```bash
cloudflared tunnel --url http://localhost:5010
```

cloudflared will print a public HTTPS URL, e.g.:
```
https://some-random-words.trycloudflare.com
```

Ensure this is add as a user secret for `OrderDemo.MCP`

```bash
dotnet user-secrets set "Auth0:Mcp:CallbackUrl"    "https://some-random-words.trycloudflare.com"
```

### 3. Start the servers

```bash
cd OrderDemo/OrderDemo.AppHost && aspire start
```


### 4. Add the Custom Connector in Claude.ai

1. Go to **Claude.ai → Settings → Connectors**
2. Click **Add custom connector**
3. Paste the tunnel URL from step 3
4. Save

The four tools (`search_orders`, `get_order_detail`, `get_order_stats`, `get_top_customers`) will be available in your Claude.ai conversations.

> The tunnel URL changes each time cloudflared restarts — update the connector URL when that happens.

## Sample prompt

Once the connector is active, try this in a Claude.ai conversation:

> You have access to the order-demo connector. Using it, please do the following:
>
> 1. Get overall order stats for the last 30 days — total orders, total revenue, and average order value.
> 2. Find the top 5 customers by spend over the same period. For the #1 customer, look up their most recent order by searching for their last name and show the full order detail.
> 3. Search for any orders placed in the last 7 days with a total over $500 — summarise how many there are and list the order numbers.
>
> Present the results as a brief report.

## Auth

| User    | Password         | Use              |
|---------|------------------|------------------|
| `admin` | `Admin@demo1!`   | API / Scalar UI  |
| `mcp`   | `Mcp@service1!`  | MCP server (auto)|

JWT tokens are valid for 8 hours. The MCP server authenticates automatically on startup.

These passwords are loaded from User Secrets — see [First-time setup](#first-time-setup).

## MCP Inspector

```bash
npx @modelcontextprotocol/inspector@latest
```

## Reset database

```bash
rm OrderDemo/OrderDemo.Api/orderdemo.db
```

Re-runs the seeder on next start: 1 000 customers, 20 products, 10 000 orders, both users.
