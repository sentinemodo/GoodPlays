import { useMutation, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import { api } from '../lib/api'

const platformSources = new Set(['SteamSync', 'PsnSync', 'XboxSync', 'SwitchSync'])

export function canSyncFromPlatform(source: string | null | undefined) {
  return source != null && platformSources.has(source)
}

export function SyncGameButton({ entryId, source }: { entryId: string; source: string | null | undefined }) {
  const queryClient = useQueryClient()
  const syncMutation = useMutation({
    mutationFn: () => api.syncLibraryEntry(entryId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['library'] })
      queryClient.invalidateQueries({ queryKey: ['catalog'] })
      queryClient.invalidateQueries({ queryKey: ['game'] })
    },
  })

  if (!canSyncFromPlatform(source)) {
    return null
  }

  return (
    <button
      type="button"
      onClick={(event) => {
        event.preventDefault()
        event.stopPropagation()
        syncMutation.mutate()
      }}
      disabled={syncMutation.isPending}
      className="inline-flex items-center gap-1 rounded-full border border-border px-2 py-1 text-xs text-foreground/80 hover:text-foreground disabled:opacity-50"
    >
      <RefreshCw className={`size-3 ${syncMutation.isPending ? 'animate-spin' : ''}`} />
      {syncMutation.isPending ? 'Syncing…' : 'Sync'}
    </button>
  )
}
