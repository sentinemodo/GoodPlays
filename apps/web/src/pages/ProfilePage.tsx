import { SignedIn, SignedOut, SignInButton, useUser } from '@clerk/clerk-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Star, UserRound } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { ActivityLog } from '../components/ActivityLog'
import { useApiAuth } from '../hooks/useApiAuth'
import { api, type PlayerProfile, type ProfileGame, type ProfileTrophy, type TrophyClass } from '../lib/api'

const hasClerk = Boolean(import.meta.env.VITE_CLERK_PUBLISHABLE_KEY)

const trophyClasses: TrophyClass[] = ['Platinum', 'Gold', 'Silver', 'Bronze']
const rarityOptions = [
  { label: 'Any rarity', value: '' },
  { label: '5% or rarer', value: '5' },
  { label: '10% or rarer', value: '10' },
  { label: '25% or rarer', value: '25' },
  { label: '50% or rarer', value: '50' },
]

function formatHours(hours: number) {
  return `${Number(hours.toFixed(1))}h`
}

function GameChip({ game, note }: { game: ProfileGame; note: string }) {
  return (
    <Link to={`/games/${game.slug}`} className="flex items-center gap-3 rounded-xl border border-border bg-card/60 p-3 hover:border-primary/50">
      {game.coverUrl ? (
        <img src={game.coverUrl} alt="" className="h-14 w-10 rounded object-cover" />
      ) : (
        <div className="flex h-14 w-10 items-center justify-center rounded bg-muted text-xs text-muted-foreground">?</div>
      )}
      <div className="min-w-0">
        <p className="truncate text-sm font-medium">{game.title}</p>
        <p className="text-xs text-muted-foreground">{note}</p>
      </div>
    </Link>
  )
}

function TrophyCard({
  trophy,
  onToggle,
}: {
  trophy: ProfileTrophy
  onToggle?: (trophy: ProfileTrophy) => void
}) {
  return (
    <div className="flex items-start gap-3 rounded-xl border border-border bg-card/60 p-3">
      {trophy.iconUrl ? (
        <img src={trophy.iconUrl} alt="" className="size-10 rounded" />
      ) : (
        <div className="flex size-10 items-center justify-center rounded bg-muted text-xs text-muted-foreground">🏆</div>
      )}
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium">{trophy.name}</p>
        <p className="text-xs text-muted-foreground">
          <Link to={`/games/${trophy.gameSlug}`} className="hover:text-primary">{trophy.gameTitle}</Link>
          {' · '}
          {trophy.trophyClass}
          {trophy.rarityPercent != null ? ` · ${trophy.rarityPercent}%` : ''}
        </p>
        {trophy.description && <p className="mt-1 text-xs text-muted-foreground">{trophy.description}</p>}
      </div>
      {onToggle && (
        <button
          type="button"
          aria-pressed={trophy.isFeatured}
          aria-label={trophy.isFeatured ? 'Unstar most important trophy' : 'Star as most important trophy'}
          onClick={() => onToggle(trophy)}
          className={`rounded-full border p-1.5 ${
            trophy.isFeatured ? 'border-trophy bg-trophy/15 text-trophy' : 'border-border text-muted-foreground hover:text-trophy'
          }`}
        >
          <Star className={`size-4 ${trophy.isFeatured ? 'fill-current' : ''}`} />
        </button>
      )}
    </div>
  )
}

