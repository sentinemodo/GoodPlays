import type { CatalogFacets, CatalogFilters, CatalogGroupBy } from '../lib/catalogTypes'
import { getGroupFilterOptions } from '../lib/catalogTypes'

type GroupFilterPanelProps = {
  groupBy: CatalogGroupBy
  facets: CatalogFacets | undefined
  filters: CatalogFilters
  inLibrary: boolean
  onFilterChange: (patch: Partial<CatalogFilters>) => void
}

const groupByLabels: Record<CatalogGroupBy, string> = {
  None: '',
  Genre: 'categories',
  Platform: 'platforms',
  Status: 'statuses',
  Tag: 'tags',
  ReleaseDate: 'release periods',
}

export function GroupFilterPanel({
  groupBy,
  facets,
  filters,
  inLibrary,
  onFilterChange,
}: GroupFilterPanelProps) {
  if (groupBy === 'None') {
    return null
  }

  const options = getGroupFilterOptions(groupBy, facets, inLibrary)
  if (options.length === 0) {
    return null
  }

  const allKeys = options.map((o) => o.key)
  const selectedKeys = filters.groupKeys ?? allKeys

  const toggleKey = (key: string) => {
    const current = filters.groupKeys ?? allKeys
    const next = current.includes(key) ? current.filter((k) => k !== key) : [...current, key]
    onFilterChange({
      groupKeys: next.length === allKeys.length ? undefined : next,
      page: 1,
    })
  }

  const selectAll = () => onFilterChange({ groupKeys: undefined, page: 1 })
  const selectNone = () => onFilterChange({ groupKeys: [], page: 1 })

  return (
    <div className="mb-4 rounded-lg border border-border bg-card/40 p-3">
      <div className="mb-2 flex items-center justify-between">
        <p className="text-xs font-semibold uppercase tracking-wide text-foreground0">
          Show {groupByLabels[groupBy]}
        </p>
        <div className="flex gap-2 text-[10px]">
          <button type="button" onClick={selectAll} className="text-primary hover:underline">
            All
          </button>
          <button type="button" onClick={selectNone} className="text-muted-foreground hover:underline">
            None
          </button>
        </div>
      </div>
      <div className="flex flex-wrap gap-x-4 gap-y-1">
        {options.map((option) => (
          <label key={option.key} className="flex cursor-pointer items-center gap-1.5 text-xs text-foreground/80">
            <input
              type="checkbox"
              checked={selectedKeys.includes(option.key)}
              onChange={() => toggleKey(option.key)}
              className="rounded border-border"
            />
            {option.label}
          </label>
        ))}
      </div>
    </div>
  )
}
