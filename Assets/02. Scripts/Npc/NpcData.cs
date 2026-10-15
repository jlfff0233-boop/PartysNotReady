using UnityEngine;

/// <summary>
/// NPC 설정 데이터 - 일반 이동 대기, 이동 범위와 방 이동 설정
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/Npc/NpcData" )]
public class NpcData : ScriptableObject
{

    [Header ( "----- 대기 -----" )]
    [SerializeField, Min ( 0f )] float _localMoveIntervalMin = 2f;       //최소 대기 시간

    [SerializeField, Min ( 0f )] float _localMoveIntervalMax = 5f;       //최대 대기 시간

    [Header ( "----- 이동 -----" )]
    [SerializeField, Min ( 0.01f )] float _localMoveRadius = 3f;        //로컬 목적지 탐색 반경
    [SerializeField, Min ( 0.01f )] float _moveSpeed = 1.5f;        //이동 속력

    [Header ( "----- 이동 판정 -----" )]
    [SerializeField, Min ( 1 )] int _localMoveTryCount = 8;     //목적지 탐색 횟수
    [SerializeField, Min ( 0.01f )] float _correctionDist = 0.5f;   //NavMesh 보정 거리
    [SerializeField, Min ( 0.01f )] float _arriveDist = 0.2f;   //도착 허용 거리
    [SerializeField, Min ( 0.1f )] float _localMoveTimeLimit = 10f; //이동 제한시간
    [SerializeField, Min ( 0f )] float _rotateSpeed = 360f;     //회전 속력

    [Header ( "----- 방 이동 -----" )]
    [SerializeField, Min ( 0.1f )] float _roomMoveIntervalMin = 10f;     //최소 방 이동 판단 간격
    [SerializeField, Min ( 0.1f )] float _roomMoveIntervalMax = 20f;     //최대 방 이동 판단 간격
    [SerializeField, Range ( 0f , 1f )] float _roomMoveChance = 0.5f;     //일반 방 이동 확률
    [SerializeField, Range ( 0f , 1f )] float _partyRoomMoveChance = 0.2f;     //파티룸 이탈 확률
    [SerializeField, Range ( 0f , 1f )] float _returnPartyRoomChance = 0.7f;     //파티룸 복귀 확률
    [SerializeField, Min ( 1 )] int _roomMoveTryCount = 16;     //방 내부 목적지 탐색 횟수
    [SerializeField, Min ( 0.1f )] float _roomConnectDist = 15f;     //경로 노드 연결 허용 거리
    [SerializeField, Min ( 0.1f )] float _roomRetryInterval = 1f;     //경로 재계산 간격
    [SerializeField, Min ( 0.1f )] float _roomMoveTimeLimit = 30f;     //방 이동 제한시간
    [SerializeField, Min ( 0.1f )] float _roomStuckTimeLimit = 3f;     //이동 정체 허용 시간


    /// <summary>
    /// 최소 대기 시간
    /// </summary>
    public float LocalMoveIntervalMin => _localMoveIntervalMin;

    /// <summary>
    /// 최대 대기 시간
    /// </summary>
    public float LocalMoveIntervalMax =>
        Mathf.Max ( _localMoveIntervalMin , _localMoveIntervalMax );

    /// <summary>
    /// 로컬 목적지 탐색 반경
    /// </summary>
    public float LocalMoveRadius => _localMoveRadius;

    /// <summary>
    /// 이동 속력
    /// </summary>
    public float MoveSpeed => _moveSpeed;

    /// <summary>
    /// 목적지 탐색 횟수
    /// </summary>
    public int LocalMoveTryCount => _localMoveTryCount;

    /// <summary>
    /// NavMesh 보정 거리
    /// </summary>
    public float CorrectionDist => _correctionDist;

    /// <summary>
    /// 도착 허용 거리
    /// </summary>
    public float ArriveDist => _arriveDist;

    /// <summary>
    /// 로컬 이동 제한시간
    /// </summary>
    public float LocalMoveTimeLimit => _localMoveTimeLimit;

    /// <summary>
    /// 회전 속력
    /// </summary>
    public float RotateSpeed => _rotateSpeed;

    /// <summary>
    /// 최소 방 이동 판단 간격
    /// </summary>
    public float RoomMoveIntervalMin => _roomMoveIntervalMin;

    /// <summary>
    /// 최대 방 이동 판단 간격
    /// </summary>
    public float RoomMoveIntervalMax => Mathf.Max ( _roomMoveIntervalMin , _roomMoveIntervalMax );

    /// <summary>
    /// 일반 방 이동 확률
    /// </summary>
    public float RoomMoveChance => _roomMoveChance;

    /// <summary>
    /// 파티룸 이탈 확률
    /// </summary>
    public float PartyRoomMoveChance => _partyRoomMoveChance;

    /// <summary>
    /// 파티룸 복귀 확률
    /// </summary>
    public float ReturnPartyRoomChance => _returnPartyRoomChance;

    /// <summary>
    /// 방 내부 목적지 탐색 횟수
    /// </summary>
    public int RoomMoveTryCount => _roomMoveTryCount;

    /// <summary>
    /// 경로 노드 연결 허용 거리
    /// </summary>
    public float RoomConnectDist => _roomConnectDist;

    /// <summary>
    /// 경로 재계산 간격
    /// </summary>
    public float RoomRetryInterval => _roomRetryInterval;

    /// <summary>
    /// 방 이동 제한시간
    /// </summary>
    public float RoomMoveTimeLimit => _roomMoveTimeLimit;

    /// <summary>
    /// 이동 정체 허용 시간
    /// </summary>
    public float RoomStuckTimeLimit => _roomStuckTimeLimit;


}
