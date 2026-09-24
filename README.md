# GoodPlays

Goodreads-for-games — personal game library, ratings, and discovery.

Architecture docs live at [`Architectures/game-library-platform/`](../../Architectures/game-library-platform/overview.md) (relative to this repo in the workspace).

## Phase 0 scope (complete)

Core library loop:

- ASP.NET Core 9 API with Clerk JWT + webhook user sync, Hangfire + Redis wiring, Serilog
- IGDB metadata gateway with local catalog fallback (`GET /api/v1/games/search`, `POST /api/v1/games`)
- Library CRUD scoped to authenticated user (`GET/POST/PUT /api/v1/library`)
- EF Core 9 schema for core catalog + library tables (Phase 1 social/import stubs included)
- React 19 + Vite SPA with Clerk shell, game search + manual add UI on `/library`
- Docker Compose for local Postgres 16 + Redis
- GitHub Actions CI for `dotnet` and `apps/web`

Not implemented yet: CSV/image imports, platform sync, ML.NET hybrid training.

## Public demo (GitHub Pages)

The web app deploys automatically on push to `main`:

**https://sentinemodo.github.io/GoodPlays/**

Use this URL when registering a Twitch/IGDB application (company website, privacy policy at `/privacy`).

### One-time repo setup

1. GitHub → **Settings** → **Pages** → Source: **GitHub Actions**
2. Optional repository **Variables**: `VITE_API_URL` (public API base URL when deployed)
3. Repository **Secret** (Settings → Secrets → Actions): `VITE_CLERK_PUBLISHABLE_KEY` (enables sign-in on Pages)

Local builds keep `VITE_BASE_PATH` unset (served from `/`). The Pages workflow sets `VITE_BASE_PATH=/GoodPlays/`.

## Prerequisites

- .NET 9 SDK
- Node.js 22+
- Docker (optional, for Postgres/Redis)

## Local development

1. Copy environment template:

   ```bash
   cp .env.example .env
   cp .env.example apps/web/.env
   ```

2. Start infrastructure:

   ```bash
   docker compose up -d
   ```

3. Apply database migrations:

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/GoodPlays.Infrastructure --startup-project src/GoodPlays.Api
   ```

4. Run API:

   ```bash
   dotnet run --project src/GoodPlays.Api
   ```

   Swagger: http://localhost:5280/swagger  
   Health: http://localhost:5280/health  
   Hangfire (dev + Redis): http://localhost:5280/hangfire

5. Run web app:

   ```bash
   cd apps/web
   npm install
   npm run dev
   ```

   App: http://localhost:5180

## Build & test

```bash
dotnet build
dotnet test
cd apps/web && npm ci && npm run build && npm run lint
```

## Configuration

See [`.env.example`](.env.example) and [`dependencies/repo-map.md`](../../Architectures/game-library-platform/dependencies/repo-map.md) for service secrets (Clerk, Neon, Upstash, IGDB, etc.).

## Solution layout

```
GoodPlays.sln
src/GoodPlays.Api/            # Web API, Hangfire, auth
src/GoodPlays.Domain/         # Entities, enums
src/GoodPlays.Infrastructure/ # EF Core, migrations
src/GoodPlays.Ml/             # ML.NET stub
apps/web/                     # React + Vite SPA
tests/GoodPlays.Tests/        # xUnit tests
```

## Clerk configuration

Your instance: `https://apt-cat-8367.clerk.accounts.dev` (from publishable key `pk_test_...`).

| Variable | File | Purpose |
|----------|------|---------|
| `VITE_CLERK_PUBLISHABLE_KEY` | `apps/web/.env` | Frontend sign-in |
| `Clerk__Authority` | root `.env` (export before `dotnet run`) | API JWT validation |
| `Clerk__WebhookSecret` | root `.env` | Webhook signature verification |

### Allowed origins (Clerk Dashboard)

There is no separate **Allowed origins** menu in current Clerk UI.

- **Local dev** (`http://localhost:5180`) — works automatically on Development instances.
- **GitHub Pages** — **Configure → Domains → Add domain** → `sentinemodo.github.io`

### Webhook (local, via ngrok)

1. Start API: `dotnet run --project src/GoodPlays.Api`
2. In another terminal: `ngrok http 5280`
3. Clerk Dashboard → **Configure → Webhooks → Add endpoint**  
   URL: `https://YOUR-NGROK-ID.ngrok-free.app/api/v1/webhooks/clerk`  
   Events: `user.created`, `user.updated`, `user.deleted`
