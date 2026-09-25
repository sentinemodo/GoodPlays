import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api, type PlatformConnectionSummary, type XboxSyncResult } from '../lib/api'

function formatTimestamp(value: string | null) {
  if (!value) {
    return 'Never'
  }
  return new Date(value).toLocaleString()
}

export function XboxConnectionPanel() {
  const queryClient = useQueryClient()
  const [callbackUrl, setCallbackUrl] = useState('')
  const [loginUrl, setLoginUrl] = useState<string | null>(null)
  const [connectError, setConnectError] = useState<string | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)
  const [syncResult, setSyncResult] = useState<XboxSyncResult | null>(null)

  const connectionsQuery = useQuery({
    queryKey: ['platform-connections'],
    queryFn: api.getPlatformConnections,
  })
  const connection = connectionsQuery.data?.find((item) => item.platform === 'Xbox') ?? null

  const loginMutation = useMutation({
    mutationFn: api.xboxLogin,
    onSuccess: (login) => {
      setLoginUrl(login.url)
      window.open(login.url, '_blank', 'noopener,noreferrer')
    },
    onError: (error: Error) => setConnectError(error.message),
  })

  const connectMutation = useMutation({
    mutationFn: () => api.connectXbox(callbackUrl.trim()),
    onSuccess: () => {
      setCallbackUrl('')
      setConnectError(null)
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
    },
    onError: (error: Error) => setConnectError(error.message),
  })

  const disconnectMutation = useMutation({
    mutationFn: api.disconnectXbox,
    onSuccess: () => {
      setSyncResult(null)
      setSyncError(null)
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
    },
  })

  const syncMutation = useMutation({
    mutationFn: api.syncXbox,
    onSuccess: (result) => {
      setSyncError(null)
      setSyncResult(result)
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
      queryClient.invalidateQueries({ queryKey: ['library'] })
    },
    onError: (error: Error) => setSyncError(error.message),
  })

  return (
    <section className="rounded-2xl border border-border bg-card/60 p-5">
      <h2 className="text-lg font-medium text-foreground">Xbox library sync</h2>
      <p className="mt-2 text-sm text-muted-foreground">
        Sign in with Microsoft, then paste the final browser address. GoodPlays imports games you have launched.
        Xbox does not provide playtime, so hours stay as you entered them.
      </p>
      {connection ? (
        <ConnectedCard
          connection={connection}
          onSync={() => syncMutation.mutate()}
          isSyncing={syncMutation.isPending}
          onDisconnect={() => {
            if (window.confirm('Disconnect Xbox from GoodPlays? Your library entries will stay.')) {
              disconnectMutation.mutate()
            }
          }}
          syncError={syncError}
          syncResult={syncResult}
        />
      ) : (
        <form
          className="mt-4 flex flex-col gap-3"
          onSubmit={(event) => {
            event.preventDefault()
            connectMutation.mutate()
          }}
        >
          <button
            type="button"
            onClick={() => loginMutation.mutate()}
            className="w-fit rounded-lg border border-border px-3 py-1.5 text-sm text-foreground/80 hover:text-foreground"
          >
            {loginMutation.isPending ? 'Opening Microsoft…' : 'Open Microsoft sign-in'}
          </button>
          {loginUrl && (
            <p className="text-xs text-muted-foreground">
              After sign-in, copy the address that starts with login.live.com and paste it below.
            </p>
          )}
          <textarea
            value={callbackUrl}
            onChange={(event) => setCallbackUrl(event.target.value)}
            placeholder="https://login.live.com/oauth20_desktop.srf?code=…"
            rows={3}
            className="rounded-lg border border-border bg-background px-3 py-2 text-sm"
          />
          <button
            type="submit"
            disabled={connectMutation.isPending || callbackUrl.trim().length === 0}
            className="w-fit rounded-lg bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground disabled:opacity-50"
          >
            {connectMutation.isPending ? 'Connecting…' : 'Connect Xbox'}
          </button>
          {connectError && <p className="text-sm text-amber-300">{connectError}</p>}
        </form>
      )}
    </section>
  )
}

function ConnectedCard({
  connection,
  onSync,
  isSyncing,
  onDisconnect,
  syncError,
  syncResult,
}: {
  connection: PlatformConnectionSummary
  onSync: () => void
  isSyncing: boolean
  onDisconnect: () => void
  syncError: string | null
  syncResult: XboxSyncResult | null
}) {
  return (
    <div className="mt-4 rounded-lg border border-emerald-900/50 bg-emerald-950/20 p-4">
      <p className="text-sm font-medium text-emerald-300">Connected</p>
      <p className="mt-1 text-foreground">{connection.displayName ?? connection.externalAccountId}</p>
      <p className="mt-1 text-xs text-muted-foreground">Last sync {formatTimestamp(connection.lastSyncAt)}</p>
      <div className="mt-3 flex flex-wrap gap-2">
        <button
          type="button"
          onClick={onSync}
          disabled={isSyncing}
          className="rounded-lg bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
        >
          {isSyncing ? 'Syncing…' : 'Sync now'}
        </button>
        <button
          type="button"
          onClick={onDisconnect}
          className="rounded-lg border border-border px-3 py-1.5 text-sm text-foreground/80"
        >
          Disconnect
        </button>
      </div>
      {syncError && <p className="mt-3 text-sm text-amber-300">{syncError}</p>}
      {syncResult && !('queued' in syncResult && syncResult.queued) && 'addedCount' in syncResult && (
        <p className="mt-3 text-sm text-muted-foreground">
          Added {syncResult.addedCount} · Updated {syncResult.updatedCount} · Skipped {syncResult.skippedCount}
          {syncResult.warning ? ` · ${syncResult.warning}` : ''}
        </p>
      )}
      {syncResult && 'queued' in syncResult && syncResult.queued && (
        <p className="mt-3 text-sm text-blue-400">{syncResult.message}</p>
      )}
    </div>
  )
}
