namespace TunnelTanks.Core.Entities;

using TunnelTanks.Core.Types;

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

    public void Draw(uint[] surface, int surfaceWidth, int surfaceHeight)
    {
        if (!IsAlive) return;

        uint color = Type switch
        {
            SpriteType.FailedInteraction => new Color(0xff, 0x34, 0x08).ToArgb(),
            SpriteType.InfoMarker => new Color(0xff, 0xff, 0x4a).ToArgb(),
            _ => new Color(0xff, 0xff, 0xff).ToArgb(),
        };

        int x = Position.X, y = Position.Y;
        void SetPx(int px, int py) {
            if (px >= 0 && py >= 0 && px < surfaceWidth && py < surfaceHeight)
                surface[px + py * surfaceWidth] = color;
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

    public void Draw(uint[] surface, int surfaceWidth, int surfaceHeight)
    {
        foreach (var sprite in _sprites)
            sprite.Draw(surface, surfaceWidth, surfaceHeight);
    }

    public void RemoveAll() => _sprites.Clear();
}
