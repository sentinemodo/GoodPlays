import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api, type GameSearchResult } from '../lib/api'

export function AddGameSearch() {
  const queryClient = useQueryClient()
  const [query, setQuery] = useState('')
  const [debouncedQuery, setDebouncedQuery] = useState('')

  const searchQuery = useQuery({
    queryKey: ['game-search', debouncedQuery],
    queryFn: () => api.searchGames(debouncedQuery),
    enabled: debouncedQuery.length >= 2,
  })

  const addMutation = useMutation({
    mutationFn: async (result: GameSearchResult) => {
      const gameId =
        result.source === 'local' && result.id !== '00000000-0000-0000-0000-000000000000'
          ? result.id
          : (await api.importGame(result.igdbId!)).id

      return api.addToLibrary(gameId)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['library'] })
    },
  })

  function handleSearchSubmit(event: React.FormEvent) {
    event.preventDefault()
    setDebouncedQuery(query.trim())
  }

  return (
    <section className="rounded-2xl border border-border bg-card/60 p-6">
      <h2 className="text-lg font-medium text-foreground">Add a game</h2>
      <p className="mt-1 text-sm text-muted-foreground">
        Search IGDB (when configured) or your local catalog, then add to your library.
      </p>

      <form onSubmit={handleSearchSubmit} className="mt-4 flex gap-2">
        <input
          type="search"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Search games…"
          className="flex-1 rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-foreground0"
        />
        <button
          type="submit"
          className="rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground"
        >
          Search
        </button>
      </form>

      {searchQuery.isFetching && <p className="mt-4 text-sm text-muted-foreground">Searching…</p>}

      {searchQuery.error && (
        <p className="mt-4 text-sm text-amber-300">Search failed. Is the API running?</p>
      )}

      {searchQuery.data && searchQuery.data.length === 0 && debouncedQuery.length >= 2 && (
        <p className="mt-4 text-sm text-muted-foreground">No matches for “{debouncedQuery}”.</p>
      )}

      {searchQuery.data && searchQuery.data.length > 0 && (
        <ul className="mt-4 space-y-2">
          {searchQuery.data.map((result) => {
            const key = result.source === 'igdb' ? `igdb-${result.igdbId}` : result.id
            const isAdding =
              addMutation.isPending && addMutation.variables?.title === result.title

            return (
              <li
                key={key}
                className="flex items-center justify-between gap-3 rounded-lg border border-border px-3 py-2"
              >
                <div className="flex min-w-0 items-center gap-3">
                  {result.coverUrl ? (
                    <img
                      src={result.coverUrl}
                      alt=""
                      className="h-12 w-9 rounded object-cover"
                    />
                  ) : (
                    <div className="flex h-12 w-9 items-center justify-center rounded bg-muted text-xs text-foreground0">
                      ?
                    </div>
                  )}
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-foreground">{result.title}</p>
                    <p className="text-xs text-foreground0">
                      {result.source === 'igdb' ? 'IGDB' : 'In catalog'}
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  disabled={addMutation.isPending || (result.source === 'igdb' && !result.igdbId)}
                  onClick={() => addMutation.mutate(result)}
                  className="shrink-0 rounded-lg border border-primary px-3 py-1.5 text-xs font-medium text-primary hover:bg-primary/15 disabled:opacity-50"
                >
                  {isAdding ? 'Adding…' : 'Add'}
                </button>
              </li>
            )
          })}
        </ul>
      )}

      {addMutation.error && (
        <p className="mt-4 text-sm text-amber-300">
          Could not add game. It may already be in your library, or you may need to sign in.
        </p>
      )}
    </section>
  )
}
