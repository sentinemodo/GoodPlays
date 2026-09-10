const apiBaseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5280'

export type GetToken = () => Promise<string | null>

let getToken: GetToken = async () => null

export function setApiTokenProvider(provider: GetToken) {
  getToken = provider
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = await getToken()
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {}),
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

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

export type LibraryEntrySummary = {
  id: string
  gameId: string
  gameTitle: string
  coverUrl: string | null
  status: string
  rating: number | null
  hoursPlayed: number | null
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
  platform: 'Steam' | 'Psn'
  externalAccountId: string
  displayName: string | null
  lastSyncAt: string | null
  syncEnabled: boolean
  connectedAt: string
}

export type SteamSyncResult =
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

export const api = {
  getLibrary: () => request<LibraryEntrySummary[]>('/api/v1/library'),
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
  getRecommendations: () => request<RecommendationSummary[]>('/api/v1/recommendations'),
  createTextImport: (text: string) =>
    request<ImportJobSummary>('/api/v1/imports', {
      method: 'POST',
      body: JSON.stringify({ modality: 'Text', text }),
    }),
  getImport: (jobId: string) => request<ImportJobSummary>(`/api/v1/imports/${jobId}`),
  getPlatformConnections: () => request<PlatformConnectionSummary[]>('/api/v1/platforms'),
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
}
