using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoodPlays.Infrastructure.Migrations;

[DbContext(typeof(GoodPlaysDbContext))]
[Migration("20260911180000_PlatformSpecificLibraryEntries")]
public partial class PlatformSpecificLibraryEntries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_library_entries_user_id_game_id",
            table: "library_entries");

        migrationBuilder.CreateIndex(
            name: "IX_library_entries_user_id_source_platform_external_id",
            table: "library_entries",
            columns: new[] { "user_id", "source", "platform_external_id" },
            unique: true,
            filter: "platform_external_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_library_entries_user_id_game_id_source",
            table: "library_entries",
            columns: new[] { "user_id", "game_id", "source" },
            unique: true,
            filter: "platform_external_id IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_library_entries_user_id_source_platform_external_id",
            table: "library_entries");

        migrationBuilder.DropIndex(
            name: "IX_library_entries_user_id_game_id_source",
            table: "library_entries");

        migrationBuilder.CreateIndex(
            name: "IX_library_entries_user_id_game_id",
            table: "library_entries",
            columns: new[] { "user_id", "game_id" },
            unique: true);
    }
}
