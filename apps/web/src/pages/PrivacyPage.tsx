import { Link } from 'react-router'

export function PrivacyPage() {
  return (
    <main className="mx-auto min-h-screen max-w-2xl px-6 py-16">
      <Link to="/" className="text-sm text-slate-400 hover:text-slate-200">
        ← Back home
      </Link>

      <h1 className="mt-6 text-3xl font-semibold">Privacy Policy</h1>
      <p className="mt-2 text-sm text-slate-400">Last updated: September 2026</p>

      <div className="mt-8 space-y-6 text-sm leading-relaxed text-slate-300">
        <section>
          <h2 className="text-lg font-medium text-slate-100">Overview</h2>
          <p className="mt-2">
            GoodPlays is a personal game library application. This policy describes what data the
            service collects and how it is used.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-medium text-slate-100">Data we collect</h2>
          <ul className="mt-2 list-disc space-y-1 pl-5">
            <li>Account information from Clerk (email, display name, user ID)</li>
            <li>Library entries you create (games, ratings, play status, hours played)</li>
            <li>Technical logs required to operate and secure the API</li>
          </ul>
        </section>

        <section>
          <h2 className="text-lg font-medium text-slate-100">Third-party services</h2>
          <p className="mt-2">
            GoodPlays uses Clerk for authentication and IGDB (via Twitch) for game metadata search.
            Those providers process data according to their own privacy policies.
          </p>
        </section>

        <section>
          <h2 className="text-lg font-medium text-slate-100">Contact</h2>
          <p className="mt-2">
            Questions about this policy can be sent via the GoodPlays GitHub repository issue
            tracker.
          </p>
        </section>
      </div>
    </main>
  )
}
