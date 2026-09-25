import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import { useEffect, useRef } from 'react'
import { api, type PlatformSyncRun } from '../lib/api'

function isActive(status: PlatformSyncRun['status'] | undefined) {
  return status === 'Pending' || status === 'Processing'
}

function progressPercent(run: PlatformSyncRun) {
  if (run.totalCount <= 0) {
    return isActive(run.status) ? null : 100
  }
  return Math.min(100, Math.round((run.processedCount / run.totalCount) * 100))
}

export function SyncAllButton() {
  const queryClient = useQueryClient()
  const syncQuery = useQuery({
    queryKey: ['platform-sync-all'],
    queryFn: api.getCurrentSyncAll,
    refetchInterval: (query) => (isActive(query.state.data?.status) ? 1500 : false),
  })

  const stopMutation = useMutation({
    mutationFn: api.stopSyncAll,
    onSuccess: (run) => {
      queryClient.setQueryData(['platform-sync-all'], run)
    },
  })

  const syncMutation = useMutation({
    mutationFn: api.startSyncAll,
    onSuccess: (run) => {
      queryClient.setQueryData(['platform-sync-all'], run)
      if (!isActive(run.status)) {
        queryClient.invalidateQueries({ queryKey: ['library'] })
        queryClient.invalidateQueries({ queryKey: ['catalog'] })
        queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
      }
    },
  })

  const run = syncQuery.data ?? syncMutation.data ?? null
  const active = isActive(run?.status) || syncMutation.isPending
  const percent = run ? progressPercent(run) : null
  const previousStatus = useRef<PlatformSyncRun['status'] | null>(null)

  useEffect(() => {
    if (!run) {
      return
    }
    const previous = previousStatus.current
    previousStatus.current = run.status
    if (previous && isActive(previous) && (run.status === 'Completed' || run.status === 'Failed')) {
      queryClient.invalidateQueries({ queryKey: ['library'] })
      queryClient.invalidateQueries({ queryKey: ['catalog'] })
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
    }
  }, [queryClient, run])

  return (
    <section className="rounded-2xl border border-border bg-card/60 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold">Sync all platforms</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Pull the latest Steam, PlayStation, Xbox, and Nintendo Switch libraries in the background.
          </p>
        </div>
        <button
          type="button"
          onClick={() => syncMutation.mutate()}
          disabled={active}
          className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50"
        >
          <RefreshCw className={`size-4 ${active ? 'animate-spin' : ''}`} />
          {active ? 'Syncing…' : 'Sync all'}
        </button>
        {active && (
          <button
            type="button"
            onClick={() => stopMutation.mutate()}
            disabled={stopMutation.isPending}
            className="rounded-lg border border-border px-4 py-2 text-sm font-medium text-foreground disabled:opacity-50"
          >
            Stop sync
          </button>
        )}
      </div>

      {syncMutation.isError && (
        <p className="mt-3 text-sm text-amber-300">{(syncMutation.error as Error).message}</p>
      )}

      {run && (
        <div className="mt-4">
          <div className="flex items-center justify-between gap-3 text-sm">
            <p className="text-foreground/80">{run.phase}</p>
            <p className="text-muted-foreground">
              {run.totalCount > 0 ? `${run.processedCount} / ${run.totalCount}` : run.status}
            </p>
          </div>
          <div className="mt-2 h-2 overflow-hidden rounded-full bg-secondary">
            <div
              className={`h-full rounded-full bg-primary ${percent === null ? 'w-1/3 animate-pulse' : ''}`}
              style={percent === null ? undefined : { width: `${percent}%` }}
            />
          </div>
          <p className="mt-2 text-xs text-muted-foreground">
            Added {run.addedCount} · Updated {run.updatedCount} · Skipped {run.skippedCount} · Unmatched{' '}
            {run.unmatchedCount}
          </p>
          {run.warning && <p className="mt-2 text-sm text-amber-300">{run.warning}</p>}
          {run.errorMessage && <p className="mt-2 text-sm text-amber-300">{run.errorMessage}</p>}
        </div>
      )}
    </section>
  )
}
