using Unity.Netcode;
using UnityEngine;

// 플레이어의 위치를 동기화하기 위한 컴포넌트
public class PositionSync : NetworkBehaviour
{
    private Vector2 _lastPosition; // 마지막으로 동기화된 위치
    // 위치값 동기화를 위한 네트워크 변수
    public NetworkVariable<Vector2> networkPosition 
        = new NetworkVariable<Vector2>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Owner);

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            // 로컬 클라이언트가 소유자인 경우
            // 마지막으로 동기화된 위치와 현재 위치가 일정 거리 이상 차이가 나면
            if (Vector2.Distance(_lastPosition, transform.position) > 0.001f)
            {
                // 위치 동기화
                _lastPosition = (Vector2)transform.position;
                networkPosition.Value = _lastPosition;    
            }
        }
        else 
        {
            // 로컬 클라이언트가 소유자가 아닌 경우
            // 전달받은 동기화된 위치로 이동
            transform.position = (Vector3)networkPosition.Value;    
        }
    }
}