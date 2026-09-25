import { SignedIn, SignedOut, SignInButton } from '@clerk/clerk-react'
import { Download } from 'lucide-react'
import { ImportTextPanel } from '../components/ImportTextPanel'
import { LastSyncSummary } from '../components/LastSyncSummary'
import { PsnConnectionPanel } from '../components/PsnConnectionPanel'
import { SteamConnectionPanel } from '../components/SteamConnectionPanel'
import { useApiAuth } from '../hooks/useApiAuth'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

function ImportTools() {
  return (
    <div className="mt-8 flex flex-col gap-6">
      <LastSyncSummary />
      <ImportTextPanel />
      <div className="grid gap-6 lg:grid-cols-2">
        <SteamConnectionPanel />
        <PsnConnectionPanel />
      </div>
    </div>
  )
}

function AuthedImportTools() {
  useApiAuth()
  return <ImportTools />
}

function ImportBody({ includeTools }: { includeTools: boolean }) {
  return (
    <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8">
      <div className="flex items-center gap-2 text-primary">
        <Download className="size-5" />
        <span className="text-sm font-semibold uppercase tracking-wide">Import</span>
      </div>
      <h1 className="mt-1 font-display text-3xl font-bold tracking-tight sm:text-4xl">Bring in your games</h1>
      <p className="mt-2 max-w-2xl text-muted-foreground">
        Paste a title list, or connect Steam and PlayStation and pull owned games and playtime into your library.
      </p>
      {includeTools ? (hasClerk ? <AuthedImportTools /> : <ImportTools />) : null}
    </main>
  )
}

export function ImportPage() {
  if (!hasClerk) {
    return <ImportBody includeTools />
  }

  return (
    <>
      <SignedIn>
        <ImportBody includeTools />
      </SignedIn>
      <SignedOut>
        <main className="mx-auto max-w-3xl px-6 py-16 text-center">
          <h1 className="font-display text-3xl font-bold">Sign in to import</h1>
          <p className="mt-3 text-muted-foreground">
            Text import and Steam or PlayStation sync need an authenticated session.
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
