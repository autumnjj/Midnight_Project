using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Diagnostics;
using System.Collections;
using Debug = UnityEngine.Debug;

public class RecordingSystemManager : MonoBehaviour
{
    [Header("참조 컴포넌트")]
    public VideoRecorderManager videoRecorder;
    public BackendAPIManager backendAPI;

    [Header("UI 요소")]
    public Button recordButton;
    public Button stopButton;
    public TextMeshProUGUI statusText;

    void Start()
    {
        SetupUI();
        videoRecorder.targetCamera = Camera.main;
    }

    void SetupUI()
    {
        recordButton.onClick.AddListener(StartRecording);
        stopButton.onClick.AddListener(StopAndUpload);
    }

    public void StartRecording()
    {
        videoRecorder.StartRecording();
        UpdateUIState(true);
        statusText.text = "녹화 중...";
    }

    public void StopAndUpload()
    {
        Debug.Log("StopAndUpload");
        StartCoroutine(StopAndUploadProcess());
    }

    private IEnumerator StopAndUploadProcess()
    {
        yield return videoRecorder.StopRecording((videoPath) => {
            
        });
        
        Debug.Log("StopRecording");

        if (!string.IsNullOrEmpty(videoRecorder.outputPath))
        {
            // 썸네일 생성 코루틴 호출
            yield return StartCoroutine(GenerateThumbnail(videoRecorder.outputPath, (thumbnailPath) => {
                backendAPI.UploadVideoWithThumbnail(videoRecorder.outputPath, thumbnailPath, HandleUploadResult);
            }));
        }
        else
        {
            statusText.text = "비디오 저장 실패";
            Debug.LogError("비디오 저장 실패");
        }
        
        UpdateUIState(false);
    }

    private Process process;
    // FFmpeg로 썸네일(jpg) 생성
    IEnumerator GenerateThumbnail(string videoPath, System.Action<string> callback)
    {
        Debug.Log($"썸네일 생성 시작 {videoPath}");
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
        string thumbnailPath = Path.ChangeExtension(videoPath, "_thumbnail.jpg");
        string arguments = $"-ss 00:00:03 -i \"{videoPath}\" -vframes 1 -q:v 2 \"{thumbnailPath}\"";

        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        Process process = Process.Start(startInfo);
        yield return new WaitUntil(() => process.HasExited);
        Debug.Log(thumbnailPath);

        if (process.ExitCode == 0 && File.Exists(thumbnailPath))
        {
            Debug.Log($"✅ 썸네일 생성 성공: {thumbnailPath}");
            callback?.Invoke(thumbnailPath);
        }
        else
        {
            string error = process.StandardError.ReadToEnd();
            Debug.LogError($"❌ 썸네일 생성 실패: {error}");
            callback?.Invoke(null);
        }
        process.Dispose();
    }


    void HandleUploadResult(bool success, string message)
    {
        statusText.text = success ? "업로드 성공!" : "업로드 실패";
        Debug.Log(success ? "✅ 모든 작업 완료" : "❌ 업로드 문제 발생");
    }

    void UpdateUIState(bool isRecording)
    {
        recordButton.interactable = !isRecording;
        stopButton.interactable = isRecording;
    }
}
