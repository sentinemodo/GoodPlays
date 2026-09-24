import { SignedIn, SignedOut, SignInButton, UserButton } from '@clerk/clerk-react'
import { Compass, Gamepad2, Globe2, Library, Menu, Search, UserRound, X } from 'lucide-react'
import { useState } from 'react'
import { Link, useLocation } from 'react-router'
import { appRoute } from '../lib/routing'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

const links = [
  { href: '/', label: 'Discover', icon: Compass },
  { href: '/library', label: 'My Library', icon: Library },
  { href: '/catalog', label: 'Global', icon: Globe2 },
  { href: '/profile', label: 'Profile', icon: UserRound },
]

function navClass(active: boolean) {
  return active
    ? 'bg-primary/15 text-primary'
    : 'text-muted-foreground hover:bg-secondary hover:text-foreground'
}

export function SiteNav() {
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)

  const isActive = (href: string) => (href === '/' ? pathname === '/' : pathname.startsWith(href))

  return (
    <header className="sticky top-0 z-50 border-b border-border/60 bg-background/80 backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-7xl items-center gap-3 px-4 sm:px-6">
        <Link to="/" className="flex items-center gap-2">
          <span className="grid size-9 place-items-center rounded-xl bg-primary text-primary-foreground ring-glow">
            <Gamepad2 className="size-5" />
          </span>
          <span className="font-display text-lg font-bold tracking-tight">
            Good<span className="text-primary text-glow">Plays</span>
          </span>
        </Link>

        <nav className="ml-4 hidden items-center gap-1 md:flex">
          {links.map((link) => {
            const Icon = link.icon
            const active = isActive(link.href)
            return (
              <Link
                key={link.href}
                to={link.href}
                className={`flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${navClass(active)}`}
              >
                <Icon className="size-4" />
                {link.label}
              </Link>
            )
          })}
        </nav>

        <div className="ml-auto flex items-center gap-2">
          <Link
            to="/catalog"
            className="hidden items-center gap-2 rounded-lg border border-border bg-secondary/50 px-3 py-2 text-sm text-muted-foreground transition-colors hover:text-foreground sm:flex"
          >
            <Search className="size-4" />
            <span className="pr-6">Search games...</span>
          </Link>
          {hasClerk ? (
            <>
              <SignedIn>
                <UserButton afterSignOutUrl={appRoute('/')} />
              </SignedIn>
              <SignedOut>
                <SignInButton mode="modal">
                  <button className="rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground ring-glow">
                    Sign in
                  </button>
                </SignInButton>
              </SignedOut>
            </>
          ) : (
            <Link
              to="/sign-in"
              className="rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground ring-glow"
            >
              Sign in
            </Link>
          )}
          <button
            type="button"
            className="grid size-9 place-items-center rounded-lg border border-border md:hidden"
            onClick={() => setOpen((value) => !value)}
            aria-label="Toggle menu"
          >
            {open ? <X className="size-5" /> : <Menu className="size-5" />}
          </button>
        </div>
      </div>

      {open && (
        <nav className="flex flex-col gap-1 border-t border-border/60 p-3 md:hidden">
          {links.map((link) => {
            const Icon = link.icon
            const active = isActive(link.href)
            return (
              <Link
                key={link.href}
                to={link.href}
                onClick={() => setOpen(false)}
                className={`flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium ${navClass(active)}`}
              >
                <Icon className="size-4" />
                {link.label}
              </Link>
            )
          })}
        </nav>
      )}
    </header>
  )
}
