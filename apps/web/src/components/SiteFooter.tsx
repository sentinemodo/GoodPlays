export function SiteFooter() {
  return (
    <footer className="border-t border-slate-800/80 bg-slate-950/80 px-6 py-4">
      <p className="mx-auto max-w-4xl text-center text-xs text-slate-500">
        Game metadata and cover art provided by{' '}
        <a
          href="https://www.igdb.com/"
          target="_blank"
          rel="noreferrer noopener"
          className="text-slate-400 underline decoration-slate-600 underline-offset-2 hover:text-slate-300"
        >
          IGDB
        </a>
        .
      </p>
    </footer>
  )
}
