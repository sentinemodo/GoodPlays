# PlayStation Network (PSN) Integration Plan for GoodPlays

## Executive Summary
This document outlines the phased architecture and implementation plan for integrating PlayStation Network (PSN) into GoodPlays. The integration enables users to connect their PlayStation account via an NPSSO token, automatically import their played game history and playtime into their GoodPlays library, and keep their library updated through background synchronization.

---

## Technical Architecture & Selected Approach

### 1. Selected Approach: Direct PSN Mobile Gateway (Native C# Implementation)
Based on research evaluating community tools (`awesome-psnstats`, `psn-game-scrapper`, `psnawp`, `psn-api`):
- **Mechanism**: Use the reverse-engineered PlayStation App OAuth & GameList endpoints directly in C# using ASP.NET Core typed `HttpClient`.
- **Why Native C# over Python Subprocess**:
  - Eliminates secondary runtime environments (Python/Node) in production Docker containers.
  - Full type safety, resilience (Polly retry/rate-limiting), and direct integration with EF Core and Hangfire.
  - Lower operational overhead and memory footprint.

### 2. Protocol Details
- **Step 1: NPSSO to Authorization Code**
  - `GET https://ca.account.sony.com/api/authz/v3/oauth/authorize`
  - Cookie: `npsso={token}`
  - Redirect target gives `code` parameter.
- **Step 2: Code to Access & Refresh Tokens**
  - `POST https://ca.account.sony.com/api/authz/v3/oauth/token`
  - Returns `access_token` (~1 hour), `refresh_token` (~60 days), `expires_in`.
- **Step 3: Account Profile & Identification**
  - `GET https://api.playstation.com/userProfile/v1/internal/users/me/profile`
  - Retrieves `accountId` and `onlineId` (PSN gamertag).
- **Step 4: Title Stats & Playtime Query**
  - `GET https://m.np.playstation.com/api/gamelist/v2/users/{accountId}/titles?limit=200&offset={offset}`
  - Parses ISO 8601 `playDuration` (e.g. `PT14H23M`) to decimal hours.
  - Captures `lastPlayedDateTime`, `firstPlayedDateTime`, `playCount`, and platform tag (`PS4`, `PS5`).

---

## Phased Implementation Roadmap

### Phase 1: Security & Encryption Foundation
**Objective**: Secure credential and token storage for third-party platforms.
- **Token Protection Service**:
  - Implement `ITokenEncryptionService` using ASP.NET Core Data Protection (`IDataProtectionProvider`) to encrypt `AccessToken` and `RefreshToken` before storing in `platform_connections.access_token_enc` and `refresh_token_enc`.
- **Database Alignment**:
  - Verify `PlatformConnection` EF Core configuration.
  - Add enum extension values if needed (`ExternalIdSource.Psn`).

### Phase 2: Native C# PSN Client (`GoodPlays.Infrastructure`)
**Objective**: Build a typed, resilient client for Sony's authentication and data endpoints.
- **Interfaces & DTOs**:
  - `IPsnClient`:
    - `Task<PsnTokens> ExchangeNpssoAsync(string npsso, CancellationToken ct)`
    - `Task<PsnTokens> RefreshTokenAsync(string refreshToken, CancellationToken ct)`
    - `Task<PsnUserProfile> GetProfileAsync(string accessToken, CancellationToken ct)`
    - `Task<IReadOnlyList<PsnTitleStat>> GetPlayedTitlesAsync(string accessToken, string accountId, CancellationToken ct)`
- **Polly Resilience & Rate Limiting**:
  - Configure `HttpClient` with client-side rate limiter (e.g., max 200 requests / 15 minutes) to avoid Sony 429 / IP throttling.
- **Unit & Mock Tests**:
  - Add test suite covering token parsing, error handling (expired NPSSO, 401s), and ISO 8601 playtime conversion.

