#if UNITY_EDITOR
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only filming helper: steps the game clock by exactly 1/fps per frame, grabs the finished game view
// (3D and UI) each frame and pipes it to ffmpeg. Used to film the gameplay video; not part of the game.
public class FrameCapture : MonoBehaviour
{
    public static FrameCapture Active;
    public int frames;
    public string path;
    public string error;
    Process ff;
    Stream input;
    RenderTexture rt;
    int width, height, pending;
    bool stopping;

    public static FrameCapture Begin(string outPath, int fps, string ffmpeg)
    {
        if (Active != null) Active.End();
        var g = new GameObject("FrameCapture");
        DontDestroyOnLoad(g);
        var c = g.AddComponent<FrameCapture>();
        c.path = outPath;
        c.width = Screen.width & ~1;
        c.height = Screen.height & ~1;
        c.rt = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {Screen.width}x{Screen.height} -r {fps} -i - " +
                        $"-vf \"crop={c.width}:{c.height}:0:0\" -c:v libx264 -preset veryfast -crf 17 -pix_fmt yuv420p -movflags +faststart \"{outPath}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = false,
            CreateNoWindow = true
        };
        c.ff = Process.Start(psi);
        c.input = c.ff.StandardInput.BaseStream;
        Time.captureDeltaTime = 1f / fps;
        Active = c;
        c.StartCoroutine(c.Loop());
        return c;
    }

    IEnumerator Loop()
    {
        var eof = new WaitForEndOfFrame();
        while (!stopping)
        {
            yield return eof;
            if (stopping) break;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
            pending++;
            AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, OnReadback);
        }
    }

    void OnReadback(AsyncGPUReadbackRequest req)
    {
        pending--;
        if (req.hasError || input == null) { error = "readback error"; return; }
        var data = req.GetData<byte>();
        try { input.Write(data.ToArray(), 0, data.Length); frames++; }
        catch (System.Exception e) { error = e.Message; }
    }

    public string End()
    {
        stopping = true;
        Time.captureDeltaTime = 0f;
        AsyncGPUReadback.WaitAllRequests();
        try { input?.Flush(); input?.Close(); } catch { }
        input = null;
        if (ff != null) { ff.WaitForExit(60000); ff.Dispose(); ff = null; }
        if (rt != null) { rt.Release(); Destroy(rt); }
        Active = null;
        Destroy(gameObject);
        return $"{path} frames {frames}" + (error != null ? " error " + error : "");
    }

    void OnDestroy()
    {
        if (input != null) End();
    }
}
#endif
