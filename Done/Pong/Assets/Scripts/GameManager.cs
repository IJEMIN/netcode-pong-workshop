using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    // 싱글톤 구현
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();
            }
            return instance;
        }
    }
    
    private static GameManager instance;
    
    // 게임이 진행 중인지 여부
    public bool IsGameActive { get; private set; }
    
    // 점수 표시할 UI 텍스트
    public Text scoreText;
    
    // 플레이어 색상, 스폰 위치
    public Color[] playerColors = new Color[2];
    public Transform[] spawnPositionTransforms = new Transform[2];

    // 공 프리팹
    public GameObject ballPrefab;

    // 게임 오버 텍스트와 게임 오버시 표시할 패널
    public Text gameoverText;
    public GameObject gameoverPanel;

    // 플레이어 번호와 클라이언트 ID를 맵핑하는 딕셔너리
    private Dictionary<int, ulong> playerNumberClientIdMap 
        = new Dictionary<int, ulong>();
    
    // 플레이어 점수를 저장하는 배열
    private int[] playerScores = new int[2];
    
    // 승리 도달 점수
    private const int WinScore = 11;

    // 처음 활성화시 게임을 시작하는 처리를 실행
    public override void OnNetworkSpawn()
    {
        // 서버에서만 플레이어와 공을 스폰
        if (IsServer)
        {
            SpawnPlayer();
            SpawnBall();
        }

        // 게임 활성화
        IsGameActive = true;
        
        // 게임 오버 패널 비활성화
        gameoverPanel.SetActive(false);
        // 점수 텍스트를 0 : 0 으로 갱신
        UpdateScoreTextClientRpc(0, 0);
        
        // 클라이언트가 접속하거나 떠났을 때 호출할 콜백 등록
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }
    
    public override void OnNetworkDespawn()
    {
        // 클라이언트가 접속하거나 떠났을 때 호출할 콜백 해제
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // 게임 도중 클라이언트가 나갔을때 실행
    private void OnClientDisconnected(ulong clinetId)
    {
        // 게임 도중에 플레이어 중 한명이 나갔다면
        // 게임을 종료하고 메뉴 화면으로 돌아감
        if (IsGameActive)
        {
            ExitGame();
        }
    }

    // 플레이어들을 스폰
    private void SpawnPlayer()
    {
        // 플레이어가 2명이 아니라면 에러를 출력하고 종료
        if (NetworkManager.ConnectedClientsList.Count != 2)
        {
            Debug.LogError("Pong can only be played by 2 players...");
            return;
        }

        // 플레이어 프리팹을 가져옴
        var playerPrefab = NetworkManager.NetworkConfig.PlayerPrefab;
        
        // 두명의 플레이어를 스폰
        for(var i = 0; i < 2; i++)
        {
            var client = NetworkManager.ConnectedClientsList[i];
            
            // 각 클라이언트 ID에 해당 클라이언트가 몇 번째 플레이어인지를 맵핑하여 저장
            playerNumberClientIdMap[i] = client.ClientId;

            // 플레이어의 스폰 위치와 색상을 가져옴
            var spawnPosition = spawnPositionTransforms[i].position;
            var playerColor = playerColors[i];

            // 플레이어 프리팹을 인스턴스화하고 네트워크 스폰
            var playerGameObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            var playerPaddle = playerGameObject.GetComponent<PlayerPaddle>();
            playerPaddle.NetworkObject.SpawnAsPlayerObject(client.ClientId);
            
            // 플레이어의 스폰 위치와 색상을 클라이언트에게 전달
            playerPaddle.SpawnToPositionClientRpc(spawnPosition);
            playerPaddle.SetRendererColorClientRpc(playerColor);
        }
    }

    // 공 생성
    private void SpawnBall()
    {
        // 공 프리팹을 인스턴스화하고 네트워크 스폰
        var ballGameObject = Instantiate(ballPrefab, Vector2.zero, Quaternion.identity);
        var ball = ballGameObject.GetComponent<Ball>();
        ball.NetworkObject.Spawn();
    }

    // 점수 추가
    public void AddScore(int playerNumber, int score)
    {
        // 해당 순번의 플레이어 점수 증가
        playerScores[playerNumber] += score;
        
        // 각 클라이언트들의 점수 텍스트를 갱신
        UpdateScoreTextClientRpc(playerScores[0], playerScores[1]);

        // 만약 어느 한 플레이어의 점수가 승리 도달 점수를 넘었다면
        if (playerScores[playerNumber] >= WinScore)
        {
            // 승리한 플레이어의 클라이언트 ID를 가져옴
            var winnerId = playerNumberClientIdMap[playerNumber];
            // 승리 및 게임 오버 처리
            EndGame(winnerId);
        }
    }

    // 점수 텍스트 갱신
    [ClientRpc]
    private void UpdateScoreTextClientRpc(int player0Score, int player1Score)
    {
        scoreText.text = $"{player0Score} : {player1Score}";
    }
    
    // 서버에서 실행할 게임 종료
    public void EndGame(ulong winnerId)
    {
        // 서버에서만 실행 가능
        if (!IsServer)
        {
            return;
        }
        
        // 공을 스폰해제
        var ball = FindObjectOfType<Ball>();
        ball.NetworkObject.Despawn();

        // 게임 오버 처리를 클라이언트들에게 전파
        EndGameClientRpc(winnerId);
    }
    
    // 클라이언트에서 실행할 게임 종료 처리
    [ClientRpc]
    public void EndGameClientRpc(ulong winnerId)
    {
        // 게임 오버 처리
        IsGameActive = false;
        
        // 입력으로 전달된 승자가 자신인 경우
        if (winnerId == NetworkManager.LocalClientId)
        {
            gameoverText.text = "You Win!";
        }
        else // 입력으로 전달된 승자가 자신이 아닌 경우
        {
            gameoverText.text = "You Lose!";
        }
        
        // 게임 오버 패널 활성화
        gameoverPanel.SetActive(true);
    }
    
    // 게임과 네트워크 연결을 종료하고 메뉴 화면으로 돌아감
    public void ExitGame()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("Menu");
    }
}