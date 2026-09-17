FROM node:24-bookworm-slim
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
ENV XDG_CACHE_HOME=/tmp/.cache
WORKDIR /app
COPY deploy/article-reader/package*.json ./
RUN npm ci --omit=dev && npx playwright install --with-deps chromium
COPY deploy/article-reader/mcp.config.json /app/mcp.config.json
USER node
WORKDIR /tmp
ENTRYPOINT ["node", "/app/node_modules/@playwright/mcp/cli.js", "--config", "/app/mcp.config.json"]
