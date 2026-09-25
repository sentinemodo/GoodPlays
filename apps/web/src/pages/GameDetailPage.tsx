import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router'
import { GameTagEditor } from '../components/GameTagEditor'
import { StarRatingInput } from '../components/StarRating'
import { useApiAuth } from '../hooks/useApiAuth'
import { api, type GameDetail, type UpdateLibraryEntryRequest } from '../lib/api'
import { platformMarks } from '../lib/platformMarks'
import { halfStarsToDisplay } from '../lib/ratings'

function ratingLabel(source: string) {
  switch (source) {
    case 'Steam':
      return 'Steam'
    case 'OpenCritic':
      return 'OpenCritic'
    case 'Igdb':
      return 'IGDB'
    case 'Psn':
      return 'PlayStation'
    default:
      return source
  }
}

function GameDetailContent() {
  useApiAuth()
  const { slug = '' } = useParams()
  const queryClient = useQueryClient()
  const [commentBody, setCommentBody] = useState('')
  const [commentRating, setCommentRating] = useState<number | ''>('')

  const coverPolls = useRef(0)
  useEffect(() => {
    coverPolls.current = 0
  }, [slug])

  const { data, isLoading, error } = useQuery({
    queryKey: ['game', slug],
    queryFn: () => api.getGame(slug),
    enabled: Boolean(slug),
    refetchInterval: (query) => {
      const game = query.state.data
      const missing = Boolean(game && (!game.coverUrl || game.dlc.some((dlc) => !dlc.coverUrl)))
      if (!missing) {
        coverPolls.current = 0
        return false
      }
      if (coverPolls.current >= 8) {
        return false
      }
      coverPolls.current += 1
      return 4000
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ entryId, body }: { entryId: string; body: UpdateLibraryEntryRequest }) =>
      api.updateLibraryEntry(entryId, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['game', slug] })
      queryClient.invalidateQueries({ queryKey: ['catalog'] })
      queryClient.invalidateQueries({ queryKey: ['library'] })
    },
  })

  const commentMutation = useMutation({
    mutationFn: () =>
      api.createComment(slug, {
        body: commentBody,
        rating: commentRating === '' ? null : commentRating,
      }),
    onSuccess: () => {
      setCommentBody('')
      setCommentRating('')
      queryClient.invalidateQueries({ queryKey: ['game', slug] })
    },
  })

  if (isLoading) {
    return <p className="mx-auto max-w-5xl px-6 py-12 text-muted-foreground">Loading game…</p>
  }

  if (error || !data) {
    return (
      <p className="mx-auto max-w-5xl px-6 py-12 text-amber-300">
        Game not found or API unavailable.
      </p>
    )
  }

  return <GameDetailView game={data} onUpdateLibrary={updateMutation.mutate} onSubmitComment={() => commentMutation.mutate()} commentBody={commentBody} setCommentBody={setCommentBody} commentRating={commentRating} setCommentRating={setCommentRating} commentPending={commentMutation.isPending} />
}

