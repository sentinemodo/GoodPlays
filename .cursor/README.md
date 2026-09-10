# GoodPlays — Cursor agents & rules

Project-specific AI configuration for the GoodPlays monorepo.

## Agents (`.cursor/agents/`)

| Agent | Invoke | Role |
|-------|--------|------|
| **project-architect** | `/project-architect` | Strategic architecture; maintains `Architectures/game-library-platform/` |
| **cicd-release** | `/cicd-release` | CI/CD, Railway, GitHub Pages, versioning |
| **tdd** | `/tdd` | Test-first implementation (C# + TypeScript) |
| **website-developer** | `/website-developer` | React SPA features, API integration, UX |
| **website-tester** | `/website-tester` | Vitest, xUnit, Playwright — tests only |
| **machine-learning-expert** | `/machine-learning-expert` | ML.NET reco, research agent, enrichment (local) |

## Rules (`.cursor/rules/`)

| Rule | Scope |
|------|-------|
| `goodplays-core.mdc` | Always — monorepo conventions, phases, subagents |
| `csharp-tdd.mdc` | `**/*.cs` |
| `typescript-tdd.mdc` | `apps/web/**/*.{ts,tsx}` |
| `react-frontend.mdc` | `apps/web/**` |
| `ml-dotnet.mdc` | `src/GoodPlays.Ml/**` |
| `cicd-goodplays.mdc` | CI, Docker, Railway |

## Architecture source of truth

External (workspace): `Architectures/game-library-platform/`  
Locked decisions: `product-decisions.md`

## Typical flows

- **New feature:** `/project-architect` (if boundaries change) → `/tdd` or `/website-developer`
- **UI work:** `/website-developer` with `react-frontend` + `typescript-tdd` rules
- **Reco / ML:** `/machine-learning-expert` + `ml-dotnet` rule
- **Ship:** `/cicd-release` before merge to `main`
