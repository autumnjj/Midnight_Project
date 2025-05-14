using UnityEngine;
using System.Collections;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class VideoRecorderManager : MonoBehaviour
{
    public Camera targetCamera;
    public int recordWidth = 1920;
    public int recordHeight = 1080;
    public int frameRate = 30;
    public int ffmpegQuality = 20;
    public string saveFolder = "Recordings";

    private bool isRecording = false;
    public string outputPath;
    private RenderTexture renderTexture;
    private Camera recordingCamera;
    private int frameIndex = 0;
    private string framesDirectory;
    private Coroutine captureCoroutine;

    public bool IsRecording => isRecording;
    public string OutputPath => outputPath;

    void Start()
    {
        InitializeComponents();
        SetupDirectories();
    }

    void InitializeComponents()
    {
        GameObject recordingCameraGO = new GameObject("RecordingCamera");
        recordingCamera = recordingCameraGO.AddComponent<Camera>();
        recordingCamera.CopyFrom(targetCamera);
        recordingCamera.enabled = false;

        renderTexture = new RenderTexture(recordWidth, recordHeight, 24);
        renderTexture.Create();
    }

    void SetupDirectories()
    {
        framesDirectory = Path.Combine(Application.temporaryCachePath, "frames");
        Directory.CreateDirectory(framesDirectory);
    }

    public void StartRecording()
    {
        if (!isRecording)
        {
            isRecording = true;
            frameIndex = 0;
            outputPath = GetNewRecordingPath();
            captureCoroutine = StartCoroutine(CaptureFrames());
            Debug.Log($"🎥 녹화 시작: {outputPath}");
        }
    }

    public IEnumerator StopRecording(System.Action<string> onComplete)
    {
        if (isRecording)
        {
            isRecording = false;
            // 코루틴이 자연스럽게 종료되지만, 혹시 모르니 명시적으로 중지
            if (captureCoroutine != null)
            {
                StopCoroutine(captureCoroutine);
                captureCoroutine = null;
            }
            yield return StartCoroutine(GenerateVideoFile(onComplete));
            Debug.Log("🛑 녹화 중지 요청됨");
        }
    }

    IEnumerator CaptureFrames()
    {
        recordingCamera.targetTexture = renderTexture;
        recordingCamera.enabled = true;

        while (isRecording)
        {
            yield return new WaitForEndOfFrame();

            RenderTexture.active = renderTexture;
            Texture2D frame = new Texture2D(recordWidth, recordHeight, TextureFormat.RGB24, false);
            frame.ReadPixels(new Rect(0, 0, recordWidth, recordHeight), 0, 0);
            frame.Apply();

            File.WriteAllBytes(
                Path.Combine(framesDirectory, $"frame_{frameIndex:D6}.png"),
                frame.EncodeToPNG()
            );

            DestroyImmediate(frame);
            frameIndex++;
            yield return new WaitForSeconds(1f / frameRate);
        }

        recordingCamera.enabled = false;
        RenderTexture.active = null;
        Debug.Log("녹화 코루틴 완전히 종료됨");
    }

    IEnumerator GenerateVideoFile(System.Action<string> callback)
    {
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
        string arguments = $"-r {frameRate} -i \"{framesDirectory}/frame_%06d.png\" " +
                           $"-vcodec libx264 -crf {ffmpegQuality} -pix_fmt yuv420p \"{outputPath}\"";
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Process process = Process.Start(startInfo);
        yield return new WaitUntil(() => process.HasExited);

        if (process.ExitCode == 0 && File.Exists(outputPath))
        {
            callback?.Invoke(outputPath);
            Debug.Log("HHHHHHH!!");
        }
        else
        {
            Debug.LogError("❌ 비디오 생성 실패");
            callback?.Invoke(null);
        }
        process.Dispose();
    }

    string GetNewRecordingPath()
    {
        string dir = Path.Combine(Application.dataPath, saveFolder);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"recording_{System.DateTime.Now:yyyyMMdd_HHmmss}.mp4");
    }

    void OnDestroy()
    {
        if (renderTexture != null)
            renderTexture.Release();
        if (recordingCamera != null && recordingCamera.gameObject != null)
            DestroyImmediate(recordingCamera.gameObject);
    }
}
