namespace Tunnerer.Core.Entities;

using Tunnerer.Core.Types;
using Tunnerer.Core.Config;
using Tunnerer.Core.Rendering;

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
    public int Count => _sprites.Count;

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

    public int CopyRenderStates(Span<SpriteRenderState> destination)
    {
        int count = Math.Min(destination.Length, _sprites.Count);
        for (int i = 0; i < count; i++)
        {
            var s = _sprites[i];
            destination[i] = new SpriteRenderState(
                Position: s.Position,
                Type: s.Type,
                IsAlive: s.IsAlive);
        }
        return count;
    }

    public void RemoveAll() => _sprites.Clear();
}