4. Copy signing secret → `Clerk__WebhookSecret` in root `.env`

First-time ngrok: sign up at [ngrok.com](https://ngrok.com), then `ngrok config add-authtoken YOUR_TOKEN`.

Without Clerk configured, Development mode uses a local dev user for library endpoints.

### Load root `.env` for API (PowerShell)

```powershell
Get-Content .env | ForEach-Object {
  if ($_ -match '^\s*([^#=]+)=(.*)$') { Set-Item -Path "Env:$($matches[1].Trim())" -Value $matches[2].Trim() }
}
dotnet run --project src/GoodPlays.Api
```

## Phase 1 scope (complete)

Import and discovery:

- Text import pipeline (`POST /api/v1/imports`) with Hangfire `ImportParseText` job (inline fallback without Redis)
- Import status polling (`GET /api/v1/imports`, `GET /api/v1/imports/{id}`)
- Research recommendations via LLM + catalog fallback (`GET /api/v1/recommendations`)
- Web UI: paste-to-import on `/library`, recommendations on home when signed in

Set `LLM__ApiKey` in root `.env` for AI-powered suggestions (OpenAI-compatible; defaults to `gpt-4o-mini`).

## Phase 2 scope (Railway + Neon + Upstash)

Deploy the API to Railway with managed Postgres (Neon) and Redis (Upstash) so GitHub Pages can call a public backend.

### What was added

- `Dockerfile` + `railway.toml` — container build and health check for Railway
- Cloud connection resolver — accepts `DATABASE_URL` (Neon) and `REDIS_URL` (Upstash `rediss://`) in addition to explicit connection strings
- Production CORS — allows `https://sentinemodo.github.io` (GitHub Pages)
- Auto-migrations on deploy when `RunDbMigrations=true` (set in `appsettings.Production.json`)

### One-time Railway setup

1. **Neon** — [neon.tech](https://neon.tech) → create project → copy connection string (pooled is fine)
2. **Upstash** — [upstash.com](https://upstash.com) → create Redis database → copy `rediss://` URL (needed for Hangfire import jobs)
3. **Railway** — [railway.app](https://railway.app) → **New Project** → **Deploy from GitHub repo** → select `GoodPlays`
4. Railway detects `railway.toml` and builds from the root `Dockerfile`

### Railway environment variables

Set these on the Railway service (Settings → Variables):

| Variable | Value |
|----------|-------|
| `DATABASE_URL` | Neon connection string (or use Railway Neon plugin) |
| `REDIS_URL` | Upstash `rediss://...` URL |
| `Clerk__Authority` | `https://apt-cat-8367.clerk.accounts.dev` |
| `Clerk__WebhookSecret` | From Clerk Dashboard → Webhooks |
| `IGDB__ClientId` | Twitch dev portal |
| `IGDB__ClientSecret` | Twitch dev portal |
| `LLM__ApiKey` | Optional — OpenAI key for recommendations |

Railway sets `PORT` and `ASPNETCORE_ENVIRONMENT=Production` automatically in the container.

### Wire GitHub Pages to the API

1. Copy the Railway public URL (e.g. `https://goodplays-api-production.up.railway.app`)
2. GitHub repo → **Settings → Secrets and variables → Actions → Variables**
3. Set `VITE_API_URL` to the Railway URL (no trailing slash)
4. Re-run the **Deploy GitHub Pages** workflow (or push to `main`)

### Clerk webhook (production)

Clerk Dashboard → **Configure → Webhooks → Add endpoint**:

- URL: `https://YOUR-RAILWAY-URL/api/v1/webhooks/clerk`
- Events: `user.created`, `user.updated`, `user.deleted`

Copy the signing secret → `Clerk__WebhookSecret` on Railway.

### Verify deployment

```bash
curl https://YOUR-RAILWAY-URL/health
```

Expect `"status":"Healthy"` when Postgres (and Redis, if configured) are reachable.

### Local vs cloud

| Concern | Local | Railway |
|---------|-------|---------|
| Postgres | Docker Compose (`DATABASE_URL` is ignored) | Neon `DATABASE_URL` |
| Redis | Docker Compose | Upstash `REDIS_URL` |
| Import jobs | Inline without Redis | Hangfire with Upstash |
| Frontend | `localhost:5180` | GitHub Pages + `VITE_API_URL` |

## Next steps for implementers (Phase 3+)

1. Object storage (R2) for CSV/image import modalities
2. ML.NET hybrid recommendation training (Phase 3)
