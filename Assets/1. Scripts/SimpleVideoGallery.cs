using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Debug = UnityEngine.Debug;

// ThumbnailItemController 없이도 작동하는 SimpleVideoGallery
public class SimpleVideoGallery : MonoBehaviour
{
    [Header("서버 설정")]
    [Tooltip("예: http://172.16.16.154:3000")]
    public string baseServerURL = "http://172.16.16.154:8080";
    
    [Header("UI 요소")]
    public Transform thumbnailParent; // 썸네일이 들어갈 ScrollView Content
    public GameObject thumbnailItemPrefab; // 썸네일 아이템 프리팹 (이미지 + 버튼)
    public VideoPlayer videoPlayer; // 비디오 플레이어
    public RawImage videoScreen; // 비디오 화면
    public Button backButton; // 갤러리로 돌아가기 버튼
    
    [Header("설정")]
    public int thumbnailsPerRow = 3; // 한 줄에 몇 개씩
    public float thumbnailWidth = 500f; // 1920x1080에 최적화된 크기
    public float thumbnailHeight = 280f; // 16:9 비율 (500 * 0.56)
    public float spacing = 20f; // 썸네일 간격
    
    // 비디오 정보
    [System.Serializable]
    public class VideoData
    {
        public string filename;
        public string title;
    }
    
    // 배열 형태 JSON을 위한 래퍼 클래스
    [System.Serializable]
    public class FileListWrapper
    {
        public string[] files;
    }
    
    private List<VideoData> videos = new List<VideoData>();
    private bool isPlaying = false;
    
    // 서버 엔드포인트 통합 (public으로 변경)
    public string GetServerURL(string endpoint)
    {
        print( $"{baseServerURL}{endpoint}");
        return $"{baseServerURL}{endpoint}";
    }
    
    void Start()
    {
        Debug.Log("=== SimpleVideoGallery 시작 ===");
        Debug.Log($"서버 URL: {baseServerURL}");
        
        SetupVideoPlayer();
        SetupUI();
        
        // 초기화 후 파일 목록 로드
        LoadVideoList();
    }
    
    void SetupVideoPlayer()
    {
        // VideoPlayer 설정
        if (videoPlayer != null && videoScreen != null)
        {
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            videoPlayer.targetTexture = rt;
            videoScreen.texture = rt;
            
            // 처음에는 비디오 화면 숨기기
            videoScreen.gameObject.SetActive(false);
            Debug.Log("VideoPlayer 설정 완료");
        }
        else
        {
            Debug.LogError("VideoPlayer 또는 VideoScreen이 설정되지 않았습니다!");
        }
    }
    
    void SetupUI()
    {
        // Back 버튼 설정
        if (backButton != null)
        {
            backButton.onClick.AddListener(BackToGallery);
            backButton.gameObject.SetActive(false); // 처음에는 숨기기
        }
        
        // Grid Layout 설정
        if (thumbnailParent != null)
        {
            SetupGridLayout();
        }
        else
        {
            Debug.LogError("thumbnailParent가 설정되지 않았습니다!");
        }
    }
    
    void SetupGridLayout()
    {
        if (thumbnailParent.GetComponent<GridLayoutGroup>() == null)
        {
            GridLayoutGroup grid = thumbnailParent.gameObject.AddComponent<GridLayoutGroup>();
            
            float itemHeight = thumbnailHeight + 80; // 버튼과 제목 공간 추가
            
            grid.cellSize = new Vector2(thumbnailWidth, itemHeight);
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraintCount = thumbnailsPerRow;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.childAlignment = TextAnchor.UpperCenter;
            
            Debug.Log($"Grid Layout 설정 - 셀 크기: {grid.cellSize}");
        }
    }
    
    public void LoadVideoList()
    {
        Debug.Log("=== 비디오 목록 로드 시작 ===");
        StartCoroutine(LoadVideoListCoroutine());
    }
    
