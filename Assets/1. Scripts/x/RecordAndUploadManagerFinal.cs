using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using System.Diagnostics;
using TMPro;
using Debug = UnityEngine.Debug;

public class RecordAndUploadManagerFinal : MonoBehaviour
{
    [Header("Recording Settings")]
    public Camera displayCamera;
    public int recordWidth = 1920;
    public int recordHeight = 1080;
    public int frameRate = 30;
    public int quality = 20;
    
    [Header("Upload Settings")]
    public string serverURL = "http://172.16.16.154:8080/api/videos/upload";
    public bool autoUploadAfterRecording = false; // 기본값을 false로 변경
    
    [Header("UI Elements")]
    public Button recordButton;
    public Button stopButton;
    public Button uploadButton;
    public TextMeshProUGUI statusText;
    //public Slider progressBar;
    
    [Header("Save Settings")]
    public string saveFolder = "Recordings";
    
    [Header("Scene Management")]
    public bool autoChangeSceneAfterUpload = false;
    public string nextSceneName = "VideoGalleryScene";
    
    // 상태 변수들
    private bool isRecording = false;
    private bool isUploading = false;
    private string lastRecordedVideoPath = "";
    
    private string outputPath;
    private RenderTexture renderTexture;
    private Camera recordingCamera;
    private int frameIndex = 0;
    private string framesDirectory;
    
