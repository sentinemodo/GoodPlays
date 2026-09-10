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
