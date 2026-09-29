using ImmichFrame.Core.Api;

namespace ImmichFrame.Core.Logic.Pool;

/// <summary>
/// Hides a pool while <paramref name="isEnabled"/> is false. Reporting an empty count is
/// enough to take it out of a <see cref="MultiAssetPool"/>, which weighs pools by count.
/// </summary>
public class ToggleableAssetPool(IAssetPool inner, Func<bool> isEnabled) : IAssetPool
{
    public Task<long> GetAssetCount(CancellationToken ct = default)
        => isEnabled() ? inner.GetAssetCount(ct) : Task.FromResult(0L);

    public Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
        => isEnabled() ? inner.GetAssets(requested, ct) : Task.FromResult(Enumerable.Empty<AssetResponseDto>());
}
