/** Half-star scale stored in API: 2 = 1★, 10 = 5★ */
export function halfStarsToDisplay(halfStars: number | null | undefined): string {
  if (halfStars == null) {
    return '—'
  }

  return `${(halfStars / 2).toFixed(1)}★`
}

export function starsToHalfStars(stars: number): number {
  return Math.round(Math.min(5, Math.max(0.5, stars)) * 2)
}
