using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// MediaPipeData 스크립트에서 정의된 PoseName enum 사용을 위해 필요합니다.
// LandmarkDataStructures.cs에 정의된 데이터 구조 클래스 사용을 위해 필요합니다.
// 만약 MediaPipeData 또는 LandmarkDataStructures가 다른 네임스페이스에 있다면 using [네임스페이스]; 추가 필요

// ===============================================================================================
// 참고: 이 IKCharacter.cs 파일에는 아래 데이터 구조 클래스들을 정의하지 마세요!
//       LandmarkPosition, AllLandmarksData_ForJsonUtility, PoseData_ForJsonUtility 클래스는
//       별도의 LandmarkDataStructures.cs 파일에 정의되어 있어야 합니다.
//       이 스크립트는 해당 파일을 참조하여 클래스를 사용합니다.
//
//       PoseName enum은 MediaPipeData.cs 파일에 정의되어 있습니다.
//       이 스크립트는 해당 파일을 참조하여 enum을 사용합니다.
// ===============================================================================================


public class IKCharacter : MonoBehaviour
{
    // MediaPipeData 스크립트로부터 최신 데이터와 可視化 오브젝트 Transform을 전달받습니다.
    // 이 스크립트는 MediaPipeData가 데이터를 수신했을 때 호출하는 UpdateIKTargets 함수를 통해 데이터를 받습니다.
    // public MediaPipeData data; // MediaPipeData를 직접 참조하여 데이터를 가져오지는 않습니다.


    [Header("IK 타겟")]
    [Tooltip("캐릭터 Rig에 연결된 오른쪽 손 IK Target 오브젝트를 할당하세요.")]
    public Transform rigRightHandTarget; // 오른쪽 손 IK Target (Inspector에서 연결)

    // 오른쪽 팔꿈치 IK (폴 타겟) 오브젝트 (Inspector에서 연결 - 선택 사항)
    // Animation Rigging의 TwoBoneIKConstraint 설정 시 폴 타겟으로 사용될 오브젝트입니다.
    [Tooltip("캐릭터 Rig에 연결된 오른쪽 팔꿈치 IK 폴 타겟 오브젝트를 할당하세요. (선택 사항)")]
    public Transform rigRightElbowTarget;

    // 왼쪽 손 IK 타겟 오브젝트 (Inspector에서 연결)
    [Tooltip("캐릭터 Rig에 연결된 왼쪽 손 IK Target 오브젝트를 할당하세요.")]
    public Transform rigLeftHandTarget;

    // 왼쪽 팔꿈치 IK (폴 타겟) 오브젝트 (Inspector에서 연결 - 선택 사항)
    [Tooltip("캐릭터 Rig에 연결된 왼쪽 팔꿈치 IK 폴 타겟 오브젝트를 할당하세요. (선택 사항)")]
    public Transform rigLeftElbowTarget;

    // 오른쪽 발 IK 타겟 오브젝트 (Inspector에서 연결)
    [Tooltip("캐릭터 Rig에 연결된 오른쪽 발 IK Target 오브젝트를 할당하세요.")]
    public Transform rigRightFootTarget;

    // 오른쪽 무릎 IK (폴 타겟) 오브젝트 (Inspector에서 연결 - 선택 사항)
    [Tooltip("캐릭터 Rig에 연결된 오른쪽 무릎 IK 폴 타겟 오브젝트를 할당하세요. (선택 사항)")]
    public Transform rigRightKneeTarget;

    // 왼쪽 발 IK 타겟 오브젝트 (Inspector에서 연결)
    [Tooltip("캐릭터 Rig에 연결된 왼쪽 발 IK 타겟 오브젝트를 할당하세요.")]
    public Transform rigLeftFootTarget;

    // 왼쪽 무릎 IK (폴 타겟) 오브젝트 (Inspector에서 연결 - 선택 사항)
    [Tooltip("캐릭터 Rig에 연결된 왼쪽 무릎 IK 폴 타겟 오브젝트를 할당하세요. (선택 사항)")]
    public Transform rigLeftKneeTarget;

    // --- 회전 타겟 필드는 제거되었습니다 ---
    // public Transform rigRightHandRotationTarget;
    // public Transform rigLeftHandRotationTarget;
    // public Transform rigRightFootRotationTarget;
    // public Transform rigLeftFootRotationTarget;

    // --- 다시 추가된 머리 회전 타겟 필드 ---
    [Header("회전 타겟")] // 이 헤더를 손/발 회전 타겟 필드가 없으므로 머리 회전 타겟 필드 위로 이동
    [Tooltip("캐릭터 Rig에 연결된 머리 Rotation Target 오브젝트를 할당하세요.")]
    public Transform rigHeadRotationTarget; // 머리 Rotation 타겟 (Inspector에서 연결)


    // キャラクターの Animator コンポーネント 参照
    Animator anim;

    // キャラクター モデルの 実際 関節 距離 (Start 時点で 測定)
    // IK 計算時 この 距離を 参考にして ターゲット 位置の スケールを 調整できます。
    float rightShoulderToElbowDistance;
    float rightElbowToWristDistance;
    float leftShoulderToElbowDistance;
    float leftElbowToWristDistance;
    float rightHipToKneeDistance;
    float rightKneeToAnkleDistance;
    float leftHipToKneeDistance;
    float leftKneeToAnkleDistance;

    // キャラクターの 実際 ヒップ 間距離 (スケール補正 基準)
    private float characterHipDistance; // private으로 변경

    // MediaPipeデータから受け取った最新データ保存変数
    // この変数はMediaPipeData.OnLandmarkDataReceived関数から呼び出されるUpdateIKTargets関数で更新されます。
    private Transform[] updatedLandmarkTransforms; // MediaPipeData.allPoints 배열 (MediaPipeData에서 Unity 좌표로 변환되고 위치가 업데이트된 ランドマーク 可視化 オブジェクトのTransform)
    private Dictionary<string, LandmarkPosition> latestLandmarkPositions; // MediaPipeDataから 受け取った 元のランドマークデータ (JSON 파싱 結果, 0-1 範囲 x,y,z)

    // キャラクター全体移動およびスケール調整のための設定 (Inspectorで調整)
    [Header("캐릭터 이동 설정")]
    // Y 位置 アップデート 임계値 設定
    [Tooltip("MediaPipe 엉덩이 平均 Y 値がこの 임계値 以上 変動해야 キャラクター Y 位置를 アップデートします。")]
    public float yMovementThreshold = 0.03f; // 임계値 (0~1 範囲, 調整必要)

    // Y 位置 補間 速度 制御 (임계値 方式 使用時 滑らかな移動に使用)
    [Tooltip("캐릭터 Y 位置 アップデート時 目標 位置まで 滑らかに 移動する 時間。0이면 即時移動。")]
    public float ySmoothTime = 0.1f; // SmoothDamp 使用時 補間 時間 (調整必要)

