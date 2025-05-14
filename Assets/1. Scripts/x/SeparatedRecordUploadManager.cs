using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using System.Diagnostics;
using TMPro;
using Debug = UnityEngine.Debug;

// Stop = 저장만, Upload = 업로드 + 다음 씬 이동 (ProgressBar 제거)
public class SeparatedRecordUploadManager : MonoBehaviour
{
    [Header("Recording Settings")]
    public Camera displayCamera;
    public int recordWidth = 1920;
    public int recordHeight = 1080;
    public int frameRate = 30;
    public int quality = 20;
    
    [Header("Upload Settings")]
    public string serverURL = "http://172.16.16.154:8080/api/videos/upload";
    
    [Header("UI Elements")]
    public Button recordButton;
    public Button stopButton;
    public Button uploadButton;
    public TextMeshProUGUI statusText;
    
    [Header("Save Settings")]
    public string saveFolder = "Recordings";
    
    [Header("Scene Management")]
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
        
        Debug.Log("=== 분리된 녹화/업로드 시스템 ===");
        Debug.Log("Stop = 저장만, Upload = 업로드 + 씬 이동");
        Debug.Log($"Server URL: {serverURL}");
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
            uploadButton.onClick.AddListener(UploadAndChangeScene);
        
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
            
            Debug.Log($"🎬 녹화 시작: {outputPath}");
            
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
            Debug.Log("=== STOP 버튼 클릭 ===");
            isRecording = false;

            recordingCamera.enabled = false;
            recordingCamera.targetTexture = null;

            Debug.Log("녹화 중지, 비디오 저장 시작");
            Debug.Log($"현재 outputPath: {outputPath}");
        
            // 즉시 UI 업데이트해서 Record 버튼은 활성화
            UpdateUI();
        
