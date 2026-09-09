const apiBaseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5280'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
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
  status: string
  rating: number | null
  hoursPlayed: number | null
}

export type GameSearchResult = {
  id: string
  title: string
  slug: string
  coverUrl: string | null
}

export const api = {
  getLibrary: () => request<LibraryEntrySummary[]>('/api/v1/library'),
  searchGames: (query: string) =>
    request<GameSearchResult[]>(`/api/v1/games/search?q=${encodeURIComponent(query)}`),
  getRecommendations: () => request<unknown[]>('/api/v1/recommendations'),
}
