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

# OrderDemo.Mcp
cd ../OrderDemo.Mcp
dotnet user-secrets set "Auth0:Domain"                  "<tenant>.us.auth0.com"
dotnet user-secrets set "Auth0:Audience"                "https://api.orderdemo.dev"
dotnet user-secrets set "Auth0:Mcp:ClientId"            "<Order Demo MCP — Client ID>"
dotnet user-secrets set "Auth0:Mcp:ClientSecret"        "<Order Demo MCP — Client Secret>"
dotnet user-secrets set "Auth0:Management:ClientId"     "<MCP Management — Client ID>"
dotnet user-secrets set "Auth0:Management:ClientSecret" "<MCP Management — Client Secret>"
```

### 3. Configure Auth0

Auth0 brokers user login for the Vue dashboard and the MCP server (Claude.ai Custom Connector). Set this up once in the [Auth0 dashboard](https://manage.auth0.com), then mirror the values into User Secrets and `.env.local`.

**In the Auth0 dashboard, create:**

| Resource | Type | Key settings |
|----------|------|--------------|
| `Order Demo API` | API | Identifier `https://api.orderdemo.dev`, RS256, permission `orders:read` |
| `Order Demo SPA` | Single Page Application | Allowed Callback / Logout / Web Origins: `http://localhost:5173` |
| `Order Demo MCP` | Regular Web Application | Allowed Callback URL: `https://claude.ai/api/mcp/auth_callback` |
| `Order Demo MCP Management` | Machine to Machine | Authorise against **Auth0 Management API** with `create:clients`, `delete:clients`, `read:clients` |
| Test user | Database connection | `Username-Password-Authentication` — used to log in via the dashboard and Claude.ai connector |

On the API, enable **Allow Skipping User Consent** for development, and ensure the SPA and MCP applications are authorised with the `orders:read` scope under **Application Access**.

**Then set the remaining values:**

```bash
# OrderDemo.Api
cd OrderDemo/OrderDemo.Api
dotnet user-secrets set "Auth0:Domain"    "<tenant>.us.auth0.com"
dotnet user-secrets set "Auth0:Audience" "https://api.orderdemo.dev"
```

```bash
# OrderDemo.Web — create OrderDemo/OrderDemo.Web/.env.local
VITE_AUTH0_DOMAIN=<tenant>.us.auth0.com
VITE_AUTH0_CLIENT_ID=<Order Demo SPA — Client ID>
VITE_AUTH0_AUDIENCE=https://api.orderdemo.dev
```

## Quick start

```bash
cd OrderDemo/OrderDemo.AppHost
aspire start
```

`aspire start` boots all three services and the Aspire dashboard. The CLI prints a dashboard URL (with login token) — open it to see resources, logs, traces, and metrics.

| Resource | URL | Notes |
|----------|-----|-------|
| `api` | http://localhost:5000 | REST API; `/scalar` for the API explorer |
| `mcp` | http://localhost:5010 | MCP server (Streamable HTTP) |
| `web` | http://localhost:5176 | Vue 3 dashboard — proxies `/api/*` to the API |

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

`OrderDemo/run.sh` starts the API and MCP together in this mode (no dashboard, no telemetry).

## MCP (Claude.ai — Custom Connector)

Claude.ai connects to local MCP servers via a public tunnel. This demo uses [cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/) to create one.

### 1. Install cloudflared

**macOS (Homebrew):**
```bash
brew install cloudflared
```

**Direct download:** https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/

### 2. Start the tunnel

Pick one of the two options below. The quick tunnel is zero-config but rotates the URL every restart; the named tunnel needs a (free) Cloudflare account but gives you a stable custom-domain URL.

#### Option A — Quick tunnel (no account, URL rotates each restart)

In a separate terminal:

```bash
cloudflared tunnel --url http://localhost:5010
```

cloudflared will print a public HTTPS URL, e.g.:
```
https://some-random-words.trycloudflare.com
```

Update the Custom Connector URL in Claude.ai Settings each time the tunnel URL changes.

#### Option B — Named tunnel with a custom domain (stable URL)

Requires a Cloudflare account with a domain already added to it. The tunnel UUID and credentials persist on disk, so the same hostname keeps working across restarts.

**One-time setup:**

```bash
# 1. Authenticate cloudflared with your Cloudflare account
cloudflared tunnel login

# 2. Create the tunnel — note the UUID printed in the output
cloudflared tunnel create order-demo-mcp
```

Create `~/.cloudflared/config.yml` (replace `<UUID>` and `mcp.yourdomain.com`):

```yaml
tunnel: order-demo-mcp
credentials-file: /Users/<you>/.cloudflared/<UUID>.json

ingress:
  - hostname: mcp.yourdomain.com
    service: http://localhost:5010
  - service: http_status:404
```

> The catch-all `http_status:404` rule at the end is required — cloudflared rejects configs without a default service.

Point a DNS record at the tunnel:

```bash
cloudflared tunnel route dns order-demo-mcp mcp.yourdomain.com
```

**Each session — start the named tunnel:**

```bash
cloudflared tunnel run order-demo-mcp
```

The MCP server is now reachable at `https://mcp.yourdomain.com` — same URL every time, no connector churn between sessions.

### 3. Start the servers

```bash
cd OrderDemo/OrderDemo.AppHost && aspire start
```

### 4. Add the Custom Connector in Claude.ai

1. Go to **Claude.ai → Settings → Connectors**
2. Click **Add custom connector**
3. Paste the tunnel URL (Option A) or your custom domain URL (Option B)
4. Complete the Auth0 login flow when prompted
5. Save

The tools (`search_orders`, `get_order_detail`, `get_order_stats`, `get_top_customers`) will be available in your Claude.ai conversations.

> If you used **Option A** (quick tunnel), the connector URL changes each time cloudflared restarts — update it in Claude.ai Settings. **Option B** (named tunnel) keeps the same URL across restarts.

## Sample prompt

Once the connector is active, try this in a Claude.ai conversation:

> Please do the following:
>
> 1. Get overall order stats for the last 30 days — total orders, total revenue, and average order value.
> 2. Find the top 5 customers by spend over the same period. For the #1 customer, look up their most recent order by searching for their last name and show the full order detail.
> 3. Search for any orders placed in the last 7 days with a total over $500 — summarise how many there are and list the order numbers.
>
> Present the results as a brief report.

## Auth

Authentication uses Auth0 for all user-facing surfaces (Vue dashboard and MCP connector). A local JWT backdoor is available for API testing via Scalar only.

| User | Password | Use |
|------|----------|-----|
| `admin` | `Admin@demo1!` | Scalar UI / API testing only |
| Auth0 test user | set in Auth0 dashboard | Vue dashboard + Claude.ai connector |

JWT backdoor tokens are valid for 8 hours. All other authentication is handled by Auth0.

These passwords are loaded from User Secrets — see [First-time setup](#first-time-setup).

## MCP Inspector

```bash
npx @modelcontextprotocol/inspector@latest
```

## Reset database

```bash
rm OrderDemo/OrderDemo.Api/orderdemo.db
```

Re-runs the seeder on next start: 1,000 customers, 20 products, 10,000 orders, admin user.