using System.Collections.Generic;
using UnityEngine;

namespace SilksongHelper;

public sealed class SpriteAnimation
{
    private readonly Texture2D[] _frames;
    private float _time;

    public float Fps { get; }
    public bool Loop { get; set; } = true;

    public int FrameCount => _frames.Length;

    public SpriteAnimation(IReadOnlyList<Texture2D> frames, float fps = 12f)
    {
        if (frames == null || frames.Count == 0)
            _frames = new[] { Texture2D.whiteTexture };
        else
        {
            _frames = new Texture2D[frames.Count];
            for (int i = 0; i < frames.Count; i++)
                _frames[i] = frames[i];
        }
        Fps = fps;
    }

    public Texture2D CurrentFrame
    {
        get
        {
            if (_frames.Length == 1)
                return _frames[0];
            _time += Time.deltaTime;
            float period = 1f / Mathf.Max(Fps, 0.01f);
            int idx = Mathf.FloorToInt(_time / period);
            if (Loop)
            {
                idx %= _frames.Length;
                if (idx < 0)
                    idx += _frames.Length;
            }
            else if (idx >= _frames.Length)
            {
                idx = _frames.Length - 1;
            }
            return _frames[idx];
        }
    }

    public void Reset() => _time = 0f;
}
