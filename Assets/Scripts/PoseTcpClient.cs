using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent; // 백그라운드 스레드와 메인 스레드 간 데이터 전달용 큐
using System.Collections; // 코루틴 사용
using System.IO; // StreamReader 사용
using System.Threading.Tasks; // Task 기반 비동기 처리를 위해 추가
using System.Collections.Generic; // Dictionary 사용을 위해 추가 (주석 참고)
// using System.Reflection; // Reflection은 MediaPipeData로 옮겨졌으므로 여기서는 필요 없을 수 있습니다.


// ===============================================================================================
// NOTE: 이 PoseTcpClient.cs 파일에는 아래 데이터 구조 클래스들을 정의하지 마세요!
//       LandmarkPosition, AllLandmarksData_ForJsonUtility, PoseData_ForJsonUtility 클래스는
//       별도의 LandmarkDataStructures.cs 파일에 정의되어 있어야 합니다.
//       이 스크립트는 해당 파일을 참조하여 클래스를 사용합니다.
// ===============================================================================================


public class PoseTcpClient : MonoBehaviour
{
    [Header("TCP 설정")]
    public string serverIP = "127.0.0.1"; // 파이썬 서버 IP (보통 로컬이면 이거 쓰면 됨)
    public int serverPort = 5005; // 파이썬 서버 포트
    public float reconnectDelay = 3f; // 연결 끊어졌을 때 재접속 시도 간격 (초)

    // MediaPipeData 스크립트 참조 필드 (Inspector에서 연결)
    [Header("연동 스크립트")]
    [Tooltip("씬에 있는 MediaPipeData 스크립트가 붙은 오브젝트를 연결하세요.")]
    public MediaPipeData mediaPipeDataManager;

    // --- private 멤버 변수 선언 ---
    // TCP 통신 및 스레드 관리에 필요한 변수들입니다.
    private TcpClient client; // TCP 클라이언트 객체
    private NetworkStream stream; // 네트워크 스트림 (데이터 읽고 쓰기)
    private Thread receiveThread; // 데이터 수신용 백그라운드 스레드
    private bool isConnected = false; // 현재 서버에 연결되어 있는지 상태
    private volatile bool stopThread = false; // 스크립트 종료 또는 비활성화 신호 (volatile 키워드로 여러 스레드 접근 시 동기화 문제 방지)

    // 백그라운드 스레드에서 수신한 데이터를 메인 스레드로 전달하기 위한 큐
    // ConcurrentQueue는 여러 스레드에서 동시에 안전하게 데이터를 넣고 뺄 수 있습니다.
    private ConcurrentQueue<string> receivedMessageQueue = new ConcurrentQueue<string>();

    // 서버 연결 시도 코루틴 참조 (재접속 로직 관리에 사용)
    private Coroutine connectCoroutine;


    // 스크립트가 활성화될 때 Unity에 의해 자동으로 호출됨
    void OnEnable()
    {
        Debug.Log("[CLIENT] 클라이언트 스크립트 활성화.");
        stopThread = false; // 스크립트 활성화 시 스레드 종료 신호 초기화
        // 큐 비우기 (스크립트 재활성화 시 이전 데이터 제거)
        // receivedMessageQueue = new ConcurrentQueue<string>(); // 새로운 큐 생성 또는 Clear() (ConcurrentQueue는 Clear() 없음)
        // 여기서는 Clear() 대신 새 객체를 할당하는 것이 일반적이지만, 기존 큐를 계속 사용해도 무방.
        // 중요한 건 데이터 처리가 밀리지 않도록 하는 것.

        // MediaPipeData 스크립트 참조가 연결되었는지 확인 (연결 안 되면 데이터 전달 안 됨)
        if (mediaPipeDataManager == null)
        {
            Debug.LogError("[CLIENT] MediaPipeDataManager 스크립트가 할당되지 않았습니다! 데이터 처리가 제대로 되지 않을 수 있습니다. Inspector에서 연결해주세요.");
            // 경고만 하고 계속 진행하여 연결 시도 자체는 가능하게 합니다.
        }

        // 서버 연결 시도 코루틴 시작
        // 이미 실행 중인 코루틴이 있다면 중복 실행 방지
        if (connectCoroutine == null)
        {
            connectCoroutine = StartCoroutine(ConnectToServer());
        }
    }

