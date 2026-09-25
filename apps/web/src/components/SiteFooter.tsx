import { Link } from 'react-router'

export function SiteFooter() {
  return (
    <footer className="border-t border-border/80 bg-background/80 px-6 py-4">
      <p className="mx-auto max-w-4xl text-center text-xs text-muted-foreground">
        Game metadata and cover art provided by{' '}
        <a
          href="https://www.igdb.com/"
          target="_blank"
          rel="noreferrer noopener"
          className="text-foreground/80 underline decoration-border underline-offset-2 hover:text-foreground"
        >
          IGDB
        </a>
        .{' '}
        <Link to="/admin" className="text-foreground/80 underline decoration-border underline-offset-2 hover:text-foreground">
          Admin
        </Link>
      </p>
    </footer>
  )
}
