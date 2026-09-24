import { SignIn } from '@clerk/clerk-react'
import { Link } from 'react-router'
import { appRoute } from '../lib/routing'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)
const signInPath = appRoute('/sign-in')

export function SignInPage() {
  return (
    <main className="mx-auto flex min-h-[calc(100vh-4rem)] max-w-lg flex-col items-center justify-center gap-6 px-6 py-12">
      <Link to="/" className="text-sm text-muted-foreground hover:text-foreground">
        ← Back home
      </Link>
      {hasClerk ? (
        <SignIn routing="path" path={signInPath} signUpUrl={signInPath} />
      ) : (
        <p className="text-center text-muted-foreground">
          Configure <code className="text-primary">VITE_CLERK_PUBLISHABLE_KEY</code> to enable
          Clerk sign-in.
        </p>
      )}
    </main>
  )
}
