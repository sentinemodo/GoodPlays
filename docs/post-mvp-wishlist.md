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
