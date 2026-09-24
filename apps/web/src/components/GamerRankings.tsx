import { Crown } from 'lucide-react'

export type GamerRankingRow = {
  category: string
  position: string | null
  topGamer: string | null
}

const defaultRows: GamerRankingRow[] = [
  { category: 'Games', position: null, topGamer: null },
  { category: 'Hours', position: null, topGamer: null },
  { category: 'Trophies', position: null, topGamer: null },
]

function cell(value: string | null) {
  return value && value.trim() ? value : '—'
}

export function GamerRankings({ rows = defaultRows }: { rows?: GamerRankingRow[] }) {
  return (
    <section className="rounded-3xl border border-border bg-card/70 p-4 sm:p-5">
      <div className="mb-3 flex items-center gap-2 text-xp">
        <Crown className="size-4" />
        <h2 className="font-display text-sm font-bold tracking-wide uppercase">Gamer rankings</h2>
      </div>
      <table className="w-full text-left text-sm">
        <thead>
          <tr className="text-[11px] uppercase tracking-wide text-muted-foreground">
            <th className="pb-2 font-semibold">Category</th>
            <th className="pb-2 font-semibold">Position</th>
            <th className="pb-2 font-semibold">Top gamer</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.category} className="border-t border-border/70">
              <td className="py-2.5 font-medium">{row.category}</td>
              <td className="py-2.5 font-display font-bold text-primary">{cell(row.position)}</td>
              <td className="py-2.5 text-foreground/90">{cell(row.topGamer)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="mt-3 text-xs text-muted-foreground">
        Community boards fill in as more libraries sync.
      </p>
    </section>
  )
}
