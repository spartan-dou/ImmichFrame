namespace ImmichFrame.Core.Logic.Pool;

/// <summary>
/// Fork: the memories pool of every account, so that "memories only" falls back to the
/// usual assets only when no account has memories today.
/// </summary>
public class TodaysMemories
{
    private readonly object _gate = new();
    private readonly List<IAssetPool> _pools = new();

    /// <returns>Disposing it removes the pool, as the account's logic is disposed on reload.</returns>
    public IDisposable Register(IAssetPool memories)
    {
        lock (_gate)
        {
            _pools.Add(memories);
        }

        return new Registration(this, memories);
    }

    public async Task<bool> AnyAccountHasSome(CancellationToken ct = default)
    {
        IAssetPool[] pools;
        lock (_gate)
        {
            pools = _pools.ToArray();
        }

        foreach (var pool in pools)
        {
            if (await pool.GetAssetCount(ct) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void Unregister(IAssetPool memories)
    {
        lock (_gate)
        {
            _pools.Remove(memories);
        }
    }

    private sealed class Registration(TodaysMemories owner, IAssetPool memories) : IDisposable
    {
        public void Dispose() => owner.Unregister(memories);
    }
}