    // 스크립트가 비활성화되거나 파괴될 때 Unity에 의해 자동으로 호출됨
    // 애플리케이션 종료 시에도 호출됩니다.
    void OnDisable()
    {
        Debug.Log("[CLIENT] 클라이언트 스크립트 비활성화.");
        stopThread = true; // 백그라운드 수신 스레드 및 연결 코루틴에 종료 신호 보냄

        // 실행 중인 연결 시도 코루틴 중지
        if (connectCoroutine != null)
        {
            StopCoroutine(connectCoroutine); // Coroutine.StopCoroutine(connectCoroutine); 도 가능
            connectCoroutine = null; // 참조 해제
        }
        // StopAllCoroutines(); // 이 스크립트의 모든 코루틴을 멈추려면 이거 사용

        // 백그라운드 데이터 수신 스레드를 안전하게 종료
        // 스레드가 ReceiveThread 함수의 ReadLine()에서 대기 중일 수 있으므로,
        // 클라이언트 소켓을 닫아서 예외를 발생시켜 스레드를 깨우는 것이 일반적입니다.
        if (receiveThread != null && receiveThread.IsAlive) // 스레드가 존재하고 실행 중인지 확인
        {
            // 클라이언트 연결이 유효하다면 닫기 시도
            if (client != null && client.Connected)
            {
                try { client.Close(); Debug.Log("[CLIENT] OnDisable에서 클라이언트 연결 닫기 시도 (수신 스레드 중지 유도)."); } catch (Exception ex) { Debug.LogWarning("[CLIENT] OnDisable 클라이언트 닫기 오류: " + ex.Message); }
            }

            // 스레드가 종료될 때까지 일정 시간 대기
            // 너무 짧으면 스레드가 종료되지 않고, 너무 길면 애플리케이션 종료가 지연됩니다.
            receiveThread.Join(200); // 0.2초 (200ms) 대기

            // Join 시간 내에 스레드가 종료되지 않았으면 강제 종료 (최후의 수단, 주의해서 사용)
            if (receiveThread.IsAlive)
            {
                Debug.LogWarning("[CLIENT] 수신 스레드 강제 종료 시도...");
                try { receiveThread.Abort(); } catch (Exception ex) { Debug.LogWarning("[CLIENT] 수신 스레드 강제 종료 오류: " + ex.Message); }
            }
            receiveThread = null; // 스레드 참조 해제
        }
        else if (receiveThread != null)
        {
            // 스레드 객체는 있지만 이미 종료된 상태라면 참조만 해제
            receiveThread = null;
        }


        // 네트워크 스트림 및 클라이언트 소켓 자원 해제
        // OnDisable 시작 시 client.Close()를 호출했으므로 stream도 닫혔을 가능성이 높지만,
        // 혹시 모를 경우를 위해 명시적으로 정리합니다.
        if (stream != null)
        {
            try { stream.Close(); } catch (Exception ex) { Debug.LogWarning("[CLIENT] 스트림 닫기 오류: " + ex.Message); }
            try { stream.Dispose(); } catch (Exception ex) { Debug.LogWarning("[CLIENT] 스트림 Dispose 오류: " + ex.Message); }
            stream = null;
        }
        // 클라이언트 객체 최종 정리
        if (client != null)
        {
            // Dispose()는 TcpClient가 사용하는 관리/비관리 리소스를 해제합니다.
            try { client.Dispose(); Debug.Log("[CLIENT] OnDisable에서 클라이언트 Dispose 완료."); } catch (Exception ex) { Debug.LogWarning("[CLIENT] OnDisable 클라이언트 Dispose 오류: " + ex.Message); }
            client = null;
        }

        isConnected = false; // 현재 연결 상태를 끊어짐으로 표시
        Debug.Log("[CLIENT] 연결이 끊어졌습니다.");
    }

