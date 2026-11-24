using System.Collections;
using UnityEngine;
#if UNITY_EDITOR && SATURA_SPACE
using SaturaSpace;
#endif

// Satura Space handles for driving and filming Spear Drop. Compiled only inside the editor with Satura Space.
public static class SpearDropHandles
{
#if UNITY_EDITOR && SATURA_SPACE
    static SpearDropGame G => SpearDropGame.I;

    [SaturaHandle("Starts a fresh round: five dummies, eight spears, score 0. Returns the state.")]
    public static string StartRun()
    {
        G.NewRound();
        return G.State();
    }

    [SaturaHandle("The game state: round, score, spears, dummies down, last result, time scale")]
    public static string GetState() => G.State();

    [SaturaHandle("One bot throw at dummy index (0-4), at the head or the chest. Waits until the throw is resolved and returns the state.")]
    public static IEnumerator BotThrow(int dummy, bool head)
    {
        yield return G.BotThrow(dummy, head);
        yield return G.State();
    }

    [SaturaHandle("A player-style throw along a view: yaw and pitch in degrees, power 0-1. Waits for the result.")]
    public static IEnumerator Throw(float yaw, float pitch, float power)
    {
        var p = G.player;
        while (G.busy || p.held == null || p.mode != SpearThrower.Mode.Aim) yield return null;
        p.botDriven = true;
        p.yaw = yaw; p.pitch = pitch; p.charge = power; p.charging = true;
        p.ApplyLook();
        p.Release(null);
        yield return null;
        while (G.busy) yield return null;
        p.botDriven = false;
        yield return G.State();
    }

    [SaturaHandle("The filmed bot run: starts a round, then throws at the dummies one by one (count throws). Yields the state.")]
    public static IEnumerator DemoRun(int count)
    {
        G.NewRound();
        G.player.botDriven = true;
        int[] order = { 1, 0, 4, 2, 3, 0, 1, 2 };
        bool[] head = { true, true, true, true, false, true, true, true };
        for (int i = 0; i < count && i < order.Length; i++)
        {
            yield return G.BotThrow(order[i], head[i]);
            if (G.roundOver) break;
        }
        yield return G.State();
    }

    [SaturaHandle("Starts filming the game view (3D and UI) to an mp4 with ffmpeg; the game clock steps 1/fps per frame")]
    public static string StartCapture(string path, int fps, string ffmpeg)
    {
        FrameCapture.Begin(path, fps, ffmpeg);
        return $"capturing {Screen.width}x{Screen.height} to {path}";
    }

    [SaturaHandle("Stops filming and finishes the mp4")]
    public static string StopCapture() => FrameCapture.Active != null ? FrameCapture.Active.End() : "not capturing";

    [SaturaHandle("Gives control back to the keyboard and mouse")]
    public static string PlayerControl()
    {
        G.player.botDriven = false;
        return G.State();
    }
#endif
}
