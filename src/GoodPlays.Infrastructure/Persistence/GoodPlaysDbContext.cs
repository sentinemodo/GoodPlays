using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Persistence;

public class GoodPlaysDbContext(DbContextOptions<GoodPlaysDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameExternalId> GameExternalIds => Set<GameExternalId>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<GameGenre> GameGenres => Set<GameGenre>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<GamePlatform> GamePlatforms => Set<GamePlatform>();
    public DbSet<LibraryEntry> LibraryEntries => Set<LibraryEntry>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<PlatformConnection> PlatformConnections => Set<PlatformConnection>();
    public DbSet<GameRatingCache> GameRatingCaches => Set<GameRatingCache>();
    public DbSet<GameNewsItem> GameNewsItems => Set<GameNewsItem>();
    public DbSet<GameEnrichmentRun> GameEnrichmentRuns => Set<GameEnrichmentRun>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<GameComment> GameComments => Set<GameComment>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<GameTag> GameTags => Set<GameTag>();
    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<ShelfEntry> ShelfEntries => Set<ShelfEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClerkId).HasColumnName("clerk_id");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.DisplayName).HasColumnName("display_name");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(e => e.ClerkId).IsUnique();
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.ToTable("games");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.SortTitle).HasColumnName("sort_title");
            entity.Property(e => e.ReleaseDate).HasColumnName("release_date");
            entity.Property(e => e.Summary).HasColumnName("summary");
            entity.Property(e => e.CoverUrl).HasColumnName("cover_url");
            entity.Property(e => e.Developer).HasColumnName("developer");
            entity.Property(e => e.Publisher).HasColumnName("publisher");
            entity.Property(e => e.GameType).HasColumnName("game_type").HasConversion<string>();
            entity.Property(e => e.ParentGameId).HasColumnName("parent_game_id");
            entity.Property(e => e.MetadataStatus).HasColumnName("metadata_status").HasConversion<string>();
            entity.Property(e => e.IsHiddenFromCatalog).HasColumnName("is_hidden_from_catalog").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasOne(e => e.ParentGame)
                .WithMany(e => e.ChildGames)
                .HasForeignKey(e => e.ParentGameId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GameExternalId>(entity =>
        {
            entity.ToTable("game_external_ids");
            entity.HasKey(e => new { e.GameId, e.Source });
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.Source).HasColumnName("source").HasConversion<string>();
            entity.Property(e => e.ExternalId).HasColumnName("external_id");
            entity.HasIndex(e => new { e.Source, e.ExternalId }).IsUnique();
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("genres");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        modelBuilder.Entity<GameGenre>(entity =>
        {
            entity.ToTable("game_genres");
            entity.HasKey(e => new { e.GameId, e.GenreId });
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.GenreId).HasColumnName("genre_id");
        });

        modelBuilder.Entity<Platform>(entity =>
        {
            entity.ToTable("platforms");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        modelBuilder.Entity<GamePlatform>(entity =>
        {
            entity.ToTable("game_platforms");
            entity.HasKey(e => new { e.GameId, e.PlatformId });
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.PlatformId).HasColumnName("platform_id");
        });

        modelBuilder.Entity<LibraryEntry>(entity =>
        {
            entity.ToTable("library_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>();
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.HoursPlayed).HasColumnName("hours_played").HasPrecision(8, 2);
            entity.Property(e => e.HoursPlayedSource).HasColumnName("hours_played_source").HasConversion<string>();
            entity.Property(e => e.HoursPlayedLocked).HasColumnName("hours_played_locked").HasDefaultValue(false);
            entity.Property(e => e.IsLoved).HasColumnName("is_loved").HasDefaultValue(false);
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.Source).HasColumnName("source").HasConversion<string>();
            entity.Property(e => e.Visibility).HasColumnName("visibility").HasConversion<string>();
            entity.Property(e => e.PlatformExternalId).HasColumnName("platform_external_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => new { e.UserId, e.Source, e.PlatformExternalId })
                .IsUnique()
                .HasFilter("platform_external_id IS NOT NULL");
            entity.HasIndex(e => new { e.UserId, e.GameId, e.Source })
                .IsUnique()
                .HasFilter("platform_external_id IS NULL");
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => new { e.UserId, e.UpdatedAt });
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("user_profiles");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Username).HasColumnName("username");
            entity.Property(e => e.Bio).HasColumnName("bio");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.IsProfilePublic).HasColumnName("is_profile_public").HasDefaultValue(true);
            entity.Property(e => e.LibraryVisibility).HasColumnName("library_visibility").HasConversion<string>();
            entity.Property(e => e.StatsJson).HasColumnName("stats_json").HasColumnType("jsonb");
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasOne(e => e.User)
                .WithOne(e => e.Profile)
                .HasForeignKey<UserProfile>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportJob>(entity =>
        {
            entity.ToTable("import_jobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Modality).HasColumnName("modality").HasConversion<string>();
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>();
            entity.Property(e => e.SourceObjectKey).HasColumnName("source_object_key");
            entity.Property(e => e.StatsJson).HasColumnName("stats_json").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PlatformConnection>(entity =>
        {
            entity.ToTable("platform_connections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Platform).HasColumnName("platform").HasConversion<string>();
            entity.Property(e => e.ExternalAccountId).HasColumnName("external_account_id");
            entity.Property(e => e.DisplayName).HasColumnName("display_name");
            entity.Property(e => e.AccessTokenEnc).HasColumnName("access_token_enc");
            entity.Property(e => e.RefreshTokenEnc).HasColumnName("refresh_token_enc");
            entity.Property(e => e.TokenExpiresAt).HasColumnName("token_expires_at");
            entity.Property(e => e.Scopes).HasColumnName("scopes");
            entity.Property(e => e.LastSyncAt).HasColumnName("last_sync_at");
            entity.Property(e => e.SyncEnabled).HasColumnName("sync_enabled").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => new { e.UserId, e.Platform }).IsUnique();
        });

        modelBuilder.Entity<GameRatingCache>(entity =>
        {
            entity.ToTable("game_ratings_cache");
            entity.HasKey(e => new { e.GameId, e.Source });
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.Source).HasColumnName("source").HasConversion<string>();
            entity.Property(e => e.Score).HasColumnName("score").HasPrecision(5, 2);
            entity.Property(e => e.ReviewCount).HasColumnName("review_count");
            entity.Property(e => e.Url).HasColumnName("url");
            entity.Property(e => e.FetchedAt).HasColumnName("fetched_at");
        });

        modelBuilder.Entity<GameNewsItem>(entity =>
        {
            entity.ToTable("game_news_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.Source).HasColumnName("source");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Url).HasColumnName("url");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.FetchedAt).HasColumnName("fetched_at");
            entity.HasIndex(e => new { e.GameId, e.Url }).IsUnique();
        });

        modelBuilder.Entity<GameEnrichmentRun>(entity =>
        {
            entity.ToTable("game_enrichment_runs");
            entity.HasKey(e => new { e.GameId, e.Kind });
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>();
            entity.Property(e => e.LastRunAt).HasColumnName("last_run_at");
            entity.Property(e => e.NextRunAt).HasColumnName("next_run_at");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.HasIndex(e => e.NextRunAt);
        });

        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.ToTable("achievements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.ExternalId).HasColumnName("external_id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IconUrl).HasColumnName("icon_url");
            entity.Property(e => e.RarityPercent).HasColumnName("rarity_pct").HasPrecision(5, 2);
            entity.Property(e => e.FetchedAt).HasColumnName("fetched_at");
            entity.HasIndex(e => new { e.GameId, e.ExternalId }).IsUnique();
        });

        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.ToTable("user_achievements");
            entity.HasKey(e => new { e.UserId, e.AchievementId });
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AchievementId).HasColumnName("achievement_id");
            entity.Property(e => e.UnlockedAt).HasColumnName("unlocked_at");
            entity.Property(e => e.IsFeatured).HasColumnName("is_featured").HasDefaultValue(false);
        });

        modelBuilder.Entity<GameComment>(entity =>
        {
            entity.ToTable("game_comments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.Visibility).HasColumnName("visibility").HasConversion<string>();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => new { e.GameId, e.CreatedAt });
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("tags");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.Property(e => e.Scope).HasColumnName("scope").HasConversion<string>();
            entity.HasIndex(e => new { e.UserId, e.Slug }).IsUnique();
        });

        modelBuilder.Entity<GameTag>(entity =>
        {
            entity.ToTable("game_tags");
            entity.HasKey(e => new { e.GameId, e.TagId });
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.TagId).HasColumnName("tag_id");
        });

        modelBuilder.Entity<Shelf>(entity =>
        {
            entity.ToTable("shelves");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(e => new { e.UserId, e.Slug }).IsUnique();
        });

        modelBuilder.Entity<ShelfEntry>(entity =>
        {
            entity.ToTable("shelf_entries");
            entity.HasKey(e => new { e.ShelfId, e.GameId });
            entity.Property(e => e.ShelfId).HasColumnName("shelf_id");
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.AddedAt).HasColumnName("added_at");
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetColumnType("timestamptz");
                }
            }
        }
    }
}