    // 매 프레임마다 Unity에 의해 자동으로 호출됨 (메인 스레드)
    // 백그라운드 스레드에서 받아온 데이터를 처리하는 역할
    void Update()
    {
        // 백그라운드 수신 스레드가 receivedMessageQueue에 넣어둔 메시지(JSON 문자열)를
        // 메인 스레드인 Update 함수에서 하나씩(또는 모두) 꺼내서 처리합니다.
        // TryDequeue는 큐가 비어있으면 false를 반환하고, 비어있지 않으면 true를 반환하며 요소를 꺼냅니다.
        // while 루프를 사용하여 큐에 데이터가 있는 동안 계속 처리합니다.
        while (receivedMessageQueue.TryDequeue(out string jsonMessage))
        {
            // 큐에서 꺼낸 JSON 메시지를 처리하는 함수 호출
            // ProcessReceivedData 함수는 이 클래스 내부에 정의되어 있습니다.
            ProcessReceivedData(jsonMessage);
        }

        // TODO: 현재 연결 상태(isConnected 변수)를 확인하여 유니티 UI에 표시하는 등의 로직을 추가할 수 있습니다.
        // 예: if (!isConnected) { ShowDisconnectedMessageOnUI(); } else { HideDisconnectedMessage(); }
    }

    // 수신된 JSON 메시지를 파싱하고 MediaPipeData 스크립트로 전달하는 함수 (메인 스레드에서 실행됨)
    // 이 함수는 Update에서 receivedMessageQueue에서 데이터를 꺼내왔을 때 호출됩니다.
    void ProcessReceivedData(string jsonMessage)
    {
        // 받아온 메시지가 비어있거나 공백만 있는지 확인
        if (string.IsNullOrWhiteSpace(jsonMessage))
        {
            //Debug.LogWarning("[CLIENT] Received empty or whitespace message. Skipping processing."); // 디버깅용 로그 (데이터 많으면 주석 처리)
            return; // 유효하지 않은 메시지는 무시
        }

        try
        {
            // JsonUtility를 사용하여 JSON 문자열을 PoseData_ForJsonUtility 객체로 파싱합니다.
            // PoseData_ForJsonUtility 클래스는 LandmarkDataStructures.cs 파일에 정의되어 있어야 합니다.
            // AllLandmarksData_ForJsonUtility 클래스도 마찬가지이며, 이 클래스에는 모든 33개 랜드마크 필드가 정의되어 있어야 합니다.
            PoseData_ForJsonUtility poseData = JsonUtility.FromJson<PoseData_ForJsonUtility>(jsonMessage);

            // JSON 파싱이 성공하고 poseData 객체와 랜드마크 데이터가 유효하며,
            // MediaPipeDataManager 스크립트가 Inspector에서 잘 할당되어 있다면
            // MediaPipeData 스크립트의 OnLandmarkDataReceived 함수를 호출하여 파싱된 데이터를 전달합니다.
            if (poseData != null && poseData.landmarks != null && mediaPipeDataManager != null)
            {
                // MediaPipeData 스크립트의 데이터 처리 함수 호출
                mediaPipeDataManager.OnLandmarkDataReceived(poseData);
            }
            // else // 데이터 유효성 문제 또는 MediaPipeDataManager 할당 문제 발생 시 로그 (필요시 주석 해제)
            // {
            //      if (poseData == null || poseData.landmarks == null)
            //          Debug.LogWarning("[CLIENT] 파싱된 랜드마크 데이터가 유효하지 않습니다 (poseData is null or landmarks is null). Received JSON: " + jsonMessage);
            //      if (mediaPipeDataManager == null)
            //          Debug.LogWarning("[CLIENT] MediaPipeDataManager 스크립트가 할당되지 않았습니다. 수신된 데이터를 처리할 곳이 없습니다. Inspector에서 연결해주세요.");
            // }

            // TODO: 이 스크립트(PoseTcpClient)의 역할은 데이터 수신 및 파싱, 그리고 MediaPipeData로의 전달까지입니다.
            //      랜드마크 시각화 오브젝트의 위치 업데이트나 캐릭터 IK 적용 로직은 모두 MediaPipeData 및 IKCharacter 스크립트로 위임했습니다.
            //      이전에 여기에 혹시 남아있을 수 있는 시각화/IK 업데이트 관련 로직은 모두 삭제하거나 주석 처리해야 합니다.


        }
        catch (ArgumentException ae) // JsonUtility.FromJson 에서 주로 발생하는 오류 (예: JSON 형식이 잘못됨, 필수 필드 누락 등)
        {
            Debug.LogError($"[CLIENT] JSON Deserialization Error (ArgumentException): {ae.Message}. Received JSON: {jsonMessage}");
            // JSON 파싱 오류 발생 시 MediaPipeData에 오류 상태를 알리는 등의 처리를 할 수 있습니다.
            // if (mediaPipeDataManager != null) mediaPipeDataManager.HandleProcessingError("JSON Parsing Error");
        }
        catch (Exception ex) // 그 외 데이터 처리 중 발생한 일반 예외
        {
            Debug.LogError($"[CLIENT] Received Data Processing Error: {ex.Message}. Received JSON: {jsonMessage}");
            // 그 외 데이터 처리 오류 발생 시 MediaPipeData에 오류 상태를 알리는 등의 처리를 할 수 있습니다.
            // if (mediaPipeDataManager != null) mediaPipeDataManager.HandleProcessingError("General Processing Error");
        }
    }


