using Battle.Logic;

namespace Game.UI
{
public sealed class BattleActionTimelineUIContext : IUIContext
{
    public BattleActionTimelineUIContext(IBattleActionOrderSource source)
    {
        Source = source;
    }

    public IBattleActionOrderSource Source { get; }
}
}
