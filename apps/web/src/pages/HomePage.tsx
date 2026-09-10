import { SignedIn, SignedOut, SignInButton, UserButton } from '@clerk/clerk-react'
import { Link } from 'react-router'
import { RecommendationsPanel } from '../components/RecommendationsPanel'
import { useApiAuth } from '../hooks/useApiAuth'
import { appRoute } from '../lib/routing'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

function SignedInRecommendations() {
  useApiAuth()
  return (
    <div className="mt-6">
      <RecommendationsPanel />
    </div>
  )
}

export function HomePage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-3xl flex-col gap-8 px-6 py-16">
      <header className="flex items-center justify-between">
        <div>
          <p className="text-sm uppercase tracking-[0.2em] text-emerald-400">GoodPlays</p>
          <h1 className="mt-2 text-4xl font-semibold">Goodreads for games</h1>
        </div>
        {hasClerk ? (
          <>
            <SignedIn>
              <UserButton afterSignOutUrl={appRoute('/')} />
            </SignedIn>
            <SignedOut>
              <SignInButton mode="modal">
                <button className="rounded-lg bg-emerald-500 px-4 py-2 font-medium text-slate-950">
                  Sign in
                </button>
              </SignInButton>
            </SignedOut>
          </>
        ) : (
          <Link
            to="/sign-in"
            className="rounded-lg bg-emerald-500 px-4 py-2 font-medium text-slate-950"
          >
            Sign in
          </Link>
        )}
      </header>

      <section className="rounded-2xl border border-slate-800 bg-slate-900/60 p-8">
        <p className="text-lg text-slate-300">
          Track your library, rate games, and discover what to play next. Search IGDB and add games to your collection.
        </p>
        <div className="mt-6 flex flex-wrap gap-3">
          <Link
            to="/library"
            className="rounded-lg bg-emerald-500 px-4 py-2 font-medium text-slate-950"
          >
            Open library
          </Link>
          <Link
            to="/sign-in"
            className="rounded-lg border border-slate-700 px-4 py-2 text-slate-200"
          >
            Sign in page
          </Link>
          <Link
            to="/privacy"
            className="rounded-lg border border-slate-700 px-4 py-2 text-slate-200"
          >
            Privacy policy
          </Link>
        </div>
      </section>

      {hasClerk ? (
        <>
          <SignedIn>
            <SignedInRecommendations />
          </SignedIn>
          <SignedOut>
            <section className="mt-6 rounded-2xl border border-slate-800 bg-slate-900/60 p-6 text-slate-400">
              Sign in to see personalized game recommendations.
            </section>
          </SignedOut>
        </>
      ) : (
        <div className="mt-6">
          <RecommendationsPanel />
        </div>
      )}
    </main>
  )
}
