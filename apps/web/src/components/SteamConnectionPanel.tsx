import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api, type PlatformConnectionSummary, type SteamSyncResult } from '../lib/api'

const STEAM_API_KEY_URL = 'https://steamcommunity.com/dev/apikey'

function formatTimestamp(value: string | null) {
  if (!value) {
    return 'Never'
  }
  return new Date(value).toLocaleString()
}

function SyncStatus({ result }: { result: SteamSyncResult }) {
  if ('queued' in result && result.queued) {
    return (
      <p className="mt-3 text-sm text-primary">
        {result.message || 'Steam sync queued. Check back shortly and refresh your library.'}
      </p>
    )
  }

  return (
    <div className="mt-3 rounded-lg border border-border bg-background/50 p-4 text-sm">
      <p className="text-foreground/80">
        Synced at <span className="text-primary">{formatTimestamp(result.syncedAt)}</span>
      </p>
      <p className="mt-1 text-muted-foreground">
        Added {result.addedCount} · Updated {result.updatedCount} · Skipped {result.skippedCount} ·
        Unmatched {result.unmatchedCount}
      </p>
      {result.warning && <p className="mt-2 text-amber-300">{result.warning}</p>}
    </div>
  )
}

function ConnectedSteamCard({
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
  syncResult: SteamSyncResult | null
}) {
  return (
    <div className="mt-4 rounded-lg border border-primary/40 bg-primary/10 p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-primary">Connected</p>
          <p className="mt-1 text-foreground">{connection.displayName ?? connection.externalAccountId}</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Steam ID {connection.externalAccountId} · Last sync {formatTimestamp(connection.lastSyncAt)}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={onSync}
            disabled={isSyncing}
            className="rounded-lg bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground disabled:opacity-50"
          >
            {isSyncing ? 'Syncing…' : 'Sync now'}
          </button>
          <button
            type="button"
            onClick={onDisconnect}
            className="rounded-lg border border-border px-3 py-1.5 text-sm text-foreground/80 hover:border-border hover:text-foreground"
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

export function SteamConnectionPanel() {
  const queryClient = useQueryClient()
  const [steamIdOrUrl, setSteamIdOrUrl] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [connectError, setConnectError] = useState<string | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)
  const [syncResult, setSyncResult] = useState<SteamSyncResult | null>(null)

  const connectionsQuery = useQuery({
    queryKey: ['platform-connections'],
    queryFn: api.getPlatformConnections,
  })

  const steamConnection = connectionsQuery.data?.find((c) => c.platform === 'Steam') ?? null

  const connectMutation = useMutation({
    mutationFn: () => api.connectSteam(steamIdOrUrl.trim(), apiKey.trim()),
    onSuccess: () => {
      setSteamIdOrUrl('')
      setApiKey('')
      setConnectError(null)
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
    },
    onError: (error: Error) => {
      setConnectError(error.message)
    },
  })

  const disconnectMutation = useMutation({
    mutationFn: api.disconnectSteam,
    onSuccess: () => {
      setSyncResult(null)
      setSyncError(null)
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
    },
  })

  const syncMutation = useMutation({
    mutationFn: api.syncSteam,
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
    if (!window.confirm('Disconnect Steam from GoodPlays? Your library entries will stay.')) {
      return
    }
    disconnectMutation.mutate()
  }

  return (
    <section className="rounded-2xl border border-border bg-card/60 p-6">
      <h2 className="text-lg font-medium text-foreground">Steam library sync</h2>
      <p className="mt-1 text-sm text-muted-foreground">
        Link your Steam account to import owned games and playtime. You need a{' '}
        <a
          href={STEAM_API_KEY_URL}
          target="_blank"
          rel="noreferrer"
          className="text-primary hover:text-primary"
        >
          Steam Web API key
        </a>{' '}
        and Game details set to Public in your Steam privacy settings. Re-sync anytime to refresh cover art and
        metadata from IGDB.
      </p>

      {connectionsQuery.isLoading && <p className="mt-4 text-sm text-muted-foreground">Loading connection…</p>}
      {connectionsQuery.isError && (
        <p className="mt-4 text-sm text-amber-300">Could not load platform connections.</p>
      )}

      {steamConnection ? (
        <ConnectedSteamCard
          connection={steamConnection}
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
              <label htmlFor="steam-id" className="block text-xs font-medium text-muted-foreground">
                Steam ID or profile URL
              </label>
              <input
                id="steam-id"
                type="text"
                value={steamIdOrUrl}
                onChange={(e) => setSteamIdOrUrl(e.target.value)}
                placeholder="76561198000000000 or steamcommunity.com/id/username"
                className="mt-1 w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground"
              />
            </div>
            <div>
              <label htmlFor="steam-api-key" className="block text-xs font-medium text-muted-foreground">
                Steam Web API key
              </label>
              <input
                id="steam-api-key"
                type="password"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                autoComplete="off"
                className="mt-1 w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground"
              />
            </div>
            <button
              type="submit"
              disabled={!steamIdOrUrl.trim() || !apiKey.trim() || connectMutation.isPending}
              className="rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50"
            >
              {connectMutation.isPending ? 'Connecting…' : 'Connect Steam'}
            </button>
            {connectError && <p className="text-sm text-amber-300">{connectError}</p>}
          </form>
        )
      )}

      {disconnectMutation.isError && (
        <p className="mt-3 text-sm text-amber-300">Failed to disconnect Steam.</p>
      )}
    </section>
  )
}
