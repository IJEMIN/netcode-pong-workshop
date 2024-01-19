using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : NetworkBehaviour
{
    // 플레이어 목록과 준비 여부를 표시할 UI 텍스트
    public Text lobbyText;

    // 게임을 시작하기 위해 필요한 최소한의 준비된 플레이어 수
    private const int MinimumReadyCountToStartGame = 2;

    // 플레이어 준비 상태를 저장하는 딕셔너리
    private Dictionary<ulong, bool> _clientReadyStates
        = new Dictionary<ulong, bool>();

    // OnNetworkSpawn은 NetworkBehaviour가 생성될 때 호출됨
    public override void OnNetworkSpawn()
    {
        // 먼저 자기 자신의 준비 상태를 등록
        _clientReadyStates.Add(NetworkManager.LocalClientId, false);

        if (IsServer)
        {
            // 클라이언트가 접속하거나 떠났을 때 호출할 콜백 등록
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            // 클라이언트가 씬 로드를 완료했을 때 호출할 콜백 등록
            NetworkManager.SceneManager.OnLoadComplete += OnClientSceneLoadComplete;
        }

        UpdateLobbyText(); // 로비 텍스트를 갱신
    }

    private void OnDisable()
    {
        if (!IsServer)
        {
            return;
        }

        if (NetworkManager.Singleton != null)
        {
            // 서버가 네트워크 매니저에게 등록한 콜백을 해제
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.SceneManager.OnLoadComplete -= OnClientSceneLoadComplete;    
        }
    }

    // 클라이언트가 연결되었을때 실행할 콜백
    private void OnClientConnected(ulong clientId)
    {
        // 클라이언트 상태 목록에 해당 클라이언트 추가
        _clientReadyStates.Add(clientId, false);
        UpdateLobbyText(); // 호스트의 로비 텍스트를 갱신 
    }
    
    // 클라이언트가 씬 로드를 완료했을 때 실행할 콜백
    private void OnClientSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        // 로비 씬에서만 동작
        if (sceneName != "Lobby")
        {
            return;
        }

        // 호스트의 클라이언트들 준비 상태들을 모든 클라이언트들에게 동기화
        foreach (KeyValuePair<ulong, bool> pair in _clientReadyStates)
        {
            var id = pair.Key;
            var isReady = pair.Value;
            SetClientIsReadyClientRpc(id, isReady);
        }
    }

    // 클라이언트 접속이 끊겼을 때 실행할 콜백
    private void OnClientDisconnected(ulong clientId)
    {
        // 클라이언트 상태 목록에 해당 클라이언트 제거
        if (_clientReadyStates.ContainsKey(clientId))
        {
            _clientReadyStates.Remove(clientId);
        }

        // 다른 클라이언트들에서도 해당 클라이언트를 딕셔너리에서 제거
        RemovePlayerClientRpc(clientId);
        UpdateLobbyText(); // 호스트의 로비 텍스트를 갱신
    }

    // 서버가 다른 클라이언트들에게 어떤 클라이언트의 준비 상태가 변경됨을 알리는 RPC 메서드
    // 서버에서 요청되어 각 클라이언트들에서 실행됨
    [ClientRpc]
    private void SetClientIsReadyClientRpc(ulong clientId, bool isReady)
    {
        // 호스트에서 실행되지 않도록 막기
        if (IsServer)
        {
            return;
        }
        
        // 준비 상태 갱신
        _clientReadyStates[clientId] = isReady;
        UpdateLobbyText(); // 로비 텍스트 갱신
    }

    // 서버가 다른 클라이언트들에게 어떤 클라이언트의 준비 상태가 변경됨을 알리는 RPC 메서드
    // 서버에서 요청되어 각 클라이언트들에서 실행됨
    [ClientRpc]
    private void RemovePlayerClientRpc(ulong clientId)
    {
        if (IsServer)
        {
            return;
        }

        // 딕셔너리에서 해당 클라이언트 제거
        _clientReadyStates.Remove(clientId);
        UpdateLobbyText(); // 로비 텍스트 갱신
    }

    // 로비 텍스트를 갱신
    private void UpdateLobbyText()
    {
        var stringBuilder = new StringBuilder();

        // 딕셔너리에 저장된 플레이어들의 준비 상태를 문자열로 조합
        foreach (var pair in _clientReadyStates)
        {
            // 플레이어의 ID와 준비 상태를 가져옴
            var clientId = pair.Key;
            var isReady = pair.Value;
            
            if (isReady)
            {
                stringBuilder.AppendLine($"PLAYER_{clientId} : READY");
            }
            else
            {
                stringBuilder.AppendLine($"PLAYER_{clientId} : NOT READY");
            }
        }

        // 로비 텍스트에 조합한 문자열을 적용
        lobbyText.text = stringBuilder.ToString();
    }

    // 게임을 시작할 수 있는지 확인
    private bool CheckIsReadyToStart()
    {
        // 최소한 2명의 플레이어가 있어야 게임을 시작할 수 있음
        if (_clientReadyStates.Count < MinimumReadyCountToStartGame)
        {
            return false;
        }

        // 모든 플레이어가 준비 상태여야 게임을 시작할 수 있음
        foreach (var isReady in _clientReadyStates.Values)
        {
            if (!isReady)
            {
                return false;
            }
        }

        return true;
    }

    // 게임 시작
    private void StartGame()
    {
        // 호스트가 아니면 실행하지 않음
        if (!IsServer)
        {
            return;
        }

        // 서버가 네트워크 매니저에게 등록한 콜백을 해제
        NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.SceneManager.OnLoadComplete -= OnClientSceneLoadComplete;
        
        // 서버가 게임 씬을 로드하도록 요청
        NetworkManager.SceneManager.LoadScene("InGame", LoadSceneMode.Single);
    }

    // 클라이언트가 준비 버튼을 눌렀을때 실행하는 메서드
    public void SetPlayerIsReady()
    {
        // 로컬 클라이언트의 ID를 가져옴
        var localClientId = NetworkManager.LocalClientId;

        // 준비 버튼을 누르면 준비 상태를 반전시킴
        _clientReadyStates[localClientId] = !_clientReadyStates[localClientId];
        var isReady = _clientReadyStates[localClientId];

        UpdateLobbyText(); // 로비 텍스트 갱신

        if (IsServer)
        {
            // 클라이언트에게 변경된 호스트의 준비 상태를 동기화
            SetClientIsReadyClientRpc(localClientId, isReady);
            
            // 모든 클라이언트가 준비 상태라면 게임을 시작
            if (CheckIsReadyToStart())
            {
                StartGame();
            }
        }
        else
        {
            // 호스트에게 변경된 클라이언트의 준비 상태를
            // 모든 클라이언트에게 동기화하도록 요청
            SetClientIsReadyServerRpc(localClientId, isReady);
        }
    }
    
    // 클라이언트가 준비 상태가 변경됬음을 서버에게 알리기 위한 RPC 메서드
    // 클라이언트에서 요청되어 서버에서 실행됨
    [ServerRpc(RequireOwnership = false)]
    private void SetClientIsReadyServerRpc(ulong clientId, bool isReady)
    {
        // 호스트의 딕셔너리를 갱신
        _clientReadyStates[clientId] = isReady;
        // 다른 클라이언트들에게 변경된 클라이언트의 준비 상태를 동기화
        SetClientIsReadyClientRpc(clientId, isReady);
        UpdateLobbyText(); // 호스트의 로비 텍스트를 갱신
        
        // 모든 클라이언트가 준비 상태라면 게임을 시작
        if (CheckIsReadyToStart())
        {
            StartGame();
        }
    }
}