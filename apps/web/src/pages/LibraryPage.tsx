import { SignedIn, SignedOut, SignInButton } from '@clerk/clerk-react'
import { AddGameSearch } from '../components/AddGameSearch'
import { CatalogBrowsePage } from '../components/CatalogBrowsePage'
import { LastSyncSummary } from '../components/LastSyncSummary'
import { SyncAllButton } from '../components/SyncAllButton'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

function LibraryTools() {
  return (
    <div className="mb-6 flex flex-col gap-4">
      <SyncAllButton />
      <LastSyncSummary />
      <AddGameSearch />
    </div>
  )
}

function LibraryContent() {
  return (
    <CatalogBrowsePage
      basePath="/library"
      title="My library"
      subtitle="Games in your collection"
      banner={<LibraryTools />}
    />
  )
}

export function LibraryPage() {
  if (!hasClerk) {
    return <LibraryContent />
  }

  return (
    <>
      <SignedIn>
        <LibraryContent />
      </SignedIn>
      <SignedOut>
        <main className="mx-auto max-w-4xl px-6 py-12 text-center">
          <p className="text-muted-foreground">Sign in to manage your game library.</p>
          <SignInButton mode="modal">
            <button className="mt-4 rounded-xl bg-primary px-4 py-2 font-semibold text-primary-foreground">
              Sign in
            </button>
          </SignInButton>
        </main>
      </SignedOut>
    </>
  )
}
