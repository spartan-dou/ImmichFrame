using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.Core.Logic;

public class MemoriesSwitch : IMemoriesSwitch
{
    private volatile bool _enabled = true;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }
}