    // 서버에 연결을 시도하고 연결 상태를 관리하며, 연결이 끊어졌을 때 재연결을 시도하는 코루틴
    // 이 코루틴은 OnEnable에서 StartCoroutine을 통해 실행됩니다.
    IEnumerator ConnectToServer()
    {
        Debug.Log($"[CLIENT] 서버 {serverIP}:{serverPort}에 연결 시도 코루틴 시작.");

        // 스크립트 비활성화 신호(stopThread = true)가 오거나 연결이 성공할 때까지 반복 시도
        while (!stopThread && !isConnected)
        {
            // 각 연결 시도 전에 이전 객체 참조를 초기화합니다.
            client = null;
            stream = null;
            receiveThread = null; // 이전 수신 스레드 참조 정리

            Debug.Log($"[CLIENT] {serverIP}:{serverPort} 연결 시도 중...");
            Task connectTask = null; // C# 비동기 연결 작업을 관리하기 위한 Task 객체
            bool initialConnectAttemptFailed = false; // TcpClient 객체 생성 또는 ConnectAsync 시작 시 예외 발생 플래그

            try
            {
                client = new TcpClient(); // 새 TcpClient 객체 생성
                // 지정된 IP 주소 및 포트로 서버에 비동기적으로 연결 시도
                // ConnectAsync는 메인 스레드를 블록하지 않고 백그라운드에서 연결을 시도합니다.
                connectTask = client.ConnectAsync(serverIP, serverPort);
                // 주의: 이 try 블록 안에는 yield return을 사용하지 않습니다.
            }
            catch (SocketException sockEx) // 소켓 관련 예외 처리 (예: 서버 주소 잘못됨, 방화벽에 막힘, 네트워크 사용 불가 등)
            {
                Debug.LogError($"[CLIENT] 연결 Task 생성 중 소켓 오류 발생: {sockEx.SocketErrorCode} - {sockEx.Message}.");
                // 예외 발생 시 client 객체가 생성되었다면 정리합니다.
                if (client != null) { try { client.Dispose(); } catch { } client = null; }
                initialConnectAttemptFailed = true; // 초기 연결 시도 실패 플래그 설정
                                                    // catch 블록 안에서는 yield return을 사용하지 않습니다.
            }
            catch (Exception e) // 그 외 연결 시도 시작 중 발생한 일반 예외 처리
            {
                Debug.LogError($"[CLIENT] 연결 Task 생성 중 일반 오류 발생: {e.Message}.");
                // 예외 발생 시 client 객체 정리
                if (client != null) { try { client.Dispose(); } catch { } client = null; }
                initialConnectAttemptFailed = true; // 초기 연결 시도 실패 플래그 설정
                                                    // catch 블록 안에서는 yield return을 사용하지 않습니다.
            }

            // --- 연결 Task 완료 대기 및 결과 처리 ---
            // 이 부분은 initial try-catch 블록 바로 밖에 위치합니다.
            // initialConnectAttemptFailed 플래그를 사용하여 connectTask가 제대로 생성되어 시작되었는지 확인합니다.
            if (!initialConnectAttemptFailed && connectTask != null)
            {
                // 비동기 연결 작업(Task)이 완료될 때까지 또는 스크립트 종료 신호(stopThread = true)가 올 때까지 대기합니다.
                // yield return null을 사용하여 유니티 메인 스레드가 블록되지 않고 다음 프레임으로 넘어가도록 합니다.
                while (!connectTask.IsCompleted && !stopThread)
                {
                    yield return null; // <-- yield return은 여기서 사용됩니다 (try/catch 블록의 외부).
                }

                // Task 완료 대기 중 스크립트 비활성화 신호가 왔다면 코루틴을 즉시 종료합니다.
                if (stopThread)
                {
                    Debug.Log("[CLIENT] 연결 대기 중 스크립트 비활성화 감지, 코루틴 종료.");
                    // 클라이언트 객체가 생성되었고 아직 정리되지 않았다면 여기서 정리합니다.
                    if (client != null) { try { client.Dispose(); } catch { } client = null; }
                    yield break; // 코루틴 실행 중단 및 종료
                }

                // Task가 완료되었습니다. 연결 시도 결과를 확인하고 스트림을 얻습니다.
                // Task 실행 중 발생한 예외나 스트림 획득 시 발생할 수 있는 예외를 처리합니다.
                try
                {
                    // connectTask.IsFaulted가 true이면 Task 실행 중에 예외가 발생했음을 의미합니다.
                    if (connectTask.IsFaulted)
                    {
                        // Task.Exception은 AggregateException일 수 있으며, InnerException에 실제 예외가 담겨 있을 수 있습니다.
                        Debug.LogError($"[CLIENT] 연결 Task 실행 중 오류 발생: {connectTask.Exception?.InnerException?.Message ?? connectTask.Exception?.Message}.");
                        isConnected = false; // 연결 실패로 간주합니다.
                    }
                    // Task는 성공적으로 완료되었지만, 실제 클라이언트의 Connected 속성이 false인 경우 (예: 서버에서 연결 거부)
                    else if (client != null && client.Connected) // Task 성공 + 클라이언트 객체 유효 + Connected 상태 확인
                    {
                        // 연결 성공! 서버와 데이터를 주고받을 네트워크 스트림을 얻습니다.
                        stream = client.GetStream();
                        isConnected = true; // 현재 연결 상태를 연결됨으로 업데이트합니다.
                        Debug.Log("[CLIENT] 서버에 연결 성공 및 스트림 획득!");

                        // 서버로부터 데이터를 계속 수신하기 위한 백그라운드 스레드를 시작합니다.
                        // ReceiveThread 함수는 블록되는 ReadLine() 호출을 포함하므로 별도 스레드에서 실행해야 합니다.
                        receiveThread = new Thread(ReceiveThread);
                        receiveThread.IsBackground = true; // 백그라운드 스레드로 설정하여 앱 종료 시 같이 종료되도록 함
                        receiveThread.Start(); // 스레드 실행 시작

                        // 연결이 성공했으므로 이 연결 시도 코루틴은 목적을 달성했습니다. 종료합니다.
                        yield break; // 코루틴 실행 중단 및 종료
                    }
                    else
                    {
                        // Task는 완료되었지만 client.Connected가 false인 경우 (예: 서버에서 바로 연결을 끊음)
                        Debug.LogWarning("[CLIENT] 연결 Task 완료 후 Connected 상태 false. 서버에서 연결을 거부했거나 즉시 연결을 종료했습니다.");
                        isConnected = false; // 연결 실패로 간주합니다.
                    }
                }
                catch (Exception ex) // Task 결과 접근, GetStream() 호출, receiveThread 시작 등에서 발생할 수 있는 예외
                {
                    Debug.LogError($"[CLIENT] 연결 결과 확인/스트림 획득/스레드 시작 중 오류: {ex.Message}.");
                    isConnected = false; // 연결 실패로 간주합니다.
                }
                finally // try-catch 블록 결과와 상관없이 항상 실행됩니다.
                {
                    // 연결이 성공했으면 client/stream 자원을 여기서 정리하지 않습니다 (계속 사용해야 하므로).
                    // 연결이 실패했으면 client 객체를 정리합니다 (다음 시도에서 새로 생성).
                    if (!isConnected && client != null)
                    {
                        try { client.Dispose(); } catch { }
                        client = null;
                    }
                }
            }
            else // initialConnectAttemptFailed가 true, 즉 Task 시작 자체에 실패한 경우
            {
                Debug.LogWarning("[CLIENT] 연결 시도 Task 시작에 실패했습니다 (TcpClient 생성 또는 ConnectAsync 호출 오류).");
                isConnected = false; // 연결 실패로 간주합니다.
                                     // client 객체는 initial try-catch 블록에서 이미 정리되었거나 null 상태일 것입니다.
            }


            // --- 다음 연결 시도까지 대기 ---
            // 현재 연결 시도가 성공하지 못했고 (isConnected = false)
            // 스크립트 종료 신호도 아니라면 (!stopThread)
            // 일정 시간 대기(reconnectDelay) 후 while 루프의 다음 반복에서 재연결을 시도합니다.
            // 이 yield return은 모든 try/catch 블록의 **외부**에 위치합니다.
            if (!isConnected && !stopThread)
            {
                Debug.LogWarning($"[CLIENT] {reconnectDelay}초 후 재연결 시도...");
                yield return new WaitForSeconds(reconnectDelay); // <-- yield return은 여기서 사용 (모든 catch 블록 밖)
            }
            // isConnected가 true가 되면 (연결 성공하면) while 루프 조건(!isConnected)이 false가 되어
            // yield return new WaitForSeconds(reconnectDelay); 부분을 건너뛰고 바로 다음 루프 조건 검사로 가서 루프가 자연스럽게 종료됩니다.

        } // while (!stopThread && !isConnected) 루프 끝

        // while 루프를 빠져나온 이유에 따라 코루틴 종료 메시지를 출력합니다.
        if (isConnected)
        {
            Debug.Log("[CLIENT] 연결 시도 코루틴 종료 (연결 성공).");
        }
        else if (stopThread)
        {
            Debug.Log("[CLIENT] 연결 시도 코루틴 종료 (stopThread=true, 스크립트 비활성화/종료).");
        }
        else
        {
            // 이 경우는 논리적으로 발생하면 안 되는 상황입니다.
            Debug.Log("[CLIENT] 연결 시도 코루틴 종료 (알 수 없는 이유 - SHOULD NOT HAPPEN with current loop condition).");
        }
        connectCoroutine = null; // 코루틴 실행 완료 또는 중단 시 참조 해제
    }

