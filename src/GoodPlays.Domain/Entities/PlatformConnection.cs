using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class PlatformConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public PlatformConnectionPlatform Platform { get; set; }
    public required string ExternalAccountId { get; set; }
    public string? DisplayName { get; set; }
    public string? AccessTokenEnc { get; set; }
    public string? RefreshTokenEnc { get; set; }
    public DateTimeOffset? TokenExpiresAt { get; set; }
    public string[] Scopes { get; set; } = [];
    public DateTimeOffset? LastSyncAt { get; set; }
    public bool SyncEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
