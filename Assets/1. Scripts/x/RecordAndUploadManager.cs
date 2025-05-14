using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Diagnostics;
using TMPro;

// 녹화 완료 후 자동 업로드 기능이 포함된 레코더 (수정됨)
public class RecordAndUploadManager : MonoBehaviour
{
    [Header("Recording Settings")]
    public Camera displayCamera;
    public int recordWidth = 1920;
    public int recordHeight = 1080;
    public int frameRate = 30;
    public int quality = 20;
    
    [Header("Upload Settings")]
    public string serverURL = "http://172.16.16.154:8080/api/videos/upload";
    public bool autoUploadAfterRecording = true;
    
    [Header("UI Elements")]
    public Button recordButton;
    public Button stopButton;
    public Button uploadButton;
    public TextMeshProUGUI statusText;
    //public Slider progressBar;
    
    [Header("Save Settings")]
    public string saveFolder = "Recordings";
    
    private bool isRecording = false;
    private bool isUploading = false;
    private string outputPath;
    private RenderTexture renderTexture;
    private Camera recordingCamera;
    private int frameIndex = 0;
    private string framesDirectory;
    private string lastRecordedVideoPath;
    
    void Start()
    {
        SetupComponents();
        SetupUI();
    }
    
    void SetupComponents()
    {
        if (displayCamera == null)
            displayCamera = Camera.main;
        
        SetupRecordingCamera();
        SetupDirectories();
        SetupRenderTexture();
        CheckFFmpeg();
    }
    
    void SetupUI()
    {
        recordButton.onClick.AddListener(StartRecording);
        stopButton.onClick.AddListener(StopRecording);
        uploadButton.onClick.AddListener(UploadLastVideo);
        
        //progressBar.gameObject.SetActive(false);
        UpdateUI();
    }
    
    void SetupRecordingCamera()
    {
        GameObject recordingCameraGO = new GameObject("RecordingCamera");
        recordingCamera = recordingCameraGO.AddComponent<Camera>();
        recordingCamera.CopyFrom(displayCamera);
        recordingCamera.enabled = false;
        
        recordingCamera.transform.SetParent(displayCamera.transform);
        recordingCamera.transform.localPosition = Vector3.zero;
        recordingCamera.transform.localRotation = Quaternion.identity;
    }
    
    void SetupDirectories()
    {
        string recordingsDir = Path.Combine(Application.dataPath, saveFolder);
        Directory.CreateDirectory(recordingsDir);
        
        framesDirectory = Path.Combine(Application.temporaryCachePath, "frames");
        Directory.CreateDirectory(framesDirectory);
        
        outputPath = Path.Combine(recordingsDir, $"recording_{System.DateTime.Now:yyyyMMdd_HHmmss}.mp4");
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
    }
    
