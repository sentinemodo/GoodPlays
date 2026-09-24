import { SignedIn, SignedOut, SignInButton } from '@clerk/clerk-react'
import { RefreshCw } from 'lucide-react'
import { PsnConnectionPanel } from '../components/PsnConnectionPanel'
import { SteamConnectionPanel } from '../components/SteamConnectionPanel'
import { useApiAuth } from '../hooks/useApiAuth'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

function SyncPanels() {
  return (
    <div className="mt-8 grid gap-6 lg:grid-cols-2">
      <SteamConnectionPanel />
      <PsnConnectionPanel />
    </div>
  )
}

function AuthedSyncPanels() {
  useApiAuth()
  return <SyncPanels />
}

function ProfileBody({ includeTools }: { includeTools: boolean }) {
  return (
    <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8">
      <div className="flex items-center gap-2 text-primary">
        <RefreshCw className="size-5" />
        <span className="text-sm font-semibold uppercase tracking-wide">Profile</span>
      </div>
      <h1 className="mt-1 font-display text-3xl font-bold tracking-tight sm:text-4xl">Sync your platforms</h1>
      <p className="mt-2 max-w-2xl text-muted-foreground">
        Connect Steam and PlayStation, then pull owned games and playtime into your library. Sign-in is required
        before a sync can run.
      </p>
      {includeTools ? (hasClerk ? <AuthedSyncPanels /> : <SyncPanels />) : null}
    </main>
  )
}

export function ProfilePage() {
  if (!hasClerk) {
    return <ProfileBody includeTools />
  }

  return (
    <>
      <SignedIn>
        <ProfileBody includeTools />
      </SignedIn>
      <SignedOut>
        <main className="mx-auto max-w-3xl px-6 py-16 text-center">
          <h1 className="font-display text-3xl font-bold">Sign in to sync</h1>
          <p className="mt-3 text-muted-foreground">
            Platform connections live on your profile and need an authenticated session.
          </p>
          <SignInButton mode="modal">
            <button className="mt-6 rounded-xl bg-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground ring-glow">
              Sign in
            </button>
          </SignInButton>
        </main>
      </SignedOut>
    </>
  )
}