function GameDetailView({
  game,
  onUpdateLibrary,
  onSubmitComment,
  commentBody,
  setCommentBody,
  commentRating,
  setCommentRating,
  commentPending,
}: {
  game: GameDetail
  onUpdateLibrary: (args: { entryId: string; body: UpdateLibraryEntryRequest }) => void
  onSubmitComment: () => void
  commentBody: string
  setCommentBody: (v: string) => void
  commentRating: number | ''
  setCommentRating: (v: number | '') => void
  commentPending: boolean
}) {
  const entry = game.userLibraryEntry
  const marks = platformMarks(game.platforms)

  return (
    <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8">
      <Link to="/catalog" className="text-sm text-muted-foreground hover:text-foreground">
        ← Back to global library
      </Link>

      <section className="relative mt-4 overflow-hidden rounded-3xl border border-border">
        <div className="absolute inset-0 bg-gradient-to-br from-primary/30 via-card to-cyan/10" />
        <div className="relative flex flex-col gap-6 p-5 sm:flex-row sm:items-end sm:p-8">
          {game.coverUrl ? (
            <img src={game.coverUrl} alt="" className="aspect-[3/4] w-40 rounded-xl object-cover shadow-2xl sm:w-48" />
          ) : (
            <div className="flex aspect-[3/4] w-40 items-center justify-center rounded-xl bg-muted text-muted-foreground sm:w-48">
              No cover
            </div>
          )}
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              {game.releaseDate && (
                <span className="rounded-full bg-primary/15 px-2.5 py-1 text-xs font-semibold text-primary">
                  {game.releaseDate.slice(0, 4)}
                </span>
              )}
              {game.genres.map((genre) => (
                <span key={genre} className="rounded-full border border-border px-2.5 py-1 text-xs text-muted-foreground">
                  {genre}
                </span>
              ))}
            </div>
            <h1 className="mt-3 font-display text-3xl font-bold tracking-tight sm:text-5xl">{game.title}</h1>
            <p className="mt-1 text-muted-foreground">
              {[game.developer, game.publisher].filter(Boolean).join(' · ')}
            </p>
            {game.summary && (
              <p className="mt-3 max-w-2xl text-sm leading-relaxed text-foreground/80">{game.summary}</p>
            )}
            {marks.length > 0 && (
              <div className="mt-4 flex flex-wrap gap-2">
                {marks.map((mark) => (
                  <span
                    key={mark.name}
                    className="rounded-lg px-2 py-1 text-xs font-bold text-white"
                    style={{ background: mark.color }}
                    title={mark.name}
                  >
                    {mark.short}
                  </span>
                ))}
              </div>
            )}
          </div>
        </div>
      </section>

      <section className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Players" value={String(game.stats.totalPlayers)} />
        <StatCard label="Total hours" value={`${game.stats.totalHours}h`} />
        <StatCard
          label="Avg rating"
          value={game.stats.avgRating != null ? halfStarsToDisplay(game.stats.avgRating) : '—'}
        />
        <StatCard label="Completed" value={`${game.stats.completionRate.toFixed(0)}%`} />
      </section>

      {game.ratings.length > 0 && (
        <section className="mt-8">
          <h2 className="mb-3 text-lg font-medium">External ratings</h2>
          <div className="flex flex-wrap gap-3">
            {game.ratings.map((r) => (
              <a
                key={r.source}
                href={r.url ?? undefined}
                target="_blank"
                rel="noreferrer"
                className="rounded-lg border border-border bg-card/60 px-4 py-2 text-sm"
              >
                <span className="text-muted-foreground">{ratingLabel(r.source)}</span>
                <span className="ml-2 font-medium text-foreground">
                  {r.score != null ? r.score : '—'}
                  {r.reviewCount != null ? ` (${r.reviewCount})` : ''}
                </span>
              </a>
            ))}
          </div>
        </section>
      )}

      {game.heroes.length > 0 && (
        <section className="mt-8">
          <h2 className="mb-3 text-lg font-medium">Heroes</h2>
          <ul className="space-y-2">
            {game.heroes.map((hero) => (
              <li key={`${hero.metric}-${hero.username}`} className="rounded-lg border border-border px-4 py-2 text-sm">
                <span className="font-medium text-primary">{hero.displayName ?? hero.username}</span>
                <span className="text-muted-foreground"> — {hero.metric}: {hero.value}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {entry && (
        <section className="mt-8 rounded-2xl border border-border bg-card/60 p-6">
          <h2 className="mb-4 text-lg font-medium">Your library entry</h2>
          <GameTagEditor gameId={game.id} gameTags={game.userTags ?? []} />
          <div className="flex flex-wrap gap-4">
            <label className="text-sm text-muted-foreground">
              Status
              <select
                value={entry.status}
                onChange={(e) =>
                  onUpdateLibrary({ entryId: entry.id, body: { status: e.target.value } })
                }
                className="ml-2 rounded border border-border bg-secondary px-2 py-1 text-foreground"
              >
                {['Owned', 'Playing', 'Completed', 'Backlog', 'Dropped'].map((s) => (
                  <option key={s} value={s}>{s}</option>
                ))}
              </select>
            </label>
            <div className="text-sm text-muted-foreground">
              <p className="mb-1">Rating</p>
              <StarRatingInput
                value={entry.rating}
                onChange={(rating) =>
                  onUpdateLibrary({
                    entryId: entry.id,
                    body: { rating },
                  })
                }
              />
            </div>
            <label className="text-sm text-muted-foreground">
              Hours
              <input
                type="number"
                min={0}
                step={0.1}
                value={entry.hoursPlayed ?? ''}
                onChange={(e) =>
                  onUpdateLibrary({
                    entryId: entry.id,
                    body: { hoursPlayed: e.target.value === '' ? null : Number(e.target.value) },
                  })
                }
                className="ml-2 w-20 rounded border border-border bg-secondary px-2 py-1 text-foreground"
              />
            </label>
          </div>
        </section>
      )}

      {game.dlc.length > 0 && (
        <section className="mt-8">
          <h2 className="mb-3 text-lg font-medium">DLC</h2>
          <ul className="space-y-2">
            {game.dlc.map((d) => (
              <li key={d.id}>
                <Link to={`/games/${d.slug}`} className="text-sm text-primary hover:underline">
                  {d.title}
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}

      {game.achievements.length > 0 && (
        <section className="mt-8">
          <h2 className="mb-3 text-lg font-medium">Achievements</h2>
          <ul className="space-y-3">
            {game.achievements.map((a) => (
              <li key={a.id} className="rounded-lg border border-border px-4 py-3">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-foreground">{a.name}</p>
                  {a.unlockedByCurrentUser && (
                    <span className="rounded bg-primary/15 px-2 py-0.5 text-[10px] text-primary">Unlocked</span>
                  )}
                </div>
                {a.description && <p className="mt-1 text-xs text-muted-foreground">{a.description}</p>}
                {a.owners.length > 0 && (
                  <p className="mt-2 text-xs text-muted-foreground">
                    Unlocked by: {a.owners.map((o) => o.displayName ?? o.username).join(', ')}
                  </p>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}

      {game.news.length > 0 && (
        <section className="mt-8">
          <h2 className="mb-3 text-lg font-medium">News</h2>
          <ul className="space-y-2">
            {game.news.map((n) => (
              <li key={n.id}>
                <a href={n.url} target="_blank" rel="noreferrer" className="text-sm text-primary hover:underline">
                  [{n.source}] {n.title}
                </a>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="mt-8 rounded-2xl border border-border bg-card/60 p-6">
        <h2 className="mb-4 text-lg font-medium">Reviews & comments</h2>
        <div className="mb-6 space-y-3">
          <textarea
            value={commentBody}
            onChange={(e) => setCommentBody(e.target.value)}
            placeholder="Share your thoughts…"
            rows={3}
            className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground"
          />
          <div className="flex items-center gap-3">
            <StarRatingInput
              value={commentRating === '' ? null : commentRating}
              onChange={(rating) => setCommentRating(rating ?? '')}
            />
            <button
              type="button"
              disabled={commentPending || !commentBody.trim()}
              onClick={onSubmitComment}
              className="rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50"
            >
              Post review
            </button>
          </div>
        </div>
        <ul className="space-y-4">
          {game.comments.map((c) => (
            <li key={c.id} className="border-t border-border pt-4">
              <p className="text-sm font-medium text-foreground">
                {c.displayName ?? c.username}
                {c.rating != null ? ` · ${halfStarsToDisplay(c.rating)}` : ''}
              </p>
              <p className="mt-1 text-sm text-foreground/80">{c.body}</p>
            </li>
          ))}
          {game.comments.length === 0 && <p className="text-sm text-muted-foreground">No reviews yet.</p>}
        </ul>
      </section>
    </main>
  )
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-border bg-card/60 p-4">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="mt-1 font-display text-xl font-bold text-foreground">{value}</p>
    </div>
  )
}

export function GameDetailPage() {
  return <GameDetailContent />
}
