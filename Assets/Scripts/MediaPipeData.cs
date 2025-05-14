using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System; // Reflection 사용을 위해 추가
using System.Reflection; // Reflection 사용을 위해 추가

// TODO: 여기에 있었던 LandmarkPosition, AllLandmarksData_ForJsonUtility, PoseData_ForJsonUtility 클래스 정의를 삭제합니다.
//      이 클래스들은 이제 LandmarkDataStructures.cs 파일에서 정의됩니다.


public enum PoseName // 랜드마크 이름과 인덱스 매핑용 enum
{
    NOSE = 0, LEFT_EYE_INNER = 1, LEFT_EYE = 2, LEFT_EYE_OUTER = 3, RIGHT_EYE_INNER = 4, RIGHT_EYE = 5,
    RIGHT_EYE_OUTER = 6, LEFT_EAR = 7, RIGHT_EAR = 8, MOUTH_LEFT = 9, MOUTH_RIGHT = 10,
    LEFT_SHOULDER = 11, RIGHT_SHOULDER = 12, LEFT_ELBOW = 13, RIGHT_ELBOW = 14, LEFT_WRIST = 15,
    RIGHT_WRIST = 16, LEFT_PINKY = 17, RIGHT_PINKY = 18, LEFT_INDEX = 19, RIGHT_INDEX = 20,
    LEFT_THUMB = 21, RIGHT_THUMB = 22, LEFT_HIP = 23, RIGHT_HIP = 24, LEFT_KNEE = 25, RIGHT_KNEE = 26,
    LEFT_ANKLE = 27, RIGHT_ANKLE = 28, LEFT_HEEL = 29, RIGHT_HEEL = 30, LEFT_FOOT_INDEX = 31, RIGHT_FOOT_INDEX = 32,
    POSE_MAX = 33 // 총 랜드마크 개수
}

public class MediaPipeData : MonoBehaviour
{
    [Header("설정")]
    public GameObject pointFactory; // 시각화할 오브젝트 프리팹

    // PoseTcpClient 스크립트 참조 (이제 PoseTcpClient가 호출하므로 필요 없을 수도 있음)
    // public PoseTcpClient tcpClient; // 주석 처리 또는 삭제

    [Header("시각화")]
    public Transform[] allPoints; // 생성된 시각화 오브젝트들의 Transform

    // 랜드마크 이름으로 오브젝트를 빠르게 찾기 위한 Dictionary
    private Dictionary<string, Transform> landmarkPointMap = new Dictionary<string, Transform>();

    // MediaPipe PoseLandmark 이름들을 순서대로 저장한 배열 (enum 순서와 일치)
    private string[] landmarkNames = {
        "NOSE", "LEFT_EYE_INNER", "LEFT_EYE", "LEFT_EYE_OUTER", "RIGHT_EYE_INNER", "RIGHT_EYE",
        "RIGHT_EY_OUTER", "LEFT_EAR", "RIGHT_EAR", "MOUTH_LEFT", "MOUTH_RIGHT",
        "LEFT_SHOULDER", "RIGHT_SHOULDER", "LEFT_ELBOW", "RIGHT_ELBOW", "LEFT_WRIST", // <-- 오른쪽 팔꿈치 오타 수정됨
        "RIGHT_WRIST", "LEFT_PINKY", "RIGHT_PINKY", "LEFT_INDEX", "RIGHT_INDEX",
        "LEFT_THUMB", "RIGHT_THUMB", "LEFT_HIP", "RIGHT_HIP", "LEFT_KNEE", "RIGHT_KNEE",
        "LEFT_ANKLE", "RIGHT_ANKLE", "LEFT_HEEL", "RIGHT_HEEL", "LEFT_FOOT_INDEX", "RIGHT_FOOT_INDEX"
    };


    // 유니티 좌표계 변환 설정
    [Header("좌표 변환 설정")]
    public float visualizerScale = 10.0f; // 조정 필요
    public float baseZ = 0; // 조정 필요
    public float zScale = 5.0f; // 조정 필요

    // IKCharacter 스크립트 참조 (Inspector에서 연결)
    [Header("연동 스크립트")]
    public IKCharacter ikCharacter;


