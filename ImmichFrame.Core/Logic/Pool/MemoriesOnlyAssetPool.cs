using ImmichFrame.Core.Api;

namespace ImmichFrame.Core.Logic.Pool;

/// <summary>
/// Fork: while <paramref name="only"/> holds, today's memories alone, whatever the memories
/// switch says. On a day without memories, the usual assets rather than a blank frame.
/// </summary>
public class MemoriesOnlyAssetPool(IAssetPool memories, IAssetPool usual, Func<bool> only) : IAssetPool
{
    public async Task<long> GetAssetCount(CancellationToken ct = default)
        => await (await Current(ct)).GetAssetCount(ct);

    public async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
        => await (await Current(ct)).GetAssets(requested, ct);

    private async Task<IAssetPool> Current(CancellationToken ct)
        => only() && await memories.GetAssetCount(ct) > 0 ? memories : usual;
}
