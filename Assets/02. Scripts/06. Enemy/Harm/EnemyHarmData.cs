using UnityEngine;

/// <summary>
/// 공격 적 설정 데이터 - 저지 횟수와 잠복, 이동, 공격 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Enemy/EnemyHarmData" )]
public class EnemyHarmData : ScriptableObject
{
    [Header( "----- 공격 전환 -----" )]
    [SerializeField, Min( 1 )] int _requiredCount;       //공격 전환에 필요한 저지 횟수
    [SerializeField, Range( 0f, 1f )]
    float _retaliationChance = 0.8f;     //저지 기준 도달 시 보복 확률

    [Header( "----- 잠복 -----" )]
    [SerializeField, Min( 0f )] float _hideDuration;        //활동 재개 전 잠복 시간
    [SerializeField, Min( 0.01f )] float _eventInterval;        //사건 발생과 목표 재탐색 간격

    [Header( "----- 접근 -----" )]
    [SerializeField, Min( 0.01f )] float _searchDist;       //플레이어 탐색 거리
    [SerializeField, Min( 0.01f )] float _chaseSpeed;       //접근 속력
    [SerializeField, Min( 0.01f )] float _attackDist;       //공격 상태 진입 거리
    [SerializeField, Min( 0.01f )] float _chaseTimeLimit;       //접근 제한 시간
    [SerializeField, Min( 0f )] float _arriveTolerance;       //접근 정지 위치 오차
    [SerializeField, Min( 0.01f )] float _loseTargetDist;       //수평 거리 기준 추격 중단 거리

    [Header( "----- 이동 -----" )]
    [SerializeField, Min( 0.01f )] float _rotateSpeed;       //회전 속력
    [SerializeField, Min( 0.01f )] float _sampleDist;       //NavMesh 위치 탐색 거리
    [SerializeField, Min( 0.01f )] float _refreshInterval;       //목적지 갱신 간격

    [Header( "----- 절도 -----" )]
    [SerializeField] ItemDataMap _stealItemMap;     //절도 허용 아이템 맵
    [SerializeField, Min( 1 )] int _maxCapacity = 3;     //최대 수용량
    [SerializeField, Min( 0.1f )] float _collectTimeLimit = 10f;     //수집 제한시간
    [SerializeField, Min( 0.01f )] float _collectSpeed = 3f;     //아이템 접근 속력
    [SerializeField, Min( 0.01f )] float _takeDist = 1f;     //아이템 확보 거리
    [SerializeField, Min( 0f )] float _takeHeight = 1.5f;     //확보 가능한 높이 차이

    [Header( "----- 공격 -----" )]
    [SerializeField, Min( 0f )] float _attackDelay;       //공격 준비 시간
    [SerializeField, Min( 0.01f )] float _attackDuration;       //명중 검사 지속 시간
    [SerializeField, Min( 0f )] float _attackHeight;       //공격 접근 시 허용 높이 차이
    [SerializeField, Min( 0.01f )] float _stunDuration;       //명중 시 스턴 시간

    [Header( "----- 도주 -----" )]
    [SerializeField, Min( 0.01f )] float _escapeSpeed;       //도주 속력
    [SerializeField, Min( 0.01f )] float _escapeArriveDist;       //도주 지점 도착 허용 거리
    [SerializeField, Min( 0.01f )] float _escapeTimeLimit;       //도주 제한 시간
    [SerializeField, Min( 0.1f )] float _escapeMinDistance = 18f;		//도주 경로의 최소 이동 거리
    [SerializeField, Min( 0.01f )] float _escapeConnectDist = 5f;     //출발/도착 위치에서 후보 노드까지 허용할 경로 길이
    [SerializeField, Min( 0.01f )] float _escapeRetryInterval = 0.5f;     //도주 경로 재계산 간격
    [SerializeField, Min( 0.01f )] float _escapeStuckTimeLimit = 2f;     //도주 중 진행 없이 기다릴 최대 시간
    [SerializeField, Min( 0f )] float _escapeArriveTolerance = 0.05f;     //도주 정지 위치 오차


    [Header( "----- 방 이동 -----" )]
    [SerializeField, Range( 0f, 1f )] float _moveRoomChance = 0.8f;     //활동 종료 후 다른 방으로 이동할 확률



