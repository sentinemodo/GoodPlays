import { SignedIn, SignedOut, SignInButton, useUser } from '@clerk/clerk-react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useApiAuth } from '../hooks/useApiAuth'
import { api, type AdminLogQuery } from '../lib/api'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

const ranges = [
  { id: 'all', label: 'Any time' },
  { id: '24h', label: 'Last 24 hours' },
  { id: '7d', label: 'Last 7 days' },
  { id: '30d', label: 'Last 30 days' },
] as const

function rangeStart(range: (typeof ranges)[number]['id']) {
  const now = Date.now()
  if (range === '24h') return new Date(now - 24 * 60 * 60 * 1000).toISOString()
  if (range === '7d') return new Date(now - 7 * 24 * 60 * 60 * 1000).toISOString()
  if (range === '30d') return new Date(now - 30 * 24 * 60 * 60 * 1000).toISOString()
  return undefined
}

function formatWhen(value: string) {
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value
  return parsed.toLocaleString()
}

function AdminLogs() {
  useApiAuth()
  const [range, setRange] = useState<(typeof ranges)[number]['id']>('7d')
  const [category, setCategory] = useState('')
  const [search, setSearch] = useState('')
  const [submittedSearch, setSubmittedSearch] = useState('')
  const [nickname, setNickname] = useState('')
  const [submittedNickname, setSubmittedNickname] = useState('')
  const from = useMemo(() => rangeStart(range), [range])

  const query: AdminLogQuery = {
    category: category || undefined,
    from,
    q: submittedSearch || undefined,
    nickname: submittedNickname || undefined,
    limit: 100,
  }

  const syncUser = useMutation({
    mutationFn: () => api.syncUserByNickname(nickname.trim()),
  })

  const sessionQuery = useQuery({
    queryKey: ['admin-session'],
    queryFn: api.getAdminSession,
    retry: false,
  })

  const logsQuery = useQuery({
    queryKey: ['admin-logs', query],
    queryFn: () => api.getAdminLogs(query),
    enabled: sessionQuery.isSuccess,
  })

  if (sessionQuery.isLoading) {
    return <p className="text-sm text-muted-foreground">Checking admin access…</p>
  }

  if (sessionQuery.isError) {
    return (
      <p className="text-sm text-amber-300">
        This account is not on the admin allowlist. Set Admin:Emails on the API to your sign-in email.
      </p>
    )
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">Signed in as {sessionQuery.data?.email}</p>
      <form
        className="flex flex-wrap gap-2"
        onSubmit={(event) => {
          event.preventDefault()
          setSubmittedSearch(search.trim())
          setSubmittedNickname(nickname.trim())
        }}
      >
        <label className="text-sm">
          <span className="mb-1 block text-xs text-muted-foreground">Time</span>
          <select
            value={range}
            onChange={(event) => setRange(event.target.value as (typeof ranges)[number]['id'])}
            className="rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm"
          >
            {ranges.map((item) => (
              <option key={item.id} value={item.id}>{item.label}</option>
            ))}
          </select>
        </label>
        <label className="text-sm">
          <span className="mb-1 block text-xs text-muted-foreground">Category</span>
          <select
            value={category}
            onChange={(event) => setCategory(event.target.value)}
            className="rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm"
          >
            <option value="">All categories</option>
            {(logsQuery.data?.categories ?? ['Login', 'Sync', 'FirstPlay', 'Trophy', 'Playtime', 'Ranking']).map((item) => (
              <option key={item} value={item}>{item}</option>
            ))}
          </select>
        </label>
        <label className="min-w-40 text-sm">
          <span className="mb-1 block text-xs text-muted-foreground">Nickname</span>
          <input
            value={nickname}
            onChange={(event) => setNickname(event.target.value)}
            placeholder="nightowl"
            className="w-full rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm outline-none focus:border-primary/60"
          />
        </label>
        <label className="min-w-48 flex-1 text-sm">
          <span className="mb-1 block text-xs text-muted-foreground">Search</span>
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Message, category, or email"
            className="w-full rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm outline-none focus:border-primary/60"
          />
        </label>
        <button
          type="submit"
          className="self-end rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground"
        >
          Search
        </button>
        <button
          type="button"
          disabled={!nickname.trim() || syncUser.isPending}
          onClick={() => {
            setSubmittedNickname(nickname.trim())
            syncUser.mutate()
          }}
          className="self-end rounded-xl border border-border px-4 py-2 text-sm font-semibold disabled:opacity-50"
        >
          {syncUser.isPending ? 'Starting sync…' : 'Sync this player'}
        </button>
      </form>
      {syncUser.isError && (
        <p className="text-sm text-amber-300">
          {syncUser.error instanceof Error ? syncUser.error.message : 'Could not start sync.'}
        </p>
      )}
      {syncUser.isSuccess && (
        <p className="text-sm text-muted-foreground">
          Sync {syncUser.data.status.toLowerCase()} for @{nickname.trim()}: {syncUser.data.phase}
        </p>
      )}
      {logsQuery.isLoading && <p className="text-sm text-muted-foreground">Loading logs…</p>}
      {logsQuery.isError && <p className="text-sm text-amber-300">Could not load logs.</p>}
      {logsQuery.data && (
        <>
          <p className="text-xs text-muted-foreground">{logsQuery.data.total} matching entries</p>
          {logsQuery.data.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">No logs match these filters.</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {logsQuery.data.items.map((entry) => (
                <li key={entry.id} className="rounded-xl border border-border bg-card/60 px-3 py-2">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="text-xs font-semibold uppercase tracking-wide text-primary">{entry.category}</span>
                    <time className="text-xs text-muted-foreground">{formatWhen(entry.createdAt)}</time>
                  </div>
                  <p className="mt-1">{entry.message}</p>
                  {(entry.username || entry.userEmail) && (
                    <p className="mt-1 text-xs text-muted-foreground">
                      {entry.username ? `@${entry.username}` : ''}
                      {entry.username && entry.userEmail ? ' · ' : ''}
                      {entry.userEmail}
                    </p>
                  )}
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </div>
  )
}

function SignedInAdmin() {
  const { user } = useUser()
  return (
    <AdminLogs key={user?.id ?? 'signed-in'} />
  )
}

export function AdminLogsPage() {
  return (
    <main className="mx-auto max-w-4xl px-4 py-8 sm:px-6">
      <p className="text-sm font-semibold uppercase tracking-wide text-primary">Admin</p>
      <h1 className="font-display text-3xl font-bold tracking-tight">Logs</h1>
      <p className="mt-1 text-sm text-muted-foreground">
        Sign-in, sync, and profile milestones. Filter by time and category, or search the text.
      </p>
      <div className="mt-6">
        {!hasClerk ? (
          <AdminLogs />
        ) : (
          <>
            <SignedIn>
              <SignedInAdmin />
            </SignedIn>
            <SignedOut>
              <div className="rounded-2xl border border-border bg-card/60 p-6 text-center">
                <p className="text-sm text-muted-foreground">Sign in with an admin account to view logs.</p>
                <SignInButton mode="modal">
                  <button className="mt-4 rounded-xl bg-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground">
                    Admin sign in
                  </button>
                </SignInButton>
              </div>
            </SignedOut>
          </>
        )}
      </div>
    </main>
  )
}
