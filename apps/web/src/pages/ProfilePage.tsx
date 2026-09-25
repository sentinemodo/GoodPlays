import { SignedIn, SignedOut, SignInButton } from '@clerk/clerk-react'
import { UserRound } from 'lucide-react'
import { Link } from 'react-router'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

function ProfileBody() {
  return (
    <main className="mx-auto max-w-3xl px-4 py-6 sm:px-6 sm:py-8">
      <div className="flex items-center gap-2 text-primary">
        <UserRound className="size-5" />
        <span className="text-sm font-semibold uppercase tracking-wide">Profile</span>
      </div>
      <h1 className="mt-1 font-display text-3xl font-bold tracking-tight sm:text-4xl">Your account</h1>
      <p className="mt-2 text-muted-foreground">
        Text import and Steam or PlayStation sync live on the import page.
      </p>
      <Link
        to="/import"
        className="mt-6 inline-flex rounded-xl bg-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground ring-glow"
      >
        Open import & sync
      </Link>
    </main>
  )
}

export function ProfilePage() {
  if (!hasClerk) {
    return <ProfileBody />
  }

  return (
    <>
      <SignedIn>
        <ProfileBody />
      </SignedIn>
      <SignedOut>
        <main className="mx-auto max-w-3xl px-6 py-16 text-center">
          <h1 className="font-display text-3xl font-bold">Sign in</h1>
          <p className="mt-3 text-muted-foreground">Your profile is available after you sign in.</p>
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
