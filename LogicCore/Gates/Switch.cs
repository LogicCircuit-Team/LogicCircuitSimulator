using LogicCore.Core;

namespace LogicCore.Gates;

public class Switch : Gate
{
    public override string GateType => "SWITCH";

    public bool State
    {
        get => Output;
        set => Output = value;
    }

    public Switch() : base(0)
    {
    }

    public void Toggle()
    {
        State = !State;
    }

    public override void Evaluate()
    {
    }
}