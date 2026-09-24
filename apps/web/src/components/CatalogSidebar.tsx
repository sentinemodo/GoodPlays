import { Link } from 'react-router'
import type { CatalogFacets, CatalogFilters } from '../lib/catalogTypes'

type CatalogSidebarProps = {
  facets: CatalogFacets | undefined
  filters: CatalogFilters
  basePath: '/catalog' | '/library'
  onFilterChange: (patch: Partial<CatalogFilters>) => void
  sidebarExtra?: React.ReactNode
}

const decades = [2020, 2010, 2000, 1990, 1980]

const libraryStatuses = ['Owned', 'Playing', 'Completed', 'Backlog', 'Dropped']

export function CatalogSidebar({ facets, filters, basePath, onFilterChange, sidebarExtra }: CatalogSidebarProps) {
  const inLibrary = filters.inLibrary ?? basePath === '/library'

  return (
    <aside className="w-full shrink-0 space-y-6 lg:w-72">
      <div>
        <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-foreground0">Browse</p>
        <div className="space-y-1 text-sm">
          <Link
            to="/catalog"
            className={`block rounded px-2 py-1 ${basePath === '/catalog' ? 'bg-primary/15 text-primary' : 'text-foreground/80 hover:bg-muted'}`}
          >
            Global library
          </Link>
          <Link
            to="/library"
            className={`block rounded px-2 py-1 ${basePath === '/library' ? 'bg-primary/15 text-primary' : 'text-foreground/80 hover:bg-muted'}`}
          >
            In my library
          </Link>
        </div>
      </div>

      {sidebarExtra && <div className="space-y-6">{sidebarExtra}</div>}

      <div className="rounded-lg border border-border bg-card/40 p-3 text-xs text-muted-foreground">
        {inLibrary && facets?.myLibraryCount != null ? (
          <p>
            {facets.myLibraryCount} games
            {facets.myLibraryHours != null ? ` · ${facets.myLibraryHours}h` : ''}
          </p>
        ) : (
          <p>{facets?.totalGames ?? '—'} games in GoodPlays</p>
        )}
      </div>

      {inLibrary && (
        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-foreground0">Status</p>
          <div className="flex flex-wrap gap-1">
            {libraryStatuses.map((status) => (
              <button
                key={status}
                type="button"
                onClick={() =>
                  onFilterChange({
                    status: filters.status === status ? undefined : status,
                    page: 1,
                  })
                }
                className={`rounded-full border px-2 py-0.5 text-[11px] ${
                  filters.status === status
                    ? 'border-primary text-primary'
                    : 'border-border text-muted-foreground hover:border-border'
                }`}
              >
                {status}
              </button>
            ))}
          </div>
        </div>
      )}

      {inLibrary && (facets?.librarySources.length ?? 0) > 0 && (
        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-foreground0">Library source</p>
          <div className="flex flex-wrap gap-1">
            {facets!.librarySources.map((source) => (
              <button
                key={source.slug}
                type="button"
                onClick={() =>
                  onFilterChange({
                    source: filters.source === source.slug ? undefined : source.slug,
                    page: 1,
                  })
                }
                className={`rounded-full border px-2 py-0.5 text-[11px] ${
                  filters.source === source.slug
                    ? 'border-primary text-primary'
                    : 'border-border text-muted-foreground hover:border-border'
                }`}
              >
                {source.name} ({source.count})
              </button>
            ))}
          </div>
        </div>
      )}

      <FacetList
        title="Categories"
        items={facets?.categories ?? []}
        active={filters.genre}
        onSelect={(slug) => onFilterChange({ genre: filters.genre === slug ? undefined : slug, page: 1 })}
      />

      <FacetList
        title="Platforms"
        items={facets?.platforms ?? []}
        active={filters.platform}
        onSelect={(slug) => onFilterChange({ platform: filters.platform === slug ? undefined : slug, page: 1 })}
      />

      {(facets?.tags.length ?? 0) > 0 && (
        <FacetList
          title="Tags"
          items={facets?.tags ?? []}
          active={filters.tag}
          onSelect={(slug) => onFilterChange({ tag: filters.tag === slug ? undefined : slug, page: 1 })}
        />
      )}

      {(facets?.shelves.length ?? 0) > 0 && (
        <FacetList
          title="Shelves"
          items={facets?.shelves ?? []}
          active={filters.shelf}
          onSelect={(slug) => onFilterChange({ shelf: filters.shelf === slug ? undefined : slug, page: 1 })}
        />
      )}

      <div>
        <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-foreground0">Release decade</p>
        <div className="flex flex-wrap gap-1">
          {decades.map((decade) => (
            <button
              key={decade}
              type="button"
              onClick={() =>
                onFilterChange({
                  releaseDecade: filters.releaseDecade === decade ? undefined : decade,
                  page: 1,
                })
              }
              className={`rounded-full border px-2 py-0.5 text-[11px] ${
                filters.releaseDecade === decade
                  ? 'border-primary text-primary'
                  : 'border-border text-muted-foreground hover:border-border'
              }`}
            >
              {decade}s
            </button>
          ))}
        </div>
      </div>
    </aside>
  )
}

function FacetList({
  title,
  items,
  active,
  onSelect,
}: {
  title: string
  items: { slug: string; name: string; count: number }[]
  active?: string
  onSelect: (slug: string) => void
}) {
  if (items.length === 0) {
    return null
  }

  return (
    <div>
      <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-foreground0">{title}</p>
      <ul className="max-h-48 space-y-1 overflow-y-auto text-sm">
        {items.map((item) => (
          <li key={item.slug}>
            <button
              type="button"
              onClick={() => onSelect(item.slug)}
              className={`flex w-full items-center justify-between rounded px-2 py-1 text-left ${
                active === item.slug ? 'bg-primary/15 text-primary' : 'text-foreground/80 hover:bg-muted'
              }`}
            >
              <span className="truncate">{item.name}</span>
              <span className="ml-2 shrink-0 text-xs text-foreground0">{item.count}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}
