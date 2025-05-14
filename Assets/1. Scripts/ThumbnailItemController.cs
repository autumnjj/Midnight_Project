using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using TMPro;
using System.IO;
using UnityEngine.Video;
using Debug = UnityEngine.Debug;

// 개별 썸네일 아이템을 관리하는 스크립트
public class ThumbnailItemController : MonoBehaviour
{
    [Header("UI 요소")]
    public RawImage thumbnailImage; // 썸네일 이미지
    public Button playButton; // 재생 버튼
    //public TextMeshProUGUI titleText; // 제목 텍스트 (옵션)
    
    private SimpleVideoGallery.VideoData videoData; // 이 썸네일의 비디오 정보
    private SimpleVideoGallery gallery; // 메인 갤러리 참조
    private int videoIndex; // 비디오 인덱스
    
    // 썸네일 아이템 설정 메서드
    public void Setup(SimpleVideoGallery.VideoData video, SimpleVideoGallery galleryManager, int index)
    {
        videoData = video;
        gallery = galleryManager;
        videoIndex = index;
        
        UpdateUI();
        LoadThumbnailImage();
    }
    
    void UpdateUI()
    {
        /*
        // 제목 텍스트 설정 (있다면)
        if (titleText != null && videoData != null)
        {
            // 파일명에서 확장자 제거하고 표시
            string displayTitle = System.IO.Path.GetFileNameWithoutExtension(videoData.filename);
            titleText.text = displayTitle;
        }
        */
        // 재생 버튼 이벤트 설정
        if (playButton != null)
        {
            // 기존 리스너 제거 후 새로 추가
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayButtonClicked);
        }
    }
    
    void OnPlayButtonClicked()
    {
        if (gallery != null && videoData != null)
        {
            Debug.Log($"재생 버튼 클릭: {videoData.filename}");
            gallery.PlayVideo(videoData, videoIndex);
        }
    }
    
    void LoadThumbnailImage()
    {
        if (gallery == null || thumbnailImage == null || videoData == null)
            return;
            
        StartCoroutine(LoadThumbnailImageCoroutine());
    }
    
    IEnumerator LoadThumbnailImageCoroutine()
{
    if (gallery == null || thumbnailImage == null || videoData == null)
        yield break;
    
    Debug.Log($"=== 썸네일 로드 시작: {videoData.filename} ===");
    
    // 1단계: SimpleVideoGallery의 올바른 엔드포인트 사용
    string baseFilename = Path.GetFileNameWithoutExtension(videoData.filename);
    string thumbnailFilename = baseFilename + "_thumbnail.jpg";
    
    string thumbnailURL = gallery.GetServerURL($"/api/videos/thumbnails/download/{thumbnailFilename}");
    Debug.Log($"1단계 - 기본 썸네일 시도: {thumbnailURL}");
    
    // 방법 1: UnityWebRequestTexture.GetTexture 대신 일반 GET 시도
    using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(thumbnailURL))
    {
        request.timeout = 30;
        
        // CORS 및 접근 권한을 위한 헤더 추가
        request.SetRequestHeader("Access-Control-Allow-Origin", "*");
        request.SetRequestHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        request.SetRequestHeader("Access-Control-Allow-Headers", "Content-Type");
        
        yield return request.SendWebRequest();
        
        Debug.Log($"1단계 응답 코드: {request.responseCode}");
        Debug.Log($"1단계 응답 결과: {request.result}");
        Debug.Log($"1단계 에러: {request.error}");
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D thumbnail = ((DownloadHandlerTexture)request.downloadHandler).texture;
            if (thumbnailImage != null)
            {
                thumbnailImage.texture = thumbnail;
                Debug.Log($"✅ 썸네일 로드 성공 (기본): {videoData.filename}");
                yield break;
            }
        }
        else
        {
            Debug.LogWarning($"1단계 실패: {request.error}");
        }
    }
    
    // 방법 2: 일반 GET 요청으로 다시 시도
    Debug.Log("일반 GET 요청으로 재시도");
    using (UnityWebRequest request = UnityWebRequest.Get(thumbnailURL))
    {
        request.timeout = 30;
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            // 수동으로 텍스처 생성
            byte[] imageData = request.downloadHandler.data;
            Texture2D thumbnail = new Texture2D(1, 1);
            
            if (thumbnail.LoadImage(imageData))
            {
                if (thumbnailImage != null)
                {
                    thumbnailImage.texture = thumbnail;
                    Debug.Log($"✅ 썸네일 로드 성공 (일반 GET): {videoData.filename}");
                    yield break;
                }
            }
        }
        else
        {
            Debug.LogWarning($"일반 GET도 실패: {request.error}");
        }
    }
    
    // 2단계: 원본 파일명으로 시도
    thumbnailURL = gallery.GetServerURL($"/api/videos/thumbnails/download/{videoData.filename}");
    Debug.Log($"2단계 - 원본 파일명 시도: {thumbnailURL}");
    
    using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(thumbnailURL))
    {
        request.timeout = 30;
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D thumbnail = ((DownloadHandlerTexture)request.downloadHandler).texture;
            if (thumbnailImage != null)
            {
                thumbnailImage.texture = thumbnail;
                Debug.Log($"✅ 썸네일 로드 성공 (원본명): {videoData.filename}");
                yield break;
            }
        }
        else
        {
            Debug.LogWarning($"2단계 실패: {request.error}");
        }
    }
    
    // 3단계: 다른 확장자들로 시도
    string[] possibleExtensions = { ".jpg", ".jpeg", ".png" };
    
    foreach (string ext in possibleExtensions)
    {
        string possibleFilename = baseFilename + ext;
        thumbnailURL = gallery.GetServerURL($"/api/videos/thumbnails/download/{possibleFilename}");
        Debug.Log($"3단계 - 확장자 시도 ({ext}): {thumbnailURL}");
        
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(thumbnailURL))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D thumbnail = ((DownloadHandlerTexture)request.downloadHandler).texture;
                if (thumbnailImage != null)
                {
                    thumbnailImage.texture = thumbnail;
                    Debug.Log($"✅ 썸네일 로드 성공 ({ext}): {videoData.filename}");
                    yield break;
                }
            }
        }
    }
    
    // 4단계: _thumbnail suffix 시도
    string[] thumbnailSuffixes = { "_thumbnail.jpg", "_thumbnail.jpeg", "_thumbnail.png" };
    
    foreach (string suffix in thumbnailSuffixes)
    {
        string possibleFilename = baseFilename + suffix;
        thumbnailURL = gallery.GetServerURL($"/api/videos/thumbnails/download/{possibleFilename}");
        Debug.Log($"4단계 - suffix 시도 ({suffix}): {thumbnailURL}");
        
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(thumbnailURL))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D thumbnail = ((DownloadHandlerTexture)request.downloadHandler).texture;
                if (thumbnailImage != null)
                {
                    thumbnailImage.texture = thumbnail;
                    Debug.Log($"✅ 썸네일 로드 성공 ({suffix}): {videoData.filename}");
                    yield break;
                }
            }
        }
    }
    
    // 모든 시도 실패시 로컬 썸네일 찾기
    Debug.Log("서버 썸네일 없음, 로컬 썸네일 확인");
    yield return StartCoroutine(LoadLocalThumbnail(Path.Combine(Application.temporaryCachePath, "Recordings", thumbnailFilename)));
    
    // 그래도 없으면 기본 썸네일
    if (thumbnailImage != null && thumbnailImage.texture == null)
    {
        Debug.Log($"모든 시도 실패, 기본 썸네일 적용: {videoData.filename}");
        SetDefaultThumbnail();
    }
}
    
    // 로컬 썸네일 파일 로드
    IEnumerator LoadLocalThumbnail(string thumbnailPath)
    {
        byte[] imageData = File.ReadAllBytes(thumbnailPath);
        Texture2D thumbnail = new Texture2D(1, 1);
        
        if (thumbnail.LoadImage(imageData))
        {
            if (thumbnailImage != null)
            {
                thumbnailImage.texture = thumbnail;
                Debug.Log($"로컬 썸네일 로드 성공: {Path.GetFileName(thumbnailPath)}");
            }
        }
        else
        {
            Debug.LogError($"로컬 썸네일 로드 실패: {thumbnailPath}");
            SetDefaultThumbnail();
        }
        
        yield return null;
    }
    
    // 동적 썸네일 생성 (비디오 첫 프레임 캡처)
    IEnumerator GenerateLocalThumbnail()
    {
        // 임시 VideoPlayer로 썸네일 생성
        GameObject tempPlayerGO = new GameObject("TempVideoPlayer");
        VideoPlayer tempPlayer = tempPlayerGO.AddComponent<VideoPlayer>();
        RenderTexture tempRT = new RenderTexture(320, 180, 16);
        
        tempPlayer.renderMode = VideoRenderMode.RenderTexture;
        tempPlayer.targetTexture = tempRT;
        tempPlayer.playOnAwake = false;
        tempPlayer.isLooping = false;
        
        // 비디오 파일 경로 설정 (로컬 파일이 있는 경우만)
        string videoPath = GetVideoFilePath();
        if (!string.IsNullOrEmpty(videoPath))
        {
            tempPlayer.url = "file://" + videoPath;
            tempPlayer.Prepare();
            
            // 준비 완료까지 대기
            while (!tempPlayer.isPrepared)
            {
                yield return null;
            }
            
            // 첫 프레임으로 이동
            tempPlayer.time = 1.0; // 1초 지점
            tempPlayer.Play();
            yield return new WaitForSeconds(0.1f);
            tempPlayer.Pause();
            
            // RenderTexture에서 Texture2D로 변환
            RenderTexture.active = tempRT;
            Texture2D thumbnail = new Texture2D(tempRT.width, tempRT.height, TextureFormat.RGB24, false);
            thumbnail.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
            thumbnail.Apply();
            RenderTexture.active = null;
            
            // 썸네일 적용
            if (thumbnailImage != null)
            {
                thumbnailImage.texture = thumbnail;
                Debug.Log($"동적 썸네일 생성 성공: {videoData.filename}");
            }
            
            // 썸네일 파일로 저장 (다음에 재사용)
            SaveThumbnailToFile(thumbnail, videoPath);
        }
        else
        {
            Debug.LogError($"비디오 파일을 찾을 수 없음: {videoData.filename}");
            SetDefaultThumbnail();
        }
        
        // 정리
        tempRT.Release();
        DestroyImmediate(tempPlayerGO);
    }
    
    // 비디오 파일 경로 찾기 (로컬 파일용)
    string GetVideoFilePath()
    {
        // 임시 폴더 확인
        string tempPath = Path.Combine(Application.temporaryCachePath, "Recordings", videoData.filename);
        if (File.Exists(tempPath))
            return tempPath;
        
        // 원본 폴더 확인
        string assetPath = Path.Combine(Application.dataPath, "Recordings", videoData.filename);
        if (File.Exists(assetPath))
            return assetPath;
        
        return null;
    }
    
    // 썸네일을 파일로 저장
    void SaveThumbnailToFile(Texture2D thumbnail, string videoPath)
    {
        try
        {
            string baseFileName = Path.GetFileNameWithoutExtension(videoPath);
            // 여기서 확장자를 .mp4로 변경해야 함
            string thumbnailPath = Path.Combine(Path.GetDirectoryName(videoPath), baseFileName + "_thumbnail.jpg");
        
            byte[] thumbnailData = thumbnail.EncodeToJPG(80);  // JPG 대신 다른 형식으로?
            File.WriteAllBytes(thumbnailPath, thumbnailData);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"썸네일 저장 실패: {e.Message}");
        }
    }
    
    void SetDefaultThumbnail()
    {
        if (thumbnailImage == null) return;
        
        // 회색 기본 이미지 생성
        Texture2D defaultTexture = new Texture2D(1, 1);
        defaultTexture.SetPixel(0, 0, Color.gray);
        defaultTexture.Apply();
        
        thumbnailImage.texture = defaultTexture;
        Debug.Log($"기본 썸네일 적용: {videoData?.filename}");
    }
    
    void OnDestroy()
    {
        // 메모리 누수 방지를 위해 동적으로 생성된 텍스처 해제
        if (thumbnailImage != null && thumbnailImage.texture != null)
        {
            // 기본 텍스처가 아닌 경우에만 해제
            if (thumbnailImage.texture.width > 1 || thumbnailImage.texture.height > 1)
            {
                DestroyImmediate(thumbnailImage.texture);
            }
        }
    }
}