using Unity.Netcode; // 유니티 넷코드
using Unity.Netcode.Transports.UTP; // 유니티 넷코드 UTP 트랜스포트
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public Text infoText; // 연결 정보를 표시할 텍스트
    public InputField hostAddressInputField; // 호스트 주소를 입력할 인풋 필드
    private const ushort DefaultPort = 7777; // 기본 포트

    private void Awake()
    {
        infoText.text = string.Empty; // 텍스트 초기화
        // 연결을 직접 승인하도록 설정
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
    }

    private void OnEnable()
    {
        // 접속이 종료된 경우 호출되는 콜백을 등록
        NetworkManager.Singleton.OnClientDisconnectCallback
            += OnClientDisconnectCallback;
    }

    private void OnDisable()
    {
        // 게임 종료시 네트워크 매니저가 먼저 파괴되는 경우에 대한 예외 처리
        if (NetworkManager.Singleton != null)
        {
            // 접속이 종료된 경우 호출되는 콜백을 해제
            NetworkManager.Singleton.OnClientDisconnectCallback
                -= OnClientDisconnectCallback;
        }
    }

    // 클라이언트가 연결을 끊었을 때 호출되는 콜백
    private void OnClientDisconnectCallback(ulong obj)
    {
        // 연결 종료 이유를 가져옴
        var disconnectReason = NetworkManager.Singleton.DisconnectReason;
        infoText.text = disconnectReason;
        Debug.Log(disconnectReason);
    }

    // 연결을 승인할 때 호출되는 콜백
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        // 총 플레이어 수가 2명 이상이면 연결을 거부
        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            response.Approved = true;
            // 플레이어 오브젝트는 직접 생성할것
            response.CreatePlayerObject = false; 
        }
        else
        {
            response.Approved = false;
            response.Reason = "Max player in session is 2";
        }
    }

    // 호스트로 게임을 생성할 때 호출되는 메서드
    public void CreateGameAsHost()
    {
        // 네트워크 매니저 가져오기
        var networkManager = NetworkManager.Singleton;
        // 네트워크 트랜스포트 설정 가져오기
        var transport
            = (UnityTransport)networkManager.NetworkConfig.NetworkTransport;
        // 사용할 포트 번호 지정
        transport.ConnectionData.Port = DefaultPort;

        // 호스트로 게임을 시작
        if (networkManager.StartHost())
        {
            // 게임에 참가하는 모든 플레이어가 로비 씬을 로드하도록 설정
            networkManager.SceneManager
                .LoadScene("Lobby", LoadSceneMode.Single);
        }
        else
        {
            // 호스트 시작에 실패한 경우
            infoText.text = "Host failed to start";
            Debug.LogError("Host failed to start");
        }
    }

    // 클라이언트로 게임에 참여할 때 호출되는 메서드
    public void JoinGameAsClient()
    {
        var networkManager = NetworkManager.Singleton;
        var transport
            = (UnityTransport)networkManager.NetworkConfig.NetworkTransport;

        // 호스트 주소를 설정
        transport.SetConnectionData(hostAddressInputField.text, DefaultPort);

        // 클라이언트로 게임에 참여
        if (!NetworkManager.Singleton.StartClient())
        {
            // 클라이언트 시작에 실패한 경우
            infoText.text = "Client failed to start";
            Debug.LogError("Client failed to start");
        }
    }
}