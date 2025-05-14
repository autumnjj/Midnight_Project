using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Watch 버튼 전용 스크립트 - 독립적으로 작동
public class WatchButtonController : MonoBehaviour
{
    [Header("Scene Settings")]
    public string gallerySceneName = "VideoGalleryScene";
    
    [Header("UI Elements")]
    public Button watchButton;
    public TextMeshProUGUI statusText; // 선택적, 없어도 됨
    
    void Start()
    {
        Debug.Log("=== WatchButtonController Start() ===");
        Debug.Log($"gallerySceneName: {gallerySceneName}");
        SetupButton();
    }
    
    void SetupButton()
    {
        if (watchButton != null)
        {
            watchButton.onClick.AddListener(GoToGallery);
            watchButton.interactable = true; // 항상 활성화
            Debug.Log("Watch 버튼 설정 완료");
        }
        else
        {
            Debug.LogWarning("Watch Button이 연결되지 않았습니다!");
        }
    }
    
    public void GoToGallery()
    {
        Debug.Log("=== Watch 버튼 클릭됨 ===");
        Debug.Log($"이동할 씬: {gallerySceneName}");
        
        // Build Settings에 씬이 있는지 확인
        bool sceneExists = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            Debug.Log($"Build Settings 씬 {i}: {sceneName}");
            
            if (sceneName == gallerySceneName)
            {
                sceneExists = true;
                break;
            }
        }
        
        if (!sceneExists)
        {
            Debug.LogError($"씬 '{gallerySceneName}'이 Build Settings에 없습니다!");
            Debug.LogError("File → Build Settings에서 VideoGalleryScene을 추가하세요.");
            return;
        }
        
        if (statusText != null)
            statusText.text = "갤러리로 이동 중...";
        
        try
        {
            Debug.Log($"씬 로드 시작: {gallerySceneName}");
            SceneManager.LoadScene(gallerySceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"씬 로드 오류: {e.Message}");
        }
    }
    
    // 수동으로 호출 가능한 메서드 (Inspector에서 Button 이벤트로 연결 가능)
    public void OnWatchButtonClicked()
    {
        GoToGallery();
    }
}