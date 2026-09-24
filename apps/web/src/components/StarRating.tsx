import { halfStarsToDisplay, starsToHalfStars } from '../lib/ratings'

type StarRatingProps = {
  value: number | null
  onChange?: (halfStars: number | null) => void
  size?: 'sm' | 'md'
  readOnly?: boolean
}

export function StarRatingDisplay({ value, size = 'sm' }: { value: number | null; size?: 'sm' | 'md' }) {
  const stars = value == null ? 0 : value / 2
  const starSize = size === 'md' ? 'text-lg' : 'text-sm'

  return (
    <span className={`inline-flex items-center gap-0.5 ${starSize} text-trophy`} aria-label={halfStarsToDisplay(value)}>
      {Array.from({ length: 5 }, (_, index) => {
        const filled = stars - index
        if (filled >= 1) {
          return <span key={index}>★</span>
        }
        if (filled >= 0.5) {
          return (
            <span key={index} className="relative inline-block w-[1em]">
              <span className="text-muted-foreground">★</span>
              <span className="absolute inset-0 w-1/2 overflow-hidden">★</span>
            </span>
          )
        }
        return (
          <span key={index} className="text-muted-foreground">
            ★
          </span>
        )
      })}
    </span>
  )
}

export function StarRatingInput({ value, onChange, size = 'md' }: StarRatingProps) {
  if (!onChange) {
    return <StarRatingDisplay value={value} size={size} />
  }

  const starSize = size === 'md' ? 'text-2xl' : 'text-lg'

  const setHalfStars = (halfStars: number) => {
    onChange(value === halfStars ? null : halfStars)
  }

  return (
    <div className={`inline-flex items-center gap-1 ${starSize}`}>
      {Array.from({ length: 5 }, (_, index) => {
        const leftValue = index * 2 + 1
        const rightValue = (index + 1) * 2
        const filled = (value ?? 0) / 2 - index

        return (
          <span key={index} className="relative inline-flex text-trophy">
            <button
              type="button"
              aria-label={`${index + 0.5} stars`}
              onClick={() => setHalfStars(leftValue)}
              className="absolute inset-y-0 left-0 z-10 w-1/2 opacity-0"
            />
            <button
              type="button"
              aria-label={`${index + 1} stars`}
              onClick={() => setHalfStars(rightValue)}
              className="absolute inset-y-0 right-0 z-10 w-1/2 opacity-0"
            />
            {filled >= 1 ? (
              <span>★</span>
            ) : filled >= 0.5 ? (
              <span className="relative inline-block w-[1em]">
                <span className="text-muted-foreground">★</span>
                <span className="absolute inset-0 w-1/2 overflow-hidden">★</span>
              </span>
            ) : (
              <span className="text-muted-foreground">★</span>
            )}
          </span>
        )
      })}
      {value != null && (
        <button
          type="button"
          onClick={() => onChange(null)}
          className="ml-2 text-xs text-foreground0 hover:text-foreground/80"
        >
          Clear
        </button>
      )}
    </div>
  )
}

export { starsToHalfStars, halfStarsToDisplay }
