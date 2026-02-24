namespace TunnelTanks.Core.Entities;

using TunnelTanks.Core.Types;
using TunnelTanks.Core.Config;

public class Sprite
{
    public Position Position { get; set; }
    public TimeSpan Lifetime { get; set; }
    public SpriteType Type { get; }
    public bool IsAlive { get; set; } = true;

    public Sprite(Position position, SpriteType type, TimeSpan lifetime)
    {
        Position = position;
        Type = type;
        Lifetime = lifetime;
    }

    public void Advance(TimeSpan dt)
    {
        Lifetime -= dt;
        if (Lifetime <= TimeSpan.Zero)
            IsAlive = false;
    }

    public void Draw(Surface surface)
    {
        if (!IsAlive) return;

        uint color = Type switch
        {
            SpriteType.FailedInteraction => Tweaks.Colors.FailedInteraction.ToArgb(),
            SpriteType.InfoMarker => Tweaks.Colors.InfoMarker.ToArgb(),
            _ => Color.White.ToArgb(),
        };

        int x = Position.X, y = Position.Y;
        void SetPx(int px, int py) {
            if (surface.IsInside(px, py))
                surface.Pixels[px + py * surface.Width] = color;
        }

        if (Type == SpriteType.FailedInteraction)
        {
            SetPx(x - 1, y - 1); SetPx(x + 1, y - 1);
            SetPx(x, y);
            SetPx(x - 1, y + 1); SetPx(x + 1, y + 1);
        }
        else
        {
            SetPx(x, y);
        }
    }
}

public enum SpriteType { FailedInteraction, InfoMarker }

public class SpriteList
{
    private readonly List<Sprite> _sprites = new();

    public void Add(Sprite sprite) => _sprites.Add(sprite);

    public void Advance(TimeSpan dt)
    {
        for (int i = _sprites.Count - 1; i >= 0; i--)
        {
            _sprites[i].Advance(dt);
            if (!_sprites[i].IsAlive)
                _sprites.RemoveAt(i);
        }
    }

    public void Draw(Surface surface)
    {
        foreach (var sprite in _sprites)
            sprite.Draw(surface);
    }

    public void RemoveAll() => _sprites.Clear();
}
