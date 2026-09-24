import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router'
import { useApiAuth } from '../hooks/useApiAuth'
import { api } from '../lib/api'
import type { CatalogFilters, CatalogGroupBy, CatalogSortField } from '../lib/catalogTypes'
import { showSortDirectionToggle, sortUsesDescendingDefault } from '../lib/catalogTypes'
import { CatalogSidebar } from './CatalogSidebar'
import { GameListView } from './GameListView'
import { GroupFilterPanel } from './GroupFilterPanel'

type CatalogBrowsePageProps = {
  basePath: '/catalog' | '/library'
  title: string
  subtitle: string
  sidebarExtra?: React.ReactNode
  banner?: React.ReactNode
}

function parseFilters(params: URLSearchParams, basePath: '/catalog' | '/library'): CatalogFilters {
  const sort = (params.get('sort') as CatalogSortField) || 'Title'
  const descParam = params.get('desc')
  const desc = descParam === null ? sortUsesDescendingDefault(sort) : descParam === 'true'

  return {
    search: params.get('search') ?? undefined,
    genre: params.get('genre') ?? undefined,
    platform: params.get('platform') ?? undefined,
    tag: params.get('tag') ?? undefined,
    shelf: params.get('shelf') ?? undefined,
    releaseDecade: params.get('releaseDecade') ? Number(params.get('releaseDecade')) : undefined,
    inLibrary: basePath === '/library' || params.get('inLibrary') === 'true',
    status: params.get('status') ?? undefined,
    source: params.get('source') ?? undefined,
    sort,
    desc,
    page: params.get('page') ? Number(params.get('page')) : 1,
    pageSize: params.get('pageSize') ? Number(params.get('pageSize')) : 20,
    groupBy: (params.get('groupBy') as CatalogGroupBy) || 'None',
    groupKeys: params.get('groupKeys')
      ? params.get('groupKeys')!.split(',').filter(Boolean)
      : undefined,
  }
}

function filtersToParams(filters: CatalogFilters): URLSearchParams {
  const params = new URLSearchParams()
  if (filters.search) params.set('search', filters.search)
  if (filters.genre) params.set('genre', filters.genre)
  if (filters.platform) params.set('platform', filters.platform)
  if (filters.tag) params.set('tag', filters.tag)
  if (filters.shelf) params.set('shelf', filters.shelf)
  if (filters.releaseDecade) params.set('releaseDecade', String(filters.releaseDecade))
  if (filters.status) params.set('status', filters.status)
  if (filters.source) params.set('source', filters.source)
  if (filters.sort && filters.sort !== 'Title') params.set('sort', filters.sort)
  if (filters.desc && showSortDirectionToggle(filters.sort ?? 'Title')) params.set('desc', 'true')
  if (filters.page && filters.page > 1) params.set('page', String(filters.page))
  if (filters.pageSize && filters.pageSize !== 20) params.set('pageSize', String(filters.pageSize))
  if (filters.groupBy && filters.groupBy !== 'None') params.set('groupBy', filters.groupBy)
  if (filters.groupKeys && filters.groupKeys.length > 0) {
    params.set('groupKeys', filters.groupKeys.join(','))
  }
  return params
}

