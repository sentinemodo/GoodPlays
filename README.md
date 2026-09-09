# GoodPlays

Goodreads-for-games — personal game library, ratings, and discovery.

Architecture docs live at [`Architectures/game-library-platform/`](../../Architectures/game-library-platform/overview.md) (relative to this repo in the workspace).

## Phase 0 scope

Runnable skeleton only:

- ASP.NET Core 9 API with stub controllers, Clerk JWT hook, Hangfire + Redis wiring, Serilog
- EF Core 9 schema for core catalog + library tables (Phase 1 social/import stubs included)
- React 19 + Vite SPA with Clerk shell, React Router, TanStack Query
- Docker Compose for local Postgres 16 + Redis
- GitHub Actions CI for `dotnet` and `apps/web`

Not implemented yet: IGDB search, manual add flow, import pipeline, research recommendations.

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

   App: http://localhost:5173

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

## Next steps for implementers

1. Wire Clerk JWT authority + user sync webhook → `users` table
2. Implement IGDB metadata gateway + manual add/search (Phase 0 exit criteria)
3. Flesh out library POST/PUT endpoints with auth context
4. Connect Neon/Upstash in Railway for preview deploys
