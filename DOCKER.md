# Mewdeko Docker image

`sylveondeko/mewdeko` runs the Mewdeko bot. It does not contain a Discord token, database password, API key, or any other credentials. Supply a `credentials.json` file when starting the container.

## Tags

- `nightly`: newest successful build from `main`
- `sha-<commit>`: immutable build from a specific commit
- `latest` and a version tag: published for a versioned release

The image supports `linux/amd64` and `linux/arm64`.

## Quick start with Docker Compose

This is the recommended setup. It starts PostgreSQL, Redis, the bot, and the optional dashboard on one private Docker network.

```bash
curl -O https://raw.githubusercontent.com/SylveonDeko/Mewdeko/main/docker-compose.example.yml
curl -O https://raw.githubusercontent.com/SylveonDeko/Mewdeko/main/credentials.docker.example.json
curl -O https://raw.githubusercontent.com/SylveonDeko/Mewdeko/main/dashboard.docker.env.example

mv credentials.docker.example.json credentials.json
mv dashboard.docker.env.example dashboard.env
mv docker-compose.example.yml compose.yaml
```

Edit `credentials.json`, `dashboard.env`, and the PostgreSQL password in `compose.yaml`. Then start the stack:

```bash
docker compose up -d
docker compose logs -f mewdeko
```

The dashboard is available at `http://localhost:3000`. For production, place it behind an HTTPS reverse proxy and register that public URL as a Discord OAuth redirect.

## Required bot credentials

| `credentials.json` key | Purpose |
| --- | --- |
| `Token` | Discord bot token. Never commit or publish it. |
| `OwnerIds` | Discord user IDs allowed to use owner-only commands. |
| `PsqlConnectionString` | PostgreSQL connection string. The Compose template uses the `postgres` service hostname. |
| `RedisConnections` | Redis address. The Compose template uses `redis:6379`. |
| `IsApiEnabled` | Must be `true` when using the dashboard. |
| `ApiPort` | Bot HTTP API port; use `5001` unless deliberately changing it everywhere. |
| `ApiKey` | Shared secret for dashboard-to-bot API calls. Must equal dashboard `MEWDEKO_API_KEY`. |
| `JwtSecret` | Shared secret for dashboard user JWTs. Must equal dashboard `BOT_JWT_SECRET`. |

Generate independent secrets with `openssl rand -hex 32`.

## Bot-only deployment

If PostgreSQL and Redis already exist, create `credentials.json` from [`credentials_example.json`](src/Mewdeko/credentials_example.json), point it at those services, and run:

```bash
docker run -d \
  --name mewdeko \
  --restart unless-stopped \
  -v "$PWD/credentials.json:/app/credentials.json:ro" \
  -v mewdeko-data:/app/data \
  -v mewdeko-logs:/app/logs \
  sylveondeko/mewdeko:nightly
```

Only publish port `5001` when another service outside Docker needs the bot API. Keep it private when the dashboard runs on the same Docker network.

## Dashboard

The companion image is [`sylveondeko/mewdash`](https://hub.docker.com/r/sylveondeko/mewdash). Its required environment variables and standalone launch command are documented in the [dashboard Docker guide](https://github.com/SylveonDeko/MewdekoDash/blob/main/DOCKER.md).