### Phase 3: PSN Synchronization Engine & Catalog Reconciliation
**Objective**: Reconcile PSN titles with GoodPlays database & IGDB catalog.
- **Matching Pipeline**:
  - Normalize PSN title names (strip "™", "®", edition suffixes, PS4/PS5 indicators).
  - Search GoodPlays database first; if missing, query IGDB using `IIgdbClient`.
  - Save PSN external IDs (`ExternalIdSource.Psn`) to prevent future fuzzy lookups.
- **Library Updates**:
  - Create or update `LibraryEntry`:
    - Set `Source = LibraryEntrySource.PsnSync`.
    - Set `HoursPlayed = parsedHours` (if `HoursPlayedLocked == false`).
    - Set `HoursPlayedSource = HoursPlayedSource.Psn`.
    - Update `LastPlayedAt` / `StartedAt` / `CompletedAt` when applicable.
- **Handling Ambiguity**:
  - Log unmatched or ambiguous games into a synchronization report (similar to `ImportStatsDocument`).

### Phase 4: API Endpoints & Hangfire Job Scheduling
**Objective**: Provide user-facing API routes for connecting, disconnecting, and syncing PSN.
- **Endpoints (`PlatformConnectionsController`)**:
  - `POST /api/v1/platforms/psn/connect`: Accepts `{ npsso }`, exchanges tokens, fetches profile, stores connection.
  - `GET /api/v1/platforms`: Returns connection statuses (PSN, Steam) with `displayName`, `lastSyncAt`, `syncEnabled`.
  - `POST /api/v1/platforms/psn/sync`: Enqueues an on-demand sync job.
  - `DELETE /api/v1/platforms/psn`: Disconnects account and purges tokens.
- **Hangfire Sync Job**:
  - `PsnSyncJob`: Background worker that checks token expiry, refreshes token using `RefreshTokenEnc`, queries PSN API, and runs the reconciliation pipeline.
  - Add recurring job for daily/weekly automated synchronization.

### Phase 5: Web UI (React SPA)
**Objective**: User interface for managing PlayStation connection and sync progress.
- **Settings / Platform Connection Page (`/settings` or modal on `/library`)**:
  - Card for "PlayStation Network".
  - Step-by-step guidance showing how users can obtain their `npsso` code from `ca.account.sony.com/api/v1/ssocookie`.
  - Input field with password masking for submitting NPSSO.
- **Status & Sync Controls**:
  - Connected badge displaying PSN Online ID / GamerTag and Last Sync timestamp.
  - "Sync Now" button with progress indicator.
  - Disconnect button.
- **Library Tagging**:
  - Display PlayStation icon / badge and synced playtime tag on games imported via PSN.

---

## Edge Cases, Risks & Mitigations

| Risk / Constraint | Detail | Mitigation |
|---|---|---|
| **NPSSO Expiration** | NPSSO tokens expire in ~60 days, and refresh tokens expire or get invalidated if user logs out on browser. | Gracefully flag connection as `SyncRequiresReauth`. Notify user in UI to renew token without breaking existing library records. |
| **Playtime Availability** | Sony's API only exposes playtime for PS4 and PS5 native titles, not PS3/Vita. | Fall back to library entitlement presence (flag as `Owned` without playtime) for older legacy purchases if entitlements endpoint is queried. |
| **Account Banning Risk** | Unofficial API reverse-engineered from mobile app. | Adhere to Sony mobile app rate limits (client throttled to max 25 req/min, batch requests, staggered Hangfire jobs). Avoid excessive polling. |
| **Title Name Ambiguity** | PSN game titles often include platform qualifiers (e.g. `God of War (PS4)` vs `God of War`). | Implement title normalization rules and fallback to user review if IGDB match confidence is low. |
| **Security of Tokens** | NPSSO and OAuth tokens provide account read access. | Never log raw tokens in Serilog. Store tokens encrypted via ASP.NET Data Protection (`IDataProtectionProvider`). |
