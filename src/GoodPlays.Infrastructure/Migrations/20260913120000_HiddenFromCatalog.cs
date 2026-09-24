using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoodPlays.Infrastructure.Migrations;

[DbContext(typeof(GoodPlaysDbContext))]
[Migration("20260913120000_HiddenFromCatalog")]
public partial class HiddenFromCatalog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_hidden_from_catalog",
            table: "games",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "is_hidden_from_catalog",
            table: "games");
    }
}
