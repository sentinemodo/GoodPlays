export type PlatformMark = {
  name: string
  short: string
  color: string
}

function markFor(name: string): PlatformMark {
  const normalized = name.toLowerCase()

  if (normalized.includes('playstation 5') || normalized.includes('ps5')) {
    return { name, short: 'PS5', color: '#4f9dff' }
  }
  if (normalized.includes('playstation 4') || normalized.includes('ps4')) {
    return { name, short: 'PS4', color: '#4f9dff' }
  }
  if (normalized.includes('playstation') || normalized.includes('ps vita') || normalized.includes('psp')) {
    return { name, short: 'PS', color: '#4f9dff' }
  }
  if (normalized.includes('xbox series')) {
    return { name, short: 'XSX', color: '#5bd45b' }
  }
  if (normalized.includes('xbox one')) {
    return { name, short: 'XB1', color: '#5bd45b' }
  }
  if (normalized.includes('xbox')) {
    return { name, short: 'XB', color: '#5bd45b' }
  }
  if (normalized.includes('switch')) {
    return { name, short: 'NSW', color: '#ff5a5a' }
  }
  if (normalized.includes('wii') || normalized.includes('nintendo') || normalized.includes('3ds')) {
    return { name, short: 'NIN', color: '#ff5a5a' }
  }
  if (normalized.includes('steam') || normalized.includes('windows') || normalized === 'pc' || normalized.includes('pc (')) {
    return { name, short: 'PC', color: '#66c0f4' }
  }
  if (normalized.includes('mac')) {
    return { name, short: 'MAC', color: '#c8a9ff' }
  }
  if (normalized.includes('linux')) {
    return { name, short: 'LNX', color: '#f59e0b' }
  }
  if (normalized.includes('epic')) {
    return { name, short: 'EPC', color: '#c8a9ff' }
  }
  if (normalized.includes('gog')) {
    return { name, short: 'GOG', color: '#ffb3f0' }
  }

  const short = name.replace(/[^a-z0-9]/gi, '').slice(0, 3).toUpperCase() || 'PLT'
  return { name, short, color: '#a78bfa' }
}

export function platformMarks(platforms: string[]): PlatformMark[] {
  const seen = new Set<string>()
  const marks: PlatformMark[] = []

  for (const platform of platforms) {
    const name = platform.trim()
    const key = name.toLowerCase()
    if (!key || seen.has(key)) {
      continue
    }
    seen.add(key)
    marks.push(markFor(name))
  }

  return marks
}

export function multiPlatformMarks(platforms: string[]): PlatformMark[] {
  const marks = platformMarks(platforms)
  return marks.length > 1 ? marks : []
}
