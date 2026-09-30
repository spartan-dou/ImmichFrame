using ImmichFrame.Core.Api;

namespace ImmichFrame.Core.Logic.Pool;

/// <summary>
/// Fork: MultiAssetPool draws each asset on its own, with replacement, and a memory can also
/// sit in an album. The same photo twice in a batch ended up side by side in split view.
/// </summary>
public class DistinctAssetPool(IAssetPool inner) : IAssetPool
{
    public Task<long> GetAssetCount(CancellationToken ct = default) => inner.GetAssetCount(ct);

    public async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
        => (await inner.GetAssets(requested, ct)).DistinctBy(asset => asset.Id).ToList();
}
