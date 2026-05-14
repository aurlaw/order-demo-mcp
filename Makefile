APPHOST_DIR := OrderDemo/OrderDemo.AppHost

.PHONY: up down tunnel aspire logs describe

## Start Aspire (detached) then open the Cloudflare tunnel in the foreground.
## Press Ctrl-C to stop the tunnel; run `make down` to tear down Aspire.
up:
	@echo "Starting Aspire..."
	cd $(APPHOST_DIR) && aspire start
	@echo "Starting Cloudflare tunnel (order-demo-mcp → order-demo-mcp.aurlaw.dev)..."
	cloudflared tunnel run order-demo-mcp

## Tear down Aspire.
down:
	cd $(APPHOST_DIR) && aspire stop

## Start only the Cloudflare tunnel (useful when Aspire is already running).
tunnel:
	cloudflared tunnel run order-demo-mcp

## Start only Aspire (detached).
aspire:
	cd $(APPHOST_DIR) && aspire start

## Tail logs for a resource: make logs r=api  (api | mcp | web)
logs:
	cd $(APPHOST_DIR) && aspire logs $(r)

## Show resource states and URLs.
describe:
	cd $(APPHOST_DIR) && aspire describe