    void Awake() // 오브젝트 생성 및 초기화
    {
        Debug.Log("[MediaPipeData] 스크ript Awake.");
        allPoints = new Transform[(int)PoseName.POSE_MAX];
        landmarkPointMap.Clear();

        if (pointFactory == null)
        { Debug.LogError("[MediaPipeData] pointFactory 프리팹 할당 안 됨!"); return; }
        if (landmarkNames.Length != (int)PoseName.POSE_MAX)
        { Debug.LogError($"[MediaPipeData] 이름 배열 크기({landmarkNames.Length})와 enum 크기({(int)PoseName.POSE_MAX}) 불일치!"); return; }

        for (int i = 0; i < (int)PoseName.POSE_MAX; i++)
        {
            GameObject point = Instantiate(pointFactory);
            point.transform.parent = transform;
            point.name = landmarkNames[i]; // 또는 ((PoseName)i).ToString();
            allPoints[i] = point.transform;
            landmarkPointMap[point.name] = point.transform;
        }
        Debug.Log($"[MediaPipeData] {allPoints.Length}개의 랜드마크 시각화 오브젝트 생성 완료.");
    }

    // PoseTcpClient로부터 데이터 전달받는 함수
    public void OnLandmarkDataReceived(PoseData_ForJsonUtility poseData)
    {
        if (poseData != null && poseData.landmarks != null)
        {
            System.Reflection.FieldInfo[] landmarkFields = typeof(AllLandmarksData_ForJsonUtility).GetFields();

            if (allPoints == null || allPoints.Length != (int)PoseName.POSE_MAX || landmarkPointMap.Count == 0)
            { Debug.LogError("[MediaPipeData] 시각화 오브젝트 초기화 안 됨. IKCharacter 업데이트 건너뜀."); return; }

            // IKCharacter로 넘겨줄 데이터 준비 (Dictionary<string, LandmarkPosition>)
            Dictionary<string, LandmarkPosition> currentLandmarkPositions = new Dictionary<string, LandmarkPosition>();


            foreach (var field in landmarkFields)
            {
                string landmarkName = field.Name;
                LandmarkPosition landmarkPos = field.GetValue(poseData.landmarks) as LandmarkPosition;

                if (landmarkPos != null && landmarkPointMap.TryGetValue(landmarkName, out Transform visualizerTransform))
                {
                    // MediaPipe x, y (0-1 범위) -> 유니티 좌표 변환
                    float unityX = (landmarkPos.x - 0.5f) * visualizerScale;
                    float unityY = (0.5f - landmarkPos.y) * visualizerScale; // Y축 반전 예시
                    float unityZ = baseZ + landmarkPos.z * zScale; // Z 스케일 및 방향 조정 필요

                    // 시각화 오브젝트 위치 업데이트
                    visualizerTransform.position = new Vector3(unityX, unityY, unityZ);

                    // IKCharacter에 전달할 데이터 Dictionary에 추가
                    currentLandmarkPositions[landmarkName] = landmarkPos;
                }
            }

            // IKCharacter 업데이트 함수 호출
            if (ikCharacter != null)
            {
                // 업데이트된 시각화 오브젝트 Transform과 원본 LandmarkPosition 데이터를 함께 넘겨줍니다.
                ikCharacter.UpdateIKTargets(currentLandmarkPositions, allPoints);
            }
        }
    }

    // 외부에서 랜드마크 이름으로 해당 시각화 오브젝트의 Transform 얻는 헬퍼 함수 (IKCharacter에서 사용)
    public Transform GetLandmarkTransform(string landmarkName)
    {
        if (landmarkPointMap.TryGetValue(landmarkName, out Transform visualizerTransform))
        {
            return visualizerTransform;
        }
        // Debug.LogWarning($"[MediaPipeData] 랜드마크 '{landmarkName}'에 해당하는 시각화 오브젝트를 찾을 수 없습니다."); // 매 프레임 로그 방지
        return null;
    }

    // --- 나머지 Update, OnDestroy 함수는 이전 코드와 동일 ---
    // Update 함수는 비워두거나 다른 용도로 사용
    // void Update() { }

    // 오브젝트 파괴 시 정리
    void OnDestroy() { }

}
