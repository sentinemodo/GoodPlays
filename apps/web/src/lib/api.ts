import type { CatalogFacets, CatalogFilters, PaginatedCatalog } from './catalogTypes'
import { buildCatalogQuery } from './catalogTypes'

const apiBaseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5280'

export type GetToken = () => Promise<string | null>

let getToken: GetToken = async () => null

export function setApiTokenProvider(provider: GetToken) {
  getToken = provider
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = await getToken()
  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(init?.headers ?? {}),
      },
    })
  } catch {
    throw new Error(
      `Cannot reach the API at ${apiBaseUrl}. Start it with: dotnet run --project src/GoodPlays.Api`,
    )
  }

  if (!response.ok) {
    let message =
      response.status === 401
        ? 'Sign in required, or your session expired.'
        : `API request failed: ${response.status}`
    try {
      const body = (await response.json()) as { message?: string }
      if (body.message) {
        message = body.message
      }
    } catch {
      // ignore non-JSON error bodies
    }
    throw new Error(message)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

export type LibraryEntrySource =
  | 'Manual'
  | 'ImportText'
  | 'ImportImage'
  | 'ImportCsv'
  | 'ResearchReco'
  | 'SteamSync'
  | 'PsnSync'

export type LibraryEntrySummary = {
  id: string
  gameId: string
  gameTitle: string
  coverUrl: string | null
  status: string
  rating: number | null
  hoursPlayed: number | null
  source: LibraryEntrySource
  updatedAt: string
}

export type GameSearchResult = {
  id: string
  title: string
  slug: string
  coverUrl: string | null
  igdbId: number | null
  source: 'local' | 'igdb'
}

export type ImportedGame = {
  id: string
  title: string
  slug: string
  coverUrl: string | null
}

export type ImportLineSummary = {
  rawLine: string
  outcome: string
  matchedTitle: string | null
  gameId: string | null
  igdbId: number | null
}

export type ImportJobSummary = {
  id: string
  modality: string
  status: string
  stats: {
    addedCount: number
    skippedCount: number
    unmatchedCount: number
    ambiguousCount: number
    lines: ImportLineSummary[]
  }
  createdAt: string
  updatedAt: string
}

export type RecommendationSummary = {
  gameId: string
  title: string
  coverUrl: string | null
  score: number
  reason: string | null
}

export type PlatformConnectionSummary = {
  platform: 'Steam' | 'Psn' | 'Xbox' | 'Switch'
  externalAccountId: string
  displayName: string | null
  lastSyncAt: string | null
  syncEnabled: boolean
  connectedAt: string
}

export type PlatformSyncResult =
  | {
      queued: true
      message: string
    }
  | {
      queued?: false
      addedCount: number
      updatedCount: number
      skippedCount: number
      unmatchedCount: number
      syncedAt: string
      warning: string | null
    }

export type SteamSyncResult = PlatformSyncResult

export type PsnSyncResult = PlatformSyncResult

export type XboxSyncResult = PlatformSyncResult

export type SwitchSyncResult = PlatformSyncResult

export type XboxLogin = { url: string }

export type SwitchLogin = { url: string; codeVerifier: string }

export type ActivityEntry = {
  id: string
  category: string
  message: string
  createdAt: string
}

export type AdminLogEntry = ActivityEntry & {
  userId: string | null
  userEmail: string | null
  username: string | null
}

export type AdminLogPage = {
  items: AdminLogEntry[]
  total: number
  categories: string[]
}

export type AdminLogQuery = {
  category?: string
  from?: string
  to?: string
  q?: string
  nickname?: string
  limit?: number
  offset?: number
}

export type PlatformSyncRun = {
  id: string
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Stopped'
  phase: string
  processedCount: number
  totalCount: number
  addedCount: number
  updatedCount: number
  skippedCount: number
  unmatchedCount: number
  warning: string | null
  errorMessage: string | null
  createdAt: string
  updatedAt: string
}

export type UpdateLibraryEntryRequest = {
  status?: string
  rating?: number | null
  hoursPlayed?: number | null
}

export type GameDetail = {
  id: string
  title: string
  slug: string
  summary: string | null
  coverUrl: string | null
  releaseDate: string | null
  developer: string | null
  publisher: string | null
  gameType: string
  genres: string[]
  platforms: string[]
  dlc: ImportedGame[]
  stats: {
    totalPlayers: number
    activePlayersLast30Days: number
    totalHours: number
    avgRating: number | null
    medianRating: number | null
    completionRate: number
    backlogCount: number
  }
  ratings: {
    source: string
    score: number | null
    reviewCount: number | null
    url: string | null
    fetchedAt: string
  }[]
  heroes: { username: string; displayName: string | null; value: number; metric: string }[]
  achievements: {
    id: string
    name: string
    description: string | null
    iconUrl: string | null
    rarityPercent: number | null
    unlockedByCurrentUser: boolean
    owners: { username: string; displayName: string | null; unlockedAt: string }[]
  }[]
  news: { id: string; source: string; title: string; url: string; publishedAt: string | null }[]
  comments: {
    id: string
    username: string
    displayName: string | null
    body: string
    rating: number | null
    createdAt: string
  }[]
  userLibraryEntry: LibraryEntrySummary | null
  userTags: string[]
}

export type TagSummary = { id: string; name: string; slug: string; scope: string }
export type ShelfSummary = { id: string; name: string; slug: string; gameCount: number }

export type ProfileStat = { key: string; label: string; gameCount: number; hours: number }

export type ProfileGame = {
  gameId: string
  title: string
  slug: string
  coverUrl: string | null
  hoursPlayed: number | null
  lastPlayed: string | null
  isLoved: boolean
}

export type TrophyClass = 'Platinum' | 'Gold' | 'Silver' | 'Bronze'

export type ProfileTrophy = {
  achievementId: string
  gameId: string
  gameTitle: string
  gameSlug: string
  name: string
  description: string | null
  iconUrl: string | null
  rarityPercent: number | null
  trophyClass: TrophyClass
  unlockedAt: string
  isFeatured: boolean
}

export type PlayerProfile = {
  username: string
  displayName: string | null
  bio: string | null
  avatarUrl: string | null
  mostLovedGame: ProfileGame | null
  mostLovedIsStarred: boolean
  platforms: ProfileStat[]
  categories: ProfileStat[]
  topGamesByTime: ProfileGame[]
  lastPlayedGames: ProfileGame[]
  mostImportantTrophy: ProfileTrophy | null
  mostRecentTrophy: ProfileTrophy | null
  trophies: ProfileTrophy[]
  trophyGames: { gameId: string; title: string }[]
}

export const api = {
  getLibrary: () => request<LibraryEntrySummary[]>('/api/v1/library'),
  browseCatalog: (filters: CatalogFilters) =>
    request<PaginatedCatalog>(`/api/v1/catalog/games?${buildCatalogQuery(filters)}`),
  getCatalogFacets: () => request<CatalogFacets>('/api/v1/catalog/facets'),
  getGame: (slug: string) => request<GameDetail>(`/api/v1/games/${encodeURIComponent(slug)}`),
  updateLibraryEntry: (entryId: string, body: UpdateLibraryEntryRequest) =>
    request<LibraryEntrySummary>(`/api/v1/library/${entryId}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  createComment: (slug: string, body: { body: string; rating?: number | null }) =>
    request<GameDetail['comments'][0]>(`/api/v1/games/${encodeURIComponent(slug)}/comments`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  getTags: () => request<TagSummary[]>('/api/v1/tags'),
  createTag: (name: string) =>
    request<TagSummary>('/api/v1/tags', { method: 'POST', body: JSON.stringify({ name }) }),
  tagGame: (tagSlug: string, gameId: string) =>
    request<void>(`/api/v1/tags/${encodeURIComponent(tagSlug)}/games/${gameId}`, { method: 'POST' }),
  untagGame: (tagSlug: string, gameId: string) =>
    request<void>(`/api/v1/tags/${encodeURIComponent(tagSlug)}/games/${gameId}`, { method: 'DELETE' }),
  getShelves: () => request<ShelfSummary[]>('/api/v1/shelves'),
  createShelf: (name: string) =>
    request<ShelfSummary>('/api/v1/shelves', { method: 'POST', body: JSON.stringify({ name }) }),
  searchGames: (query: string) =>
    request<GameSearchResult[]>(`/api/v1/games/search?q=${encodeURIComponent(query)}`),
  importGame: (igdbId: number) =>
    request<ImportedGame>('/api/v1/games', {
      method: 'POST',
      body: JSON.stringify({ igdbId }),
    }),
  addToLibrary: (gameId: string, status = 'Owned') =>
    request<LibraryEntrySummary>('/api/v1/library', {
      method: 'POST',
      body: JSON.stringify({ gameId, status }),
    }),
  removeFromLibrary: (entryId: string) =>
    request<void>(`/api/v1/library/${entryId}`, {
      method: 'DELETE',
    }),
  setLovedGame: (entryId: string, loved: boolean) =>
    request<void>(`/api/v1/library/${entryId}/loved`, {
      method: 'PUT',
      body: JSON.stringify({ loved }),
    }),
  getProfile: (filters?: { gameId?: string; trophyClass?: string; maxRarity?: number }) => {
    const params = new URLSearchParams()
    if (filters?.gameId) params.set('gameId', filters.gameId)
    if (filters?.trophyClass) params.set('trophyClass', filters.trophyClass)
    if (filters?.maxRarity != null) params.set('maxRarity', String(filters.maxRarity))
    const query = params.toString()
    return request<PlayerProfile>(`/api/v1/profile${query ? `?${query}` : ''}`)
  },
  updateProfile: (body: { bio?: string | null; avatarUrl?: string | null; username?: string | null }) =>
    request<PlayerProfile>('/api/v1/profile', {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  setFeaturedTrophy: (achievementId: string, featured: boolean) =>
    request<void>(`/api/v1/profile/trophies/${achievementId}/featured`, {
      method: 'PUT',
      body: JSON.stringify({ featured }),
    }),
  getRecommendations: () => request<RecommendationSummary[]>('/api/v1/recommendations'),
  createTextImport: (text: string) =>
    request<ImportJobSummary>('/api/v1/imports', {
      method: 'POST',
      body: JSON.stringify({ modality: 'Text', text }),
    }),
  getImport: (jobId: string) => request<ImportJobSummary>(`/api/v1/imports/${jobId}`),
  getPlatformConnections: () => request<PlatformConnectionSummary[]>('/api/v1/platforms'),
  startSyncAll: () =>
    request<PlatformSyncRun>('/api/v1/platforms/sync-all', {
      method: 'POST',
    }),
  getCurrentSyncAll: () => request<PlatformSyncRun | undefined>('/api/v1/platforms/sync-all/current'),
  stopSyncAll: () =>
    request<PlatformSyncRun>('/api/v1/platforms/sync-all/stop', {
      method: 'POST',
    }),
  syncLibraryEntry: (entryId: string) =>
    request<void>(`/api/v1/library/${entryId}/sync`, {
      method: 'POST',
    }),
  getActivity: () => request<ActivityEntry[]>('/api/v1/activity'),
  getAdminSession: () => request<{ email: string; isAdmin: boolean }>('/api/v1/admin/logs/session'),
  syncUserByNickname: (nickname: string) =>
    request<PlatformSyncRun>('/api/v1/admin/logs/sync', {
      method: 'POST',
      body: JSON.stringify({ nickname }),
    }),
  getAdminLogs: (query: AdminLogQuery) => {
    const params = new URLSearchParams()
    if (query.category) params.set('category', query.category)
    if (query.from) params.set('from', query.from)
    if (query.to) params.set('to', query.to)
    if (query.q) params.set('q', query.q)
    if (query.nickname) params.set('nickname', query.nickname)
    if (query.limit != null) params.set('limit', String(query.limit))
    if (query.offset != null) params.set('offset', String(query.offset))
    const suffix = params.size > 0 ? `?${params.toString()}` : ''
    return request<AdminLogPage>(`/api/v1/admin/logs${suffix}`)
  },
  connectSteam: (steamIdOrUrl: string, apiKey: string) =>
    request<PlatformConnectionSummary>('/api/v1/platforms/steam/connect', {
      method: 'POST',
      body: JSON.stringify({ steamIdOrUrl, apiKey }),
    }),
  disconnectSteam: () =>
    request<void>('/api/v1/platforms/steam', {
      method: 'DELETE',
    }),
  syncSteam: async (): Promise<SteamSyncResult> => {
    const token = await getToken()
    const response = await fetch(`${apiBaseUrl}/api/v1/platforms/steam/sync`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    })

    if (!response.ok) {
      let message = `API request failed: ${response.status}`
      try {
        const body = (await response.json()) as { message?: string }
        if (body.message) {
          message = body.message
        }
      } catch {
        // ignore non-JSON error bodies
      }
      throw new Error(message)
    }

    const body = (await response.json()) as Record<string, unknown>
    if ('message' in body && !('addedCount' in body)) {
      return { queued: true, message: String(body.message) }
    }

    return body as SteamSyncResult
  },
  connectPsn: (npsso: string) =>
    request<PlatformConnectionSummary>('/api/v1/platforms/psn/connect', {
      method: 'POST',
      body: JSON.stringify({ npsso }),
    }),
  disconnectPsn: () =>
    request<void>('/api/v1/platforms/psn', {
      method: 'DELETE',
    }),
  syncPsn: async (): Promise<PsnSyncResult> => {
    const token = await getToken()
    const response = await fetch(`${apiBaseUrl}/api/v1/platforms/psn/sync`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    })

    if (!response.ok) {
      let message = `API request failed: ${response.status}`
      try {
        const body = (await response.json()) as { message?: string }
        if (body.message) {
          message = body.message
        }
      } catch {
        // ignore non-JSON error bodies
      }
      throw new Error(message)
    }

    const body = (await response.json()) as Record<string, unknown>
    if ('message' in body && !('addedCount' in body)) {
      return { queued: true, message: String(body.message) }
    }

    return body as PsnSyncResult
  },
  xboxLogin: () => request<XboxLogin>('/api/v1/platforms/xbox/login'),
  connectXbox: (callbackUrl: string) =>
    request<PlatformConnectionSummary>('/api/v1/platforms/xbox/connect', {
      method: 'POST',
      body: JSON.stringify({ callbackUrl }),
    }),
  disconnectXbox: () =>
    request<void>('/api/v1/platforms/xbox', {
      method: 'DELETE',
    }),
  syncXbox: () => syncPlatform('/api/v1/platforms/xbox/sync'),
  switchLogin: () => request<SwitchLogin>('/api/v1/platforms/switch/login'),
  connectSwitch: (callbackUrl: string, codeVerifier: string) =>
    request<PlatformConnectionSummary>('/api/v1/platforms/switch/connect', {
      method: 'POST',
      body: JSON.stringify({ callbackUrl, codeVerifier }),
    }),
  disconnectSwitch: () =>
    request<void>('/api/v1/platforms/switch', {
      method: 'DELETE',
    }),
  syncSwitch: () => syncPlatform('/api/v1/platforms/switch/sync'),
}

async function syncPlatform(path: string): Promise<PlatformSyncResult> {
  const token = await getToken()
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  })

  if (!response.ok) {
    let message = `API request failed: ${response.status}`
    try {
      const body = (await response.json()) as { message?: string }
      if (body.message) {
        message = body.message
      }
    } catch {
      // ignore non-JSON error bodies
    }
    throw new Error(message)
  }

  const body = (await response.json()) as Record<string, unknown>
  if ('message' in body && !('addedCount' in body)) {
    return { queued: true, message: String(body.message) }
  }

  return body as PlatformSyncResult
}
