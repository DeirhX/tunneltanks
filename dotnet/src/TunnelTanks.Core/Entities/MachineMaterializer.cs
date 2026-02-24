namespace TunnelTanks.Core.Entities;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Resources;
using TunnelTanks.Core.Entities.Machines;

public class MachineMaterializer
{
    private readonly int _ownerColor;
    private Machine? _carrying;

    public MachineMaterializer(int ownerColor)
    {
        _ownerColor = ownerColor;
    }

    public bool IsCarrying => _carrying != null;

    public void TryBuild(MachineType type, Position position, MaterialContainer resources, MachineList machineList)
    {
        if (_carrying != null) return;

        var cost = type == MachineType.Harvester
            ? new MaterialAmount(1000, 0)
            : new MaterialAmount(500, 0);

        if (!resources.CanPay(cost)) return;
        resources.Pay(cost);

        _carrying = new Machine(position, type, _ownerColor);
        _carrying.State = MachineState.Transporting;
        machineList.Add(_carrying);
    }

    public void UpdatePosition(Position position)
    {
        if (_carrying != null)
            _carrying.Position = position;
    }

    public void PlantMachine()
    {
        if (_carrying == null) return;
        _carrying.State = MachineState.Planted;
        _carrying = null;
    }
}
