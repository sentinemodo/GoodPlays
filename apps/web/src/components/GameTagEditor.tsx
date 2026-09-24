import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '../lib/api'

type GameTagEditorProps = {
  gameId: string
  gameTags: string[]
  compact?: boolean
}

export function GameTagEditor({ gameId, gameTags, compact = false }: GameTagEditorProps) {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [newTagName, setNewTagName] = useState('')

  const { data: allTags = [] } = useQuery({
    queryKey: ['tags'],
    queryFn: api.getTags,
    enabled: open,
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['catalog'] })
    queryClient.invalidateQueries({ queryKey: ['game'] })
    queryClient.invalidateQueries({ queryKey: ['catalog-facets'] })
    queryClient.invalidateQueries({ queryKey: ['tags'] })
  }

  const addMutation = useMutation({
    mutationFn: (tagSlug: string) => api.tagGame(tagSlug, gameId),
    onSuccess: invalidate,
  })

  const removeMutation = useMutation({
    mutationFn: (tagSlug: string) => api.untagGame(tagSlug, gameId),
    onSuccess: invalidate,
  })

  const createMutation = useMutation({
    mutationFn: (name: string) => api.createTag(name),
    onSuccess: (tag) => {
      setNewTagName('')
      addMutation.mutate(tag.slug)
    },
  })

  const userTags = allTags.filter((t) => t.scope === 'User')
  const assignedSlugs = new Set(
    allTags.filter((t) => gameTags.includes(t.name)).map((t) => t.slug),
  )

  if (compact && !open) {
    return (
      <button
        type="button"
        onClick={(e) => {
          e.preventDefault()
          e.stopPropagation()
          setOpen(true)
        }}
        className="rounded border border-border px-1.5 py-0.5 text-[10px] text-muted-foreground hover:border-primary hover:text-primary"
      >
        {gameTags.length > 0 ? gameTags.join(', ') : '+ Tag'}
      </button>
    )
  }

  return (
    <div
      className={`${compact ? 'mt-1' : 'mt-3'} rounded-lg border border-border bg-background/60 p-2`}
      onClick={(e) => {
        e.preventDefault()
        e.stopPropagation()
      }}
    >
      <div className="mb-1 flex items-center justify-between">
        <p className="text-[10px] font-semibold uppercase tracking-wide text-foreground0">Tags</p>
        {compact && (
          <button type="button" onClick={() => setOpen(false)} className="text-[10px] text-foreground0 hover:text-foreground/80">
            Close
          </button>
        )}
      </div>

      {gameTags.length > 0 && (
        <div className="mb-2 flex flex-wrap gap-1">
          {gameTags.map((name) => {
            const tag = allTags.find((t) => t.name === name)
            return (
              <span
                key={name}
                className="inline-flex items-center gap-1 rounded-full border border-violet-900/60 px-2 py-0.5 text-[10px] text-violet-300"
              >
                {name}
                {tag && (
                  <button
                    type="button"
                    onClick={() => removeMutation.mutate(tag.slug)}
                    className="text-violet-400 hover:text-violet-200"
                    aria-label={`Remove ${name}`}
                  >
                    ×
                  </button>
                )}
              </span>
            )
          })}
        </div>
      )}

      <div className="space-y-1">
        {userTags
          .filter((t) => !assignedSlugs.has(t.slug))
          .map((tag) => (
            <button
              key={tag.id}
              type="button"
              onClick={() => addMutation.mutate(tag.slug)}
              className="block w-full rounded px-2 py-0.5 text-left text-[11px] text-muted-foreground hover:bg-muted hover:text-primary"
            >
              + {tag.name}
            </button>
          ))}
      </div>

      <form
        className="mt-2 flex gap-1"
        onSubmit={(e) => {
          e.preventDefault()
          if (newTagName.trim()) {
            createMutation.mutate(newTagName.trim())
          }
        }}
      >
        <input
          type="text"
          value={newTagName}
          onChange={(e) => setNewTagName(e.target.value)}
          placeholder="New tag…"
          className="min-w-0 flex-1 rounded border border-border bg-secondary px-2 py-1 text-[11px] text-foreground"
        />
        <button
          type="submit"
          disabled={!newTagName.trim() || createMutation.isPending}
          className="rounded bg-primary px-2 py-1 text-[11px] text-primary-foreground disabled:opacity-40"
        >
          Add
        </button>
      </form>
    </div>
  )
}
