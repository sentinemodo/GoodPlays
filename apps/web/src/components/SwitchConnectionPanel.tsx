import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api, type SwitchSyncResult } from '../lib/api'

function formatTimestamp(value: string | null) {
  if (!value) {
    return 'Never'
  }
  return new Date(value).toLocaleString()
}

export function SwitchConnectionPanel() {
  const queryClient = useQueryClient()
  const [callbackUrl, setCallbackUrl] = useState('')
  const [codeVerifier, setCodeVerifier] = useState<string | null>(null)
  const [connectError, setConnectError] = useState<string | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)
  const [syncResult, setSyncResult] = useState<SwitchSyncResult | null>(null)

  const connectionsQuery = useQuery({
    queryKey: ['platform-connections'],
    queryFn: api.getPlatformConnections,
  })
  const connection = connectionsQuery.data?.find((item) => item.platform === 'Switch') ?? null

  const loginMutation = useMutation({
    mutationFn: api.switchLogin,
    onSuccess: (login) => {
      setCodeVerifier(login.codeVerifier)
      setConnectError(null)
      window.open(login.url, '_blank', 'noopener,noreferrer')
    },
    onError: (error: Error) => setConnectError(error.message),
  })

  const connectMutation = useMutation({
    mutationFn: () => api.connectSwitch(callbackUrl.trim(), codeVerifier ?? ''),
    onSuccess: () => {
      setCallbackUrl('')
      setCodeVerifier(null)
      setConnectError(null)
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
    },
    onError: (error: Error) => setConnectError(error.message),
  })

  const disconnectMutation = useMutation({
    mutationFn: api.disconnectSwitch,
    onSuccess: () => {
      setSyncResult(null)
      setSyncError(null)
      queryClient.invalidateQueries({ queryKey: ['platform-connections'] })
    },
  })

  const syncMutation = useMutation({
    mutationFn: api.syncSwitch,
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
      <h2 className="text-lg font-medium text-foreground">Nintendo Switch library sync</h2>
      <p className="mt-2 text-sm text-muted-foreground">
        Sign in to your Nintendo Account, then paste the npf:// link the browser cannot open. GoodPlays imports Play
        Activity: games you have launched, with minutes played. This uses Nintendo’s app API, the same kind of
        unofficial access as PlayStation.
      </p>
      {connection ? (
        <div className="mt-4 rounded-lg border border-red-900/40 bg-red-950/20 p-4">
          <p className="text-sm font-medium text-red-300">Connected</p>
          <p className="mt-1 text-foreground">{connection.displayName ?? connection.externalAccountId}</p>
          <p className="mt-1 text-xs text-muted-foreground">Last sync {formatTimestamp(connection.lastSyncAt)}</p>
          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => syncMutation.mutate()}
              disabled={syncMutation.isPending}
              className="rounded-lg bg-red-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
            >
              {syncMutation.isPending ? 'Syncing…' : 'Sync now'}
            </button>
            <button
              type="button"
              onClick={() => {
                if (window.confirm('Disconnect Nintendo Switch from GoodPlays? Your library entries will stay.')) {
                  disconnectMutation.mutate()
                }
              }}
              className="rounded-lg border border-border px-3 py-1.5 text-sm text-foreground/80"
            >
              Disconnect
            </button>
          </div>
          {syncError && <p className="mt-3 text-sm text-amber-300">{syncError}</p>}
          <SyncLine result={syncResult} />
        </div>
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
            {loginMutation.isPending ? 'Opening Nintendo…' : 'Open Nintendo sign-in'}
          </button>
          {codeVerifier && (
            <p className="text-xs text-muted-foreground">
              When the page fails to open, copy the full address beginning with npf and paste it here. Keep this tab
              open so the sign-in can be finished.
            </p>
          )}
          <textarea
            value={callbackUrl}
            onChange={(event) => setCallbackUrl(event.target.value)}
            placeholder="npf…://auth#session_token_code=…"
            rows={3}
            className="rounded-lg border border-border bg-background px-3 py-2 text-sm"
          />
          <button
            type="submit"
            disabled={connectMutation.isPending || !codeVerifier || callbackUrl.trim().length === 0}
            className="w-fit rounded-lg bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground disabled:opacity-50"
          >
            {connectMutation.isPending ? 'Connecting…' : 'Connect Switch'}
          </button>
          {connectError && <p className="text-sm text-amber-300">{connectError}</p>}
        </form>
      )}
    </section>
  )
}

function SyncLine({ result }: { result: SwitchSyncResult | null }) {
  if (!result) {
    return null
  }
  if ('queued' in result && result.queued) {
    return <p className="mt-3 text-sm text-blue-400">{result.message}</p>
  }
  if (!('addedCount' in result)) {
    return null
  }
  return (
    <p className="mt-3 text-sm text-muted-foreground">
      Added {result.addedCount} · Updated {result.updatedCount} · Skipped {result.skippedCount}
      {result.warning ? ` · ${result.warning}` : ''}
    </p>
  )
}
