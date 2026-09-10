# Steam Integration Plan for GoodPlays

## Executive Summary
This document outlines the phased architecture and implementation plan for integrating Steam into GoodPlays. In contrast to PlayStation Network (which relies on unofficial reverse-engineered mobile endpoints), Valve provides an **official, public Web API** (`api.steampowered.com`). The integration enables users to link their Steam account (via SteamID64 / Steam Web API Key or OpenID), retrieve their full owned games library with exact playtime (`playtime_forever`), reconcile Steam titles with GoodPlays/IGDB, and keep their library updated through automated background synchronization.

---

## Technical Architecture & Selected Approach

### 1. Selected Approach: Direct Valve Web API (Native C# Implementation)
Based on official Valve documentation and open-source exporters (`steam-library-exporter`, SteamWorks Web API):
- **Mechanism**: Call official Steam Web API HTTP endpoints directly from C# via typed `HttpClient` in `GoodPlays.Infrastructure`.
- **Authentication Model**:
  - **User Web API Key + SteamID64**: The user inputs their SteamID64 (or custom vanity URL) and their Steam Web API Key (generated at `https://steamcommunity.com/dev/apikey`).
  - **Optional Public Mode**: For users with public game details on their Steam profile, a platform-level Steam Web API Key can query public libraries given only their `SteamID64` without requiring users to create their own API key. If the profile is private, user-specific key or OpenID/privacy setting update is required.
  - **Storage**: Keys/tokens are encrypted at rest using `ITokenEncryptionService` via ASP.NET Core Data Protection (`IDataProtectionProvider`) in `PlatformConnection.AccessTokenEnc`.
- **Why Native C# over Python CLI (`steam-library-exporter`)**:
  - Direct type safety, zero external runtime/Python dependencies in Docker containers.
  - Native integration with Hangfire job execution, EF Core persistence, and Polly resilience.

### 2. Protocol & Endpoint Specifications
- **Host**: `https://api.steampowered.com`
- **Step 1: Vanity URL Resolution (Optional Convenience)**
  - `GET /ISteamUser/ResolveVanityURL/v1/?key={key}&vanityurl={customUrl}`
  - Resolves custom profile URLs to 64-bit Steam ID (`steamid`).
- **Step 2: Profile & Player Summary**
  - `GET /ISteamUser/GetPlayerSummaries/v2/?key={key}&steamids={steamid}`
  - Returns `personaname` (display name), `avatarfull`, `profileurl`, and `communityvisibilitystate`.
- **Step 3: Owned Games & Playtime (Core)**
  - `GET /IPlayerService/GetOwnedGames/v1/?key={key}&steamid={steamid}&include_appinfo=1&include_played_free_games=1&format=json`
  - Returns array of games:
    - `appid`: Unique Steam Application ID (integer).
    - `name`: Game title string.
    - `playtime_forever`: Total play time in minutes (converted to decimal hours: `playtime_forever / 60.0m`).
    - `playtime_2weeks`: Playtime over last two weeks (optional velocity metric).
    - `rtime_last_played`: Unix epoch timestamp of last played session.
    - `img_icon_url`: Steam CDN app icon hash.
- **Step 4: Recently Played Games (Fast incremental sync)**
  - `GET /IPlayerService/GetRecentlyPlayedGames/v1/?key={key}&steamid={steamid}&count=0`
  - Returns titles launched in the past 2 weeks for lightweight delta syncs.
- **Step 5: Achievements & Player Stats (Optional Phase)**
  - `GET /ISteamUserStats/GetPlayerAchievements/v1/?key={key}&steamid={steamid}&appid={appid}`
  - `GET /ISteamUserStats/GetSchemaForGame/v2/?key={key}&appid={appid}`
  - Returns unlocked achievements, unlock timestamps, and completion rates.

---

## Phased Implementation Roadmap

### Phase 1: Security & Storage Foundation
**Objective**: Ensure secure storage of Steam credentials and verify database alignment.
- **Token Protection & Entity Configuration**:
  - Re-use `ITokenEncryptionService` (ASP.NET Core Data Protection) shared with the PSN implementation to encrypt user Steam Web API keys in `platform_connections.access_token_enc`.
  - Store SteamID64 in `platform_connections.external_account_id`.
  - Store Steam persona name in `platform_connections.display_name`.
- **Database & Domain Verification**:
  - Verify `PlatformConnectionPlatform.Steam` enum value exists.
  - Verify `HoursPlayedSource.Steam` exists.
  - Verify `LibraryEntrySource.SteamSync` exists.
  - Verify `ExternalIdSource.Steam` exists in `GameExternalId`.

### Phase 2: Native C# Steam Client (`GoodPlays.Infrastructure`)
**Objective**: Build a typed, resilient client for Valve's Web API.
- **Interfaces & DTOs**:
  - `ISteamClient`:
    - `Task<string?> ResolveVanityUrlAsync(string vanityOrUrl, string apiKey, CancellationToken ct)`
    - `Task<SteamPlayerSummary?> GetPlayerSummaryAsync(string steamId64, string apiKey, CancellationToken ct)`
    - `Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(string steamId64, string apiKey, CancellationToken ct)`
    - `Task<IReadOnlyList<SteamRecentlyPlayedGame>> GetRecentlyPlayedGamesAsync(string steamId64, string apiKey, CancellationToken ct)`
    - `Task<SteamPlayerAchievementsResult?> GetPlayerAchievementsAsync(string steamId64, uint appId, string apiKey, CancellationToken ct)`
