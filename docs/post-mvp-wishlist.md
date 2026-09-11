# Post-MVP wishlist

Tracked ideas that are out of scope for the current MVP but worth building next.

## Steam Web API onboarding guide

**Priority:** High (platform sync UX)

Create a dedicated in-app help page (e.g. `/help/steam-api`) that walks users through granting a Steam Web API key safely:

- Explain what the key is used for (read-only library + playtime sync)
- Step-by-step screenshots for [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey)
- Privacy settings checklist (Game details → Public)
- Security reassurance: key is encrypted at rest, never logged, revocable anytime
- FAQ: domain name field, key rotation, disconnecting Steam
- Link from the Steam connection panel on `/library`

**Acceptance:** A non-technical user can connect Steam without external docs.

## Platform API keys management (Steam Web API persistence)

**Priority:** High (platform sync UX)

Steam Web API keys should survive sign-out and re-sign-in — users should not need to re-enter their key every session. Today the key is stored encrypted in `platform_connections`, but disconnecting Steam or UX gaps may force re-entry; this item covers durable credential storage plus a dedicated management UI.

### Settings page: `/settings/integrations` (or `/settings/api-keys`)

- **List** connected platforms and stored credentials (masked key, e.g. `****…abcd`)
- **Add** — connect Steam (Steam ID / profile URL + Web API key)
- **Edit** — rotate or update an existing key without full disconnect/reconnect
- **Remove** — revoke a platform key or disconnect a platform (with confirmation)
- Future: PSN NPSSO token, other platform keys in the same pattern

### Backend / security

- Keys remain encrypted at rest via `ITokenEncryptionService` (ASP.NET Data Protection)
- Credentials are scoped to the authenticated user account in the database, not browser local storage
- Sign-out must **not** delete stored keys; only explicit remove/disconnect clears them
- Optional: `last_used_at` / `key_rotated_at` metadata for audit and rotation reminders

### UX notes

- Link from the Steam panel on `/library` → “Manage API keys”
- After sign-in, if a Steam connection exists, show connected state immediately without prompting for the key again
- Empty state when no keys: CTA to add Steam + link to the onboarding guide (see above)

**Acceptance:** User connects Steam once, signs out, signs back in, and can sync immediately without re-entering their Web API key. User can update or remove keys from settings without touching raw database or support.

## Platform sync progress UI

**Priority:** High (platform sync UX)

Steam (and future PSN) sync can take a long time for large libraries. Today progress is poorly visible — users see a spinner or a queued message with no sense of how far along the job is.

### User-facing progress

- Real-time sync status: `Fetching from Steam…` → `Matching games…` → `Updating library…`
- Progress bar or `42 / 380 games processed` with counts for added / updated / skipped / unmatched
- Poll sync job status (similar to text import jobs) when running via Hangfire; inline progress events for synchronous runs
- Allow navigating away; show toast or badge when sync completes
- Surface errors per game (e.g. unmatched titles) in an expandable summary, not only aggregate counts

### Performance (related)

- Batch DB writes where safe; avoid per-game round-trips to IGDB during sync
- Prefer incremental sync (`GetRecentlyPlayedGames`) for routine updates; full sync on first connect only

**Acceptance:** A user with 500+ Steam games sees continuous progress during sync and knows the job is still running or has finished without guessing.

## Local catalog first (shared game repository)

**Priority:** High (performance + metadata cost)

When resolving Steam AppIDs or titles during sync, import, or recommendations, **always check the local GoodPlays catalog first** before calling IGDB. If any user has already imported a game (by Steam external ID, IGDB ID, or normalized title), reuse that `games` row for all subsequent users.

### Resolution order (target)

1. `game_external_ids` where `source = Steam` and `external_id = appId`
2. Local title / slug match on `games` (normalized title)
3. `game_external_ids` where `source = Igdb` (if Steam→IGDB mapping already stored)
4. IGDB API lookup (only on cache miss)
5. Persist result locally so the next user hits step 1–3

### Benefits

- Fewer IGDB API calls and faster sync for popular titles
- Consistent metadata (covers, titles) across users
- Lower rate-limit risk as user base grows

**Acceptance:** Second user syncing the same Steam game never triggers an IGDB request if the first user already resolved it.

## Admin catalog browser

**Priority:** Medium (operations)

Admin-only page (e.g. `/admin/games` or `/admin/catalog`) showing all games in the shared repository pulled or created by any player.

### Features

- Paginated list of all `games` with title, cover, metadata status, external IDs (Steam, IGDB), created/updated timestamps
- Filter by source (`SteamSync`, `ImportText`, `Manual`, IGDB import, etc.) and `MetadataStatus`
- Search by title or external ID
- Optional: count of library entries referencing each game (how many users have it)
- Optional: link to IGDB / Steam store for verification
- Restricted to admin role (Clerk org role, allowlist, or env-configured admin user IDs)

**Acceptance:** An operator can inspect the full shared catalog, see which games were pulled via platform sync vs manual add, and diagnose duplicate or missing metadata without direct database access.

## Library sorting

**Priority:** Medium (library UX)

Let users sort their library list by:

- Title (A–Z / Z–A)
- Hours played
- Recently updated / added
- Status (Playing, Completed, Backlog, etc.)

Persist sort preference per user (local storage or profile setting).

## Library title search

**Priority:** Medium (library UX)

Add a search/filter box on `/library` to find games by title within the user's collection (client-side filter for MVP-scale libraries; server-side query when libraries grow large).

## Recommend something similar

**Priority:** Medium (discovery)

Per-game action: **Recommend something similar to…** that seeds the research recommendation engine from a chosen library title (instead of the whole library). Surface results on home or in a slide-over panel with one-click add to library.
