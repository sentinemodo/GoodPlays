import { Link } from 'react-router'
import type { CatalogGame } from '../lib/catalogTypes'
import { formatLibrarySource } from '../lib/libraryLabels'
import { multiPlatformMarks } from '../lib/platformMarks'
import { halfStarsToDisplay } from '../lib/ratings'
import { GameTagEditor } from './GameTagEditor'
import { StarRatingDisplay } from './StarRating'

function PlatformMarks({ platforms }: { platforms: string[] }) {
  const marks = multiPlatformMarks(platforms)
  if (marks.length === 0) {
    return null
  }

  return (
    <span className="inline-flex flex-wrap items-center gap-1" title="On more than one platform">
      {marks.map((mark) => (
        <span
          key={mark.name}
          className="rounded px-1.5 py-0.5 text-[10px] font-bold leading-none text-white"
          style={{ background: mark.color }}
          title={mark.name}
        >
          {mark.short}
        </span>
      ))}
    </span>
  )
}

function formatLastPlayed(date: string | null): string | null {
  if (!date) return null
  const parsed = new Date(date)
  if (Number.isNaN(parsed.getTime())) return null
  return parsed.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function LibraryBadges({ game }: { game: CatalogGame }) {
  const sourceLabel = formatLibrarySource(game.librarySource)

  return (
    <>
      {game.libraryStatus && (
        <span className="rounded-full border border-primary/40 px-2 py-0.5 text-[10px] uppercase tracking-wide text-primary">
          {game.libraryStatus}
        </span>
      )}
      {sourceLabel && (
        <span className="rounded-full border border-cyan/40 px-2 py-0.5 text-[10px] uppercase tracking-wide text-cyan">
          {sourceLabel}
        </span>
      )}
    </>
  )
}

type GameCardProps = {
  game: CatalogGame
  view?: 'list' | 'grid'
  variant?: 'base' | 'dlc'
  showTagEditor?: boolean
  showMultiPlatform?: boolean
}

export function GameCard({
  game,
  view = 'list',
  variant = 'base',
  showTagEditor = false,
  showMultiPlatform = false,
}: GameCardProps) {
  const platformMarks = showMultiPlatform ? <PlatformMarks platforms={game.platforms} /> : null

  if (view === 'grid' && variant === 'base') {
    return (
      <div className="group overflow-hidden rounded-xl border border-border bg-card/60 transition hover:border-primary/50">
        <Link to={`/games/${game.slug}`}>
          {game.coverUrl ? (
            <img src={game.coverUrl} alt="" className="aspect-[3/4] w-full object-cover" />
          ) : (
            <div className="flex aspect-[3/4] items-center justify-center bg-muted text-muted-foreground">?</div>
          )}
          <div className="p-3">
            <p className="truncate text-sm font-medium text-foreground group-hover:text-primary">{game.title}</p>
            <p className="mt-1 flex flex-wrap items-center gap-1 text-xs text-muted-foreground">
              <span>{game.totalPlayers} players</span>
              {platformMarks}
              <LibraryBadges game={game} />
            </p>
          </div>
        </Link>
        {game.dlc.length > 0 && (
          <div className="grid grid-cols-4 gap-1 border-t border-border p-2">
            {game.dlc.slice(0, 4).map((dlc) => (
              <Link
                key={dlc.id}
                to={`/games/${dlc.slug}`}
                title={dlc.title}
                className="overflow-hidden rounded border border-border hover:border-primary/50"
              >
                {dlc.coverUrl ? (
                  <img src={dlc.coverUrl} alt="" className="aspect-square w-full object-cover" />
                ) : (
                  <div className="flex aspect-square items-center justify-center bg-muted text-[8px] text-muted-foreground">
                    DLC
                  </div>
                )}
              </Link>
            ))}
            {game.dlc.length > 4 && (
              <div className="flex aspect-square items-center justify-center rounded border border-border bg-background text-[10px] text-muted-foreground">
                +{game.dlc.length - 4}
              </div>
            )}
          </div>
        )}
      </div>
    )
  }

  const isDlc = variant === 'dlc'
  const lastPlayed = formatLastPlayed(game.userLastPlayed)

  return (
    <div
      className={`rounded-2xl border border-border bg-card/60 transition hover:border-primary/50 ${
        isDlc ? 'ml-6 border-dashed bg-background/40' : ''
      }`}
    >
    <Link
      to={`/games/${game.slug}`}
      className={`flex items-center gap-3 px-4 py-3 ${isDlc ? 'py-2' : ''}`}
    >
      {game.coverUrl ? (
        <img
          src={game.coverUrl}
          alt=""
          className={`rounded object-cover ${isDlc ? 'h-10 w-7' : 'h-14 w-10'}`}
        />
      ) : (
        <div
          className={`flex items-center justify-center rounded bg-muted text-xs text-muted-foreground ${
            isDlc ? 'h-10 w-7' : 'h-14 w-10'
          }`}
        >
          ?
        </div>
      )}
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className={`font-medium text-foreground ${isDlc ? 'text-xs' : 'text-sm'}`}>{game.title}</p>
          {isDlc && (
            <span className="rounded-full border border-border px-2 py-0.5 text-[10px] uppercase tracking-wide text-muted-foreground">
              DLC
            </span>
          )}
          {platformMarks}
          <LibraryBadges game={game} />
        </div>
        <p className="text-xs text-muted-foreground">
          {[game.categories.slice(0, 2).join(', '), game.platforms.slice(0, 2).join(', ')]
            .filter(Boolean)
            .join(' · ')}
          {game.userRating != null && (
            <>
              {' · '}
              <StarRatingDisplay value={game.userRating} />
              <span className="ml-1">{halfStarsToDisplay(game.userRating)}</span>
            </>
          )}
          {game.userHours != null ? ` · ${game.userHours}h` : ''}
          {lastPlayed ? ` · Last played ${lastPlayed}` : ''}
          {game.totalPlayers > 0 ? ` · ${game.totalPlayers} players` : ''}
        </p>
      </div>
    </Link>
    {showTagEditor && !isDlc && game.libraryEntryId && (
      <div className="px-4 pb-2">
        <GameTagEditor gameId={game.id} gameTags={game.userTags} compact />
      </div>
    )}
    </div>
  )
}
