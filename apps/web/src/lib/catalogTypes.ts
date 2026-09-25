export type CatalogSortField = 'Title' | 'ReleaseDate' | 'UpdatedAt' | 'Rating' | 'Hours' | 'TotalPlayers' | 'LastPlayed'
export type CatalogGroupBy = 'None' | 'Genre' | 'Platform' | 'Status' | 'Tag' | 'ReleaseDate'

export type CatalogGame = {
  id: string
  title: string
  slug: string
  coverUrl: string | null
  releaseDate: string | null
  categories: string[]
  platforms: string[]
  totalPlayers: number
  avgRating: number | null
  libraryEntryId: string | null
  libraryStatus: string | null
  userRating: number | null
  userHours: number | null
  librarySource: string | null
  userLastPlayed: string | null
  userTags: string[]
  dlc: CatalogGame[]
  isLoved?: boolean
}

export type CatalogGroup = {
  key: string
  label: string
  items: CatalogGame[]
}

export type PaginatedCatalog = {
  items: CatalogGame[]
  totalCount: number
  page: number
  pageSize: number
  groups: CatalogGroup[] | null
}

export type CatalogFacetCount = {
  slug: string
  name: string
  count: number
}

export type CatalogFacets = {
  totalGames: number
  myLibraryCount: number | null
  myLibraryHours: number | null
  categories: CatalogFacetCount[]
  platforms: CatalogFacetCount[]
  tags: CatalogFacetCount[]
  shelves: CatalogFacetCount[]
  librarySources: CatalogFacetCount[]
  libraryStatuses: CatalogFacetCount[]
}

export type CatalogFilters = {
  search?: string
  genre?: string
  platform?: string
  tag?: string
  shelf?: string
  releaseDecade?: number
  inLibrary?: boolean
  status?: string
  source?: string
  sort?: CatalogSortField
  desc?: boolean
  page?: number
  pageSize?: number
  groupBy?: CatalogGroupBy
  groupKeys?: string[]
}

export type GroupFilterOption = {
  key: string
  label: string
}

export const RELEASE_DATE_GROUP_OPTIONS: GroupFilterOption[] = [
  { key: 'last-month', label: 'Last month' },
  { key: 'last-year', label: 'Last year' },
  { key: 'decade-2020', label: '2020s' },
  { key: 'decade-2010', label: '2010s' },
  { key: 'decade-2000', label: '2000s' },
  { key: 'decade-1990', label: '1990s' },
  { key: 'decade-1980', label: '1980s' },
  { key: 'unknown', label: 'Unknown' },
]

export const LIBRARY_STATUS_OPTIONS: GroupFilterOption[] = [
  { key: 'Owned', label: 'Owned' },
  { key: 'Playing', label: 'Playing' },
  { key: 'Completed', label: 'Completed' },
  { key: 'Backlog', label: 'Backlog' },
  { key: 'Dropped', label: 'Dropped' },
]

const descendingDefaultSorts: CatalogSortField[] = [
  'ReleaseDate',
  'UpdatedAt',
  'Rating',
  'Hours',
  'TotalPlayers',
  'LastPlayed',
]

export function sortUsesDescendingDefault(sort: CatalogSortField): boolean {
  return descendingDefaultSorts.includes(sort)
}

export function showSortDirectionToggle(sort: CatalogSortField): boolean {
  return sort === 'Title' || sort === 'ReleaseDate'
}

export function appendSortDirection(
  params: URLSearchParams,
  sort: CatalogSortField | undefined,
  desc: boolean | undefined,
) {
  const field = sort ?? 'Title'
  if (!showSortDirectionToggle(field)) {
    return
  }

  if (sortUsesDescendingDefault(field)) {
    params.set('desc', desc === false ? 'false' : 'true')
    return
  }

  if (desc) {
    params.set('desc', 'true')
  }
}

export function buildCatalogQuery(filters: CatalogFilters): string {
  const params = new URLSearchParams()
  if (filters.search) params.set('search', filters.search)
  if (filters.genre) params.set('genre', filters.genre)
  if (filters.platform) params.set('platform', filters.platform)
  if (filters.tag) params.set('tag', filters.tag)
  if (filters.shelf) params.set('shelf', filters.shelf)
  if (filters.releaseDecade) params.set('releaseDecade', String(filters.releaseDecade))
  if (filters.inLibrary) params.set('inLibrary', 'true')
  if (filters.status) params.set('status', filters.status)
  if (filters.source) params.set('source', filters.source)
  if (filters.sort) params.set('sort', filters.sort)
  appendSortDirection(params, filters.sort, filters.desc)
  params.set('page', String(filters.page ?? 1))
  params.set('pageSize', String(filters.pageSize ?? 20))
  if (filters.groupBy && filters.groupBy !== 'None') params.set('groupBy', filters.groupBy)
  if (filters.groupKeys && filters.groupKeys.length > 0) {
    params.set('groupKeys', filters.groupKeys.join(','))
  }
  return params.toString()
}

export function getGroupFilterOptions(
  groupBy: CatalogGroupBy,
  facets: CatalogFacets | undefined,
  inLibrary: boolean,
): GroupFilterOption[] {
  switch (groupBy) {
    case 'Genre':
      return (facets?.categories ?? []).map((c) => ({ key: c.slug, label: c.name }))
    case 'Platform':
      if (inLibrary) {
        return (facets?.librarySources ?? []).map((s) => ({ key: s.slug, label: s.name }))
      }
      return (facets?.platforms ?? []).map((p) => ({ key: p.slug, label: p.name }))
    case 'Status':
      return (facets?.libraryStatuses ?? []).length > 0
        ? facets!.libraryStatuses.map((s) => ({ key: s.slug, label: s.name }))
        : LIBRARY_STATUS_OPTIONS
    case 'Tag':
      return [
        ...(facets?.tags ?? []).map((t) => ({ key: t.slug, label: t.name })),
        { key: 'untagged', label: 'Untagged' },
      ]
    case 'ReleaseDate':
      return RELEASE_DATE_GROUP_OPTIONS
    default:
      return []
  }
}