- **Polly Resilience & Error Handling**:
  - Configure `HttpClient` with retry on HTTP 5xx and exponential backoff.
  - Handle common Steam API errors: `401 Unauthorized` (invalid API key), `403 Forbidden` (private profile game details), `429 Too Many Requests`.
- **Unit & Mock Tests**:
  - Unit tests verifying JSON deserialization, minutes-to-hours conversion, error code translations, and vanity URL parsing.

### Phase 3: Steam Synchronization Engine & Catalog Reconciliation
**Objective**: Match Steam `appid` and titles with GoodPlays database & IGDB catalog.
- **Reconciliation & External ID Mapping**:
  - **Direct Steam AppID Match**: Check `game_external_ids` where `source == ExternalIdSource.Steam && external_id == appId.ToString()`.
  - **IGDB Lookup by Steam AppID / External Category**: IGDB tracks Steam external IDs (`category = 1`); query IGDB for matching Steam AppID before falling back to title searches.
  - **Fuzzy Title Search**: If no direct AppID match, normalize game title (strip edition tags, soundtrack markers, "Demo") and query local DB / IGDB via `IGameCatalogService`.
  - **Persist Mapping**: Save `ExternalIdSource.Steam` mapping for newly resolved games to guarantee O(1) resolution in future sync cycles.
- **Library Updates**:
  - Create or update `LibraryEntry`:
    - Set `Source = LibraryEntrySource.SteamSync`.
    - Set `HoursPlayed = Math.Round(playtime_forever / 60.0m, 2)` (if `HoursPlayedLocked == false`).
    - Set `HoursPlayedSource = HoursPlayedSource.Steam`.
    - Set `StartedAt` / `CompletedAt` / `UpdatedAt` based on `rtime_last_played`.
    - If `playtime_forever > 0` and current status is `Wishlist` or `Backlog`, optionally transition to `Playing` or `Owned`.
- **Ambiguity & Discrepancy Reporting**:
  - Track imported, updated, skipped, and unmatched games in a sync result summary.

### Phase 4: API Endpoints & Hangfire Job Scheduling
**Objective**: Expose management endpoints and schedule background synchronization.
- **API Endpoints (`PlatformConnectionsController`)**:
  - `POST /api/v1/platforms/steam/connect`: Accepts `{ steamIdOrUrl, apiKey }`, resolves vanity URL if needed, validates key via `GetPlayerSummary`, and stores connection.
  - `GET /api/v1/platforms`: Returns connection statuses (Steam, PSN) with `displayName`, `lastSyncAt`, `syncEnabled`.
  - `POST /api/v1/platforms/steam/sync`: Enqueues an on-demand sync job in Hangfire.
  - `DELETE /api/v1/platforms/steam`: Disconnects account, revokes sync, and removes encrypted credentials.
- **Hangfire Job Architecture**:
  - `SteamSyncJob`:
    - Decrypts API key and retrieves `external_account_id` (SteamID64).
    - Fetches full owned games list (`GetOwnedGames`).
    - Runs catalog reconciliation and library upsert.
    - Updates `last_sync_at` timestamp.
  - `SteamRecentSyncJob`: Lightweight recurring job (e.g. daily) calling `GetRecentlyPlayedGames` for fast incremental updates without re-scanning thousands of untouched games.

### Phase 5: Web UI (React SPA)
**Objective**: User interface for connecting Steam and managing sync status.
- **Platform Connection Component (`/settings` or modal on `/library`)**:
  - Card for "Steam".
  - Input for Steam ID (supports 64-bit ID or profile URL / vanity name).
  - Optional / Required input for Steam Web API Key with direct link to `https://steamcommunity.com/dev/apikey`.
  - Privacy indicator reminder: note that "Game details" must be set to "Public" in Steam privacy settings if not using a personal key.
- **Connection Status & Controls**:
  - Connected badge displaying Steam persona avatar, name, and last sync timestamp.
  - "Sync Now" button with spinner.
  - Disconnect button with confirmation modal.
- **Library Tagging**:
  - Display Steam icon badge and playtime tag (`XX hrs (Steam)`) on library entries synced from Steam.

---

## Edge Cases, Risks & Mitigations

| Risk / Constraint | Detail | Mitigation |
|---|---|---|
| **Private Game Details Privacy Setting** | Even if a profile is public, Steam allows "Game details" to be set to Private or Friends Only. `GetOwnedGames` returns empty. | Detect empty results when profile exists; return user-friendly diagnostic message prompting user to set "Game details: Public" or verify their API key. |
| **SteamID vs Vanity URL** | Users frequently supply profile URLs (`https://steamcommunity.com/id/username`) or vanity names instead of numeric 64-bit SteamIDs. | Implement regex detection and automatic `ResolveVanityURL` resolution on connect. |
| **Free Games & DLCs in App List** | `GetOwnedGames` can return dedicated servers, DLCs, and free-to-play demos or tools that are not actual games. | Filter out known non-game categories and cross-reference with IGDB game type validation. |
| **API Rate Limits** | Steam Web API officially asks clients not to exceed 100,000 requests per day (approx. 1 request per second average). | Batch requests, cache metadata, throttle outgoing calls via Polly, and use `GetRecentlyPlayedGames` for frequent incremental syncs. |
| **Playtime Locked by User** | Users may have manually adjusted hours played on GoodPlays and locked the field. | Respect `library_entries.hours_played_locked`: do not overwrite `hours_played` when lock is enabled. |
| **Security of Web API Key** | The user's API key can read account stats and ban statuses. | Encrypt via ASP.NET Data Protection before persisting to database; never log keys in plaintext logs. |
