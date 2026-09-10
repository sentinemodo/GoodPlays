/** Strip trailing slash; empty string when deployed at domain root. */
export const appBasePath = import.meta.env.BASE_URL.replace(/\/$/, '')

export function appRoute(segment: string): string {
  const normalized = segment.startsWith('/') ? segment : `/${segment}`
  return appBasePath ? `${appBasePath}${normalized}` : normalized
}
