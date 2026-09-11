import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api, type PlatformConnectionSummary, type PsnSyncResult } from '../lib/api'

const NPSSO_URL = 'https://ca.account.sony.com/api/v1/ssocookie'

function formatTimestamp(value: string | null) {
  if (!value) {
    return 'Never'
  }
  return new Date(value).toLocaleString()
}

function SyncStatus({ result }: { result: PsnSyncResult }) {
  if ('queued' in result && result.queued) {
    return (
      <p className="mt-3 text-sm text-blue-400">
        {result.message || 'PlayStation sync queued. Check back shortly and refresh your library.'}
      </p>
    )
  }

  return (
    <div className="mt-3 rounded-lg border border-slate-800 bg-slate-950/50 p-4 text-sm">
      <p className="text-slate-300">
        Synced at <span className="text-blue-400">{formatTimestamp(result.syncedAt)}</span>
      </p>
      <p className="mt-1 text-slate-400">
        Added {result.addedCount} · Updated {result.updatedCount} · Skipped {result.skippedCount} ·
        Unmatched {result.unmatchedCount}
      </p>
      {result.warning && <p className="mt-2 text-amber-300">{result.warning}</p>}
    </div>
  )
}

function ConnectedPsnCard({
  connection,
  onDisconnect,
  onSync,
  isSyncing,
  syncError,
  syncResult,
}: {
  connection: PlatformConnectionSummary
  onDisconnect: () => void
  onSync: () => void
  isSyncing: boolean
  syncError: string | null
  syncResult: PsnSyncResult | null
}) {
  return (
    <div className="mt-4 rounded-lg border border-blue-900/50 bg-blue-950/20 p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-blue-300">Connected</p>
          <p className="mt-1 text-slate-100">{connection.displayName ?? connection.externalAccountId}</p>
          <p className="mt-1 text-xs text-slate-400">
            PSN ID {connection.externalAccountId} · Last sync {formatTimestamp(connection.lastSyncAt)}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={onSync}
            disabled={isSyncing}
            className="rounded-lg bg-blue-500 px-3 py-1.5 text-sm font-medium text-slate-950 disabled:opacity-50"
          >
            {isSyncing ? 'Syncing…' : 'Sync now'}
          </button>
          <button
            type="button"
            onClick={onDisconnect}
            className="rounded-lg border border-slate-700 px-3 py-1.5 text-sm text-slate-300 hover:border-slate-500 hover:text-slate-100"
          >
            Disconnect
          </button>
        </div>
      </div>
      {syncError && <p className="mt-3 text-sm text-amber-300">{syncError}</p>}
      {syncResult && <SyncStatus result={syncResult} />}
    </div>
  )
}

export function PsnConnectionPanel() {
  const queryClient = useQueryClient()
  const [npsso, setNpsso] = useState('')
  const [connectError, setConnectError] = useState<string | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)
  const [syncResult, setSyncResult] = useState<PsnSyncResult | null>(null)

  const connectionsQuery = useQuery({
    queryKey: ['platform-connections'],
    queryFn: api.getPlatformConnections,
  })

  const psnConnection = connectionsQuery.data?.find((c) => c.platform === 'Psn') ?? null

  const connectMutation = useMutation({
    mutationFn: () => api.connectPsn(npsso.trim()),
    onSuccess: () => {
      setNpsso('')
      setConnectError(null)
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
    },
    onError: (error: Error) => {
      setConnectError(error.message)
    },
  })

  const disconnectMutation = useMutation({
    mutationFn: api.disconnectPsn,
    onSuccess: () => {
      setSyncResult(null)
      setSyncError(null)
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
    },
  })

  const syncMutation = useMutation({
    mutationFn: api.syncPsn,
    onSuccess: (result) => {
      setSyncResult(result)
      setSyncError(null)
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
      if (!('queued' in result && result.queued)) {
        queryClient.invalidateQueries({ queryKey: ['library'] })
      }
    },
    onError: (error: Error) => {
      setSyncError(error.message)
      setSyncResult(null)
    },
  })

  const handleDisconnect = () => {
    if (!window.confirm('Disconnect PlayStation from GoodPlays? Your library entries will stay.')) {
      return
    }
    disconnectMutation.mutate()
  }

  return (
    <section className="rounded-2xl border border-slate-800 bg-slate-900/60 p-6">
      <h2 className="text-lg font-medium text-slate-100">PlayStation library sync</h2>
      <p className="mt-1 text-sm text-slate-400">
        Link your PlayStation account to import played games and playtime. Sign in at playstation.com, then copy your
        NPSSO token from{' '}
        <a href={NPSSO_URL} target="_blank" rel="noreferrer" className="text-blue-400 hover:text-blue-300">
          Sony&apos;s SSOCookie endpoint
        </a>
        . Tokens expire after ~60 days — reconnect when sync fails.
      </p>

      {connectionsQuery.isLoading && <p className="mt-4 text-sm text-slate-400">Loading connection…</p>}
      {connectionsQuery.isError && (
        <p className="mt-4 text-sm text-amber-300">Could not load platform connections.</p>
      )}

      {psnConnection ? (
        <ConnectedPsnCard
          connection={psnConnection}
          onDisconnect={handleDisconnect}
          onSync={() => syncMutation.mutate()}
          isSyncing={syncMutation.isPending}
          syncError={syncError}
          syncResult={syncResult}
        />
      ) : (
        !connectionsQuery.isLoading && (
          <form
            className="mt-4 space-y-3"
            onSubmit={(event) => {
              event.preventDefault()
              setConnectError(null)
              connectMutation.mutate()
            }}
          >
            <div>
              <label htmlFor="psn-npsso" className="block text-xs font-medium text-slate-400">
                NPSSO token
              </label>
              <input
                id="psn-npsso"
                type="password"
                value={npsso}
                onChange={(e) => setNpsso(e.target.value)}
                autoComplete="off"
                placeholder="64-character token from ssocookie"
                className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 placeholder:text-slate-600"
              />
            </div>
            <button
              type="submit"
              disabled={!npsso.trim() || connectMutation.isPending}
              className="rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-slate-950 disabled:opacity-50"
            >
              {connectMutation.isPending ? 'Connecting…' : 'Connect PlayStation'}
            </button>
            {connectError && <p className="text-sm text-amber-300">{connectError}</p>}
          </form>
        )
      )}

      {disconnectMutation.isError && (
        <p className="mt-3 text-sm text-amber-300">Failed to disconnect PlayStation.</p>
      )}
    </section>
  )
}
