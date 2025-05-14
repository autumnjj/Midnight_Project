using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Diagnostics;
using TMPro;
using Debug = UnityEngine.Debug;

// 백엔드 API에 맞춘 RecordUploadManager (단순화된 버전)
public class BackendRecordUploadManager : MonoBehaviour
{
    [Header("Recording Settings")]
    public Camera displayCamera;
    public int recordWidth = 1920;
    public int recordHeight = 1080;
    public int frameRate = 30;
    public int quality = 20;
    
    [Header("Upload Settings")]
    [Tooltip("Base server URL: http://172.16.16.154:8080")]
    public string baseServerURL = "http://172.16.16.154:8080";
    
    [Header("UI Elements")]
    public Button recordButton;
    public Button stopButton;
    public Button testConnectionButton;
    public TextMeshProUGUI statusText;
    
    [Header("Save Settings")]
    public string saveFolder = "Recordings";
    
    // 상태 변수들
    private bool isRecording = false;
    private bool isProcessing = false;
    
    private string outputPath;
    private RenderTexture renderTexture;
    private Camera recordingCamera;
    private int frameIndex = 0;
    private string framesDirectory;
    
    void Start()
    {
        // 초기 상태 설정
        isRecording = false;
        isProcessing = false;
        
        SetupComponents();
        SetupUI();
        
        Debug.Log("=== 백엔드 API 연동 녹화/업로드 시스템 ===");
        Debug.Log($"Base Server URL: {baseServerURL}");
        Debug.Log($"Upload URL: {baseServerURL}{VideoUploadAPI.UPLOAD_ENDPOINT}");
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
            stopButton.onClick.AddListener(StopAndUpload);
        
        if (testConnectionButton != null)
        {
            testConnectionButton.onClick.AddListener(TestServerConnection);
            Debug.Log("Test Connection 버튼 설정 완료");
        }
        else
        {
            Debug.LogError("Test Connection Button이 null입니다!");
        }
        
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
        if (!isRecording && !isProcessing)
        {
            isRecording = true;
            frameIndex = 0;
            
            string recordingsDir = Path.Combine(Application.dataPath, saveFolder);
            outputPath = Path.Combine(recordingsDir, $"recording_{System.DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            
            Debug.Log($"🎬 녹화 시작: {outputPath}");
            
            ClearFrames();
            
            recordingCamera.targetTexture = renderTexture;
            recordingCamera.enabled = true;
            
            StartCoroutine(CaptureFrames());
            UpdateUI();
            UpdateStatus("녹화 중...");
        }
    }
    
    public void StopAndUpload()
    {
        if (isRecording)
        {
            Debug.Log("=== 백엔드 API 업로드 시작 ===");
            
            isRecording = false;
            isProcessing = true;

            recordingCamera.enabled = false;
            recordingCamera.targetTexture = null;

            Debug.Log("녹화 중지, 백엔드 API 저장 및 업로드 시작");
            
            StartCoroutine(SaveAndUploadWithBackendAPI());
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
    
    // 백엔드 API를 사용한 저장 및 업로드
    IEnumerator SaveAndUploadWithBackendAPI()
    {
        // 1. 비디오 저장
        UpdateStatus("비디오 저장 중...");
        
        bool videoSaveSuccess = false;
        yield return StartCoroutine(CreateVideo((result) => {
            videoSaveSuccess = result;
        }));

        if (!videoSaveSuccess || !File.Exists(outputPath))
        {
            Debug.LogError("❌ 비디오 저장 실패");
            UpdateStatus("비디오 저장 실패");
            isProcessing = false;
            UpdateUI();
            yield break;
        }
        
        Debug.Log($"✅ 비디오 저장 성공: {outputPath}");
        FileInfo videoFile = new FileInfo(outputPath);
        Debug.Log($"✅ 비디오 파일 크기: {videoFile.Length / 1024.0 / 1024.0:F2} MB");
        
        // 2. 썸네일 생성
        UpdateStatus("썸네일 생성 중...");
        
        string thumbnailPath = null;
        yield return StartCoroutine(GenerateThumbnailCoroutine(outputPath, result => {
            thumbnailPath = result;
        }));
        
        // 3. 백엔드 API를 사용한 업로드
        bool uploadSuccess = false;
        yield return StartCoroutine(UploadVideoWithThumbnail(thumbnailPath, success => {
            uploadSuccess = success;
        }));
        
        // 4. 결과 정리
        if (uploadSuccess)
        {
            UpdateStatus("✅ 업로드 완료!");
            Debug.Log("✅ 백엔드 API 업로드 성공");
        }
        else
        {
            UpdateStatus("❌ 업로드 실패");
            Debug.LogError("❌ 백엔드 API 업로드 실패");
        }
        
        // 5. 정리
        if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
            File.Delete(thumbnailPath);
        
        yield return new WaitForSeconds(2f);
        UpdateStatus("녹화 준비");
        
        isProcessing = false;
        UpdateUI();
    }
    
    // 비디오와 썸네일을 함께 업로드
    IEnumerator UploadVideoWithThumbnail(string thumbnailPath, System.Action<bool> callback)
    {
        UpdateStatus("비디오 + 썸네일 업로드 중...");
        Debug.Log("=== 백엔드 API 업로드 시작 ===");
        
        // 파일 검증
        if (!File.Exists(outputPath))
        {
            Debug.LogError($"업로드할 비디오 파일 없음: {outputPath}");
            callback(false);
            yield break;
        }
        
        // 파일 읽기
        byte[] videoData = File.ReadAllBytes(outputPath);
        string videoFilename = Path.GetFileName(outputPath);
        
        byte[] thumbnailData = null;
        string thumbnailFilename = "";
        
        if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
        {
            thumbnailData = File.ReadAllBytes(thumbnailPath);
            thumbnailFilename = Path.GetFileName(thumbnailPath);
            Debug.Log($"썸네일 포함 업로드: {thumbnailFilename}");
        }
        else
        {
            Debug.LogWarning("썸네일이 없어서 비디오만 업로드합니다.");
        }
        
        Debug.Log($"업로드할 파일: {videoFilename} ({videoData.Length / 1024.0 / 1024.0:F2} MB)");
        
        // 백엔드 API 웹 요청 생성
        UnityWebRequest webRequest;
        
        if (thumbnailData != null)
        {
            // 비디오 + 썸네일 동시 업로드
            webRequest = VideoUploadAPI.CreateVideoWithThumbnailUploadRequest(
                videoData, videoFilename, thumbnailData, thumbnailFilename, baseServerURL);
        }
        else
        {
            // 비디오만 업로드
            webRequest = VideoUploadAPI.CreateVideoUploadRequest(videoData, videoFilename, baseServerURL);
        }
        
        Debug.Log($"업로드 URL: {webRequest.uri}");
        
        // 진행률 모니터링과 함께 업로드 실행
        var operation = webRequest.SendWebRequest();
        float lastProgress = 0f;
        
        while (!operation.isDone)
        {
            float progress = webRequest.uploadProgress * 100f;
            if (progress - lastProgress > 5f) // 5%마다 로그
            {
                Debug.Log($"업로드 진행률: {progress:F1}% ({webRequest.uploadedBytes / 1024.0 / 1024.0:F2} MB)");
                UpdateStatus($"업로드 중... {progress:F1}%");
                lastProgress = progress;
            }
            yield return null;
        }
        
        // 결과 처리
        Debug.Log("=== 백엔드 API 업로드 결과 ===");
        Debug.Log($"결과: {webRequest.result}");
        Debug.Log($"상태 코드: {webRequest.responseCode}");
        Debug.Log($"업로드된 바이트: {webRequest.uploadedBytes / 1024.0 / 1024.0:F2} MB");
        Debug.Log($"서버 응답: {webRequest.downloadHandler.text}");
        
        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("✅ 백엔드 API 업로드 성공!");
            
            // 결과 파싱 시도
            try
            {
                var result = VideoUploadAPI.GetResultFromJson<VideoUploadAPI.UploadResult>(webRequest);
                if (result != null && result.data != null)
                {
                    Debug.Log($"서버에 저장된 파일명: {result.data.filename}");
                    Debug.Log($"서버에서 확인한 파일 크기: {result.data.filesize / 1024.0 / 1024.0:F2} MB");
                    if (!string.IsNullOrEmpty(result.data.thumbnailFilename))
                    {
                        Debug.Log($"썸네일 파일명: {result.data.thumbnailFilename}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"응답 파싱 실패 (업로드는 성공): {e.Message}");
            }
            
            callback(true);
        }
        else
        {
            Debug.LogError($"❌ 백엔드 API 업로드 실패: {webRequest.error}");
            Debug.LogError($"응답 코드: {webRequest.responseCode}");
            
            // 상세 에러 분석
            switch (webRequest.responseCode)
            {
                case 400:
                    Debug.LogError("400 Bad Request - 요청 형식이 잘못되었습니다.");
                    Debug.LogError("비디오는 'video' 필드, 썸네일은 'thumbnail' 필드를 사용하는지 확인하세요.");
                    break;
                case 413:
                    Debug.LogError("413 Payload Too Large - 파일이 너무 큽니다.");
                    break;
                case 415:
                    Debug.LogError("415 Unsupported Media Type - 지원하지 않는 파일 형식입니다.");
                    break;
                case 500:
                    Debug.LogError("500 Internal Server Error - 서버 내부 오류입니다.");
                    break;
                case 0:
                    Debug.LogError("네트워크 연결 실패 - 서버 URL과 네트워크를 확인하세요.");
                    break;
            }
            
            callback(false);
        }
        
        webRequest.Dispose();
    }
    
    // 서버 연결 테스트
    public void TestServerConnection()
    {
        StartCoroutine(TestServerConnectionCoroutine());
    }
    
    IEnumerator TestServerConnectionCoroutine()
    {
        UpdateStatus("서버 연결 테스트 중...");
        Debug.Log("=== 백엔드 API 연결 테스트 시작 ===");
        
        // 1. 기본 서버 테스트
        string testUrl = baseServerURL;
        using (UnityWebRequest request = UnityWebRequest.Get(testUrl))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            Debug.Log($"기본 서버 테스트: {testUrl}");
            Debug.Log($"결과: {request.result} - 응답 코드: {request.responseCode}");
        }
        
        // 2. 비디오 목록 조회 테스트
        using var listRequest = VideoUploadAPI.CreateVideoListRequest(baseServerURL);
        yield return listRequest.SendWebRequest();
        
        Debug.Log($"비디오 목록 조회 테스트: {listRequest.uri}");
        Debug.Log($"결과: {listRequest.result} - 응답 코드: {listRequest.responseCode}");
        if (listRequest.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"비디오 목록: {listRequest.downloadHandler.text}");
        }
        
        // 3. 썸네일 목록 조회 테스트
        using var thumbnailListRequest = VideoUploadAPI.CreateThumbnailListRequest(baseServerURL);
        yield return thumbnailListRequest.SendWebRequest();
        
        Debug.Log($"썸네일 목록 조회 테스트: {thumbnailListRequest.uri}");
        Debug.Log($"결과: {thumbnailListRequest.result} - 응답 코드: {thumbnailListRequest.responseCode}");
        if (thumbnailListRequest.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"썸네일 목록: {thumbnailListRequest.downloadHandler.text}");
        }
        
        UpdateStatus("서버 연결 테스트 완료");
        Debug.Log("=== 백엔드 API 연결 테스트 완료 ===");
    }
    
    IEnumerator CreateVideo(System.Action<bool> onComplete = null)
    {
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");

        if (!File.Exists(ffmpegPath))
        {
            Debug.LogError("FFmpeg.exe를 찾을 수 없습니다!");
            onComplete?.Invoke(false);
            yield break;
        }

        Debug.Log("🎬 ffmpeg로 비디오 생성 시작");

        string inputPattern = Path.Combine(framesDirectory, "frame_%06d.png");
        string arguments =
            $"-r {frameRate} -i \"{inputPattern}\" -vcodec libx264 -crf {quality} -pix_fmt yuv420p \"{outputPath}\"";

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

        bool success = false;
        if (process.ExitCode == 0)
        {
            yield return new WaitForSeconds(2f);
            
            if (File.Exists(outputPath))
            {
                FileInfo fileInfo = new FileInfo(outputPath);
                if (fileInfo.Length > 0)
                {
                    Debug.Log($"✅ 비디오 생성 성공: {fileInfo.Length / 1024.0 / 1024.0:F2} MB");
                    success = true;
                }
            }
        }
        else
        {
            string error = process.StandardError.ReadToEnd();
            Debug.LogError($"ffmpeg 오류: {error}");
        }

        process.Dispose();
        ClearFrames();
        onComplete?.Invoke(success);
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
            Debug.Log($"썸네일 생성 성공: {Path.GetFileName(thumbnailPath)}");
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
        
        Debug.Log($"[상태] {message}");
    }
    
    void UpdateUI()
    {
        if (recordButton != null)
            recordButton.interactable = !isRecording && !isProcessing;
    
        if (stopButton != null)
            stopButton.interactable = isRecording;
        
        // Test 버튼을 항상 활성화 (디버깅을 위해)
        if (testConnectionButton != null)
            testConnectionButton.interactable = true;
    
        if (!isRecording && !isProcessing && statusText != null)
        {
            statusText.text = "녹화 준비";
        }
    }
    
    void OnDestroy()
    {
        if (isRecording)
            StopAndUpload();
        
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