            StartCoroutine(SaveVideoOnly());
        }
    }

    
    public void UploadAndChangeScene()
    {
        if (!string.IsNullOrEmpty(lastRecordedVideoPath) && File.Exists(lastRecordedVideoPath))
        {
            Debug.Log("업로드 시작 후 씬 이동 예정");
            StartCoroutine(UploadVideoAndChangeScene(lastRecordedVideoPath));
        }
        else
        {
            UpdateStatus("업로드할 영상이 없습니다.");
            Debug.LogWarning("업로드할 영상이 없습니다!");
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
    
    // Stop 버튼용: 저장만 수행
    IEnumerator SaveVideoOnly()
    {
        Debug.Log("=== SaveVideoOnly 시작 ===");
        UpdateStatus("비디오 저장 중...");
    
        bool success = false;
        Debug.Log($"CreateVideo 호출 전 - outputPath: {outputPath}");
    
        yield return StartCoroutine(CreateVideo((result) => {
            success = result;
            Debug.Log($"CreateVideo 콜백 - success: {result}");
        }));
    
        Debug.Log($"CreateVideo 완료 - success: {success}");
        Debug.Log($"outputPath 확인: {outputPath}");
        Debug.Log($"File.Exists(outputPath): {File.Exists(outputPath)}");
    
        if (success && File.Exists(outputPath))
        {
            lastRecordedVideoPath = outputPath;
            Debug.Log($"✅ lastRecordedVideoPath 설정됨: {lastRecordedVideoPath}");
            UpdateStatus("비디오 저장 완료! Upload 버튼이 활성화되었습니다.");
        }
        else
        {
            Debug.LogError($"❌ 비디오 저장 실패 - success: {success}, File.Exists: {File.Exists(outputPath)}");
        
            // outputPath가 비어있는지 확인
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.LogError("outputPath가 비어있습니다!");
            }
        
            UpdateStatus("비디오 저장 실패");
        }
    
        Debug.Log("=== SaveVideoOnly 끝, UI 업데이트 호출 ===");
        UpdateUI();
    }


// 추가로 Context Menu 디버깅 메서드:
    [ContextMenu("Force Check Upload Button")]
    void ForceCheckUploadButton()
    {
        Debug.Log("=== 수동 Upload 버튼 체크 ===");
        UpdateUI();
    
        // Inspector에서 Upload 버튼 상태 확인
        if (uploadButton != null)
        {
            Debug.Log($"Upload Button GameObject: {uploadButton.gameObject.name}");
            Debug.Log($"Upload Button Active: {uploadButton.gameObject.activeInHierarchy}");
            Debug.Log($"Upload Button Enabled: {uploadButton.enabled}");
            Debug.Log($"Upload Button Interactable: {uploadButton.interactable}");
        }
    }
    
    
    // Upload 버튼용: 업로드 + 씬 이동
    IEnumerator UploadVideoAndChangeScene(string videoPath)
    {
        if (isUploading) yield break;
        
        isUploading = true;
        UpdateUI();
        
        Debug.Log($"업로드 시작: {videoPath}");
        
        // 썸네일 생성
        UpdateStatus("썸네일 생성 중...");
        string thumbnailPath = null;
        yield return StartCoroutine(GenerateThumbnailCoroutine(videoPath, result => {
            thumbnailPath = result;
        }));
        
        UpdateStatus("서버에 업로드 중...");
        
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
                float progress = request.uploadProgress * 100f;
                UpdateStatus($"업로드 중... {progress:F1}%");
                yield return null;
            }
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 업로드 성공!");
                Debug.Log($"서버 응답: {request.downloadHandler.text}");
                UpdateStatus("업로드 완료! 다음 씬으로 이동합니다...");
                
                // 잠시 대기 후 씬 이동
                yield return new WaitForSeconds(1.5f);
                
                Debug.Log($"🎬 {nextSceneName}로 이동합니다...");
                SceneManager.LoadScene(nextSceneName);
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
                
                yield return new WaitForSeconds(2f);
                UpdateStatus("Upload 버튼을 다시 눌러 재시도하세요.");
            }
        }
        
        // 임시 썸네일 삭제
        if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
            File.Delete(thumbnailPath);
        
        isUploading = false;
        UpdateUI();
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

        Debug.Log("🔍 ffmpeg 실행 시도");

        string inputPattern = Path.Combine(framesDirectory, "frame_%06d.png");
        string arguments =
            $"-r {frameRate} -i \"{inputPattern}\" -vcodec libx264 -crf {quality} -pix_fmt yuv420p \"{outputPath}\"";

        Debug.Log($"🔍 ffmpeg 실행 명령어: ffmpeg {arguments}");

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
    
        // 수정된 부분: 더 명확한 체크
        Debug.Log($"🔍 ffmpeg 종료 코드: {process.ExitCode}");
    
        if (process.ExitCode == 0)
        {
            Debug.Log("🔍 ffmpeg 비디오 생성 성공");
        
            // 잠시 대기 후 파일 존재 확인 (파일 생성 완료 대기)
            yield return new WaitForSeconds(0.5f);
        
            if (File.Exists(outputPath))
            {
                var fileInfo = new FileInfo(outputPath);
                Debug.Log($"✅ 비디오 파일 생성됨: {outputPath}");
                Debug.Log($"✅ 파일 크기: {fileInfo.Length} bytes");
                success = true;
            }
            else
            {
                Debug.LogError($"❌ ffmpeg 성공하였지만 파일이 없음: {outputPath}");
            }
        }
        else
        {
            string error = process.StandardError.ReadToEnd();
            Debug.LogError($"❌ ffmpeg 오류 발생 (Exit Code: {process.ExitCode}): {error}");
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
            Debug.Log("=== UpdateUI 호출 ===");
            Debug.Log($"isRecording: {isRecording}");
            Debug.Log($"isUploading: {isUploading}");
            Debug.Log($"lastRecordedVideoPath: '{lastRecordedVideoPath}'");
    
            // Record 버튼
            if (recordButton != null)
            {
                bool recordActive = !isRecording && !isUploading;
                recordButton.interactable = recordActive;
                Debug.Log($"Record 버튼 interactable: {recordActive}");
            }

            // Stop 버튼
            if (stopButton != null)
            {
                stopButton.interactable = isRecording;
                Debug.Log($"Stop 버튼 interactable: {isRecording}");
            }

            // Upload 버튼 - 여기가 핵심!
            bool hasVideo = !string.IsNullOrEmpty(lastRecordedVideoPath) && File.Exists(lastRecordedVideoPath);
            bool canUpload = !isRecording && !isUploading && hasVideo;
    
            Debug.Log("=== Upload 버튼 디버깅 ===");
            Debug.Log($"lastRecordedVideoPath 비어있는지: {string.IsNullOrEmpty(lastRecordedVideoPath)}");
            Debug.Log($"파일 존재하는지: {File.Exists(lastRecordedVideoPath)}");
            Debug.Log($"hasVideo: {hasVideo}");
            Debug.Log($"canUpload: {canUpload}");
    
            if (uploadButton != null)
            {
                uploadButton.interactable = canUpload;
                Debug.Log($"Upload 버튼 interactable 설정됨: {canUpload}");
        
                // 추가 확인: 버튼 컴포넌트 자체 상태
                Debug.Log($"Upload 버튼 GameObject active: {uploadButton.gameObject.activeInHierarchy}");
                Debug.Log($"Upload 버튼 enabled: {uploadButton.enabled}");
            }
            else
            {
                Debug.LogError("uploadButton이 null입니다!");
            }

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
    
            Debug.Log("=== UpdateUI 완료 ===");
        
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
