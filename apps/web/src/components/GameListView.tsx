import type { CatalogGame, CatalogGroup } from '../lib/catalogTypes'
import { GameCard } from './GameCard'
import { Pagination } from './Pagination'

type GameListViewProps = {
  items: CatalogGame[]
  groups: CatalogGroup[] | null
  page: number
  pageSize: number
  totalCount: number
  view: 'list' | 'grid'
  isLoading: boolean
  error: Error | null
  showTagEditor?: boolean
  showMultiPlatform?: boolean
  onToggleLoved?: (game: CatalogGame) => void
  onPageChange: (page: number) => void
  onPageSizeChange?: (pageSize: number) => void
}

function renderGameRows(
  games: CatalogGame[],
  view: 'list' | 'grid',
  showTagEditor: boolean,
  showMultiPlatform: boolean,
  onToggleLoved?: (game: CatalogGame) => void,
) {
  if (view === 'grid') {
    return (
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
        {games.map((game) => (
          <GameCard
            key={game.id}
            game={game}
            view="grid"
            showTagEditor={showTagEditor}
            showMultiPlatform={showMultiPlatform}
            onToggleLoved={onToggleLoved}
          />
        ))}
      </div>
    )
  }

  return (
    <ul className="space-y-2">
      {games.map((game) => (
        <li key={game.id}>
          <GameCard
            game={game}
            view="list"
            showTagEditor={showTagEditor}
            showMultiPlatform={showMultiPlatform}
            onToggleLoved={onToggleLoved}
          />
          {game.dlc.map((dlc) => (
            <div key={dlc.id} className="mt-1">
              <GameCard
                game={dlc}
                view="list"
                variant="dlc"
                showTagEditor={showTagEditor}
                showMultiPlatform={showMultiPlatform}
              />
            </div>
          ))}
        </li>
      ))}
    </ul>
  )
}

export function GameListView({
  items,
  groups,
  page,
  pageSize,
  totalCount,
  view,
  isLoading,
  error,
  showTagEditor = false,
  showMultiPlatform = false,
  onToggleLoved,
  onPageChange,
  onPageSizeChange,
}: GameListViewProps) {
  if (isLoading) {
    return <p className="text-muted-foreground">Loading games…</p>
  }

  if (error) {
    return <p className="text-amber-300">Could not load catalog. Start the API and refresh.</p>
  }

  const hasGroups = groups && groups.length > 0

  return (
    <div>
      {hasGroups ? (
        <div className="space-y-6">
          {groups.map((group) => (
            <section key={group.key}>
              <h3 className="mb-3 text-sm font-medium text-primary">{group.label}</h3>
              {renderGameRows(group.items, view, showTagEditor, showMultiPlatform, onToggleLoved)}
            </section>
          ))}
        </div>
      ) : items.length > 0 ? (
        renderGameRows(items, view, showTagEditor, showMultiPlatform, onToggleLoved)
      ) : (
        <p className="text-muted-foreground">No games match your filters.</p>
      )}

      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={totalCount}
        onPageChange={onPageChange}
        onPageSizeChange={onPageSizeChange}
      />
    </div>
  )
}
