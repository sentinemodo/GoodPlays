using GoodPlays.Domain.Entities;

namespace GoodPlays.Api.Services;

public interface ICurrentUserAccessor
{
    bool IsAuthEnabled { get; }

    Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken);
}
