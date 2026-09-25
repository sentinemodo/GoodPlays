import type { PaginatedCatalog } from './catalogTypes'

export function catalogHasMissingCover(data: PaginatedCatalog | undefined): boolean {
  if (!data) {
    return false
  }

  const games = [...data.items, ...(data.groups?.flatMap((group) => group.items) ?? [])]
  return games.some((game) => !game.coverUrl || game.dlc.some((dlc) => !dlc.coverUrl))
}
