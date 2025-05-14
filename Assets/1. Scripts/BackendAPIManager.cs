using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using Debug = UnityEngine.Debug;

public class BackendAPIManager : MonoBehaviour
{
    [Header("서버 설정")]
    public string baseServerURL = "http://172.16.16.154:8080";
    [Header("업로드 설정")]
    public float uploadProgressCheckInterval = 0.5f;

    public void UploadVideoWithThumbnail(
        string videoPath,
        string thumbnailPath,
        System.Action<bool, string> callback)
    {
        StartCoroutine(UploadCoroutine(videoPath, thumbnailPath, callback));
    }

    IEnumerator UploadCoroutine(
        string videoPath,
        string thumbnailPath,
        System.Action<bool, string> callback)
    {
        // 파일 유효성 검사
        if (!File.Exists(videoPath))
        {
            Debug.LogError($"업로드 파일 없음: {videoPath}");
            callback?.Invoke(false, "File not found");
            yield break;
        }
        Debug.Log("업로드 파일...");
        UnityWebRequest webRequest = CreateUploadRequest(videoPath, thumbnailPath);
        yield return SendAndTrackUpload(webRequest, callback);
    }

    UnityWebRequest CreateUploadRequest(string videoPath, string thumbnailPath)
    {
        WWWForm form = new WWWForm();
        byte[] videoData = File.ReadAllBytes(videoPath);
        string videoFilename = Path.GetFileName(videoPath);

        // 비디오 추가
        form.AddBinaryData("video", videoData, videoFilename, "video/mp4");

        // 썸네일 추가 (있을 경우)
        if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
        {
            byte[] thumbnailData = File.ReadAllBytes(thumbnailPath);
            string thumbnailFilename = Path.GetFileName(thumbnailPath);
            form.AddBinaryData("image", thumbnailData, thumbnailFilename, "image/jpeg");
        }

        // 메타데이터 추가
        // form.AddField("title", Path.GetFileNameWithoutExtension(videoFilename));
        // form.AddField("uploadTime", System.DateTime.Now.ToString());

        return UnityWebRequest.Post($"{baseServerURL}/api/videos/upload", form);
    }


    IEnumerator SendAndTrackUpload(UnityWebRequest webRequest, System.Action<bool, string> callback)
    {
        var operation = webRequest.SendWebRequest();
        float lastReportTime = Time.time;

        while (!operation.isDone)
        {
            if (Time.time - lastReportTime > uploadProgressCheckInterval)
            {
                Debug.Log($"📤 업로드 진행률: {webRequest.uploadProgress * 100:F1}%");
                lastReportTime = Time.time;
            }
            yield return null;
        }

        ProcessUploadResult(webRequest, callback);
        webRequest.Dispose();
    }

    void ProcessUploadResult(UnityWebRequest webRequest, System.Action<bool, string> callback)
    {
        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("✅ 업로드 성공");
            callback?.Invoke(true, webRequest.downloadHandler.text);
        }
        else
        {
            Debug.LogError($"❌ 업로드 실패: {webRequest.error}");
            callback?.Invoke(false, webRequest.error);
        }
    }

    // 기타 API 메서드 (목록 조회, 다운로드 등) 필요시 추가
}