    public void StartRecording()
    {
        if (!isRecording && !isUploading)
        {
            isRecording = true;
            frameIndex = 0;
            
            // 새로운 출력 경로 생성
            outputPath = Path.Combine(Path.GetDirectoryName(outputPath), 
                $"recording_{System.DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            
            ClearFrames();
            
            recordingCamera.targetTexture = renderTexture;
            recordingCamera.enabled = true;
            
            StartCoroutine(CaptureFrames());
            UpdateUI();
            
            UnityEngine.Debug.Log("녹화 시작");
        }
    }
    
    public void StopRecording()
    {
        if (isRecording)
        {
            isRecording = false;
            
            recordingCamera.enabled = false;
            recordingCamera.targetTexture = null;
            
            UpdateUI();
            StartCoroutine(CreateVideoAndUpload());
        }
    }
    
    IEnumerator CaptureFrames()
    {
        while (isRecording)
        {
            yield return new WaitForEndOfFrame();
            
            SyncCameraSettings();
            recordingCamera.Render();
            
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
        recordingCamera.fieldOfView = displayCamera.fieldOfView;
        recordingCamera.nearClipPlane = displayCamera.nearClipPlane;
        recordingCamera.farClipPlane = displayCamera.farClipPlane;
        recordingCamera.cullingMask = displayCamera.cullingMask;
        recordingCamera.backgroundColor = displayCamera.backgroundColor;
        recordingCamera.clearFlags = displayCamera.clearFlags;
    }
    
    IEnumerator CreateVideoAndUpload()
    {
        //progressBar.gameObject.SetActive(true);
        //progressBar.value = 0f;
        
        // 1. 비디오 생성
        UpdateStatus("비디오 생성 중...");
        yield return StartCoroutine(CreateVideo());
        
        if (!File.Exists(outputPath))
        {
            UpdateStatus("비디오 생성 실패");
            //progressBar.gameObject.SetActive(false);
            yield break;
        }
        
        lastRecordedVideoPath = outputPath;
        //progressBar.value = 0.5f;
        
        // 2. 자동 업로드 (설정된 경우)
        if (autoUploadAfterRecording)
        {
            UpdateStatus("자동 업로드 시작...");
            yield return StartCoroutine(UploadVideo(outputPath));
        }
        else
        {
            UpdateStatus("녹화 완료! 수동 업로드 가능");
            //progressBar.gameObject.SetActive(false);
        }
        
        UpdateUI();
    }
    
    IEnumerator CreateVideo()
    {
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
        
        if (!File.Exists(ffmpegPath))
        {
            UnityEngine.Debug.LogError("FFmpeg.exe를 찾을 수 없습니다!");
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
        
        process.Dispose();
        ClearFrames();
        
        if (process.ExitCode != 0)
        {
            UnityEngine.Debug.LogError("비디오 생성 실패");
        }
    }
    
    public void UploadLastVideo()
    {
        if (!string.IsNullOrEmpty(lastRecordedVideoPath) && File.Exists(lastRecordedVideoPath))
        {
            StartCoroutine(UploadVideo(lastRecordedVideoPath));
        }
        else
        {
            UpdateStatus("업로드할 영상이 없습니다.");
        }
    }
    
    IEnumerator UploadVideo(string videoPath)
    {
        if (isUploading) yield break;
        
        isUploading = true;
        //progressBar.gameObject.SetActive(true);
        
        // 썸네일 생성 (수정된 부분)
        UpdateStatus("썸네일 생성 중...");
        string thumbnailPath = null;
        yield return StartCoroutine(GenerateThumbnailCoroutine(videoPath, result => {
            thumbnailPath = result;
        }));
        
        if (string.IsNullOrEmpty(thumbnailPath))
        {
            UpdateStatus("썸네일 생성 실패");
            isUploading = false;
            //progressBar.gameObject.SetActive(false);
            yield break;
        }
        
        //progressBar.value = 0.3f;
        UpdateStatus("파일 업로드 중...");
        
        // 멀티파트 폼 데이터 생성
        WWWForm form = new WWWForm();
        
        byte[] videoData = File.ReadAllBytes(videoPath);
        form.AddBinaryData("video", videoData, Path.GetFileName(videoPath), "video/mp4");
        
        byte[] thumbnailData = File.ReadAllBytes(thumbnailPath);
        form.AddBinaryData("thumbnail", thumbnailData, Path.GetFileName(thumbnailPath), "image/jpeg");
        
        form.AddField("title", Path.GetFileNameWithoutExtension(videoPath));
        form.AddField("description", "Unity에서 녹화된 영상");
        form.AddField("uploadTime", System.DateTime.Now.ToString());
        
        // HTTP 업로드
        using (UnityWebRequest request = UnityWebRequest.Post(serverURL, form))
        {
            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                float progress = 0.3f + (request.uploadProgress * 0.7f);
                //progressBar.value = progress;
                UpdateStatus($"업로드 중... {(progress * 100):F1}%");
                yield return null;
            }
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.Log($"업로드 성공: {request.downloadHandler.text}");
                UpdateStatus("업로드 완료!");
                //progressBar.value = 1f;
            }
            else
            {
                UnityEngine.Debug.LogError($"업로드 실패: {request.error}");
                UpdateStatus($"업로드 실패: {request.error}");
            }
        }
        
        // 임시 썸네일 삭제
        if (File.Exists(thumbnailPath))
            File.Delete(thumbnailPath);
        
        yield return new WaitForSeconds(2f);
        isUploading = false;
        //progressBar.gameObject.SetActive(false);
        UpdateUI();
    }
    
    // 썸네일 생성 코루틴 (수정된 부분)
    IEnumerator GenerateThumbnailCoroutine(string videoPath, System.Action<string> callback)
    {
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
        string thumbnailPath = Path.ChangeExtension(videoPath, "_thumbnail.jpg");
        
        string arguments = $"-ss 3 -i \"{videoPath}\" -vframes 1 -q:v 2 \"{thumbnailPath}\"";
        
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        Process process = Process.Start(startInfo);
        
        while (!process.HasExited)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        process.Dispose();
        
        // 콜백으로 결과 반환
        if (process.ExitCode == 0 && File.Exists(thumbnailPath))
        {
            callback(thumbnailPath);
        }
        else
        {
            callback(null);
        }
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
    
    void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
    
    void UpdateUI()
    {
        recordButton.interactable = !isRecording && !isUploading;
        stopButton.interactable = isRecording;
        uploadButton.interactable = !isRecording && !isUploading && !string.IsNullOrEmpty(lastRecordedVideoPath);
        
        if (!isRecording && !isUploading && statusText != null)
        {
            statusText.text = "준비";
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
}