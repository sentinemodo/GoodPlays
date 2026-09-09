using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoodPlays.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    sort_title = table.Column<string>(type: "text", nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    cover_url = table.Column<string>(type: "text", nullable: true),
                    developer = table.Column<string>(type: "text", nullable: true),
                    publisher = table.Column<string>(type: "text", nullable: true),
                    game_type = table.Column<string>(type: "text", nullable: false),
                    parent_game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metadata_status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_games_games_parent_game_id",
                        column: x => x.parent_game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "genres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platforms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platforms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    clerk_id = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_external_ids",
                columns: table => new
                {
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_external_ids", x => new { x.game_id, x.source });
                    table.ForeignKey(
                        name: "FK_game_external_ids_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_genres",
                columns: table => new
                {
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    genre_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_genres", x => new { x.game_id, x.genre_id });
                    table.ForeignKey(
                        name: "FK_game_genres_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_game_genres_genres_genre_id",
                        column: x => x.genre_id,
                        principalTable: "genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_platforms",
                columns: table => new
                {
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_platforms", x => new { x.game_id, x.platform_id });
                    table.ForeignKey(
                        name: "FK_game_platforms_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_game_platforms_platforms_platform_id",
                        column: x => x.platform_id,
                        principalTable: "platforms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modality = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    source_object_key = table.Column<string>(type: "text", nullable: true),
                    stats_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_jobs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "library_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: true),
                    hours_played = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    hours_played_source = table.Column<string>(type: "text", nullable: false),
                    hours_played_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    started_at = table.Column<DateOnly>(type: "date", nullable: true),
                    completed_at = table.Column<DateOnly>(type: "date", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    visibility = table.Column<string>(type: "text", nullable: false),
                    platform_external_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_library_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_library_entries_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_library_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "text", nullable: false),
                    external_account_id = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    access_token_enc = table.Column<string>(type: "text", nullable: true),
                    refresh_token_enc = table.Column<string>(type: "text", nullable: true),
                    token_expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    last_sync_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    sync_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_connections_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    bio = table.Column<string>(type: "text", nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    is_profile_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    library_visibility = table.Column<string>(type: "text", nullable: false),
                    stats_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_external_ids_source_external_id",
                table: "game_external_ids",
                columns: new[] { "source", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_genres_genre_id",
                table: "game_genres",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "IX_game_platforms_platform_id",
                table: "game_platforms",
                column: "platform_id");

            migrationBuilder.CreateIndex(
                name: "IX_games_parent_game_id",
                table: "games",
                column: "parent_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_games_slug",
                table: "games",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_genres_slug",
                table: "genres",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_jobs_user_id",
                table: "import_jobs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_library_entries_game_id",
                table: "library_entries",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_library_entries_user_id_game_id",
                table: "library_entries",
                columns: new[] { "user_id", "game_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_library_entries_user_id_status",
                table: "library_entries",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_library_entries_user_id_updated_at",
                table: "library_entries",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_connections_user_id_platform",
                table: "platform_connections",
                columns: new[] { "user_id", "platform" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platforms_slug",
                table: "platforms",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_username",
                table: "user_profiles",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_clerk_id",
                table: "users",
                column: "clerk_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_external_ids");

            migrationBuilder.DropTable(
                name: "game_genres");

            migrationBuilder.DropTable(
                name: "game_platforms");

            migrationBuilder.DropTable(
                name: "import_jobs");

            migrationBuilder.DropTable(
                name: "library_entries");

            migrationBuilder.DropTable(
                name: "platform_connections");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropTable(
                name: "platforms");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
