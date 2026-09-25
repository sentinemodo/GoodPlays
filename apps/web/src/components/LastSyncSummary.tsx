import { useQuery } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import { Link } from 'react-router'
import { api } from '../lib/api'

function formatSync(lastSyncAt: string | null | undefined, connected: boolean) {
  if (!connected) {
    return 'Not connected'
  }
  if (!lastSyncAt) {
    return 'Not synced yet'
  }
  const parsed = new Date(lastSyncAt)
  if (Number.isNaN(parsed.getTime())) {
    return 'Not synced yet'
  }
  return parsed.toLocaleString()
}

export function LastSyncSummary() {
  const connectionsQuery = useQuery({
    queryKey: ['platform-connections'],
    queryFn: api.getPlatformConnections,
  })

  const steam = connectionsQuery.data?.find((connection) => connection.platform === 'Steam')
  const psn = connectionsQuery.data?.find((connection) => connection.platform === 'Psn')

  return (
    <div className="rounded-2xl border border-border bg-card/60 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2 text-sm font-semibold">
          <RefreshCw className="size-4 text-cyan" />
          Last synced
        </div>
        <Link to="/import" className="text-sm font-medium text-primary hover:underline">
          Import & sync
        </Link>
      </div>
      {connectionsQuery.isLoading && <p className="mt-3 text-sm text-muted-foreground">Loading sync status…</p>}
      {connectionsQuery.isError && (
        <p className="mt-3 text-sm text-amber-300">Sign in to see when Steam and PlayStation last synced.</p>
      )}
      {connectionsQuery.data && (
        <dl className="mt-3 grid gap-2 sm:grid-cols-2">
          <div className="rounded-xl bg-secondary/50 px-3 py-2">
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">Steam</dt>
            <dd className="mt-1 text-sm font-medium">{formatSync(steam?.lastSyncAt, Boolean(steam))}</dd>
          </div>
          <div className="rounded-xl bg-secondary/50 px-3 py-2">
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">PlayStation</dt>
            <dd className="mt-1 text-sm font-medium">{formatSync(psn?.lastSyncAt, Boolean(psn))}</dd>
          </div>
        </dl>
      )}
    </div>
  )
}
