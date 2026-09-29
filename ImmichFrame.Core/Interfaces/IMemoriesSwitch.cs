namespace ImmichFrame.Core.Interfaces;

/// <summary>
/// Runtime gate on memories, flipped through the API without touching the settings.
/// It only hides memories of accounts that have <c>ShowMemories</c> enabled, and it
/// starts enabled on every restart.
/// </summary>
public interface IMemoriesSwitch
{
    bool Enabled { get; set; }
}