export function CatalogBrowsePage({ basePath, title, subtitle, sidebarExtra, banner }: CatalogBrowsePageProps) {
  useApiAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const filters = useMemo(() => parseFilters(searchParams, basePath), [searchParams, basePath])
  const [view, setView] = useState<'list' | 'grid'>('list')
  const [searchInput, setSearchInput] = useState(filters.search ?? '')

  const { data: facets } = useQuery({
    queryKey: ['catalog-facets'],
    queryFn: api.getCatalogFacets,
  })

  const { data, isLoading, error } = useQuery({
    queryKey: ['catalog', filters],
    queryFn: () => api.browseCatalog(filters),
  })

  const updateFilters = (patch: Partial<CatalogFilters>) => {
    const nextSort = patch.sort ?? filters.sort ?? 'Title'
    const next = {
      ...filters,
      ...patch,
      inLibrary: basePath === '/library',
      desc: patch.desc ?? (patch.sort ? sortUsesDescendingDefault(nextSort) : filters.desc),
    }
    setSearchParams(filtersToParams(next), { replace: true })
  }

  const handleSearch = (event: React.FormEvent) => {
    event.preventDefault()
    updateFilters({ search: searchInput.trim() || undefined, page: 1 })
  }

  const showDescToggle = showSortDirectionToggle(filters.sort ?? 'Title')

  const fieldClass =
    'rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm text-foreground outline-none focus:border-primary/60'

  return (
    <div className="mx-auto flex max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 sm:py-8 lg:flex-row">
      <CatalogSidebar
        facets={facets}
        filters={filters}
        basePath={basePath}
        onFilterChange={updateFilters}
        sidebarExtra={sidebarExtra}
      />

      <div className="min-w-0 flex-1">
        <div className="mb-6">
          <p className="text-sm font-semibold uppercase tracking-wide text-primary">{subtitle}</p>
          <h1 className="mt-1 font-display text-3xl font-bold tracking-tight sm:text-4xl">{title}</h1>
        </div>

        {banner}

        <div className="mb-4 flex flex-wrap items-center gap-3">
          <form onSubmit={handleSearch} className="flex min-w-[200px] flex-1 gap-2">
            <input
              type="search"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder={basePath === '/catalog' ? 'Search the global library...' : 'Search your library...'}
              className={`w-full ${fieldClass}`}
            />
            <button type="submit" className="rounded-xl bg-primary px-3 py-2 text-sm font-semibold text-primary-foreground">
              Search
            </button>
          </form>

          <select
            value={filters.sort ?? 'Title'}
            onChange={(e) =>
              updateFilters({ sort: e.target.value as CatalogSortField, page: 1 })
            }
            className={fieldClass}
          >
            <option value="Title">Title</option>
            <option value="ReleaseDate">Release date</option>
            <option value="UpdatedAt">Recently updated</option>
            <option value="TotalPlayers">Most players</option>
            {basePath === '/library' && (
              <>
                <option value="Rating">Rating</option>
                <option value="Hours">Hours played</option>
              </>
            )}
          </select>

          <select
            value={filters.groupBy ?? 'None'}
            onChange={(e) =>
              updateFilters({
                groupBy: e.target.value as CatalogGroupBy,
                groupKeys: undefined,
                page: 1,
              })
            }
            className={fieldClass}
          >
            <option value="None">No grouping</option>
            <option value="Genre">Group by category</option>
            <option value="Platform">
              {basePath === '/library' ? 'Group by library source' : 'Group by platform'}
            </option>
            <option value="ReleaseDate">Group by release date</option>
            {basePath === '/library' && <option value="Status">Group by status</option>}
            <option value="Tag">Group by tag</option>
          </select>

          <div className="flex rounded-xl border border-border">
            <button
              type="button"
              onClick={() => setView('list')}
              className={`px-3 py-2 text-xs ${view === 'list' ? 'bg-primary/15 text-primary' : 'text-muted-foreground'}`}
            >
              List
            </button>
            <button
              type="button"
              onClick={() => setView('grid')}
              className={`px-3 py-2 text-xs ${view === 'grid' ? 'bg-primary/15 text-primary' : 'text-muted-foreground'}`}
            >
              Grid
            </button>
          </div>

          {showDescToggle && (
            <label className="flex items-center gap-2 text-xs text-muted-foreground">
              <input
                type="checkbox"
                checked={filters.desc ?? false}
                onChange={(e) => updateFilters({ desc: e.target.checked, page: 1 })}
              />
              Descending
            </label>
          )}
        </div>

        <GroupFilterPanel
          groupBy={filters.groupBy ?? 'None'}
          facets={facets}
          filters={filters}
          inLibrary={basePath === '/library'}
          onFilterChange={updateFilters}
        />

        <GameListView
          items={data?.items ?? []}
          groups={data?.groups ?? null}
          page={data?.page ?? filters.page ?? 1}
          pageSize={data?.pageSize ?? filters.pageSize ?? 20}
          totalCount={data?.totalCount ?? 0}
          view={view}
          isLoading={isLoading}
          error={error}
          showTagEditor={basePath === '/library'}
          showMultiPlatform={basePath === '/catalog'}
          onPageChange={(page) => updateFilters({ page })}
          onPageSizeChange={(pageSize) => updateFilters({ pageSize, page: 1 })}
        />
      </div>
    </div>
  )
}
