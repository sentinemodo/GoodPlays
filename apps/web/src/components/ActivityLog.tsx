import { useQuery } from '@tanstack/react-query'
import { ScrollText } from 'lucide-react'
import { api } from '../lib/api'

function formatWhen(value: string) {
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }
  return parsed.toLocaleString()
}

export function ActivityLog() {
  const activityQuery = useQuery({
    queryKey: ['activity'],
    queryFn: api.getActivity,
    refetchInterval: 5000,
  })

  return (
    <section className="rounded-2xl border border-border bg-card/60 p-4">
      <div className="flex items-center gap-2 text-sm font-semibold">
        <ScrollText className="size-4 text-cyan" />
        Activity
      </div>
      <p className="mt-1 text-sm text-muted-foreground">
        First sessions, trophies, 100-hour marks, and ranking milestones.
      </p>
      {activityQuery.isLoading && <p className="mt-3 text-sm text-muted-foreground">Loading activity…</p>}
      {activityQuery.isError && (
        <p className="mt-3 text-sm text-amber-300">Sign in to see activity.</p>
      )}
      {activityQuery.data && activityQuery.data.length === 0 && (
        <p className="mt-3 text-sm text-muted-foreground">No milestones yet. They appear after you play, unlock a trophy, or place on a ranking.</p>
      )}
      {activityQuery.data && activityQuery.data.length > 0 && (
        <ul className="mt-3 max-h-64 space-y-2 overflow-y-auto text-sm">
          {activityQuery.data.map((entry) => (
            <li key={entry.id} className="rounded-xl bg-secondary/50 px-3 py-2">
              <div className="flex items-center justify-between gap-3">
                <span className="text-xs font-semibold uppercase tracking-wide text-primary">{entry.category}</span>
                <time className="text-xs text-muted-foreground">{formatWhen(entry.createdAt)}</time>
              </div>
              <p className="mt-1 text-foreground/90">{entry.message}</p>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
