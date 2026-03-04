namespace Tunnerer.Desktop.Rendering;

using Tunnerer.Core;
using Tunnerer.Core.Collision;
using Tunnerer.Core.Config;
using Tunnerer.Core.Entities;
using Tunnerer.Core.Entities.Links;
using Tunnerer.Core.Entities.Machines;
using Tunnerer.Core.Entities.Projectiles;
using Tunnerer.Core.Rendering;
using Tunnerer.Core.Types;

public sealed class WorldCompositeRenderer
{
    private TankRenderState[] _tankStates = [];
    private ProjectileRenderState[] _projectileStates = [];
    private MachineRenderState[] _machineStates = [];
    private LinkRenderState[] _linkStates = [];
    private SpriteRenderState[] _spriteStates = [];

    public void Compose(World world, uint[] terrainPixels, uint[] compositePixels)
    {
        Array.Copy(terrainPixels, compositePixels, terrainPixels.Length);
        var surface = new Surface(compositePixels, world.Terrain.Width, world.Terrain.Height);

        int linkCount = world.LinkMap.CopyRenderStates(EnsureCapacity(ref _linkStates, world.LinkMap.LinkCount));
        DrawLinks(surface, _linkStates, linkCount);

        int machineCount = world.Machines.CopyRenderStates(EnsureCapacity(ref _machineStates, world.Machines.Count));
        DrawMachines(surface, _machineStates, machineCount);

        int projectileCount = world.Projectiles.CopyRenderStates(EnsureCapacity(ref _projectileStates, world.Projectiles.Count));
        DrawProjectiles(surface, _projectileStates, projectileCount);

        int spriteCount = world.Sprites.CopyRenderStates(EnsureCapacity(ref _spriteStates, world.Sprites.Count));
        DrawSprites(surface, _spriteStates, spriteCount);

        int tankCount = world.TankList.CopyRenderStates(EnsureCapacity(ref _tankStates, world.TankList.Count));
        DrawTanks(surface, _tankStates, tankCount);
    }

    private static Span<T> EnsureCapacity<T>(ref T[] buffer, int requiredCount)
    {
        if (buffer.Length < requiredCount)
            buffer = new T[Math.Max(requiredCount, 16)];
        return buffer;
    }

    private static void DrawLinks(Surface surface, LinkRenderState[] states, int count)
    {
        uint liveColor = Tweaks.Colors.LinkLive.ToArgb();
        uint blockedColor = Tweaks.Colors.LinkBlocked.ToArgb();

        for (int i = 0; i < count; i++)
        {
            ref readonly var link = ref states[i];
            if (!link.IsAlive || link.Type == LinkType.Theoretical)
                continue;

            uint color = link.Type == LinkType.Live ? liveColor : blockedColor;
            Raycaster.BresenhamLine(link.From, link.To, pos =>
            {
                if (surface.IsInside(pos.X, pos.Y))
                    surface.Pixels[pos.X + pos.Y * surface.Width] = color;
            });
        }
    }

    private static void DrawMachines(Surface surface, MachineRenderState[] states, int count)
    {
        int half = Tweaks.Machine.BoundingBoxHalfSize;
        uint templateColor = Tweaks.Colors.MachineTemplate.ToArgb();
        uint harvesterColor = Tweaks.Colors.Harvester.ToArgb();
        uint chargerColor = Tweaks.Colors.Charger.ToArgb();

        for (int i = 0; i < count; i++)
        {
            ref readonly var machine = ref states[i];
            if (!machine.IsAlive)
                continue;

            uint color = machine.State == MachineState.Template
                ? templateColor
                : machine.Type == MachineType.Harvester ? harvesterColor : chargerColor;

            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    bool edge = Math.Abs(dx) == half || Math.Abs(dy) == half;
                    if (!edge)
                        continue;
                    int px = machine.Position.X + dx;
                    int py = machine.Position.Y + dy;
                    if (surface.IsInside(px, py))
                        surface.Pixels[px + py * surface.Width] = color;
                }
            }
        }
    }

    private static void DrawProjectiles(Surface surface, ProjectileRenderState[] states, int count)
    {
        for (int i = 0; i < count; i++)
        {
            ref readonly var projectile = ref states[i];
            if (!projectile.IsAlive || !surface.IsInside(projectile.Position.X, projectile.Position.Y))
                continue;
            uint color = ProjectileList.Behaviors[(int)projectile.Type].DrawColor.ToArgb();
            surface.Pixels[projectile.Position.X + projectile.Position.Y * surface.Width] = color;
        }
    }

    private static void DrawSprites(Surface surface, SpriteRenderState[] states, int count)
    {
        for (int i = 0; i < count; i++)
        {
            ref readonly var sprite = ref states[i];
            if (!sprite.IsAlive)
                continue;

            uint color = sprite.Type switch
            {
                SpriteType.FailedInteraction => Tweaks.Colors.FailedInteraction.ToArgb(),
                SpriteType.InfoMarker => Tweaks.Colors.InfoMarker.ToArgb(),
                _ => Color.White.ToArgb(),
            };

            int x = sprite.Position.X;
            int y = sprite.Position.Y;
            if (sprite.Type == SpriteType.FailedInteraction)
            {
                SetPixel(surface, x - 1, y - 1, color);
                SetPixel(surface, x + 1, y - 1, color);
                SetPixel(surface, x, y, color);
                SetPixel(surface, x - 1, y + 1, color);
                SetPixel(surface, x + 1, y + 1, color);
            }
            else
            {
                SetPixel(surface, x, y, color);
            }
        }
    }

    private static void DrawTanks(Surface surface, TankRenderState[] states, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var tank = states[i];
            if (tank.IsDead)
                continue;

            ForEachSpritePixel(tank.Direction, tank.Position, (spriteValue, worldPos) =>
            {
                if (!surface.IsInside(worldPos.X, worldPos.Y))
                    return;
                surface.Pixels[worldPos.X + worldPos.Y * surface.Width] =
                    TankSprites.GetPixelColor(spriteValue, tank.Color).ToArgb();
            });

            var tip = new PositionF(
                tank.Position.X + tank.TurretDirection.X * Tweaks.Tank.TurretLength,
                tank.Position.Y + tank.TurretDirection.Y * Tweaks.Tank.TurretLength);
            int tx = (int)tip.X;
            int ty = (int)tip.Y;
            if (surface.IsInside(tx, ty))
                surface.Pixels[tx + ty * surface.Width] = Tweaks.Colors.TurretBarrelTip.ToArgb();
        }
    }

    private static void ForEachSpritePixel(int dir, Position origin, Action<byte, Position> visitor)
    {
        dir = Math.Clamp(dir, 0, TankSprites.DirectionCount - 1);
        var sprite = TankSprites.Sprites[dir];
        int w = TankSprites.SpriteWidth;
        int h = TankSprites.SpriteHeight;
        int cx = w / 2;
        int cy = h / 2;

        for (int sy = 0; sy < h; sy++)
        {
            for (int sx = 0; sx < w; sx++)
            {
                byte val = sprite[sx + sy * w];
                if (val == 0)
                    continue;
                var worldPos = new Position(origin.X - cx + sx, origin.Y - cy + sy);
                visitor(val, worldPos);
            }
        }
    }

    private static void SetPixel(Surface surface, int x, int y, uint color)
    {
        if (surface.IsInside(x, y))
            surface.Pixels[x + y * surface.Width] = color;
    }
}