    // 백그라운드 스레드에서 실행될 데이터 수신 함수
    // 이 함수는 ConnectToServer 코루틴에서 연결 성공 후 receiveThread를 통해 시작됩니다.
    void ReceiveThread()
    {
        Debug.Log("[CLIENT] 수신 스레드 시작.");
        // ConnectToServer 코루틴에서 설정된 stream 객체의 로컬 참조를 사용
        NetworkStream currentStream = stream;
        // stream 객체가 유효한지 확인 (연결 성공 후 설정되었어야 함)
        if (currentStream == null)
        {
            Debug.LogError("[CLIENT] 수신 스레드 시작 시 스트림이 null입니다. 수신 스레드 종료.");
            isConnected = false; // 스트림이 없으면 연결 안 된 상태로 간주
                                 // 메인 스레드에서 isConnected = false 를 감지하고 재연결 시도할 것임
            return; // 스레드 함수 종료
        }

        // StreamReader를 사용하여 네트워크 스트림에서 데이터를 텍스트 라인 단위로 읽습니다.
        // 파이썬 서버가 각 JSON 메시지 끝에 '\n'을 붙여 보내므로 ReadLine() 사용이 적합합니다.
        // ReadLine()은 데이터가 올 때까지 또는 스트림이 닫힐 때까지 블록(대기)하므로,
        // 메인 스레드가 아닌 별도의 백그라운드 스레드에서 실행되어야 UI가 멈추지 않습니다.
        // 'using' 문을 사용하여 StreamReader와 underlying stream 자원이
        // 이 블록을 벗어나거나 예외 발생 시 안전하게 닫히고 해제되도록 합니다.
        using (var reader = new StreamReader(currentStream, Encoding.UTF8))
        {
            try
            {
                // 연결 상태이고 스크립트 종료 신호가 아닐 동안 반복하여 데이터 수신 시도
                // isConnected 및 stopThread 변수는 volatile 키워드로 선언되어 있어 여러 스레드에서 안전하게 접근 가능
                while (isConnected && !stopThread)
                {
                    // 스트림에서 한 줄의 텍스트(JSON 메시지)를 읽습니다. (Blocking 호출)
                    // 서버가 Close() 하거나 연결이 끊어지면 IOException 등의 예외 발생,
                    // 서버가 정상적으로 스트림 끝을 보내면 ReadLine()이 null 반환.
                    string message = reader.ReadLine();

                    if (message != null) // 데이터를 성공적으로 한 줄 읽었으면
                    {
                        // Debug.Log($"[CLIENT] Raw data received: {message}"); // 수신 데이터 로그 (너무 많으면 주석 처리)
                        // 받아온 메시지를 메인 스레드의 처리를 위해 receivedMessageQueue에 추가합니다.
                        // ConcurrentQueue는 여러 스레드에서 동시에 데이터를 안전하게 넣고 뺄 수 있습니다.
                        receivedMessageQueue.Enqueue(message);
                    }
                    else // ReadLine()이 null을 반환하면 (예: 서버에서 정상적으로 연결을 종료한 경우)
                    {
                        Debug.LogWarning("[CLIENT] 수신 스레드: 서버에서 연결을 종료했습니다 (스트림 끝 감지).");
                        isConnected = false; // 현재 연결 상태를 끊어짐으로 업데이트합니다 (메인 스레드에서 재연결 시작 유도)
                        break; // 수신 스레드 루프를 종료합니다.
                    }
                }
            }
            catch (IOException ioEx) // 입출력 관련 오류 (예: 스트림이 외부에서 강제로 닫힘, 네트워크 문제 발생)
            {
                // 이 오류는 OnDisable에서 client.Close()를 호출하여 스레드를 중지시키려 할 때나,
                // 네트워크 연결이 갑자기 끊어졌을 때 발생할 수 있습니다.
                // 이미 연결이 끊어졌거나 스크립트 종료 중인 상태였다면 예상된 오류로 간주하고 경고만 출력합니다.
                if (!isConnected || stopThread)
                {
                    Debug.LogWarning("[CLIENT] 수신 스레드 I/O 오류 (연결 끊김 또는 종료 중 추정): " + ioEx.Message);
                }
                else
                { // 예상치 못한 오류 발생
                    Debug.LogError("[CLIENT] 수신 스레드 I/O 오류: " + ioEx.Message);
                }
                isConnected = false; // 연결 실패 상태로 업데이트합니다.
            }
            catch (ObjectDisposedException odEx) // 객체가 이미 Dispose(해제)된 상태에서 사용하려 할 때 발생
            {
                // 주로 OnDisable 등에서 stream이나 client 객체가 Dispose 되었을 때,
                // 수신 스레드가 아직 종료되지 않고 해당 객체에 접근하려 할 때 발생합니다.
                if (!isConnected || stopThread)
                {
                    Debug.LogWarning("[CLIENT] 수신 스레드 ObjectDisposed 오류 (종료 중 추정): " + odEx.Message);
                }
                else
                { // 예상치 못한 오류 발생
                    Debug.LogError("[CLIENT] 수신 스레드 ObjectDisposed 오류: " + odEx.Message);
                }
                isConnected = false; // 연결 실패 상태로 업데이트합니다.
            }
            catch (Exception ex) // 그 외 수신 스레드 실행 중 발생한 일반 예외
            {
                Debug.LogError("[CLIENT] 수신 스레드 일반 오류: " + ex.Message);
                isConnected = false; // 연결 실패 상태로 업데이트합니다.
            }
            finally // try-catch-finally 블록이 어떤 이유로든 끝날 때 항상 실행됩니다.
            {
                Debug.Log("[CLIENT] 수신 스레드 종료됨.");
                // isConnected = false 상태가 되었으므로, 메인 스레드(Update 또는 ConnectToServer)가 이를 감지하고 필요시 재연결을 시도할 것입니다.
                // client 및 stream 자원 정리는 메인 스레드 (주로 OnDisable 또는 ConnectToServer 루프의 finally 블록)에서 이루어져야 합니다.
            }
        } // 'using' 블록 끝: reader와 currentStream 자원이 Dispose 됩니다.
          // currentStream 변수는 이 함수의 로컬 변수이므로 여기서 할 일은 끝입니다.
          // 클래스 멤버 변수인 stream 필드는 이 백그라운드 스레드에서 직접 null로 설정하면 안 됩니다.
    }

    // Unity 오브젝트が破棄されるときにUnityによって自動的に呼び出されます
    // 애플리케이션 종료 시 OnDisable -> OnDestroy 순서로 호출됩니다.
    void OnDestroy()
    {
        // OnDestroy 시 OnDisable이 호출되도록 보장하여 자원 정리 로직이 실행되게 합니다.
        // 보통 OnDisable에서 대부분의 자원 정리를 하므로, 여기서는 OnDisable 호출만으로 충분합니다.
        OnDisable();
    }
}