    // --- Y および XZ 位置 変換 スケール 設定 ---
    // この 値は スケール 補正 系数 とともに 使用されます。
    [Tooltip("MediaPipe ランドマーク座標を Unity ワールド 좌표 에 변환 할 때 의 기본 スケール")]
    public float baseMovementScale = 5.0f; // 기본 이동 スケール (이전에 vertical/horizontalMovementScale 역할을 합친)

    // verticalMovementScale 変数 宣言 (Y 移動 スケール)
    [Tooltip("MediaPipe 랜드마크 좌표의 Y 値 (0~1) を ユニティ ワールド Y 位置에 変換 する 際に 使用 する スケール。 (baseMovementScale とともに 使用)")]
    public float verticalMovementScale = 1.0f; // Y 移動 スケール (baseMovementScale と 乗算され、最終 Y スケール が 決定)

    // Z 値 移動 適用 안 함 (以前 バージョン ロジック 維持)
    // public float depthMovementScale = 5.0f;
    // public float baseLandmarkZ = 0.5f;
    // public float characterBaseZ = 0.0f;

    [Tooltip("캐릭터의 足 아래 位置 를 基準 で 使用 する ランドマーク Y 値 の 基準点 (0~1)。 通常 立っている 時 平均 Y 値。MediaPipe Y는 0이 上、1이 下。")]
    public float baseLandmarkY = 0.8f; // MediaPipe Y는 0이 上、1이 下。立っている 時 足首/ヒップ 近辺 Y 値

    [Tooltip("캐릭터가 立っている 월드 空間 の 基本 Y 位置")]
    public float characterBaseY = 0.0f; // ユニティ ワールド 좌표 Y=0 (地面) 等

    // --- 신체 비율 기반 スケール 보정 설정 ---
    [Header("스케일 보정 설정")]
    [Tooltip("스케일 보정 基準 で 使用 する ランドマーク ペア（例：엉덩이）。左 ランドマーク。")]
    public PoseName scaleReferenceLandmarkLeft = PoseName.LEFT_HIP; // 스케일 基準 左 ランドマーク (PoseName enum 使用)
    [Tooltip("스케일 보정 基準 で 使用 する ランドマーク ペア（例：엉덩이）。右 ランドマーク。")]
    public PoseName scaleReferenceLandmarkRight = PoseName.RIGHT_HIP; // 스케일 基準 右 ランドマーク (PoseName enum 使用)

    [Tooltip("스케일 보정 系数 計算 時 最小 ランドマーク 間 距離。 この 値 より 小さい 場合 計算 오류 防止。")]
    public float minLandmarkDistanceForScale = 0.01f; // 0 で 割るの 防止 임계값

    [Tooltip("스케일 보정 系数 変動 に 滑らかさ を 適用 する 時間。0이면 即時 適用。")]
    public float scaleSmoothingTime = 0.1f; // 스케일 보정 系数 補間 時間

    // キャラクター 全体 回전 設定 (Inspector で 調整)
    [Header("캐릭터 회전 설정")]
    [Tooltip("キャラクターが 向く 方向を 決定 する 基準 ランドマーク ペア（例：어깨）。左 ランドマーク。")]
    public PoseName rotationLookAtLandmarkLeft = PoseName.LEFT_SHOULDER; // 회전 基準 左 ランドマーク (PoseName enum 使用)
    [Tooltip("キャラクターが 向く 方向を 決定 する 基準 ランドマーク ペア（例：어깨）。右 ランドマーク。")]
    public PoseName rotationLookAtLandmarkRight = PoseName.RIGHT_SHOULDER; // 회전 基準 右 ランドマーク (PoseName enum 使用)

    [Tooltip("MediaPipe ランドマーク 傾き を ユニティ 回전 に 変換 する 際に 使用 する スケール。 大きいほど 敏感に 回전。")]
    public float rotationScale = 1.0f; // 회전 敏感度 調整 スケール

    [Tooltip("キャラクターの 基本 回전 オフセット (Rotation)。MediaPipe 正面 と ユニティ 正面 が 異なる 場合 Y 軸の 値 を 主に 調整。")]
    public Vector3 baseRotationOffset = Vector3.zero; // 基本 回전 補正 (Inspector で 調整)

    // --- 손/발 回전 設定는 제거되었습니다 ---
    // [Header("손/발 回전 設定")]
    // public float limbRotationScale = 1.0f;
    // public Vector3 limbRotationOffset = Vector3.zero;

    // Y 位置 補間時 SmoothDamp 関数 使用のための 速度 変数 (クラス メンバー 変数 として 宣言)
    private Vector3 yVelocity = Vector3.zero;

    // Y 位置 アップデートの 基準となる MediaPipe Y 値 (キャラクターが 立っている 時の ヒップ 平均 Y 値)
    private float currentBaseLandmarkY = -1.0f; // 初期値 は 有効 でない 値 に 設定

    // スケール 補正 系数 計算時 SmoothDamp 関数 使用のための 速度 変数 (クラス メンバー 変数 として 宣言)
    private float scaleVelocity = 0.0f;

    // 現在 適用されている スケール 補正 系数
    private float currentScaleCorrectionFactor = 1.0f; // 初期値 は 1.0


    // Start is called before the first frame update
    void Start() // 初期 設定 (Animator 取得, キャラクター 関節 距離 計算)
    {
        // Animator コンポーネント 取得
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("[IKCharacter] Animator コンポーネントを 見つけられません！IKCharacter スクリプト 無効化。");
            enabled = false; // Animator がなければ スクリプト 無効化
            return;
        }

