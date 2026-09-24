import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Sparkles } from 'lucide-react'
import { api } from '../lib/api'

export function RecommendationsPanel() {
  const queryClient = useQueryClient()

  const { data, isLoading, error, refetch, isFetching } = useQuery({
    queryKey: ['recommendations'],
    queryFn: api.getRecommendations,
  })

  const addMutation = useMutation({
    mutationFn: (gameId: string) => api.addToLibrary(gameId, 'Backlog'),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['library'] })
      queryClient.invalidateQueries({ queryKey: ['catalog'] })
      queryClient.invalidateQueries({ queryKey: ['recommendations'] })
    },
  })

  const picks = (data ?? []).slice(0, 5)

  return (
    <section>
      <div className="mb-3 flex items-end justify-between gap-3">
        <div>
          <h2 className="font-display text-xl font-bold tracking-tight sm:text-2xl">Recommended for you</h2>
          <p className="mt-1 text-sm text-muted-foreground">Based on your library.</p>
        </div>
        <button
          type="button"
          onClick={() => refetch()}
          disabled={isFetching}
          className="inline-flex items-center gap-1 rounded-full bg-primary/15 px-3 py-1 text-xs font-semibold text-primary"
        >
          <Sparkles className="size-3" />
          Refresh
        </button>
      </div>

      {isLoading && <p className="text-sm text-muted-foreground">Loading recommendations…</p>}
      {error && (
        <p className="text-sm text-amber-300">
          {error instanceof Error ? error.message : 'Could not load recommendations.'}
        </p>
      )}
      {!isLoading && !error && picks.length === 0 && (
        <p className="text-sm text-muted-foreground">
          Add a few games to your library first, then refresh for personalized picks.
        </p>
      )}
      {picks.length > 0 && (
        <div className="grid grid-cols-5 gap-2 sm:gap-3">
          {picks.map((reco) => (
            <article key={reco.gameId} className="min-w-0">
              {reco.coverUrl ? (
                <img src={reco.coverUrl} alt="" className="aspect-[3/4] w-full rounded-xl object-cover" />
              ) : (
                <div className="flex aspect-[3/4] items-center justify-center rounded-xl bg-muted text-xs text-muted-foreground">
                  ?
                </div>
              )}
              <p className="mt-1.5 truncate text-xs font-semibold">{reco.title}</p>
              <button
                type="button"
                disabled={addMutation.isPending}
                onClick={() => addMutation.mutate(reco.gameId)}
                className="mt-1 text-[11px] font-semibold text-primary hover:underline disabled:opacity-50"
              >
                Add
              </button>
            </article>
          ))}
        </div>
      )}
    </section>
  )
}
