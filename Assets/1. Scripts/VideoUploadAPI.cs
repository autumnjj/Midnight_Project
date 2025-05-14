using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.IO;

// 백엔드 API용 비디오 업로드 클래스
public static class VideoUploadAPI
{
    // 백엔드 API 엔드포인트들
    public const string UPLOAD_ENDPOINT = "/api/videos/upload";
    public const string LIST_ENDPOINT = "/api/videos/list";
    public const string THUMBNAILS_LIST_ENDPOINT = "/api/videos/thumbnails";
    public const string DOWNLOAD_ENDPOINT = "/api/videos/download/";
    public const string THUMBNAIL_DOWNLOAD_ENDPOINT = "/api/videos/thumbnails/download/";
    
    // 비디오 업로드용 - WWWForm 방식 (비디오만)
    public static UnityWebRequest CreateVideoUploadRequest(byte[] videoBytes, string filename, string baseServerURL)
    {
        var formData = new WWWForm();
        formData.AddBinaryData("video", videoBytes, filename, "video/mp4");
        
        // 추가 필드들 (선택사항)
        formData.AddField("title", Path.GetFileNameWithoutExtension(filename));
        formData.AddField("description", "Unity에서 녹화된 영상");
        formData.AddField("uploadTime", System.DateTime.Now.ToString());
        
        string fullUrl = baseServerURL + UPLOAD_ENDPOINT;
        var webRequest = UnityWebRequest.Post(fullUrl, formData);
        webRequest.timeout = 120;
        return webRequest;
    }
    
    // 비디오 + 썸네일 동시 업로드 (백엔드에서 지원하는 형식)
    public static UnityWebRequest CreateVideoWithThumbnailUploadRequest(
        byte[] videoBytes, string videoFilename,
        byte[] thumbnailBytes, string thumbnailFilename,
        string baseServerURL)
    {
        var formData = new WWWForm();
        
        // 비디오 파일
        formData.AddBinaryData("video", videoBytes, videoFilename, "video/mp4");
        
        // 썸네일 파일
        formData.AddBinaryData("thumbnail", thumbnailBytes, thumbnailFilename, "image/jpeg");
        
        // 추가 필드들
        formData.AddField("title", Path.GetFileNameWithoutExtension(videoFilename));
        formData.AddField("description", "Unity에서 녹화된 영상");
        formData.AddField("uploadTime", System.DateTime.Now.ToString());
        
        string fullUrl = baseServerURL + UPLOAD_ENDPOINT;
        var webRequest = UnityWebRequest.Post(fullUrl, formData);
        webRequest.timeout = 180;
        return webRequest;
    }
    
    // 비디오 목록 조회
    public static UnityWebRequest CreateVideoListRequest(string baseServerURL)
    {
        string fullUrl = baseServerURL + LIST_ENDPOINT;
        var webRequest = UnityWebRequest.Get(fullUrl);
        webRequest.timeout = 10;
        return webRequest;
    }
    
    // 썸네일 목록 조회
    public static UnityWebRequest CreateThumbnailListRequest(string baseServerURL)
    {
        string fullUrl = baseServerURL + THUMBNAILS_LIST_ENDPOINT;
        var webRequest = UnityWebRequest.Get(fullUrl);
        webRequest.timeout = 10;
        return webRequest;
    }
    
    // 비디오 다운로드
    public static UnityWebRequest CreateVideoDownloadRequest(string filename, string baseServerURL)
    {
        string fullUrl = baseServerURL + DOWNLOAD_ENDPOINT + filename;
        var webRequest = UnityWebRequest.Get(fullUrl);
        webRequest.timeout = 60;
        return webRequest;
    }
    
    // 썸네일 다운로드
    public static UnityWebRequest CreateThumbnailDownloadRequest(string filename, string baseServerURL)
    {
        string fullUrl = baseServerURL + THUMBNAIL_DOWNLOAD_ENDPOINT + filename;
        var webRequest = UnityWebRequestTexture.GetTexture(fullUrl);
        webRequest.timeout = 30;
        return webRequest;
    }
    
    // 결과 파싱을 위한 간단한 메서드
    public static T GetResultFromJson<T>(UnityWebRequest request) where T : class
    {
        try
        {
            string json = request.downloadHandler.text;
            return JsonUtility.FromJson<T>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JSON 파싱 오류: {e.Message}");
            return null;
        }
    }
    
    // 결과 데이터 클래스들
    [System.Serializable]
    public class UploadResult
    {
        public UploadData data;
        public bool success;
        public string message;
    }
    
    [System.Serializable]
    public class UploadData
    {
        public string filename;
        public int filesize;
        public string message;
        public string thumbnailFilename;
    }
    
    // 비디오 목록용 클래스
    [System.Serializable]
    public class VideoListResult
    {
        public string[] videos;
        public bool success;
        public string message;
    }
    
    // 썸네일 목록용 클래스  
    [System.Serializable]
    public class ThumbnailListResult
    {
        public string[] thumbnails;
        public bool success;
        public string message;
    }
}