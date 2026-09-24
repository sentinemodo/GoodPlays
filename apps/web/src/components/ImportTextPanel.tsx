import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api, type ImportJobSummary } from '../lib/api'

function ImportStatus({ job }: { job: ImportJobSummary }) {
  const { stats, status } = job
  return (
    <div className="mt-3 rounded-lg border border-border bg-background/50 p-4 text-sm">
      <p className="text-foreground/80">
        Status: <span className="text-primary">{status}</span>
      </p>
      <p className="mt-1 text-muted-foreground">
        Added {stats.addedCount} · Skipped {stats.skippedCount} · Unmatched {stats.unmatchedCount}
        {stats.ambiguousCount > 0 ? ` · Needs review ${stats.ambiguousCount}` : ''}
      </p>
      {stats.lines.length > 0 && (
        <ul className="mt-3 max-h-48 space-y-1 overflow-y-auto text-xs text-muted-foreground">
          {stats.lines.map((line) => (
            <li key={line.rawLine}>
              <span className="text-foreground">{line.rawLine}</span>
              {' — '}
              {line.outcome}
              {line.matchedTitle ? ` (${line.matchedTitle})` : ''}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

export function ImportTextPanel() {
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const [activeJobId, setActiveJobId] = useState<string | null>(null)

  const importMutation = useMutation({
    mutationFn: (payload: string) => api.createTextImport(payload),
    onSuccess: (job) => {
      setActiveJobId(job.id)
      setText('')
    },
  })

  const jobQuery = useQuery({
    queryKey: ['import', activeJobId],
    queryFn: () => api.getImport(activeJobId!),
    enabled: Boolean(activeJobId),
    refetchInterval: (query) => {
      const status = query.state.data?.status
      if (!status || status === 'Completed' || status === 'Failed' || status === 'AwaitingReview') {
        return false
      }
      return 1500
    },
  })

  const isProcessing =
    jobQuery.data?.status === 'Pending' || jobQuery.data?.status === 'Processing'

  return (
    <section className="rounded-2xl border border-border bg-card/60 p-6">
      <h2 className="text-lg font-medium text-foreground">Import from text</h2>
      <p className="mt-1 text-sm text-muted-foreground">
        Paste a list of game titles (one per line). We match against IGDB and add exact hits to your library.
      </p>
      <textarea
        value={text}
        onChange={(e) => setText(e.target.value)}
        rows={5}
        placeholder={'Hades\nCeleste\nDisco Elysium'}
        className="mt-4 w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground"
      />
      <button
        type="button"
        disabled={!text.trim() || importMutation.isPending || isProcessing}
        onClick={() => importMutation.mutate(text)}
        className="mt-3 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50"
      >
        {importMutation.isPending || isProcessing ? 'Importing…' : 'Start import'}
      </button>
      {importMutation.isError && (
        <p className="mt-2 text-sm text-amber-300">Import failed. Check the API is running.</p>
      )}
      {jobQuery.data && (
        <>
          <ImportStatus job={jobQuery.data} />
          {(jobQuery.data.status === 'Completed' || jobQuery.data.status === 'AwaitingReview') && (
            <button
              type="button"
              onClick={() => {
                queryClient.invalidateQueries({ queryKey: ['library'] })
              }}
              className="mt-3 text-xs text-primary hover:text-primary"
            >
              Refresh library list
            </button>
          )}
        </>
      )}
    </section>
  )
}
