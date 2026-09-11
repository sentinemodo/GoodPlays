import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
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
      queryClient.invalidateQueries({ queryKey: ['recommendations'] })
    },
  })

  return (
    <section className="rounded-2xl border border-slate-800 bg-slate-900/60 p-6">
      <div className="mb-4 flex items-center justify-between">
        <div>
          <h2 className="text-lg font-medium text-slate-100">What to play next</h2>
          <p className="text-sm text-slate-400">
            Suggestions based on your library (AI when LLM is configured on the API).
          </p>
        </div>
        <button
          type="button"
          onClick={() => refetch()}
          disabled={isFetching}
          className="text-xs text-slate-400 hover:text-slate-200"
        >
          Refresh
        </button>
      </div>

      {isLoading && <p className="text-slate-400">Loading recommendations…</p>}
      {error && (
        <p className="text-amber-300">
          {error instanceof Error ? error.message : 'Could not load recommendations.'}
        </p>
      )}
      {!isLoading && !error && (
        <>
          {data && data.length > 0 ? (
            <ul className="space-y-3">
              {data.map((reco) => (
                <li
                  key={reco.gameId}
                  className="flex items-start gap-3 rounded-lg border border-slate-800 px-4 py-3"
                >
                  {reco.coverUrl ? (
                    <img
                      src={reco.coverUrl}
                      alt=""
                      className="h-14 w-10 rounded object-cover"
                    />
                  ) : (
                    <div className="flex h-14 w-10 items-center justify-center rounded bg-slate-800 text-xs text-slate-500">
                      ?
                    </div>
                  )}
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium text-slate-100">{reco.title}</p>
                    {reco.reason && <p className="mt-1 text-xs text-slate-400">{reco.reason}</p>}
                  </div>
                  <button
                    type="button"
                    disabled={addMutation.isPending}
                    onClick={() => addMutation.mutate(reco.gameId)}
                    className="shrink-0 rounded-md border border-emerald-700 px-3 py-1 text-xs text-emerald-400 hover:bg-emerald-950"
                  >
                    Add
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-slate-400">
              Add a few games to your library first, then refresh for personalized picks.
            </p>
          )}
        </>
      )}
    </section>
  )
}
