import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { api } from '../lib/api'

export function LibraryPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['library'],
    queryFn: api.getLibrary,
  })

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

      <section className="rounded-2xl border border-slate-800 bg-slate-900/60 p-6">
        {isLoading && <p className="text-slate-400">Loading library…</p>}
        {error && (
          <p className="text-amber-300">
            Could not load library yet. Start the API and database, then refresh.
          </p>
        )}
        {!isLoading && !error && (
          <>
            {data && data.length > 0 ? (
              <ul className="space-y-3">
                {data.map((entry) => (
                  <li
                    key={entry.id}
                    className="rounded-lg border border-slate-800 px-4 py-3 text-sm text-slate-300"
                  >
                    Game {entry.gameId} · {entry.status}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-slate-400">
                No games yet. Phase 0 placeholder — manual add flow arrives next.
              </p>
            )}
          </>
        )}
      </section>
    </main>
  )
}