function CategoryBars({ rows }: { rows: PlayerProfile['platforms'] }) {
  const top = rows.slice(0, 9)
  const rest = rows.slice(9)
  const bars = rest.length === 0
    ? top
    : [
        ...top,
        {
          key: 'other',
          label: 'Other',
          gameCount: rest.reduce((sum, row) => sum + row.gameCount, 0),
          hours: rest.reduce((sum, row) => sum + row.hours, 0),
        },
      ]
  const maxHours = Math.max(...bars.map((row) => row.hours), 1)

  return (
    <section className="rounded-2xl border border-border bg-card/40 p-4">
      <h2 className="font-display text-lg font-semibold">By category</h2>
      {bars.length === 0 ? (
        <p className="mt-2 text-sm text-muted-foreground">Nothing here yet.</p>
      ) : (
        <ul className="mt-4 space-y-3">
          {bars.map((row) => (
            <li key={row.key}>
              <div className="flex items-baseline justify-between gap-3 text-sm">
                <span className="truncate font-medium">{row.label}</span>
                <span className="shrink-0 text-xs text-muted-foreground">
                  {row.gameCount} {row.gameCount === 1 ? 'game' : 'games'} · {formatHours(row.hours)}
                </span>
              </div>
              <div className="mt-1 h-2.5 overflow-hidden rounded-full bg-muted">
                <div
                  className={`h-full rounded-full ${row.key === 'other' ? 'bg-cyan' : 'bg-primary'}`}
                  style={{ width: `${Math.max(4, (row.hours / maxHours) * 100)}%` }}
                />
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function StatTable({ title, rows }: { title: string; rows: PlayerProfile['platforms'] }) {
  return (
    <section className="rounded-2xl border border-border bg-card/40 p-4">
      <h2 className="font-display text-lg font-semibold">{title}</h2>
      {rows.length === 0 ? (
        <p className="mt-2 text-sm text-muted-foreground">Nothing here yet.</p>
      ) : (
        <ul className="mt-3 space-y-2">
          {rows.map((row) => (
            <li key={row.key} className="flex items-center justify-between gap-3 text-sm">
              <span>{row.label}</span>
              <span className="text-muted-foreground">
                {row.gameCount} {row.gameCount === 1 ? 'game' : 'games'} · {formatHours(row.hours)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function ProfileBody({ avatarFallback }: { avatarFallback?: string | null }) {
  useApiAuth()
  const queryClient = useQueryClient()
  const [gameId, setGameId] = useState('')
  const [trophyClass, setTrophyClass] = useState('')
  const [maxRarity, setMaxRarity] = useState('')
  const [bioDraft, setBioDraft] = useState<string | null>(null)
  const [avatarDraft, setAvatarDraft] = useState<string | null>(null)
  const [usernameDraft, setUsernameDraft] = useState<string | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['profile', gameId, trophyClass, maxRarity],
    queryFn: () =>
      api.getProfile({
        gameId: gameId || undefined,
        trophyClass: trophyClass || undefined,
        maxRarity: maxRarity ? Number(maxRarity) : undefined,
      }),
  })

  const saveProfile = useMutation({
    mutationFn: () =>
      api.updateProfile({
        bio: bioDraft ?? data?.bio ?? '',
        avatarUrl: avatarDraft ?? data?.avatarUrl ?? '',
        username: usernameDraft ?? data?.username ?? '',
      }),
    onSuccess: async () => {
      setBioDraft(null)
      setAvatarDraft(null)
      setUsernameDraft(null)
      await queryClient.invalidateQueries({ queryKey: ['profile'] })
    },
  })

  const featureTrophy = useMutation({
    mutationFn: (trophy: ProfileTrophy) => api.setFeaturedTrophy(trophy.achievementId, !trophy.isFeatured),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['profile'] })
    },
  })

  if (isLoading) {
    return <main className="mx-auto max-w-5xl px-4 py-8 text-muted-foreground">Loading profile…</main>
  }

  if (error || !data) {
    return (
      <main className="mx-auto max-w-5xl px-4 py-8">
        <p className="text-destructive">{error instanceof Error ? error.message : 'Could not load profile.'}</p>
      </main>
    )
  }

  const avatar = (avatarDraft ?? data.avatarUrl) || avatarFallback || null
  const displayName = data.displayName || data.username

  return (
    <main className="mx-auto max-w-5xl px-4 py-6 sm:px-6 sm:py-8">
      <div className="flex flex-col gap-6 sm:flex-row sm:items-start">
        {avatar ? (
          <img src={avatar} alt="" className="size-24 rounded-full object-cover ring-2 ring-primary/40" />
        ) : (
          <div className="flex size-24 items-center justify-center rounded-full bg-muted text-muted-foreground">
            <UserRound className="size-10" />
          </div>
        )}
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold uppercase tracking-wide text-primary">Profile</p>
          <h1 className="font-display text-3xl font-bold tracking-tight">{displayName}</h1>
          <p className="text-sm text-muted-foreground">@{usernameDraft ?? data.username}</p>
          <label className="mt-4 block text-xs font-medium text-muted-foreground" htmlFor="nickname">
            Nickname
          </label>
          <input
            id="nickname"
            value={usernameDraft ?? data.username}
            onChange={(e) => setUsernameDraft(e.target.value)}
            maxLength={24}
            className="mt-1 w-full rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm outline-none focus:border-primary/60"
          />
          <p className="mt-1 text-xs text-muted-foreground">3–24 letters, numbers, hyphens, or underscores. Must be unique.</p>
          <label className="mt-4 block text-xs font-medium text-muted-foreground" htmlFor="bio">
            Bio
          </label>
          <textarea
            id="bio"
            value={bioDraft ?? data.bio ?? ''}
            onChange={(e) => setBioDraft(e.target.value)}
            rows={3}
            maxLength={500}
            placeholder="A short note about the games you play."
            className="mt-1 w-full rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm outline-none focus:border-primary/60"
          />
          <label className="mt-3 block text-xs font-medium text-muted-foreground" htmlFor="avatar">
            Avatar URL
          </label>
          <input
            id="avatar"
            value={avatarDraft ?? data.avatarUrl ?? ''}
            onChange={(e) => setAvatarDraft(e.target.value)}
            placeholder="https://…"
            className="mt-1 w-full rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm outline-none focus:border-primary/60"
          />
          <button
            type="button"
            onClick={() => saveProfile.mutate()}
            disabled={saveProfile.isPending}
            className="mt-3 rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground"
          >
            {saveProfile.isPending ? 'Saving…' : 'Save profile'}
          </button>
          {saveProfile.isError && (
            <p className="mt-2 text-sm text-amber-300">
              {saveProfile.error instanceof Error ? saveProfile.error.message : 'Could not save profile.'}
            </p>
          )}
        </div>
      </div>

      <section className="mt-8">
        <ActivityLog />
      </section>

      <section className="mt-8">
        <h2 className="font-display text-lg font-semibold">Most loved game</h2>
        <p className="text-sm text-muted-foreground">
          {data.mostLovedIsStarred
            ? 'Starred from your library.'
            : 'No game is starred, so this is the one with the most hours.'}
        </p>
        <div className="mt-3 max-w-md">
          {data.mostLovedGame ? (
            <GameChip
              game={data.mostLovedGame}
              note={data.mostLovedGame.hoursPlayed != null ? `${data.mostLovedGame.hoursPlayed}h played` : 'No hours yet'}
            />
          ) : (
            <p className="text-sm text-muted-foreground">Add games to your library to pick a favorite.</p>
          )}
        </div>
      </section>

      <div className="mt-8 grid gap-4 md:grid-cols-2">
        <section>
          <h2 className="mb-3 font-display text-lg font-semibold">Most important trophy</h2>
          {data.mostImportantTrophy ? (
            <TrophyCard trophy={data.mostImportantTrophy} />
          ) : (
            <p className="text-sm text-muted-foreground">Star a trophy in the case below.</p>
          )}
        </section>
        <section>
          <h2 className="mb-3 font-display text-lg font-semibold">Most recent trophy</h2>
          {data.mostRecentTrophy ? (
            <TrophyCard trophy={data.mostRecentTrophy} />
          ) : (
            <p className="text-sm text-muted-foreground">Unlocked trophies show up after a platform sync.</p>
          )}
        </section>
      </div>

      <div className="mt-8 grid gap-4 md:grid-cols-2">
        <StatTable title="By platform" rows={data.platforms} />
        <CategoryBars rows={data.categories} />
      </div>

      <div className="mt-8 grid gap-6 md:grid-cols-2">
        <section>
          <h2 className="font-display text-lg font-semibold">Top 5 by time</h2>
          <div className="mt-3 space-y-2">
            {data.topGamesByTime.length === 0 && <p className="text-sm text-muted-foreground">No playtime yet.</p>}
            {data.topGamesByTime.map((game) => (
              <GameChip key={game.gameId} game={game} note={game.hoursPlayed != null ? `${game.hoursPlayed}h` : '0h'} />
            ))}
          </div>
        </section>
        <section>
          <h2 className="font-display text-lg font-semibold">Last five played</h2>
          <div className="mt-3 space-y-2">
            {data.lastPlayedGames.length === 0 && <p className="text-sm text-muted-foreground">No recent sessions yet.</p>}
            {data.lastPlayedGames.map((game) => (
              <GameChip key={game.gameId} game={game} note={game.lastPlayed ? `Last run ${game.lastPlayed}` : 'Unknown'} />
            ))}
          </div>
        </section>
      </div>

      <section className="mt-8">
        <h2 className="font-display text-lg font-semibold">Trophy case</h2>
        <div className="mt-3 flex flex-wrap gap-2">
          <select
            value={gameId}
            onChange={(e) => setGameId(e.target.value)}
            className="rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm"
            aria-label="Filter trophies by game"
          >
            <option value="">All games</option>
            {data.trophyGames.map((game) => (
              <option key={game.gameId} value={game.gameId}>{game.title}</option>
            ))}
          </select>
          <select
            value={trophyClass}
            onChange={(e) => setTrophyClass(e.target.value)}
            className="rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm"
            aria-label="Filter trophies by class"
          >
            <option value="">All classes</option>
            {trophyClasses.map((item) => (
              <option key={item} value={item}>{item}</option>
            ))}
          </select>
          <select
            value={maxRarity}
            onChange={(e) => setMaxRarity(e.target.value)}
            className="rounded-xl border border-border bg-secondary/60 px-3 py-2 text-sm"
            aria-label="Filter trophies by rarity"
          >
            {rarityOptions.map((option) => (
              <option key={option.label} value={option.value}>{option.label}</option>
            ))}
          </select>
        </div>
        <div className="mt-4 grid gap-2">
          {data.trophies.length === 0 && <p className="text-sm text-muted-foreground">No trophies match these filters.</p>}
          {data.trophies.map((trophy) => (
            <TrophyCard key={trophy.achievementId} trophy={trophy} onToggle={(item) => featureTrophy.mutate(item)} />
          ))}
        </div>
      </section>
    </main>
  )
}

function SignedInProfile() {
  const { user } = useUser()
  return <ProfileBody avatarFallback={user?.imageUrl} />
}

export function ProfilePage() {
  if (!hasClerk) {
    return <ProfileBody />
  }

  return (
    <>
      <SignedIn>
        <SignedInProfile />
      </SignedIn>
      <SignedOut>
        <main className="mx-auto max-w-3xl px-6 py-16 text-center">
          <h1 className="font-display text-3xl font-bold">Sign in</h1>
          <p className="mt-3 text-muted-foreground">Your profile is available after you sign in.</p>
          <SignInButton mode="modal">
            <button className="mt-6 rounded-xl bg-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground ring-glow">
              Sign in
            </button>
          </SignInButton>
        </main>
      </SignedOut>
    </>
  )
}
