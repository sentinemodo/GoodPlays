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