    /// <summary>
    /// 공격 전환에 필요한 저지 횟수
    /// </summary>
    public int RequiredCount => _requiredCount;

    /// <summary>
    /// 보복 선택 확률
    /// </summary>
    public float RetaliationChance => _retaliationChance;

    /// <summary>
    /// 활동 재개 전 잠복 시간
    /// </summary>
    public float HideDuration => _hideDuration;

    /// <summary>
    /// 사건 발생과 목표 재탐색 간격
    /// </summary>
    public float EventInterval => _eventInterval;

    /// <summary>
    /// 플레이어 탐색 거리
    /// </summary>
    public float SearchDist => _searchDist;

    /// <summary>
    /// 접근 속력
    /// </summary>
    public float ChaseSpeed => _chaseSpeed;

    /// <summary>
    /// 공격 상태 진입 거리
    /// </summary>
    public float AttackDist => _attackDist;

    /// <summary>
    /// 접근 제한 시간
    /// </summary>
    public float ChaseTimeLimit => _chaseTimeLimit;

    /// <summary>
    /// 회전 속력
    /// </summary>
    public float RotateSpeed => _rotateSpeed;

    /// <summary>
    /// NavMesh 위치 탐색 거리
    /// </summary>
    public float SampleDist => _sampleDist;

    /// <summary>
    /// 목적지 갱신 간격
    /// </summary>
    public float RefreshInterval => _refreshInterval;

    /// <summary>
    /// 공격 준비 시간
    /// </summary>
    public float AttackDelay => _attackDelay;

    /// <summary>
    /// 명중 검사 지속 시간
    /// </summary>
    public float AttackDuration => _attackDuration;

    /// <summary>
    /// 공격 접근 시 허용 높이 차이
    /// </summary>
    public float AttackHeight => _attackHeight;

    /// <summary>
    /// 도주 속력
    /// </summary>
    public float EscapeSpeed => _escapeSpeed;

    /// <summary>
    /// 도주 지점 도착 허용 거리
    /// </summary>
    public float EscapeArriveDist => _escapeArriveDist;

    /// <summary>
    /// 도주 제한 시간
    /// </summary>
    public float EscapeTimeLimit => _escapeTimeLimit;

    /// <summary>
    /// 도주 경로의 최소 이동 거리
    /// </summary>
    public float EscapeMinDistance => _escapeMinDistance;

    /// <summary>
    /// 접근 정지 위치 오차
    /// </summary>
    public float ArriveTolerance => _arriveTolerance;

    /// <summary>
    /// 명중 시 스턴 시간
    /// </summary>
    public float StunDuration => _stunDuration;

    /// <summary>
    /// 목표와의 수평 거리 기준 추격 중단 거리
    /// </summary>
    public float LoseTargetDist => _loseTargetDist;

    /// <summary>
    /// 절도 허용 아이템 맵
    /// </summary>
    public ItemDataMap StealItemMap => _stealItemMap;

    /// <summary>
    /// 최대 수용량
    /// </summary>
    public int MaxCapacity => _maxCapacity;

    /// <summary>
    /// 수집 제한시간
    /// </summary>
    public float CollectTimeLimit => _collectTimeLimit;

    /// <summary>
    /// 아이템 접근 속력
    /// </summary>
    public float CollectSpeed => _collectSpeed;

    /// <summary>
    /// 아이템 확보 거리
    /// </summary>
    public float TakeDist => _takeDist;

    /// <summary>
    /// 확보 가능한 높이 차이
    /// </summary>
    public float TakeHeight => _takeHeight;

    /// <summary>
    /// 활동 종료 후 방 이동 확률
    /// </summary>
    public float MoveRoomChance => _moveRoomChance;

    /// <summary>
    /// 도주 후보 노드까지 허용할 경로 길이
    /// </summary>
    public float EscapeConnectDist => _escapeConnectDist;

    /// <summary>
    /// 도주 경로 재계산 간격
    /// </summary>
    public float EscapeRetryInterval => _escapeRetryInterval;

    /// <summary>
    /// 도주 중 이동 정체 허용 시간
    /// </summary>
    public float EscapeStuckTimeLimit => _escapeStuckTimeLimit;

    /// <summary>
    /// 도주 정지 위치 오차
    /// </summary>
    public float EscapeArriveTolerance => _escapeArriveTolerance;
}
