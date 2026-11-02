using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Replays deterministic shot setup synchronously, then resumes scaled-time playback.
public sealed class SavedCutsceneClock
{
    public float Elapsed { get; private set; }
    public bool IsSeeking => remaining > 0;
    private float remaining;
    public SavedCutsceneClock(float seek = 0) { remaining = Mathf.Max(0, seek); }
    public IEnumerator Animate(float seconds, Action<float> apply)
    {
        float elapsed = Mathf.Min(remaining, seconds);
        remaining -= elapsed; Elapsed += elapsed;
        apply(seconds > 0 ? Mathf.Clamp01(elapsed / seconds) : 1);
        while (elapsed < seconds)
        {
            yield return null;
            float dt = Mathf.Min(Time.deltaTime, seconds - elapsed);
            elapsed += dt; Elapsed += dt;
            apply(Mathf.Clamp01(elapsed / seconds));
        }
    }
    public IEnumerator Run(IEnumerator sequence)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(sequence);
        try
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { (top as IDisposable)?.Dispose(); stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested);
                else yield return top.Current;
            }
        }
        finally { foreach (var enumerator in stack) (enumerator as IDisposable)?.Dispose(); }
    }
}
