import { SignedIn, SignedOut, SignInButton } from '@clerk/clerk-react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { AddGameSearch } from '../components/AddGameSearch'
import { ImportTextPanel } from '../components/ImportTextPanel'
import { SteamConnectionPanel } from '../components/SteamConnectionPanel'
import { useApiAuth } from '../hooks/useApiAuth'
import { api } from '../lib/api'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

function LibraryContent() {
  useApiAuth()

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['library'],
    queryFn: api.getLibrary,
  })

  return (
    <>
      <AddGameSearch />

      <div className="mt-6 space-y-6">
        <SteamConnectionPanel />
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
                    <div>
                      <p className="text-sm font-medium text-slate-100">{entry.gameTitle}</p>
                      <p className="text-xs text-slate-400">
                        {entry.status}
                        {entry.rating != null ? ` · ${entry.rating}/10` : ''}
                        {entry.hoursPlayed != null ? ` · ${entry.hoursPlayed}h` : ''}
                      </p>
                    </div>
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
    <main className="mx-auto min-h-screen max-w-4xl px-6 py-12">
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
