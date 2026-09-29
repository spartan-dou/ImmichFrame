namespace ImmichFrame.Core.Interfaces;

/// <summary>
/// Whether memories are shown: the only source of truth, set through the API and kept
/// across restarts. The <c>ShowMemories</c> account setting is ignored by this fork.
/// </summary>
public interface IMemoriesSwitch
{
    bool Enabled { get; set; }
}
