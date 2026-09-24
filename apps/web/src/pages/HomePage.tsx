import { SignedIn, SignedOut, SignInButton, useUser } from '@clerk/clerk-react'
import { useQuery } from '@tanstack/react-query'
import { ArrowRight, Library } from 'lucide-react'
import { Link } from 'react-router'
import { GamerRankings } from '../components/GamerRankings'
import { RecommendationsPanel } from '../components/RecommendationsPanel'
import { useApiAuth } from '../hooks/useApiAuth'
import { api } from '../lib/api'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

function WelcomeLine() {
  const { user } = useUser()
  const name = user?.firstName ?? 'player'
  return <span>Welcome back, {name}</span>
}

function LibraryStats() {
  useApiAuth()
  const { data } = useQuery({
    queryKey: ['catalog-facets'],
    queryFn: api.getCatalogFacets,
  })

  if (data?.myLibraryCount == null) {
    return null
  }

  return (
    <div className="mt-8 grid max-w-sm grid-cols-2 gap-3">
      <div className="rounded-2xl border border-border bg-card/60 px-4 py-3">
        <p className="text-xs uppercase tracking-wide text-muted-foreground">Games</p>
        <p className="mt-1 font-display text-2xl font-bold text-primary">{data.myLibraryCount}</p>
      </div>
      <div className="rounded-2xl border border-border bg-card/60 px-4 py-3">
        <p className="text-xs uppercase tracking-wide text-muted-foreground">Hours</p>
        <p className="mt-1 font-display text-2xl font-bold text-cyan">
          {data.myLibraryHours != null ? Math.round(data.myLibraryHours) : '—'}
        </p>
      </div>
    </div>
  )
}

function SignedInHeroExtras() {
  return (
    <>
      <p className="mt-6 inline-flex rounded-full bg-primary/15 px-3 py-1 text-xs font-semibold text-primary">
        <WelcomeLine />
      </p>
      <LibraryStats />
    </>
  )
}

function SignedInRecommendations() {
  useApiAuth()
  return <RecommendationsPanel />
}

export function HomePage() {
  return (
    <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8">
      <section className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <div className="relative overflow-hidden rounded-3xl border border-border bg-gradient-to-br from-primary/20 via-card to-cyan/10 p-6 sm:p-10">
          <h1 className="font-display text-4xl font-bold tracking-tight sm:text-6xl">
            Play, Flex, Discover.
          </h1>
          <p className="mt-4 max-w-xl text-pretty text-muted-foreground sm:text-lg">
            Auto-sync your libraries, show off what you have played, and let GoodPlays find your next game.
          </p>
          <div className="mt-6 flex flex-wrap gap-3">
            <Link
              to="/library"
              className="inline-flex items-center gap-2 rounded-xl bg-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground ring-glow transition-transform hover:scale-[1.02]"
            >
              <Library className="size-4" />
              Open my library
              <ArrowRight className="size-4" />
            </Link>
            <Link
              to="/catalog"
              className="inline-flex items-center gap-2 rounded-xl border border-border bg-secondary/50 px-5 py-2.5 text-sm font-semibold transition-colors hover:bg-secondary"
            >
              Browse global
            </Link>
            {hasClerk && (
              <SignedOut>
                <SignInButton mode="modal">
                  <button className="inline-flex items-center gap-2 rounded-xl border border-border bg-secondary/50 px-5 py-2.5 text-sm font-semibold transition-colors hover:bg-secondary">
                    Sign in
                  </button>
                </SignInButton>
              </SignedOut>
            )}
          </div>
          {hasClerk && (
            <SignedIn>
              <SignedInHeroExtras />
            </SignedIn>
          )}
        </div>
        <GamerRankings />
      </section>

      <section className="mt-10">
        {hasClerk ? (
          <>
            <SignedIn>
              <SignedInRecommendations />
            </SignedIn>
            <SignedOut>
              <div className="rounded-3xl border border-border bg-card/60 p-6 text-muted-foreground">
                Sign in to see personalized game recommendations.
              </div>
            </SignedOut>
          </>
        ) : (
          <RecommendationsPanel />
        )}
      </section>
    </main>
  )
}