        // キャラクター モデルの 実際 関節 距離を 測定します。
        // IK 計算時 この 距離を 参考にして ターゲット 位置 の スケールを 調整 できます。
        Transform rightShoulderBone = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform rightElbowBone = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform rightWristBone = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightShoulderBone != null && rightElbowBone != null) rightShoulderToElbowDistance = Vector3.Distance(rightShoulderBone.position, rightElbowBone.position);
        else Debug.LogWarning("[IKCharacter] キャラクターの 右肩 または 肘 Bone(HumanBodyBones)を 見つけ られません。");
        if (rightElbowBone != null && rightWristBone != null) rightElbowToWristDistance = Vector3.Distance(rightElbowBone.position, rightWristBone.position);
        else Debug.LogWarning("[IKCharacter] キャラクターの 右肘 または 手首 Bone(HumanBodyBones)を 見つけ られません。");

        Transform leftShoulderBone = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); // <-- 変数名
        Transform leftElbowBone = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm); // <-- 変数名
        Transform leftWristBone = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        if (leftShoulderBone != null && leftElbowBone != null) leftShoulderToElbowDistance = Vector3.Distance(leftShoulderBone.position, leftElbowBone.position); // <-- 変数名
        else Debug.LogWarning("[IKCharacter] キャラクターの 左肩 または 肘 Bone(HumanBodyBones)を 見つけ られません。");
        // 오타 수정: leftElbone -> leftElbowBone
        if (leftElbowBone != null && leftWristBone != null) leftElbowToWristDistance = Vector3.Distance(leftElbowBone.position, leftWristBone.position); // <-- オタ 修正 済！
        else Debug.LogWarning("[IKCharacter] キャラクターの 左肘 または 手首 Bone(HumanBodyBones)を 見つけ られません。");

        Transform rightHipBone = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform rightKneeBone = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        Transform rightAnkleBone = anim.GetBoneTransform(HumanBodyBones.RightFoot); // HumanBodyBones.RightFootは 足首ではなく 足 Boneです。Rig 設定によっては 足首 Boneが ない 場合も あります。
        // もし Rig に 足首 Bone があれば HumanBodyBones.RightAnkle 使用を 考慮
        if (rightHipBone != null && rightKneeBone != null) rightHipToKneeDistance = Vector3.Distance(rightHipBone.position, rightKneeBone.position);
        else Debug.LogWarning("[IKCharacter] キャラクターの 右ヒップ または 膝 Bone(HumanBodyBones)を 見つけ られません。");
        if (rightKneeBone != null && rightAnkleBone != null) rightKneeToAnkleDistance = Vector3.Distance(rightKneeBone.position, rightAnkleBone.position); // 膝 -> 足 Bone 距離
        else Debug.LogWarning("[IKCharacter] キャラクターの 右膝 または 足 Bone(HumanBodyBones.RightFoot)を 見つけ られません。");

        Transform leftHipBone = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform leftKneeBone = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform leftAnkleBone = anim.GetBoneTransform(HumanBodyBones.LeftFoot); // HumanBodyBones.LeftFootは 足首ではなく 足 Boneです。
        // もし Rig に 足首 Bone があれば HumanBodyBones.LeftAnkle 使用を 考慮
        if (leftHipBone != null && leftKneeBone != null) leftHipToKneeDistance = Vector3.Distance(leftHipBone.position, leftKneeBone.position);
        else Debug.LogWarning("[IKCharacter] キャラクターの 左ヒップ または 膝 Bone(HumanBodyBones)を 見つけ られません。");
        if (leftKneeBone != null && leftAnkleBone != null) leftKneeToAnkleDistance = Vector3.Distance(leftKneeBone.position, leftAnkleBone.position); // 膝 -> 足 Bone 距離
        else Debug.LogWarning("[IKCharacter] キャラクターの 左膝 または 足 Bone(HumanBodyBones)を 見つけ られません。");

        // キャラクター ヒップ間 距離 測定 (スケール補正 基準)
        Transform characterRightHipBone = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform characterLeftHipBone = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        if (characterRightHipBone != null && characterLeftHipBone != null)
        {
            characterHipDistance = Vector3.Distance(characterRightHipBone.position, characterLeftHipBone.position);
            if (characterHipDistance < 0.0001f) // 距離が0に近い 場合 問題発生防止
            {
                Debug.LogError("[IKCharacter] キャラクター ヒップ間距離が0に近いです。Rig設定を確認してください。");
                enabled = false; // スクリプト無効化
                return;
            }
        }
        else { Debug.LogError("[IKCharacter] キャラクターヒップBoneを見つけられず、ヒップ間距離測定が 不可能 です。Rig設定を確認してください。"); enabled = false; return; }


        // NOTE: IK 適用自体は この スクリプトではなく、
        //      キャラクターRigにアタッチされているAnimation Rigging Constraint コンポーネント が 担当します。
        //      この スクリプトは、Constraint が 見る IK ターゲット オブジェクト の 位置 だけを 更新します。
    }

    // MediaPipe データから 最新 ランドマーク データと 可視化 オブジェクト Transform を 受け取り、処理 を 更新 する 関数
    // この 関数は MediaPipeData.OnLandmarkDataReceived から 呼び出されます。
    public void UpdateIKTargets(Dictionary<string, LandmarkPosition> landmarkPositions, Transform[] landmarkTransforms)
    {
        // 最新 データ 取得
        latestLandmarkPositions = landmarkPositions; // Python サーバー から 受け取った 元の 0-1 範囲 データ
        updatedLandmarkTransforms = landmarkTransforms; // MediaPipeData で Unity 座標 に 変換 され、位置 が 更新 された ランドマーク 可視化 オブジェクト の Transform 配列

        // データが 有効で、IK 適用 に 必要な Transform 配列 が 準備 できて いれば、IK ターゲット の 位置 を 計算し 更新します。
        // 実際 の IK 適用 は OnAnimatorIK では なく、Rigging システム によって 行われます。
        if (latestLandmarkPositions != null && updatedLandmarkTransforms != null && anim != null)
        {
            // --- 신체 비율 기반 スケール 보정 계수 계산 ---
            CalculateScaleCorrectionFactor(); // 이 함수 정의는 아래에 있습니다!

            // 1. キャラクター 全体 の 位置 と 回전 を 更新 する ロジック を 追加
            UpdateCharacterPositionAndRotation(); // 이 함수 정의는 아래에 있습니다!

            // 2. 各 部位 の IK ターゲット 位置 を 計算し 更新 する 関数 を 呼び出し
            CalculateRightHandIKTarget(); // <-- 이 함수 정의는 아래에 있습니다!
            CalculateLeftHandIKTarget();  // <-- 이 함수 정의는 아래에 있습니다!
            CalculateRightFootIKTarget(); // <-- 이 함수 정의는 아래에 있습니다!
            CalculateLeftFootIKTarget();  // <-- 이 함수 정의는 아래에 있습니다!

            // 3. 肘/膝 の ポール ターゲット 位置 を 計算 する 関数 を 呼び出し（オプション）
            CalculateRightElbowPoleTarget(); // <-- 이 함수 정의는 아래에 있습니다!
            CalculateLeftElbowPoleTarget();  // <-- 이 함수 정의는 아래에 있습니다!
            CalculateRightKneePoleTarget();  // <-- 이 함수 정의는 아래에 있습니다!
            CalculateLeftKneePoleTarget();   // <-- 이 함수 정의는 아래에 있습니다!

            // --- 手/足 回전 アップデート ロジック は 削除 されました ---
            // CalculateLimbRotations(); // 이 함수 정의는 이제 호출되지 않습니다.

            // 5. 머리 回전 アップデート ロジック 追加（옵션）
            CalculateHeadRotation(); // <-- 이 함수 정의는 아래에 있습니다!

        }
        // else { Debug.LogWarning("[IKCharacter] 受け取った ランドマーク データ または 可視化 オブジェクト Transform 配列 が 無効 な ため、IK ターゲット の 更新 を スキップ します。"); }
    }

    // 신체 비율 기반 스케일 보정 계수를 계산하는 함수
    void CalculateScaleCorrectionFactor() // 이 함수 정의가 여기에 있어야 함!
    {
        // スケール補正 基準 と なる ランドマーク ペア の 可視化 オブジェクト Transform を 取得
        Transform scaleRefLeftVis = GetLandmarkTransform(scaleReferenceLandmarkLeft);
        Transform scaleRefRightVis = GetLandmarkTransform(scaleReferenceLandmarkRight);

        float targetScaleCorrectionFactor = currentScaleCorrectionFactor; // 基本 的に 現在 の スケール を 維持

        if (scaleRefLeftVis != null && scaleRefRightVis != null && characterHipDistance > 0.0001f)
        {
            // MediaPipe ランドマーク 間 距離 を 測定 (Unity 座標 に 変換 された 可視化 オブジェクト 基準)
            // XZ 平面 上 の 距離 を 使用 して スケール 変化 を 見る の が 安定 的 で ある 可能性 が あります。
            Vector3 landmarkLeftPosFlat = new Vector3(scaleRefLeftVis.position.x, 0, scaleRefLeftVis.position.z);
            Vector3 landmarkRightPosFlat = new Vector3(scaleRefRightVis.position.x, 0, scaleRefRightVis.position.z);
            float currentMediaPipeScaleDistance = Vector3.Distance(landmarkLeftPosFlat, landmarkRightPosFlat); // XZ 平面 距離 使用 例

            // または 3D 距離 使用
            // float currentMediaPipeScaleDistance = Vector3.Distance(scaleRefLeftVis.position, scaleRefRightVis.position); // 3D 距離 使用 例


            // MediaPipe ランドマーク 間 距離 が 小さ すぎる 場合 (データ が 不安 定 で あるか 表示 されない 場合) 計算 を 無視
            if (currentMediaPipeScaleDistance > minLandmarkDistanceForScale)
            {
                // スケール補正 系数 を 計算 (キャラクター ヒップ間 距離 を MediaPipe ランドマーク 間 距離 で 割る)
                targetScaleCorrectionFactor = characterHipDistance / currentMediaPipeScaleDistance;

                // Debug.Log($"[IKCharacter] MediaPipe Scale Dist: {currentMediaPipeScaleDistance:F4}, Target Scale Factor: {targetScaleCorrectionFactor:F4}"); // デバッグ 用
            }
            // else { Debug.LogWarning($"[IKCharacter] MediaPipe スケール 基準 ランドマーク 間 距離 が 小さ すぎます。 スケール 補正 を 無視 します。"); }
        }
        // else { Debug.LogWarning($"[IKCharacter] スケール 補正 基準 ランドマーク ({scaleReferenceLandmarkLeft}, {scaleReferenceLandmarkRight}) の Transform が 見つかりません。"); }


        // 計算 された 目標 スケール 補正 系数 を 現在 スケール 補正 系数 に SmoothDamp を 使用 して 滑らか に アップデート
        currentScaleCorrectionFactor = Mathf.SmoothDamp(currentScaleCorrectionFactor, targetScaleCorrectionFactor, ref scaleVelocity, scaleSmoothingTime);

        // Debug.Log($"[IKCharacter] 現在 スケール 補正 系数: {currentScaleCorrectionFactor:F4}"); // デバッグ 用
    }


    // キャラクター 全体 の 位置 と 回전 を アップデート する 関数 （Y 位置 임계 값 適用、スケール 補正 適用）
    void UpdateCharacterPositionAndRotation() // 이 함수 정의가 여기에 있어야 함!
    {
        // キャラクター オブジェクト（この スクリプト が アタッチ されて いる オブジェクト） の Transform を 取得
        Transform characterTransform = this.transform;

        // 1. キャラクター 全体 の 位置 更新 （XZ 平面 移動 および Y 高さ）
        // キャラクター 位置 の 基準 と なる ランドマーク 位置 を 取得（例：両 ヒップ の 平均 - 元の 0-1 範囲 データ）
        LandmarkPosition leftHipPos = GetLandmarkPosition(PoseName.LEFT_HIP);
        LandmarkPosition rightHipPos = GetLandmarkPosition(PoseName.RIGHT_HIP);

        if (leftHipPos != null && rightHipPos != null)
        {
            // 両 ヒップ ランドマーク の 平均 位置（元の 0-1 範囲）
            float averageHipX_01 = (leftHipPos.x + rightHipPos.x) / 2.0f;
            float averageHipY_01 = (leftHipPos.y + rightHipPos.y) / 2.0f;
            // float averageHipZ_01 = (leftHipPos.z + rightHipPos.z) / 2.0f; // Z 値は 位置 に 適用 しない （Z 移動 除外 バージョン）

            // ランドマーク X 座標 を Unity ワールド X 位置 に 変換
            // 画像 中央（0.5）を Unity X=0 に 合わせる 方式
            // ここに スケール 補正 系数 currentScaleCorrectionFactor を 掛けて キャラクター 全体 スケール に 合わせる
            float unityX = (averageHipX_01 - 0.5f) * baseMovementScale * currentScaleCorrectionFactor; // <-- unityX 計算 時 スケール 補正 系数 適用

            // Z 位置 は 現在 の キャラクター の Z 位置 を 維持
            float unityZ = characterTransform.position.z;


            // --- XZ 位置 は 매 フレーム アップデート ---
            // Y 位置 は 임계 값 ロジック に 따라서 条件 付き で アップデート されます。
            // 計算 された X, Z 位置 で キャラクター オブジェクト の 位置 を 更新 します （Y 位置 は 現在 の 値 を 維持し、 下 の Y アップデート ロジック によって 変更 さ れる 可能性 が あります）。
            characterTransform.position = new Vector3(unityX, characterTransform.position.y, unityZ);


            // --- MediaPipe Y 値（0-1 範囲）を Unity ワールド Y 位置 に 変換 および 更新 （임계 값 適用） ---
            // ユーザーが カメラ から 遠ざかっ たり 近づい たり し た 際 の Y 位置 の 揺れ 問題 を 解決 します。

            float currentUnityY = characterTransform.position.y; // キャラクター の 現在 の Unity Y 位置

            // currentBaseLandmarkY 初期化：最初 に 有効 な データ を 受け取っ た 時 の ヒップ 平均 Y 値 を 基準 と して 設定
            // currentBaseLandmarkY が 初期 値（-1.0f） より 小さい 場合＝まだ 初期 化 さ れて い ない 場合
            if (currentBaseLandmarkY < 0)
            {
                currentBaseLandmarkY = averageHipY_01; // 現在 の ヒップ 平均 Y 値 を 初期 基準 値 と して 設定
                Debug.Log($"[IKCharacter] Y 位置 基準点 初期化：currentBaseLandmarkY = {currentBaseLandmarkY:F4}");

                // 初期 位置 設定 時、Y 位置 も 計算 し 即時 適用 します。
                // baseLandmarkY（基準 Y 値） と 初期 基準 値（currentBaseLandmarkY） の 差 に scale を 掛け、characterBaseY に 足し ます。
                // verticalMovementScale 変数 使用 -> baseMovementScale 使用 および スケール 補正 系数 適用
                // ここ で verticalMovementScale は Y 軸 に対 する 追加 的 な スケール 調整 に 使用 でき ます。baseMovementScale は XZ を 含む 基本 的 な スケール です。
                float initialUnityY = characterBaseY + (baseLandmarkY - currentBaseLandmarkY) * verticalMovementScale * currentScaleCorrectionFactor; // <-- initialUnityY 計算 時 verticalMovementScale 使用 および スケール 補正 系数 適用

                // キャラクター の Y 位置 だけ を 更新
                characterTransform.position = new Vector3(characterTransform.position.x, initialUnityY, characterTransform.position.z);

                // 初期 化 後、この フレーム で は 位置 更新 ロジック を これ 以上 行 わ ない で 終了 （オプション）
                // return; // この return 主 석 を 解除 すれ ば 初期 化 時 回전 計算 は スキップ さ れ ます。
            }
            else // すでに 基準 Y 値 が 初期 化 さ れ て いる 場合
            {
                // ヒップ 平均 Y 値 が 基準点（currentBaseLandmarkY） から 일정 임계 값 以上 逸 脱 し た か 確認
                // Mathf.Abs(値) は 絶対 値 を 返 환 し ます。
                if (Mathf.Abs(averageHipY_01 - currentBaseLandmarkY) > yMovementThreshold)
                {
                    // ヒップ Y 値 が 基準点 から 大きく 逸 脱 し た 場合 （ジャンプ または 座り 推 定）
                    // 新しい 基準 Y 値 を 現在 の ヒップ 平均 Y 値 と し て 更新
                    currentBaseLandmarkY = averageHipY_01;
                    Debug.Log($"[IKCharacter] Y 位置 アップデート 条件 充足！ 新しい 基準点：{currentBaseLandmarkY:F4}");

                    // 新しい 基準 Y 値 と characterBaseY を 使用 し て キャラクター 目標 Y 位置 を 計算
                    // verticalMovementScale を 使用 し て Y 移動 スケール を 適用 -> baseMovementScale 使用 および スケール 補正 系数 を 適用
                    float targetUnityY = characterBaseY + (baseLandmarkY - currentBaseLandmarkY) * verticalMovementScale * currentScaleCorrectionFactor; // <-- targetUnityY 計算 時 verticalMovementScale 使用 および スケール 補正 系数 を 適用

                    // 計算 された 目標 Y 位置 に キャラクター Y 位置 を アップデート (SmoothDamp を 使用 し て 滑らか に 移動)
                    // ySmoothTime が 0 で あれ ば 即時 移動
                    float newUnityY = Mathf.SmoothDamp(characterTransform.position.y, targetUnityY, ref yVelocity.y, ySmoothTime);

                    // キャラクター の Y 位置 だけ を 更新
                    characterTransform.position = new Vector3(characterTransform.position.x, newUnityY, characterTransform.position.z);
                }
                // else ブロック で は Y 位置 を 更新 し ませ ん。 XZ 位置 だけ アップデート さ れ ます。
                // Y 位置 は XZ アップデート コード (上 の characterTransform.position = new Vector3(unityX, characterTransform.position.y, unityZ);) に よ り 現在 の 値 を 維持し ます。
            }


            // 2. キャラクター 全体 の 回전 を アップデート
            // キャラクター が 向く 方向 を 決定 する 基準 ランドマーク ペア（例：お 肩） を 取得
            Transform lookAtLeftVis = GetLandmarkTransform(rotationLookAtLandmarkLeft);
            Transform lookAtRightVis = GetLandmarkTransform(rotationLookAtLandmarkRight); // <-- フィールド 値 を 使用 する よう 修正

            // 回전 計算 に 必要 な ランドマーク Transform が 有効 か 確認
            if (lookAtLeftVis != null && lookAtRightVis != null)
            {
                // キャラクター の '右' 方向 ベクトル を 取得 し、それ を 基準 に Y 軸 回전 を 計算
                // この 方法 は 比較的 自然 な Y 軸 回전 結果 を 提供 でき ます。
                // 基準 ランドマーク の ワールド 座標 位置 を 使用 し ます。
                Vector3 leftLandmarkWorld = lookAtLeftVis.position;
                Vector3 rightLandmarkWorld = lookAtRightVis.position;

                // ランドマーク 2 つ を 結ぶ ベクトル (左 -> 右)
                Vector3 landmarkWidthVectorWorld = rightLandmarkWorld - leftLandmarkWorld;

                // キャラクター の '右' 方向 ベクトル を 推定 し ます (MediaPipe ランドマーク 基準)。
                // この ベクトル は MediaPipe 可視化 オブジェクト の ワールド 座標系 に あり ます。
                Vector3 estimatedCharacterRightDirectionWorld = landmarkWidthVectorWorld.normalized;

                // この 推定 さ れ た '右' 方向 ベクトル と Unity ワールド 座標系 の '前' (Vector3.forward) または '右' (Vector3.right) 軸 を 比較 し て
                // キャラクター が どの 方向 を 向い て いる か 推定 し ます。
                // 通常 肩 ベクトル に 垂直 で Y 軸 に 平行 な ベクトル を キャラクター の '正面' 方向 と し て 使用 し ます。
                // estimatedCharacterRightDirectionWorld と Vector3.up の 両方 に 垂直 な ベクトル -> 前/後 方向
                Vector3 estimatedCharacterForwardWorld = Vector3.Cross(estimatedCharacterRightDirectionWorld, Vector3.up).normalized;

                // この 推定 さ れ た '正面' 方向 ベクトル を 使用 し て キャラクター の 目標 回전 Quaternion を 計算 し ます。
                // Quaternion.LookRotation(forward, upwards) 関数 使用 (upwards は 通常 Vector3.up 使用)
                Quaternion targetRotation = Quaternion.LookRotation(estimatedCharacterForwardWorld, Vector3.up);

                // キャラクター オブジェクト の Y 軸 回전 だけ を 適用 (X, Z 回전 は 基本 アニメーション/Rigging に 任せる)
                // rotationScale を 回전 角度 に 掛け て 回전 敏感度 を 調整 し ます。
                // Quaternion.Euler は 角度 を 受け取る ため、targetRotation.eulerAngles.y に rotationScale を 掛け ます。
                Quaternion finalRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y * rotationScale, 0);

                // 基本 回전 オフセット を 適用 (Inspector で 調整 可能 な baseRotationOffset)
                finalRotation = finalRotation * Quaternion.Euler(baseRotationOffset);

                // 滑らか さ なし で 即時 回전 適用
                characterTransform.rotation = finalRotation;
                // TODO: 滑らか な 回전 適用 (Quaternion.Slerp 使用)
            }
        }
        // else { Debug.LogWarning("[IKCharacter] 受け取っ た ランドマーク データ また は 可視化 オブジェクト Transform 配列 が 無効 な ため、 アップデート を スキップ し ます。"); }

    }

    // --- IK ターゲット 位置 計算 関数 定義 ---
    // 各 部位 の IK ターゲット の 位置 を 計算し 更新 し ます。

    // 右手 IK ターゲット 位置 計算 および 更新
    void CalculateRightHandIKTarget() // この 関数 定義 が ここ に 必要 です！
    {
        if (rigRightHandTarget == null || anim == null) return;

        Transform rightShoulderVis = GetLandmarkTransform(PoseName.RIGHT_SHOULDER);
        Transform rightElbowVis = GetLandmarkTransform(PoseName.RIGHT_ELBOW);
        Transform rightWristVis = GetLandmarkTransform(PoseName.RIGHT_WRIST);

        if (rightShoulderVis != null && rightElbowVis != null && rightWristVis != null)
        {
            // キャラクター 肩 ボーン 位置 を 基準 に 相対 的 な IK ターゲット 位置 を 計算 し ます。
            // 受け取っ た ランドマーク 可視化 オブジェクト 位置 (ワールド 座標) から キャラクター 肩 ボーン 位置 (ワールド 座標) を 引い て 相対 的 な ベクトル を 取得 し、
            // この ベクトル を キャラクター 肩 ボーン 位置 に 足す 方法 で キャラクター 移動 と IK を 自然 に 連動 させ ます。
            Transform characterRightShoulderBone = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (characterRightShoulderBone != null)
            {
                // 右 手首 可視化 オブジェクト 位置 から キャラクター 右 肩 ボーン 位置 を 引い て 相対 ベクトル を 取得
                Vector3 relativeTargetFromShoulder = rightWristVis.position - characterRightShoulderBone.position;

                // IK ターゲット 位置 に スケール 補正 系数 currentScaleCorrectionFactor を 適用
                Vector3 adjustedRelativeTarget = relativeTargetFromShoulder * currentScaleCorrectionFactor;

                // キャラクター 右 肩 ボーン 位置 + 調整 さ れ た 相対 ベクトル = 最終 IK ターゲット ワールド 位置
                Vector3 targetPos = characterRightShoulderBone.position + adjustedRelativeTarget;

                // rig の 右 手 ターゲット の 位置 を 計算 さ れ た targetPos に 設定
                rigRightHandTarget.position = targetPos;
                // TODO: 右 手首 回전 設定 は CalculateRightHandRotation 関数 で 行い ます （現在 の コード で は 回전 ロジック は 削除 さ れ て い ます）
            }
        }
    }

    // 左手 IK ターゲット 位置 計算 および 更新
    void CalculateLeftHandIKTarget() // <-- 이 함수 정의가 여기에 있어야 함!
    {
        if (rigLeftHandTarget == null || anim == null) return;

        Transform leftShoulderVis = GetLandmarkTransform(PoseName.LEFT_SHOULDER);
        Transform leftElbowVis = GetLandmarkTransform(PoseName.LEFT_ELBOW);
        Transform leftWristVis = GetLandmarkTransform(PoseName.LEFT_WRIST);

        if (leftShoulderVis != null && leftElbowVis != null && leftWristVis != null)
        {
            // キャラクター 肩 ボーン 位置 を 基準 に 相対 的 な IK ターゲット 位置 を 計算 し ます。
            Transform characterLeftShoulderBone = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (characterLeftShoulderBone != null)
            {
                // 左 手首 可視化 オブジェクト 位置 から キャラクター 左 肩 ボーン 位置 を 引い て 相対 ベクトル を 取得
                Vector3 relativeTargetFromShoulder = leftWristVis.position - characterLeftShoulderBone.position;

                // IK ターゲット 位置 に スケール 補正 系数 currentScaleCorrectionFactor を 適用
                Vector3 adjustedRelativeTarget = relativeTargetFromShoulder * currentScaleCorrectionFactor;


                // キャラクター 左 肩 ボーン 位置 + 調整 さ れ た 相対 ベクトル = 最終 IK ターゲット ワールド 位置
                Vector3 targetPos = characterLeftShoulderBone.position + adjustedRelativeTarget;

                // rig の 左 手 ターゲット の 位置 を 計算 さ れ た targetPos に 設定
                rigLeftHandTarget.position = targetPos;
                // TODO: 左 手首 回전 設定 は CalculateLeftHandRotation 関数 で 行い ます （現在 の コード で は 回전 ロジック は 削除 さ れ て い ます）
            }
        }
    }

    // 右足 IK ターゲット 位置 計算 および 更新
    void CalculateRightFootIKTarget() // <-- 이 함수 정의가 여기에 있어야 함!
    {
        if (rigRightFootTarget == null || anim == null) return;

        Transform rightHipVis = GetLandmarkTransform(PoseName.RIGHT_HIP);
        Transform rightKneeVis = GetLandmarkTransform(PoseName.RIGHT_KNEE);
        Transform rightAnkleVis = GetLandmarkTransform(PoseName.RIGHT_ANKLE); // 足首
        // Transform rightFootEndVis = GetLandmarkTransform(PoseName.RIGHT_FOOT_INDEX); // 足 の つま先 を 使用 考慮

        if (rightHipVis != null && rightKneeVis != null && rightAnkleVis != null) // rightFootEndVis を 使用 する 場合 条件 を 変更
        {
            // キャラクター ヒップ ボーン 位置 を 基準 に 相対 的 な IK ターゲット 位置 を 計算 し ます。
            // 受け取っ た ランドマーク 可視化 オブジェクト 位置 (ワールド 座標) から キャラクター ヒップ ボーン 位置 (ワールド 座標) を 引い て 相対 的 な ベクトル を 取得 し、
            // この ベクトル を キャラクター ヒップ ボーン 位置 に 足す 方法 で キャラクター 移動 と IK を 自然 に 連動 させ ます。
            Transform characterRightHipBone = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg); // ヒップ ボーン 使用

            if (characterRightHipBone != null)
            {
                // 右 足首 ランドマーク 可視化 オブジェクト 位置 から キャラクター 右 ヒップ ボーン 位置 を 引い て 相対 ベクトル を 取得
                Vector3 relativeTargetFromHip = rightAnkleVis.position - characterRightHipBone.position; // 足首 ランドマーク 可視化 オブジェクト 位置 使用

                // IK ターゲット 位置 に スケール 補正 系数 currentScaleCorrectionFactor を 適用
                Vector3 adjustedRelativeTarget = relativeTargetFromHip * currentScaleCorrectionFactor;

                // キャラクター 右 ヒップ ボーン 位置 + 調整 さ れ た 相対 ベクトル = 最終 IK ターゲット ワールド 位置
                Vector3 targetPos = characterRightHipBone.position + adjustedRelativeTarget;

                // rig の 右 足 ターゲット の 位置 を 計算 さ れ た targetPos に 設定
                rigRightFootTarget.position = targetPos;

                // TODO: 右 足 回전 設定 は CalculateRightFootRotation 関数 で 行い ます （現在 の コード で は 回전 ロジック は 削除 さ れ て い ます）
            }
        }
    }

    // 左足 IK ターゲット 位置 計算 および 更新
    void CalculateLeftFootIKTarget() // <-- 이 함수 정의가 여기에 있어야 함!
    {
        if (rigLeftFootTarget == null || anim == null) return;

        Transform leftHipVis = GetLandmarkTransform(PoseName.LEFT_HIP);
        Transform leftKneeVis = GetLandmarkTransform(PoseName.LEFT_KNEE);
        Transform leftAnkleVis = GetLandmarkTransform(PoseName.LEFT_ANKLE); // 足首
        // Transform leftFootEndVis = GetLandmarkTransform(PoseName.LEFT_FOOT_INDEX); // 足 の つま先 を 使用 考慮

        if (leftHipVis != null && leftKneeVis != null && leftAnkleVis != null) // leftFootEndVis を 使用 する 場合 条件 を 変更
        {
            // キャラクター ヒップ ボーン 位置 を 基準 に 相対 的 な IK ターゲット 位置 を 計算 し ます。
            Transform characterLeftHipBone = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg); // ヒップ ボーン 使用

            if (characterLeftHipBone != null)
            {
                // 左 足首 ランドマーク 可視化 オブジェクト 位置 から キャラクター 左 ヒップ ボーン 位置 を 引い て 相対 ベクトル を 取得
                Vector3 relativeTargetFromHip = leftAnkleVis.position - characterLeftHipBone.position; // 足首 ランドマーク 可視化 オブジェクト 位置 使用

                // IK ターゲット 位置 に スケール 補正 系数 currentScaleCorrectionFactor を 適用
                Vector3 adjustedRelativeTarget = relativeTargetFromHip * currentScaleCorrectionFactor;

                // キャラクター 左 ヒップ ボーン 位置 + 調整 さ れ た 相対 ベクトル = 最終 IK ターゲット ワールド 位置
                Vector3 targetPos = characterLeftHipBone.position + adjustedRelativeTarget;

                // rig の 左 足 ターゲット の 位置 を 計算 さ れ た targetPos に 設定
                rigLeftFootTarget.position = targetPos;
                // TODO: 左 足 回전 設定 は CalculateLeftFootRotation 関数 で 行い ます （現在 の コード で は 回전 ロジック は 削除 さ れ て い ます）
            }
        }
    }


    // 右肘 ポール ターゲット 位置 計算 および 更新 (選択 事項)
    void CalculateRightElbowPoleTarget() // <-- 이 함수 정의가 여기에 있어야 함!
    {
        if (rigRightElbowTarget == null || anim == null) return;
        Transform rightShoulderVis = GetLandmarkTransform(PoseName.RIGHT_SHOULDER);
        Transform rightElbowVis = GetLandmarkTransform(PoseName.RIGHT_ELBOW);
        Transform rightWristVis = GetLandmarkTransform(PoseName.RIGHT_WRIST);
        if (rightShoulderVis != null && rightElbowVis != null && rightWristVis != null)
        {
            Vector3 shoulderToWristMidpoint = Vector3.Lerp(rightShoulderVis.position, rightWristVis.position, 0.5f);
            Vector3 elbowOffsetDirection = (rightElbowVis.position - shoulderToWristMidpoint).normalized;
            Transform characterRightElbowBone = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (characterRightElbowBone != null)
            {
                float poleTargetOffsetDistance = 1.0f;
                Vector3 poleTargetPos = characterRightElbowBone.position + elbowOffsetDirection * poleTargetOffsetDistance;
                rigRightElbowTarget.position = poleTargetPos;
            }
        }
    }

    // 左肘 ポール ターゲット 位置 計算 および 更新 (選択 事項)
    void CalculateLeftElbowPoleTarget() // <-- 이 함수 정의가 여기에 있어야 함!
    {
        if (rigLeftElbowTarget == null || anim == null) return;
        Transform leftShoulderVis = GetLandmarkTransform(PoseName.LEFT_SHOULDER);
        Transform leftElbowVis = GetLandmarkTransform(PoseName.LEFT_ELBOW);
        Transform leftWristVis = GetLandmarkTransform(PoseName.LEFT_WRIST);
        if (leftShoulderVis != null && leftElbowVis != null && leftWristVis != null)
        {
            Vector3 shoulderToWristMidpoint = Vector3.Lerp(leftShoulderVis.position, leftWristVis.position, 0.5f);
            Vector3 elbowOffsetDirection = (leftElbowVis.position - shoulderToWristMidpoint).normalized;

            Transform characterLeftElbowBone = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (characterLeftElbowBone != null)
            {
                float poleTargetOffsetDistance = 1.0f;
                Vector3 poleTargetPos = characterLeftElbowBone.position + elbowOffsetDirection * poleTargetOffsetDistance;
                rigLeftElbowTarget.position = poleTargetPos;
            }
        }
    }

    // 右膝 ポール ターゲット 位置 計算 および 更新 (選択 事項)
    void CalculateRightKneePoleTarget() // <-- 이 함수 정의가 여기에 있어야 함!
    {
        if (rigRightKneeTarget == null || anim == null) return;
        Transform rightHipVis = GetLandmarkTransform(PoseName.RIGHT_HIP);
        Transform rightKneeVis = GetLandmarkTransform(PoseName.RIGHT_KNEE);
        Transform rightAnkleVis = GetLandmarkTransform(PoseName.RIGHT_ANKLE);

        if (rightHipVis != null && rightKneeVis != null && rightAnkleVis != null)
        {
            Vector3 hipToAnkleMidpoint = Vector3.Lerp(rightHipVis.position, rightAnkleVis.position, 0.5f);
            Vector3 kneeOffsetDirection = (rightKneeVis.position - hipToAnkleMidpoint).normalized;

            Transform characterRightKneeBone = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            if (characterRightKneeBone != null)
            {
                float poleTargetOffsetDistance = 1.0f;
                Vector3 poleTargetPos = characterRightKneeBone.position + kneeOffsetDirection * poleTargetOffsetDistance;

                rigRightKneeTarget.position = poleTargetPos;
            }
        }
    }

    // 左膝 ポール ターゲット 位置 計算 および 更新 (選択 事項)
    void CalculateLeftKneePoleTarget() // <-- 이 함수 정의가 여기에 있어야 함!
    {
        if (rigLeftKneeTarget == null || anim == null) return;
        Transform leftHipVis = GetLandmarkTransform(PoseName.LEFT_HIP);
        Transform leftKneeVis = GetLandmarkTransform(PoseName.LEFT_KNEE);
        Transform leftAnkleVis = GetLandmarkTransform(PoseName.LEFT_ANKLE);
        if (leftHipVis != null && leftKneeVis != null && leftAnkleVis != null)
        {
            Vector3 hipToAnkleMidpoint = Vector3.Lerp(leftHipVis.position, leftAnkleVis.position, 0.5f);
            Vector3 kneeOffsetDirection = (leftKneeVis.position - hipToAnkleMidpoint).normalized;
            Transform characterLeftKneeBone = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            if (characterLeftKneeBone != null)
            {
                float poleTargetOffsetDistance = 1.0f;
                Vector3 poleTargetPos = characterLeftKneeBone.position + kneeOffsetDirection * poleTargetOffsetDistance;

                rigLeftKneeTarget.position = poleTargetPos;
            }
        }
    }


    // --- 손/발 회전 계산 함수 정의 (현재 코드에서는 호출되지 않음) ---
    // IK 타겟 오브젝트의 회전을 계산하고 설정하는 함수입니다.
    // 이 회전은 Animation Rigging의 RotationConstraint 등에 사용될 수 있습니다.
    // UpdateIKTargets 함수에서 CalculateLimbRotations() 함수 호출 부분을 주석 해제하면 사용됩니다.

    // CalculateLimbRotations 함수 (현재 코드에서는 호출되지 않음)
    // void CalculateLimbRotations() { ... }

    // 오른쪽 손목 회전 계산 및 rigRightHandRotationTarget 오브젝트에 설정 (현재 코드에서는 호출되지 않음)
    // void CalculateRightHandRotation() { ... }

    // 왼쪽 손목 회전 계산 및 rigLeftHandRotationTarget 오브젝트에 설정 (현재 코드에서는 호출되지 않음)
    // void CalculateLeftHandRotation() { ... }

    // 오른쪽 발 회전 계산 및 rigRightFootRotationTarget 오브젝트에 설정 (현재 코드에서는 호출되지 않음)
    // void CalculateRightFootRotation() { ... }

    // 왼쪽 발 회전 계산 및 rigLeftFootRotationTarget 오브젝트에 설정 (현재 코드에서는 호출되지 않음)
    // void CalculateLeftFootRotation() { ... }


    // --- 머리 회전 계산 함수 정의 (옵션) ---
    void CalculateHeadRotation() // 이 함수 정의가 여기에 있어야 합니다!
    {
        // 머리 회전을 계산하려면 코, 귀 랜드마크 위치가 필요합니다.
        Transform noseVis = GetLandmarkTransform(PoseName.NOSE); // 코
        Transform leftEarVis = GetLandmarkTransform(PoseName.LEFT_EAR); // 왼쪽 귀
        Transform rightEarVis = GetLandmarkTransform(PoseName.RIGHT_EAR); // 오른쪽 귀

        // RotationTarget 오브젝트가 할당되었는지 확인
        if (rigHeadRotationTarget == null || noseVis == null || leftEarVis == null || rightEarVis == null) return;

        // 머리의 '앞' 방향과 '위' 방향 벡터를 추정하여 회전 계산
        // 머리의 '오른쪽' 방향: 왼쪽 귀 -> 오른쪽 귀 벡터 사용 (normalized)
        Vector3 leftEarWorld = leftEarVis.position;
        Vector3 rightEarWorld = rightEarVis.position;
        Vector3 earWidthVector = rightEarWorld - leftEarWorld;
        Vector3 estimatedHeadRight = earWidthVector.normalized;

        // 머리의 '앞' 방향: 코 -> 양 귀 중간점 벡터 사용 (normalized)
        Vector3 earMidpoint = Vector3.Lerp(leftEarWorld, rightEarWorld, 0.5f); // 양 귀 중간점
        Vector3 noseToEarMid = noseVis.position - earMidpoint; // 코 -> 귀 중간점 벡터
        Vector3 estimatedHeadForward = noseToEarMid.normalized;

        // estimatedHeadForward와 estimatedHeadRight에 모두 수직인 벡터는 대략 머리의 '위' 방향을 나타낼 수 있습니다.
        // Vector3 estimatedHeadUp = Vector3.Cross(estimatedHeadRight, estimatedHeadForward).normalized; // 오른쪽 -> 앞 외적

        // 간단하게 estimatedHeadForward를 머리의 '앞'(Forward) 방향으로 사용하여 회전 계산
        // LookRotation(forward, upwards) 사용 (upwards는 대략 위 방향)
        // Vector3 estimatedHeadUpSimple = Vector3.Cross(estimatedHeadForward, estimatedHeadRight).normalized; // 앞 방향과 오른쪽 방향에 수직
        Vector3 estimatedHeadUpSimple = Vector3.up; // 간단하게 월드 UP 사용

        if (estimatedHeadForward.sqrMagnitude > 0.0001f) // 앞 방향 벡터 길이가 0이 아닌지 확인
        {
            // estimatedHeadForward를 '앞' 방향, estimatedHeadUpSimple을 '위' 방향으로 사용하여 목표 회전 계산
            Quaternion targetRotation = Quaternion.LookRotation(estimatedHeadForward, estimatedHeadUpSimple);

            // Rigging Constraint에 의해 사용될 rigHeadRotationTarget 오브젝트의 회전에 설정
            rigHeadRotationTarget.rotation = targetRotation;
            // TODO: 부드러운 회전 적용 (Quaternion.Slerp 사용)
        }
    }


    // OnAnimatorIK 콜백 함수는 삭제합니다. Rigging 시스템 사용.
    // void OnAnimatorIK(int layerIndex) { }


    // MediaPipeData에서 받은 updatedLandmarkTransforms 배열에서 랜드마크 이름에 해당하는 Transform을 찾는 헬퍼 함수
    // MediaPipeData에서 Unity 좌표로 변환되고 可視化 오브젝트의 Transform입니다.
    private Transform GetLandmarkTransform(PoseName landmarkEnum)
    {
        int index = (int)landmarkEnum;
        if (updatedLandmarkTransforms != null && index >= 0 && index < updatedLandmarkTransforms.Length)
        {
            return updatedLandmarkTransforms[index];
        }
        return null;
    }

    // MediaPipeData에서 받은 원본 LandmarkPosition Dictionary에서 랜드마크 이름에 해당하는 LandmarkPosition을 찾는 헬퍼 함수
    // Python 서버에서 보내진 0-1 범위의 원래 데이터입니다. 캐릭터 전체 위치 계산 등에 사용됩니다.
    private LandmarkPosition GetLandmarkPosition(PoseName landmarkEnum)
    {
        string landmarkName = landmarkEnum.ToString(); // Enum 이름을 문자열로 변환
        if (latestLandmarkPositions != null && latestLandmarkPositions.TryGetValue(landmarkName, out LandmarkPosition pos))
        {
            return pos; // 해당 이름의 LandmarkPosition 오브젝트 반환
        }
        return null;
    }


    // 오브젝트가 파괴될 때 자동으로 호출됩니다.
    // void OnDestroy() { } // MonoBehaviour에서 자동으로 호출됩니다.
}
