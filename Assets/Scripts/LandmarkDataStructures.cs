using UnityEngine; // [System.Serializable] 어트리뷰트 사용을 위해 필요

// MediaPipe 랜드마크 관련 데이터 구조 정의
// PoseTcpClient와 MediaPipeData 등 여러 스크립트에서 공유하여 사용합니다.

// 서버에서 보내는 랜드마크 개별 좌표 구조
[System.Serializable] // Unity의 JsonUtility로 직렬화/역직화 가능하게 해줌
public class LandmarkPosition
{
    public float x; // 0~1 범위 (이미지 가로 기준)
    public float y; // 0~1 범위 (이미지 세로 기준)
    public float z; // 상대적인 깊이 값 (사용하기 복잡할 수 있음)
    // public float visibility; // visibility를 보냈다면 추가 (파이썬 코드에서 보내지 않으므로 주석 처리)
}

// 서버에서 보내는 모든 랜드마크 데이터를 담는 구조 (JsonUtility용)
// 파이썬에서 {"NOSE": {x,y,z}, "LEFT_EYE_INNER": {x,y,z}, ...} 형태의 딕셔너리를 보내므로,
// JsonUtility로 파싱하려면 C# 클래스에 모든 33개 랜드마크 이름을 필드로 정의해야 합니다.
[System.Serializable]
public class AllLandmarksData_ForJsonUtility
{
    // MediaPipe PoseLandmark enum의 33개 이름을 필드로 모두 정의해야 합니다.
    // 이 필드 이름들은 파이썬 서버에서 보내는 JSON 키 이름과 정확히 일치해야 합니다!
    public LandmarkPosition NOSE;
    public LandmarkPosition LEFT_EYE_INNER;
    public LandmarkPosition LEFT_EYE;
    public LandmarkPosition LEFT_EYE_OUTER;
    public LandmarkPosition RIGHT_EYE_INNER;
    public LandmarkPosition RIGHT_EYE;
    public LandmarkPosition RIGHT_EYE_OUTER;
    public LandmarkPosition LEFT_EAR;
    public LandmarkPosition RIGHT_EAR;
    public LandmarkPosition MOUTH_LEFT;
    public LandmarkPosition MOUTH_RIGHT;
    public LandmarkPosition LEFT_SHOULDER;
    public LandmarkPosition RIGHT_SHOULDER;
    public LandmarkPosition LEFT_ELBOW;
    public LandmarkPosition RIGHT_ELBOW;
    public LandmarkPosition LEFT_WRIST;
    public LandmarkPosition RIGHT_WRIST;
    public LandmarkPosition LEFT_PINKY;
    public LandmarkPosition RIGHT_PINKY;
    public LandmarkPosition LEFT_INDEX;
    public LandmarkPosition RIGHT_INDEX;
    public LandmarkPosition LEFT_THUMB;
    public LandmarkPosition RIGHT_THUMB;
    public LandmarkPosition LEFT_HIP;
    public LandmarkPosition RIGHT_HIP;
    public LandmarkPosition LEFT_KNEE;
    public LandmarkPosition RIGHT_KNEE;
    public LandmarkPosition LEFT_ANKLE;
    public LandmarkPosition RIGHT_ANKLE;
    public LandmarkPosition LEFT_HEEL;
    public LandmarkPosition RIGHT_HEEL;
    public LandmarkPosition LEFT_FOOT_INDEX;
    public LandmarkPosition RIGHT_FOOT_INDEX;
}

[System.Serializable]
public class PoseData_ForJsonUtility // JsonUtility용 PoseData 구조
{
    public float timestamp; // 서버에서 보낸 타임스탬프
    public int count; // 서버에서 보낸 데이터 순번
    public AllLandmarksData_ForJsonUtility landmarks; // 모든 랜드마크 데이터
}

// ** 참고: Newtonsoft.Json 라이브러리 사용 시 데이터 구조 예시 **
/*
using Newtonsoft.Json; // 추가 필요
using System.Collections.Generic; // Dictionary 사용 필요

public class PoseData_Newtonsoft // Newtonsoft.Json용 PoseData 구조
{
    public float timestamp;
    public int count;
    public Dictionary<string, LandmarkPosition> landmarks; // Dictionary<string, T> 직접 사용 가능
}
*/

// ** 참고: 서버에서 JSON 구조를 List 형태로 변경 시 데이터 구조 예시 (권장) **
/*
[System.Serializable]
public class NamedLandmarkPosition
{
    public string name;
    public LandmarkPosition position;
}

[System.Serializable]
public class PoseData_Recommended
{
    public float timestamp;
    public int count;
    public NamedLandmarkPosition[] landmarks; // List<NamedLandmarkPosition> 대신 배열[]로 받아야 JsonUtility 사용 편함
}
*/
