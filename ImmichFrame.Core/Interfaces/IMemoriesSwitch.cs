namespace ImmichFrame.Core.Interfaces;

/// <summary>
/// Whether memories are shown: the only source of truth, set through the API and kept
/// across restarts. The <c>ShowMemories</c> account setting is ignored by this fork.
/// </summary>
public interface IMemoriesSwitch
{
    /// <summary>Memories among the usual assets.</summary>
    bool Enabled { get; set; }

    /// <summary>Memories alone, whatever <see cref="Enabled"/> says; the usual assets on a day without any.</summary>
    bool Only { get; set; }
}
