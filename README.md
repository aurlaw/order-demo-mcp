# Order Demo

Monorepo: .NET 10 Minimal API + MCP + Vue 3 dashboard + MCP server.

## Quick start

### All backend servers (recommended)

```bash
cd OrderDemo
./run.sh
```

Starts `OrderDemo.Api` (port 5000) and `OrderDemo.Mcp` (port 5010) in sequence. The MCP server waits for the API to be ready before starting. Press `Ctrl+C` to stop both.

### Individual services

```bash
# API
cd OrderDemo/OrderDemo.Api
dotnet run
# → http://localhost:5000/scalar

# MCP server
cd OrderDemo/OrderDemo.Mcp
dotnet run
# → http://localhost:5010/mcp

# Vue frontend
cd OrderDemo/OrderDemo.Web
npm install
npm run dev
# → http://localhost:5173
```

## MCP (Claude.ai — Custom Connector)

Claude.ai connects to local MCP servers via a public tunnel. This demo uses [cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/) to create one.

### 1. Install cloudflared

**macOS (Homebrew):**
```bash
brew install cloudflared
```

**Direct download:** https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/

### 2. Start the servers

```bash
cd OrderDemo && ./run.sh
```

### 3. Start the tunnel

In a separate terminal:

```bash
cloudflared tunnel --url http://localhost:5010
```

cloudflared will print a public HTTPS URL, e.g.:
```
https://some-random-words.trycloudflare.com
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

## Reset database

```bash
rm OrderDemo/OrderDemo.Api/orderdemo.db
```

Re-runs the seeder on next start: 1 000 customers, 20 products, 10 000 orders, both users.
