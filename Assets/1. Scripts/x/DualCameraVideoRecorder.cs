using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System.Diagnostics;
using TMPro;

public class DualCameraVideoRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    public Camera displayCamera; // 화면 표시용 카메라 (Main Camera)
    public int recordWidth = 1920;
    public int recordHeight = 1080;
    public int frameRate = 30;
    public int quality = 20;
    
    [Header("UI Elements")]
    public Button recordButton;
    public Button stopButton;
    public TextMeshProUGUI statusText;
    
    [Header("Save Settings")]
    public string saveFolder = "Recording";
    
    private bool isRecording = false;
    private string outputPath;
    private RenderTexture renderTexture;
    private Camera recordingCamera; // 녹화 전용 카메라
    private int frameIndex = 0;
    private string framesDirectory;
    
    void Start()
    {
        if (displayCamera == null)
            displayCamera = Camera.main;
        
        SetupRecordingCamera();
        SetupDirectories();
        SetupRenderTexture();
        
        recordButton.onClick.AddListener(StartRecording);
        stopButton.onClick.AddListener(StopRecording);
        
        UpdateUI();
        CheckFFmpeg();
    }
    
    void SetupRecordingCamera()
    {
        // 녹화용 카메라 생성
        GameObject recordingCameraGO = new GameObject("RecordingCamera");
        recordingCamera = recordingCameraGO.AddComponent<Camera>();
        
        // 메인 카메라와 동일한 설정 복사
        recordingCamera.CopyFrom(displayCamera);
        
        // 녹화용 카메라는 평상시에는 비활성화
        recordingCamera.enabled = false;
        
        // 부모를 메인 카메라로 설정하여 동일하게 움직이도록
        recordingCamera.transform.SetParent(displayCamera.transform);
        recordingCamera.transform.localPosition = Vector3.zero;
        recordingCamera.transform.localRotation = Quaternion.identity;
        
        UnityEngine.Debug.Log("녹화용 카메라 생성 완료");
    }
    
    void SetupDirectories()
    {
        string recordingsDir = Path.Combine(Application.dataPath, saveFolder);
        Directory.CreateDirectory(recordingsDir);
        
        framesDirectory = Path.Combine(Application.temporaryCachePath, "frames");
        Directory.CreateDirectory(framesDirectory);
        
        outputPath = Path.Combine(recordingsDir, $"recording_{System.DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        
        UnityEngine.Debug.Log($"녹화 파일 저장 위치: {outputPath}");
    }
    
    void SetupRenderTexture()
    {
        renderTexture = new RenderTexture(recordWidth, recordHeight, 24);
        renderTexture.Create();
    }
    
    void CheckFFmpeg()
    {
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
        
        if (!File.Exists(ffmpegPath))
        {
            UnityEngine.Debug.LogWarning("FFmpeg.exe를 StreamingAssets 폴더에 추가해주세요!");
        }
        else
        {
            UnityEngine.Debug.Log("FFmpeg 확인 완료");
        }
    }
    
    public void StartRecording()
    {
        if (!isRecording)
        {
            isRecording = true;
            frameIndex = 0;
            
            ClearFrames();
            
            // 녹화용 카메라를 활성화하고 렌더 텍스처 설정
            recordingCamera.targetTexture = renderTexture;
            recordingCamera.enabled = true;
            
            // 메인 카메라는 그대로 화면에 출력 (targetTexture는 null 유지)
            
            StartCoroutine(CaptureFrames());
            
            UpdateUI();
            UnityEngine.Debug.Log("녹화 시작 - 메인 카메라는 계속 화면에 출력됨");
        }
    }
    
    public void StopRecording()
    {
        if (isRecording)
        {
            isRecording = false;
            
            // 녹화용 카메라 비활성화
            recordingCamera.enabled = false;
            recordingCamera.targetTexture = null;
            
            UpdateUI();
            StartCoroutine(CreateVideo());
        }
    }
    
    IEnumerator CaptureFrames()
    {
        while (isRecording)
        {
            yield return new WaitForEndOfFrame();
            
            // 녹화용 카메라의 설정을 메인 카메라와 동기화
            SyncCameraSettings();
            
            // 수동으로 녹화용 카메라 렌더링
            recordingCamera.Render();
            
            // 렌더 텍스처에서 프레임 캡처
            RenderTexture.active = renderTexture;
            
            Texture2D frame = new Texture2D(recordWidth, recordHeight, TextureFormat.RGB24, false);
            frame.ReadPixels(new Rect(0, 0, recordWidth, recordHeight), 0, 0);
            frame.Apply();
            
            byte[] frameBytes = frame.EncodeToPNG();
            string framePath = Path.Combine(framesDirectory, $"frame_{frameIndex:D6}.png");
            File.WriteAllBytes(framePath, frameBytes);
            
            DestroyImmediate(frame);
            RenderTexture.active = null;
            
            frameIndex++;
            yield return new WaitForSeconds(1f / frameRate);
        }
    }
    
    void SyncCameraSettings()
    {
        // 메인 카메라의 설정을 녹화용 카메라에 동기화
        recordingCamera.fieldOfView = displayCamera.fieldOfView;
        recordingCamera.nearClipPlane = displayCamera.nearClipPlane;
        recordingCamera.farClipPlane = displayCamera.farClipPlane;
        recordingCamera.cullingMask = displayCamera.cullingMask;
        recordingCamera.backgroundColor = displayCamera.backgroundColor;
        recordingCamera.clearFlags = displayCamera.clearFlags;
        
        // Transform은 이미 부모-자식 관계로 동기화됨
    }
    
    IEnumerator CreateVideo()
    {
        if (statusText != null)
            statusText.text = "Video 생성 중...";
        
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
        
        if (!File.Exists(ffmpegPath))
        {
            UnityEngine.Debug.LogError("FFmpeg.exe를 찾을 수 없습니다!");
            if (statusText != null)
                statusText.text = "FFmpeg 없음";
            yield break;
        }
        
        string inputPattern = Path.Combine(framesDirectory, "frame_%06d.png");
        string arguments = $"-r {frameRate} -i \"{inputPattern}\" -vcodec libx264 -crf {quality} -pix_fmt yuv420p \"{outputPath}\"";
        
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        Process process = Process.Start(startInfo);
        
        while (!process.HasExited)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        if (process.ExitCode == 0)
        {
            UnityEngine.Debug.Log($"Record Done: {outputPath}");
            if (statusText != null)
                statusText.text = "Record Done!";
            
            OpenInExplorer(Path.GetDirectoryName(outputPath));
        }
        else
        {
            string error = process.StandardError.ReadToEnd();
            UnityEngine.Debug.LogError($"FFmpeg 오류: {error}");
            if (statusText != null)
                statusText.text = "생성 Failed";
        }
        
        process.Dispose();
        ClearFrames();
        
        yield return new WaitForSeconds(3f);
        ResetStatusText();
    }
    
    void ClearFrames()
    {
        if (Directory.Exists(framesDirectory))
        {
            string[] files = Directory.GetFiles(framesDirectory, "*.png");
            foreach (string file in files)
            {
                File.Delete(file);
            }
        }
    }
    
    void OpenInExplorer(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start("explorer.exe", path);
        }
    }
    
    void UpdateUI()
    {
        recordButton.interactable = !isRecording;
        stopButton.interactable = isRecording;
        
        if (statusText != null)
        {
            statusText.text = isRecording ? "Recording" : "Record Ready";
        }
    }
    
    void ResetStatusText()
    {
        if (!isRecording && statusText != null)
        {
            statusText.text = "Record Ready";
        }
    }
    
    void OnDestroy()
    {
        if (isRecording)
            StopRecording();
        
        if (renderTexture != null)
        {
            renderTexture.Release();
            DestroyImmediate(renderTexture);
        }
        
        if (recordingCamera != null && recordingCamera.gameObject != null)
        {
            DestroyImmediate(recordingCamera.gameObject);
        }
        
        ClearFrames();
    }
    
    void OnApplicationQuit()
    {
        if (isRecording)
            StopRecording();
    }
}
