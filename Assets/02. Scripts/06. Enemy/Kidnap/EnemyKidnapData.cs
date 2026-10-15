using UnityEngine;

/// <summary>
/// 납치 적 설정 데이터 - 잠복, 표적 선정, 접근과 도주 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Enemy/EnemyKidnapData" )]
public class EnemyKidnapData : ScriptableObject
{
    [Header( "----- 잠복/탐색 -----" )]
    [SerializeField, Min( 0f )] float _hideDuration = 3f;       //잠복 시간(초)
    [SerializeField, Min( 0.01f )] float _searchInterval = 1f;       //변칙 사건 요청과 표적 재탐색 간격(초)
    [SerializeField, Min( 0.01f )] float _searchDist = 15f;       //표적 탐색 반경

    [Header( "----- 표적 선정 -----" )]
    [SerializeField, Range( 0f, 1f )]
    float _npcTargetChance = 0.75f;      //NPC 선택 확률

    [Header( "----- 접근 -----" )]
    [SerializeField, Min( 0.01f )] float _chaseSpeed = 3f;       //초당 접근 거리
    [SerializeField, Min( 0.01f )] float _rotateSpeed = 360f;       //초당 회전 각도
    [SerializeField, Min( 0.01f )] float _chaseTimeLimit = 15f;       //표적 변경을 포함한 접근 제한 시간(초)
    [SerializeField, Min( 0.01f )] float _sampleDist = 1f;       //NavMesh 위치 탐색 반경
    [SerializeField, Min( 0.01f )] float _refreshInterval = 0.3f;       //경로 갱신 간격(초)

    [Header( "----- 공격 -----" )]
    [SerializeField, Min( 0f )] float _attackDist = 0.8f;       //공격 준비에 진입할 수평 거리
    [SerializeField, Min( 0f )] float _attackHeight = 1f;       //접근 완료를 허용할 높이 차이
    [SerializeField, Min( 0f )] float _arriveTolerance = 0.1f;       //접근 정지 위치 오차
    [SerializeField, Min( 0f )] float _attackDelay = 0.4f;       //공격 준비 시간(초)
    [Tooltip( "기존 설정 보존용입니다. 명중 시점은 Attack 클립의 AttackHit 이벤트로 설정합니다." )]
    [SerializeField, Min( 0f )] float _hitDelay;       //이전 타이머 판정 지연 보존
    [Tooltip( "기존 설정 보존용입니다. 포획 장착 시점은 CaptureAttach 이벤트로 설정합니다." )]
    [SerializeField, Min( 0.01f )] float _attackDuration = 0.3f;       //이전 타이머 판정 시간 보존

    [Header( "----- 저지 -----" )]
    [SerializeField, Min( 1 )] int _rescueLimit = 3;     //현재 방에서 보복 분기에 필요한 추가 저지 횟수
    [SerializeField, Range( 0f, 1f )] float _retaliationChance = 0.8f;     //보복 분기에서 플레이어 포획을 시도할 확률

    [Header( "----- 방 이동 -----" )]
    [SerializeField, Range( 0f, 1f )] float _moveRoomChance = 0.8f;     //활동 완료 후 다른 방으로 이동할 확률

    [Header( "----- 도주 -----" )]
    [SerializeField, Min( 0.01f )] float _escapeSpeed = 3.5f;       //초당 도주 거리
    [SerializeField, Min( 0.01f )] float _escapeArriveDist = 0.3f;       //도주 지점 도착 허용 거리
    [SerializeField, Min( 0.01f )] float _escapeConnectDist = 5f;     //출발/도착 위치에서 후보 노드까지 허용할 경로 길이
    [SerializeField, Min( 0.01f )] float _escapeRetryInterval = 0.5f;     //도주 경로 재계산 간격
    [SerializeField, Min( 0.01f )] float _escapeTimeLimit = 15f;     //재계산 대기를 포함한 도주 제한시간
    [SerializeField, Min( 0.01f )] float _escapeStuckTimeLimit = 2f;     //이동 진전 없이 기다릴 최대 시간
    [SerializeField, Min( 0f )] float _escapeArriveTolerance = 0.05f;     //도주 정지 위치 오차

    /// <summary>
    /// 잠복 시간
    /// </summary>
    public float HideDuration => _hideDuration;

    /// <summary>
    /// 변칙 사건 요청과 표적 재탐색 간격
    /// </summary>
    public float SearchInterval => _searchInterval;

    /// <summary>
    /// 목표 탐색 거리
    /// </summary>
    public float SearchDist => _searchDist;

    /// <summary>
    /// NPC 선택 확률
    /// </summary>
    public float NpcTargetChance => _npcTargetChance;

    /// <summary>
    /// 접근 속력
    /// </summary>
    public float ChaseSpeed => _chaseSpeed;

    /// <summary>
    /// 회전 속력
    /// </summary>
    public float RotateSpeed => _rotateSpeed;

    /// <summary>
    /// 이번 접근 시도의 전체 제한시간
    /// </summary>
    public float ChaseTimeLimit => _chaseTimeLimit;

    /// <summary>
    /// NavMesh 위치 탐색 거리
    /// </summary>
    public float SampleDist => _sampleDist;

    /// <summary>
    /// 경로 갱신 간격
    /// </summary>
    public float RefreshInterval => _refreshInterval;

    /// <summary>
    /// 공격 준비에 진입할 거리
    /// </summary>
    public float AttackDist => _attackDist;

    /// <summary>
    /// 접근 완료를 허용할 높이 차이
    /// </summary>
    public float AttackHeight => _attackHeight;

    /// <summary>
    /// 접근 정지 위치 오차
    /// </summary>
    public float ArriveTolerance => _arriveTolerance;

    /// <summary>
    /// 공격 준비 시간
    /// </summary>
    public float AttackDelay => _attackDelay;

    /// <summary>
    /// 공격 재생 시작 후 명중 검사 대기 시간
    /// </summary>
    public float HitDelay => _hitDelay;

    /// <summary>
    /// 명중 검사 시간
    /// </summary>
    public float AttackDuration => _attackDuration;

    /// <summary>
    /// 보복 분기에 필요한 추가 저지 횟수
    /// </summary>
    public int RescueLimit => _rescueLimit;

    /// <summary>
    /// 도주 속력
    /// </summary>
    public float EscapeSpeed => _escapeSpeed;

    /// <summary>
    /// 도주 도착 거리
    /// </summary>
    public float EscapeArriveDist => _escapeArriveDist;

    /// <summary>
    /// 출발/도착 위치와 후보 노드의 연결 허용 거리
    /// </summary>
    public float EscapeConnectDist => _escapeConnectDist;

    /// <summary>
    /// 도주 경로 재계산 간격
    /// </summary>
    public float EscapeRetryInterval => _escapeRetryInterval;

    /// <summary>
    /// 도주 제한시간
    /// </summary>
    public float EscapeTimeLimit => _escapeTimeLimit;

    /// <summary>
    /// 도주 중 정체 제한시간
    /// </summary>
    public float EscapeStuckTimeLimit => _escapeStuckTimeLimit;

    /// <summary>
    /// 도주 정지 위치 오차
    /// </summary>
    public float EscapeArriveTolerance => _escapeArriveTolerance;

    /// <summary>
    /// 보복 분기의 플레이어 포획 시도 확률
    /// </summary>
    public float RetaliationChance => _retaliationChance;

    /// <summary>
    /// 활동 완료 후 방 이동 확률
    /// </summary>
    public float MoveRoomChance => _moveRoomChance;
}
