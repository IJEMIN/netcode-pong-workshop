using Unity.Netcode;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    private Vector2 direction; // 이동 방향
    private const float StartSpeed = 3f; // 최초 이동 속도
    private const float MaxSpeed = 15f; // 최대 이동 속도
    private const float AdditionalSpeedPerHit = 0.2f; // 충돌시 추가되는 속도
    private float currentSpeed = StartSpeed; // 현재 속도
    
    // 최초 공의 방향을 결정
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        // 게임 시작시 공의 이동 방향은
        // 무작위성이 조금 추가된 왼쪽 방향
        direction = 
            (Vector2.left + Random.insideUnitCircle).normalized;
    }
    
    private void FixedUpdate()
    {
        // 서버가 아니거나 게임이 종료된 경우 이동 처리를 하지 않음
        if (!IsServer || !GameManager.Instance.IsGameActive)
        {
            return;
        }

        // 공의 이동 거리를 계산
        var distance = currentSpeed * Time.deltaTime;

        // 공의 이동 방향으로 레이캐스트를 통해 충돌 검사
        var hit = Physics2D.Raycast(transform.position, direction, distance);

        // 무언가와 충돌하지 않은 경우
        if (hit.collider == null)
        {
            // 위치 이동 적용
            transform.position += (Vector3)(direction * distance);
        }
        else if (hit.collider.CompareTag("ScoringZone"))
        {
            // 충돌한 게임 오브젝트가 스코어 존인 경우
            if (hit.point.x < 0f)
            {
                // 왼쪽 스코어 존인 경우 플레이어 1번에 점수 추가
                GameManager.Instance.AddScore(1, 1);
                // 공 위치가 리셋될때
                // 공을 놓친 플레이어 방향으로 공이 날아가나 랜덤성을 조금 추가 
                direction = 
                    (Vector2.left + Random.insideUnitCircle).normalized;
            }
            else
            {
                // 오른쪽 스코어 존인 경우 플레이어 0번에 점수 추가
                GameManager.Instance.AddScore(0, 1);
                direction = 
                    (Vector2.right + Random.insideUnitCircle).normalized;
            }

            // 새 공의 위치를 x축 정 중앙, y축은 랜덤으로 지정
            transform.position 
                = new Vector3(0f, Random.Range(-3f, 3f), 0f);
            // 속도 리셋
            currentSpeed = StartSpeed;
        }
        else // 무언가와 충돌했지만 스코어링 존에 충돌하지 않은 경우
        {
            // 현재 위치를 충돌 위치로 옮김
            transform.position = hit.point;

            // 충돌 지점까지 이동할시 남은 이동할 거리를 계산
            distance -= hit.distance;

            // 충돌 방향에 반사되는 방향으로 공을 튕김
            direction = Vector2.Reflect(direction, hit.normal);
            // 공의 이동 방향에 랜덤성을 조금 더함
            direction
                = (direction + Random.insideUnitCircle * 0.05f).normalized;

            // 충돌 표면에서 튕겨나가는 방향으로 남은 거리만큼 공을 이동
            transform.position += (Vector3)direction * distance;

            // 공의 이동 속도를 증가
            currentSpeed = Mathf.Min(currentSpeed + AdditionalSpeedPerHit,MaxSpeed);
        }
    }
}