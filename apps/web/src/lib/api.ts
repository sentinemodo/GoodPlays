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
    throw new Error(`API request failed: ${response.status}`)
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
  getRecommendations: () => request<RecommendationSummary[]>('/api/v1/recommendations'),
  createTextImport: (text: string) =>
    request<ImportJobSummary>('/api/v1/imports', {
      method: 'POST',
      body: JSON.stringify({ modality: 'Text', text }),
    }),
  getImport: (jobId: string) => request<ImportJobSummary>(`/api/v1/imports/${jobId}`),
}