    void Start()
    {
        SetupComponents();
        SetupUI();
        
        // 초기 상태 로그
        Debug.Log($"=== 초기 설정 ===");
        Debug.Log($"Auto Upload: {autoUploadAfterRecording}");
        Debug.Log($"Server URL: {serverURL}");
        Debug.Log($"===============");
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
        if (recordButton != null)
            recordButton.onClick.AddListener(StartRecording);
        
        if (stopButton != null)
            stopButton.onClick.AddListener(StopRecording);
        
        if (uploadButton != null)
            uploadButton.onClick.AddListener(UploadLastVideo);
        /*
        if (progressBar != null)
            progressBar.gameObject.SetActive(false);
        */
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
        
        Debug.Log($"Recordings 폴더: {recordingsDir}");
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
            Debug.LogWarning("FFmpeg.exe를 StreamingAssets 폴더에 추가해주세요!");
        }
        else
        {
            Debug.Log("FFmpeg 확인 완료");
        }
    }
    
    public void StartRecording()
    {
        if (!isRecording && !isUploading)
        {
            isRecording = true;
            frameIndex = 0;
            
            // 새로운 출력 경로 생성
            string recordingsDir = Path.Combine(Application.dataPath, saveFolder);
            outputPath = Path.Combine(recordingsDir, $"recording_{System.DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            
            Debug.Log($"녹화 시작: {outputPath}");
            
            ClearFrames();
            
            recordingCamera.targetTexture = renderTexture;
            recordingCamera.enabled = true;
            
            StartCoroutine(CaptureFrames());
            UpdateUI();
            UpdateStatus("녹화 중...");
        }
    }
    
    public void StopRecording()
    {
        if (isRecording)
        {
            isRecording = false;
            
            recordingCamera.enabled = false;
            recordingCamera.targetTexture = null;
            
            Debug.Log("녹화 중지, 비디오 생성 시작");
            
            // 자동 업로드 여부에 관계없이 비디오 먼저 생성
            StartCoroutine(CreateVideoThenDecide());
            UpdateUI();
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
    
    IEnumerator CreateVideoThenDecide()
    {
        /*
        if (progressBar != null)
            progressBar.gameObject.SetActive(true);
        */
        //progressBar.value = 0f;
        UpdateStatus("비디오 생성 중...");
        
        // 비디오 생성
        yield return StartCoroutine(CreateVideo());
        
        if (File.Exists(outputPath))
        {
            // 중요: lastRecordedVideoPath 설정
            lastRecordedVideoPath = outputPath;
            
            Debug.Log($"=== 비디오 생성 완료 ===");
            Debug.Log($"파일 경로: {lastRecordedVideoPath}");
            Debug.Log($"파일 크기: {new FileInfo(lastRecordedVideoPath).Length} bytes");
            Debug.Log($"=================");
            
            if (autoUploadAfterRecording)
            {
                // 자동 업로드
                UpdateStatus("자동 업로드 시작...");
                yield return StartCoroutine(UploadVideo(lastRecordedVideoPath));
            }
            else
            {
                // 수동 업로드 모드
                UpdateStatus("녹화 완료! Upload 버튼을 눌러 업로드하세요.");
                /*
                if (progressBar != null)
                    progressBar.gameObject.SetActive(false);
                    */
            }
        }
        else
        {
            UpdateStatus("비디오 생성 실패");
            /*
            if (progressBar != null)
                progressBar.gameObject.SetActive(false);
                */
        }
        
        // UI 업데이트 - 매우 중요!
        UpdateUI();
    }
    
    IEnumerator CreateVideo()
    {
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
        
        if (!File.Exists(ffmpegPath))
        {
            Debug.LogError("FFmpeg.exe를 찾을 수 없습니다!");
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
            Debug.Log("FFmpeg 비디오 생성 성공");
        }
        else
        {
            string error = process.StandardError.ReadToEnd();
            Debug.LogError($"FFmpeg 오류: {error}");
        }
        
        process.Dispose();
        ClearFrames();
    }
    
    public void UploadLastVideo()
    {
        Debug.Log($"=== Upload 버튼 클릭 ===");
        Debug.Log($"Last Video Path: {lastRecordedVideoPath}");
        Debug.Log($"File Exists: {!string.IsNullOrEmpty(lastRecordedVideoPath) && File.Exists(lastRecordedVideoPath)}");
        Debug.Log($"===================");
        
        if (!string.IsNullOrEmpty(lastRecordedVideoPath) && File.Exists(lastRecordedVideoPath))
        {
            StartCoroutine(UploadVideo(lastRecordedVideoPath));
        }
        else
        {
            UpdateStatus("업로드할 영상이 없습니다.");
            Debug.LogWarning("업로드할 영상이 없습니다!");
        }
    }
    
    IEnumerator UploadVideo(string videoPath)
    {
        if (isUploading)
        {
            Debug.Log("이미 업로드 중입니다.");
            yield break;
        }
        
        isUploading = true;
        UpdateUI();
        /*
        if (progressBar != null)
            progressBar.gameObject.SetActive(true);
        */
        Debug.Log($"업로드 시작: {videoPath}");
        
        // 썸네일 생성
        UpdateStatus("썸네일 생성 중...");
        string thumbnailPath = null;
        yield return StartCoroutine(GenerateThumbnailCoroutine(videoPath, result => {
            thumbnailPath = result;
        }));
        
        //progressBar.value = 0.3f;
        UpdateStatus("파일 업로드 중...");
        
        // 멀티파트 폼 데이터 생성
        WWWForm form = new WWWForm();
        
        byte[] videoData = File.ReadAllBytes(videoPath);
        form.AddBinaryData("video", videoData, Path.GetFileName(videoPath), "video/mp4");
        
        if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
        {
            byte[] thumbnailData = File.ReadAllBytes(thumbnailPath);
            form.AddBinaryData("thumbnail", thumbnailData, Path.GetFileName(thumbnailPath), "image/jpeg");
        }
        
        form.AddField("title", Path.GetFileNameWithoutExtension(videoPath));
        form.AddField("description", "Unity에서 녹화된 영상");
        form.AddField("uploadTime", System.DateTime.Now.ToString());
        
        Debug.Log($"서버로 업로드 중: {serverURL}");
        
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
                Debug.Log($"업로드 성공!");
                Debug.Log($"서버 응답: {request.downloadHandler.text}");
                UpdateStatus("업로드 완료!");
                //progressBar.value = 1f;
                
                // 업로드 성공 후 잠시 대기
                yield return new WaitForSeconds(1f);
                
                // 자동 씬 이동 (설정된 경우)
                if (autoChangeSceneAfterUpload && !string.IsNullOrEmpty(nextSceneName))
                {
                    UpdateStatus($"{nextSceneName}로 이동 중...");
                    yield return new WaitForSeconds(1f);
                    SceneManager.LoadScene(nextSceneName);
                }
            }
            else
            {
                Debug.LogError($"업로드 실패: {request.error}");
                Debug.LogError($"응답 코드: {request.responseCode}");
                if (!string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Debug.LogError($"서버 응답: {request.downloadHandler.text}");
                }
                UpdateStatus($"업로드 실패: {request.error}");
            }
        }
        
        // 임시 썸네일 삭제
        if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
            File.Delete(thumbnailPath);
        
        yield return new WaitForSeconds(2f);
        isUploading = false;
        /*
        if (progressBar != null)
            progressBar.gameObject.SetActive(false);
        */
        UpdateUI();
    }
    
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
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        
        Process process = Process.Start(startInfo);
        
        while (!process.HasExited)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        if (process.ExitCode == 0 && File.Exists(thumbnailPath))
        {
            Debug.Log($"썸네일 생성 성공: {thumbnailPath}");
            callback(thumbnailPath);
        }
        else
        {
            Debug.LogWarning("썸네일 생성 실패");
            callback(null);
        }
        
        process.Dispose();
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
        
        Debug.Log($"상태: {message}");
    }
    
    void UpdateUI()
    {
        if (recordButton != null)
            recordButton.interactable = !isRecording && !isUploading;
        
        if (stopButton != null)
            stopButton.interactable = isRecording;
        
        // Upload 버튼 활성화 조건을 명확히
        bool hasVideo = !string.IsNullOrEmpty(lastRecordedVideoPath) && File.Exists(lastRecordedVideoPath);
        bool canUpload = !isRecording && !isUploading && hasVideo;
        
        if (uploadButton != null)
        {
            uploadButton.interactable = canUpload;
        }
        
        // 디버그 로그
        Debug.Log($"=== UI 업데이트 ===");
        Debug.Log($"Upload Button Active: {uploadButton?.interactable}");
        Debug.Log($"Has Video: {hasVideo}");
        Debug.Log($"Can Upload: {canUpload}");
        Debug.Log($"Is Recording: {isRecording}");
        Debug.Log($"Is Uploading: {isUploading}");
        Debug.Log($"Last Path: {lastRecordedVideoPath}");
        Debug.Log($"================");
        
        // 상태 텍스트 업데이트
        if (!isRecording && !isUploading && statusText != null)
        {
            if (string.IsNullOrEmpty(lastRecordedVideoPath))
            {
                statusText.text = "녹화 준비";
            }
            else if (hasVideo)
            {
                statusText.text = "Upload 버튼을 눌러 업로드하세요";
            }
            else
            {
                statusText.text = "비디오 파일이 없습니다";
            }
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
    
    // Context Menu로 테스트
    [ContextMenu("Force Update UI")]
    void ForceUpdateUI()
    {
        UpdateUI();
    }
    
    [ContextMenu("Check Last Video")]
    void CheckLastVideo()
    {
        Debug.Log($"Last Video: {lastRecordedVideoPath}");
        Debug.Log($"Exists: {File.Exists(lastRecordedVideoPath)}");
    }
    
    [ContextMenu("List All Videos")]
    void ListAllVideos()
    {
        string recordingsDir = Path.Combine(Application.dataPath, saveFolder);
        if (Directory.Exists(recordingsDir))
        {
            string[] files = Directory.GetFiles(recordingsDir, "*.mp4");
            Debug.Log($"총 {files.Length}개의 비디오 파일:");
            foreach (string file in files)
            {
                Debug.Log(file);
            }
        }
    }
}
