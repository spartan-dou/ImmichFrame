using ImmichFrame.Core.Api;

namespace ImmichFrame.Core.Logic.Pool;

/// <summary>
/// Fork: while <paramref name="only"/> holds, this account's memories alone, whatever the
/// memories switch says, and none if it has none while another account has some. On a day
/// without memories on any account, the usual assets rather than a blank frame.
/// </summary>
public class MemoriesOnlyAssetPool(
    IAssetPool memories,
    IAssetPool usual,
    Func<bool> only,
    Func<CancellationToken, Task<bool>> anyAccountHasMemories) : IAssetPool
{
    public async Task<long> GetAssetCount(CancellationToken ct = default)
        => await (await Current(ct)).GetAssetCount(ct);

    public async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
        => await (await Current(ct)).GetAssets(requested, ct);

    private async Task<IAssetPool> Current(CancellationToken ct)
        => only() && await anyAccountHasMemories(ct) ? memories : usual;
}
