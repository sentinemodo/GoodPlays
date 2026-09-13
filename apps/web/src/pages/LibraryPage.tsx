import { SignedIn, SignedOut, SignInButton } from '@clerk/clerk-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { AddGameSearch } from '../components/AddGameSearch'
import { ImportTextPanel } from '../components/ImportTextPanel'
import { PsnConnectionPanel } from '../components/PsnConnectionPanel'
import { SteamConnectionPanel } from '../components/SteamConnectionPanel'
import { useApiAuth } from '../hooks/useApiAuth'
import { api } from '../lib/api'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

function platformLabel(source: string) {
  switch (source) {
    case 'SteamSync':
      return 'Steam'
    case 'PsnSync':
      return 'PlayStation'
    case 'Manual':
      return 'Manual'
    case 'ImportText':
      return 'Import'
    default:
      return null
  }
}

function LibraryContent() {
  useApiAuth()
  const queryClient = useQueryClient()
  const [removingEntryId, setRemovingEntryId] = useState<string | null>(null)

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['library'],
    queryFn: api.getLibrary,
  })

  const removeMutation = useMutation({
    mutationFn: (entryId: string) => api.removeFromLibrary(entryId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['library'] })
      queryClient.invalidateQueries({ queryKey: ['recommendations'] })
    },
    onSettled: () => setRemovingEntryId(null),
  })

  const handleRemove = (entryId: string, title: string) => {
    if (!window.confirm(`Remove "${title}" from your library?`)) {
      return
    }

    setRemovingEntryId(entryId)
    removeMutation.mutate(entryId)
  }

  return (
    <>
      <AddGameSearch />

      <div className="mt-6 space-y-6">
        <SteamConnectionPanel />
        <PsnConnectionPanel />
        <ImportTextPanel />
      </div>

      <section className="mt-6 rounded-2xl border border-slate-800 bg-slate-900/60 p-6">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-medium text-slate-100">Your games</h2>
          <button
            type="button"
            onClick={() => refetch()}
            className="text-xs text-slate-400 hover:text-slate-200"
          >
            Refresh
          </button>
        </div>

        {isLoading && <p className="text-slate-400">Loading library…</p>}
        {error && (
          <p className="text-amber-300">
            Could not load library. Start the API and database, then sign in and refresh.
          </p>
        )}
        {!isLoading && !error && (
          <>
            {data && data.length > 0 ? (
              <ul className="space-y-3">
                {data.map((entry) => (
                  <li
                    key={entry.id}
                    className="flex items-center gap-3 rounded-lg border border-slate-800 px-4 py-3"
                  >
                    {entry.coverUrl ? (
                      <img
                        src={entry.coverUrl}
                        alt=""
                        className="h-14 w-10 rounded object-cover"
                      />
                    ) : (
                      <div className="flex h-14 w-10 items-center justify-center rounded bg-slate-800 text-xs text-slate-500">
                        ?
                      </div>
                    )}
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="text-sm font-medium text-slate-100">{entry.gameTitle}</p>
                        {platformLabel(entry.source) && (
                          <span className="rounded-full border border-slate-700 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-slate-400">
                            {platformLabel(entry.source)}
                          </span>
                        )}
                      </div>
                      <p className="text-xs text-slate-400">
                        {entry.status}
                        {entry.rating != null ? ` · ${entry.rating}/10` : ''}
                        {entry.hoursPlayed != null ? ` · ${entry.hoursPlayed}h` : ''}
                      </p>
                    </div>
                    <button
                      type="button"
                      onClick={() => handleRemove(entry.id, entry.gameTitle)}
                      disabled={removingEntryId === entry.id}
                      className="shrink-0 rounded border border-slate-700 px-2 py-1 text-xs text-slate-400 hover:border-red-900 hover:text-red-300 disabled:opacity-50"
                      aria-label={`Remove ${entry.gameTitle} from library`}
                    >
                      {removingEntryId === entry.id ? 'Removing…' : 'Remove'}
                    </button>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-slate-400">No games yet. Search above to add your first title.</p>
            )}
          </>
        )}
      </section>
    </>
  )
}

export function LibraryPage() {
  return (
    <main className="mx-auto max-w-4xl px-6 py-12">
      <div className="mb-8 flex items-center justify-between">
        <div>
          <p className="text-sm text-emerald-400">Your collection</p>
          <h1 className="text-3xl font-semibold">Library</h1>
        </div>
        <Link to="/" className="text-sm text-slate-400 hover:text-slate-200">
          Back home
        </Link>
      </div>

      {hasClerk ? (
        <>
          <SignedIn>
            <LibraryContent />
          </SignedIn>
          <SignedOut>
            <section className="rounded-2xl border border-slate-800 bg-slate-900/60 p-6 text-center">
              <p className="text-slate-300">Sign in to manage your game library.</p>
              <SignInButton mode="modal">
                <button className="mt-4 rounded-lg bg-emerald-500 px-4 py-2 font-medium text-slate-950">
                  Sign in
                </button>
              </SignInButton>
            </section>
          </SignedOut>
        </>
      ) : (
        <LibraryContent />
      )}
    </main>
  )
}
