export function formatLibrarySource(source: string | null | undefined): string | null {
  if (!source) {
    return null
  }

  switch (source) {
    case 'SteamSync':
      return 'Steam'
    case 'PsnSync':
      return 'PlayStation'
    case 'Manual':
      return 'Manual'
    case 'ImportText':
    case 'ImportImage':
    case 'ImportCsv':
      return 'Import'
    case 'ResearchReco':
      return 'Recommendation'
    default:
      return source
  }
}