    IEnumerator LoadVideoListCoroutine()
    {
        string listURL = GetServerURL("/api/videos/list");
        Debug.Log($"요청 URL: {listURL}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(listURL))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();
            
            Debug.Log($"=== 서버 응답 ===");
            Debug.Log($"결과: {request.result}");
            Debug.Log($"응답 코드: {request.responseCode}");
            Debug.Log($"응답 내용: {request.downloadHandler.text}");
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                
                if (string.IsNullOrEmpty(jsonResponse))
                {
                    Debug.LogWarning("서버 응답이 비어있습니다.");
                    yield break;
                }
                
                // 서버 응답 파싱 (배열 형태)
                List<string> filenames = ParseFilenameArray(jsonResponse);
                
                if (filenames != null && filenames.Count > 0)
                {
                    Debug.Log($"파싱된 파일 개수: {filenames.Count}");
                    
                    // VideoData 리스트 생성
                    videos.Clear();
                    foreach (string filename in filenames)
                    {
                        VideoData videoData = new VideoData
                        {
                            filename = filename,
                            title = Path.GetFileNameWithoutExtension(filename)
                        };
                        videos.Add(videoData);
                        Debug.Log($"추가된 비디오: {filename}");
                    }
                    
                    // 썸네일 목록도 확인해보기
                    yield return StartCoroutine(LoadThumbnailListCoroutine());
                    
                    // 썸네일 생성
                    CreateThumbnailItems();
                }
                else
                {
                    Debug.LogWarning("파싱된 파일명이 없습니다.");
                }
            }
            else
            {
                Debug.LogError($"서버 요청 실패: {request.error}");
                Debug.LogError($"응답 코드: {request.responseCode}");
                
                // 로컬 파일 확인으로 대체
                CheckLocalFiles();
            }
        }
    }
    
    // 썸네일 목록 확인용 메서드
    IEnumerator LoadThumbnailListCoroutine()
    {
        string thumbnailListURL = GetServerURL("/api/videos/thumbnails");
        Debug.Log($"썸네일 목록 요청: {thumbnailListURL}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(thumbnailListURL))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();
            
            Debug.Log($"=== 썸네일 목록 응답 ===");
            Debug.Log($"결과: {request.result}");
            Debug.Log($"응답 코드: {request.responseCode}");
            Debug.Log($"썸네일 목록 응답: {request.downloadHandler.text}");
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                // 썸네일 파일명들 확인
                List<string> thumbnailNames = ParseFilenameArray(request.downloadHandler.text);
                Debug.Log($"썸네일 파일 개수: {thumbnailNames.Count}");
                foreach (string name in thumbnailNames)
                {
                    Debug.Log($"썸네일 파일: {name}");
                }
            }
            else
            {
                Debug.LogWarning($"썸네일 목록 로드 실패: {request.error}");
            }
        }
    }
    
    // JSON 배열을 파일명 리스트로 파싱
    List<string> ParseFilenameArray(string jsonArray)
    {
        List<string> filenames = new List<string>();
        
        try
        {
            // JSON 배열이 맞는지 확인
            if (!jsonArray.Trim().StartsWith("[") || !jsonArray.Trim().EndsWith("]"))
            {
                Debug.LogError("JSON 배열 형태가 아닙니다.");
                return filenames;
            }
            
            // Unity JsonUtility로 배열 파싱을 위한 래퍼 사용
            string wrappedJson = "{\"files\":" + jsonArray + "}";
            FileListWrapper wrapper = JsonUtility.FromJson<FileListWrapper>(wrappedJson);
            
            if (wrapper != null && wrapper.files != null)
            {
                foreach (string file in wrapper.files)
                {
                    if (!string.IsNullOrEmpty(file))
                    {
                        filenames.Add(file);
                    }
                }
            }
            else
            {
                Debug.LogWarning("JsonUtility 파싱 실패, 수동 파싱 시도");
                filenames = ManualParseArray(jsonArray);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JSON 파싱 오류: {e.Message}");
            Debug.LogError("수동 파싱 시도");
            filenames = ManualParseArray(jsonArray);
        }
        
        return filenames;
    }
    
    // 수동 JSON 배열 파싱
    List<string> ManualParseArray(string jsonArray)
    {
        List<string> filenames = new List<string>();
        
        try
        {
            // 대괄호 제거
            string content = jsonArray.Trim();
            if (content.StartsWith("[")) content = content.Substring(1);
            if (content.EndsWith("]")) content = content.Substring(0, content.Length - 1);
            
            // 쉼표로 분할
            string[] items = content.Split(',');
            
            foreach (string item in items)
            {
                string filename = item.Trim();
                
                // 따옴표 제거
                if (filename.StartsWith("\"") && filename.EndsWith("\""))
                {
                    filename = filename.Substring(1, filename.Length - 2);
                }
                
                if (!string.IsNullOrEmpty(filename))
                {
                    filenames.Add(filename);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"수동 파싱 실패: {e.Message}");
        }
        
        return filenames;
    }
    
    void CreateThumbnailItems()
    {
        Debug.Log("=== 썸네일 아이템 생성 ===");
        
        // 기존 썸네일 삭제
        foreach (Transform child in thumbnailParent)
        {
            Destroy(child.gameObject);
        }
        
        // 새 썸네일 아이템 생성
        for (int i = 0; i < videos.Count; i++)
        {
            CreateSingleThumbnailItem(videos[i], i);
        }
        
        Debug.Log($"썸네일 아이템 {videos.Count}개 생성 완료");
    }
    
    void CreateSingleThumbnailItem(VideoData video, int index)
    {
        if (thumbnailItemPrefab == null)
        {
            Debug.LogError("thumbnailItemPrefab이 설정되지 않았습니다!");
            return;
        }
        
        GameObject thumbnailItem = Instantiate(thumbnailItemPrefab, thumbnailParent);
        ThumbnailItemController controller = thumbnailItem.GetComponent<ThumbnailItemController>();
        
        if (controller != null)
        {
            // ThumbnailItemController가 있으면 기존 방식 사용
            controller.Setup(video, this, index);
            Debug.Log($"썸네일 아이템 설정 완료: {video.filename}");
        }
        else
        {
            // ThumbnailItemController가 없으면 직접 설정
            Debug.LogWarning($"ThumbnailItemController가 없습니다. 직접 설정합니다: {video.filename}");
            SetupThumbnailItemManually(thumbnailItem, video, index);
        }
    }
    
    // ThumbnailItemController 없이도 작동하도록 수동 설정
    void SetupThumbnailItemManually(GameObject thumbnailItem, VideoData video, int index)
    {
        // 썸네일 이미지 찾기
        RawImage thumbnailImage = thumbnailItem.GetComponentInChildren<RawImage>();
        
        // 재생 버튼 찾기
        Button playButton = thumbnailItem.GetComponentInChildren<Button>();
        
        // 제목 텍스트 찾기 (선택적)
        TextMeshProUGUI titleText = thumbnailItem.GetComponentInChildren<TextMeshProUGUI>();
        
        // 제목 설정
        if (titleText != null)
        {
            titleText.text = video.title;
        }
        
        // 재생 버튼 이벤트 연결
        if (playButton != null)
        {
            // 클로저 문제 방지를 위한 로컬 변수
            int videoIndex = index;
            VideoData videoData = video;
            
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(() => {
                Debug.Log($"재생 버튼 클릭: {videoData.filename}");
                PlayVideo(videoData, videoIndex);
            });
        }
        
        // 썸네일 이미지 로드
        if (thumbnailImage != null)
        {
            StartCoroutine(LoadThumbnailImageManually(thumbnailImage, video));
        }
        
        Debug.Log($"수동 설정 완료: {video.filename}");
    }
    
    // 개선된 썸네일 이미지 로딩 메서드 (올바른 엔드포인트 사용)
    IEnumerator LoadThumbnailImageManually(RawImage thumbnailImage, VideoData video)
    {
        // 1단계: 썸네일 파일명 생성하여 시도 (기본 규칙)
        string thumbnailFilename = GetThumbnailFilename(video.filename);
        string thumbnailURL = GetServerURL($"/api/videos/thumbnails/download/{thumbnailFilename}");
        
        Debug.Log($"1단계 - 기본 썸네일 파일명 시도: {thumbnailURL}");
        
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(thumbnailURL))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();
            
            Debug.Log($"1단계 응답 코드: {request.responseCode}");
            Debug.Log($"1단계 응답 결과: {request.result}");
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D thumbnail = ((DownloadHandlerTexture)request.downloadHandler).texture;
                if (thumbnailImage != null)
                {
                    thumbnailImage.texture = thumbnail;
                    Debug.Log($"✅ 썸네일 로드 성공 (기본 규칙): {video.filename}");
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning($"1단계 실패: {request.error} (코드: {request.responseCode})");
            }
        }
        
        // 2단계: 원본 파일명으로 시도 (확장자 포함)
        thumbnailURL = GetServerURL($"/api/videos/thumbnails/download/{video.filename}");
        Debug.Log($"2단계 - 원본 파일명으로 시도: {thumbnailURL}");
        
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(thumbnailURL))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();
            
            Debug.Log($"2단계 응답 코드: {request.responseCode}");
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D thumbnail = ((DownloadHandlerTexture)request.downloadHandler).texture;
                if (thumbnailImage != null)
                {
                    thumbnailImage.texture = thumbnail;
                    Debug.Log($"✅ 썸네일 로드 성공 (원본명): {video.filename}");
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning($"2단계 실패: {request.error} (코드: {request.responseCode})");
            }
        }
        
        // 3단계: 다른 확장자들로 시도
        string[] possibleExtensions = { ".jpg", ".jpeg", ".png" };
        string baseFilename = Path.GetFileNameWithoutExtension(video.filename);
        
        foreach (string ext in possibleExtensions)
        {
            string possibleFilename = baseFilename + ext;
            thumbnailURL = GetServerURL($"/api/videos/thumbnails/download/{possibleFilename}");
            Debug.Log($"3단계 - 다른 확장자 시도 ({ext}): {thumbnailURL}");
            
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
                        Debug.Log($"✅ 썸네일 로드 성공 ({ext}): {video.filename}");
                        yield break;
                    }
                }
            }
        }
        
        // 4단계: 썸네일 with _thumbnail suffix 시도
        string[] thumbnailSuffixes = { "_thumbnail.jpg", "_thumbnail.jpeg", "_thumbnail.png" };
        
        foreach (string suffix in thumbnailSuffixes)
        {
            string possibleFilename = baseFilename + suffix;
            thumbnailURL = GetServerURL($"/api/videos/thumbnails/download/{possibleFilename}");
            Debug.Log($"4단계 - 썸네일 suffix 시도 ({suffix}): {thumbnailURL}");
            
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
                        Debug.Log($"✅ 썸네일 로드 성공 ({suffix}): {video.filename}");
                        yield break;
                    }
                }
            }
        }
        
        // 5단계: 로컬 썸네일 찾기
        Debug.Log($"5단계 - 로컬 썸네일 찾기: {video.filename}");
        yield return StartCoroutine(LoadLocalThumbnail(thumbnailImage, video));
        
        // 마지막: 기본 썸네일 설정
        if (thumbnailImage != null && thumbnailImage.texture == null)
        {
            Debug.Log($"모든 단계 실패, 기본 썸네일 설정: {video.filename}");
            SetDefaultThumbnail(thumbnailImage);
        }
    }
    
    // 썸네일 파일명 생성 규칙 (서버에 맞춤)
    string GetThumbnailFilename(string videoFilename)
    {
        // 기본적으로 _thumbnail.jpg 형식으로 시도
        string baseFilename = Path.GetFileNameWithoutExtension(videoFilename);
        return baseFilename + "_thumbnail.jpg";
    }
    
    // 로컬 썸네일 찾기 및 로드
    IEnumerator LoadLocalThumbnail(RawImage thumbnailImage, VideoData video)
    {
        string thumbnailFilename = GetThumbnailFilename(video.filename);
        
        // 로컬 경로 목록
        string[] localPaths = {
            Path.Combine(Application.temporaryCachePath, "Recordings", thumbnailFilename),
            Path.Combine(Application.dataPath, "Recordings", thumbnailFilename),
            Path.Combine(Application.persistentDataPath, "Recordings", thumbnailFilename)
        };
        
        foreach (string localPath in localPaths)
        {
            if (File.Exists(localPath))
            {
                Debug.Log($"로컬 썸네일 발견: {localPath}");
                
                try
                {
                    byte[] imageData = File.ReadAllBytes(localPath);
                    Texture2D thumbnail = new Texture2D(1, 1);
                    
                    if (thumbnail.LoadImage(imageData))
                    {
                        if (thumbnailImage != null)
                        {
                            thumbnailImage.texture = thumbnail;
                            Debug.Log($"✅ 로컬 썸네일 로드 성공: {video.filename}");
                            yield break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"로컬 썸네일 이미지 로드 실패: {localPath}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"로컬 썸네일 읽기 오류: {e.Message}");
                }
            }
        }
        
        Debug.LogWarning($"로컬 썸네일을 찾을 수 없음: {video.filename}");
        yield return null;
    }
    
    void SetDefaultThumbnail(RawImage thumbnailImage)
    {
        if (thumbnailImage == null) return;
        
        // 회색 기본 이미지 생성
        Texture2D defaultTexture = new Texture2D(1, 1);
        defaultTexture.SetPixel(0, 0, Color.gray);
        defaultTexture.Apply();
        
        thumbnailImage.texture = defaultTexture;
        Debug.Log("기본 썸네일 적용 완료");
    }
    
    // 외부에서 호출 가능한 비디오 재생 메서드 (올바른 엔드포인트 사용)
    public void PlayVideo(VideoData video, int index)
    {
        if (isPlaying)
        {
            Debug.LogWarning("이미 비디오가 재생 중입니다.");
            return;
        }
        
        Debug.Log($"=== 비디오 재생 시작 ===");
        Debug.Log($"파일명: {video.filename}");
        
        isPlaying = true;
        
        // UI 전환
        if (thumbnailParent.parent.parent.gameObject != null)
            thumbnailParent.parent.parent.gameObject.SetActive(false); // ScrollView 숨기기
        
        if (videoScreen != null)
            videoScreen.gameObject.SetActive(true);
        
        if (backButton != null)
            backButton.gameObject.SetActive(true);
        
        // 올바른 비디오 다운로드 엔드포인트 사용
        string videoURL = GetServerURL($"/api/videos/download/{video.filename}");
        Debug.Log($"비디오 URL: {videoURL}");
        
        if (videoPlayer != null)
        {
            videoPlayer.url = videoURL;
            videoPlayer.Play();
        }
    }
    
    public void BackToGallery()
    {
        Debug.Log("=== 갤러리로 돌아가기 ===");
        
        isPlaying = false;
        
        // 비디오 정지
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
        
        // UI 전환
        if (thumbnailParent.parent.parent.gameObject != null)
            thumbnailParent.parent.parent.gameObject.SetActive(true); // ScrollView 표시
        
        if (videoScreen != null)
            videoScreen.gameObject.SetActive(false);
        
        if (backButton != null)
            backButton.gameObject.SetActive(false);
    }
    
    // 로컬 파일 확인 (서버 실패 시 대안)
    void CheckLocalFiles()
    {
        Debug.Log("=== 로컬 파일 확인 ===");
        
        string recordingsPath = Path.Combine(Application.dataPath, "Recordings");
        
        if (Directory.Exists(recordingsPath))
        {
            string[] mp4Files = Directory.GetFiles(recordingsPath, "*.mp4");
            Debug.Log($"로컬에서 찾은 MP4 파일: {mp4Files.Length}개");
            
            foreach (string file in mp4Files)
            {
                Debug.Log($"로컬 파일: {Path.GetFileName(file)}");
            }
            
            if (mp4Files.Length > 0)
            {
                Debug.Log("로컬 파일이 있습니다. 서버에 업로드가 필요할 수 있습니다.");
                
                // 로컬 파일을 비디오 목록에 추가
                videos.Clear();
                foreach (string file in mp4Files)
                {
                    string filename = Path.GetFileName(file);
                    VideoData videoData = new VideoData
                    {
                        filename = filename,
                        title = Path.GetFileNameWithoutExtension(filename)
                    };
                    videos.Add(videoData);
                    Debug.Log($"로컬 비디오 추가: {filename}");
                }
                
                CreateThumbnailItems();
            }
        }
        else
        {
            Debug.Log("Recordings 폴더가 존재하지 않습니다.");
        }
    }
    
    // 수동 새로고침 메서드 (public으로 버튼에 연결 가능)
    public void RefreshVideoList()
    {
        Debug.Log("수동 새로고침 요청");
        LoadVideoList();
    }
    
    void OnDestroy()
    {
        // 정리 작업
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            if (videoPlayer.targetTexture != null)
            {
                videoPlayer.targetTexture.Release();
                DestroyImmediate(videoPlayer.targetTexture);
            }
        }
    }
}