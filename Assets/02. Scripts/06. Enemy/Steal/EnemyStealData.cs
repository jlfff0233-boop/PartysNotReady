using UnityEngine;

/// <summary>
/// 절도 적 설정 데이터 - 비행, 음식 탐색과 확보 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Enemy/EnemyStealData" )]
public class EnemyStealData : ScriptableObject
{
    [Header( "----- 이동 -----" )]
    [SerializeField, Min( 0.01f )] float _moveSpeed = 3f;		//비행 속력
    [SerializeField, Min( 0f )] float _rotateSpeed = 360f;		//회전 속력
    [SerializeField, Min( 0.01f )] float _arriveDist = 0.1f;		//도착 거리
    [SerializeField, Min( 0.01f )] float _moveRetryInterval = 0.2f;		//이동 재시도 간격
    [SerializeField, Min( 0.01f )] float _moveFailureTime = 3f;		//지속 차단 제한시간

    [Header( "----- 절도 -----" )]
    [SerializeField] LayerMask _itemMask;		//아이템 레이어마스크
    [SerializeField, Min( 0.01f )] float _approachHeight = 1f;		//확보 위치 위 접근 높이
    [SerializeField, Min( 0.01f )] float _searchInterval = 0.2f;		//탐색 간격

    [SerializeField, Min( 0.01f )] float _searchDist = 10f;		//탐색 거리
    [SerializeField, Min( 0.01f )] float _takeDist = 0.5f;		//확보 거리
    [SerializeField, Min( 0.01f )] float _searchTime = 10f;		//최대 탐색 시간
    [SerializeField, Range( 0f, 1f )] float _retryChance = 0.5f;		//재탈취 확률


    /// <summary>
    /// 비행 속력
    /// </summary>
    public float MoveSpeed => _moveSpeed;

    /// <summary>
    /// 회전 속력
    /// </summary>
    public float RotateSpeed => _rotateSpeed;

    /// <summary>
    /// 도착 거리
    /// </summary>
    public float ArriveDist => _arriveDist;

    /// <summary>
    /// 이동 재시도 간격
    /// </summary>
    public float MoveRetryInterval => _moveRetryInterval;

    /// <summary>
    /// 지속 차단 제한시간
    /// </summary>
    public float MoveFailureTime => _moveFailureTime;

    /// <summary>
    /// 아이템 레이어마스크
    /// </summary>
    public LayerMask ItemMask => _itemMask;

    /// <summary>
    /// 확보 위치 위 접근 높이
    /// </summary>
    public float ApproachHeight => _approachHeight;

    /// <summary>
    /// 탐색 간격
    /// </summary>
    public float SearchInterval => _searchInterval;

    /// <summary>
    /// 탐색 거리
    /// </summary>
    public float SearchDist => _searchDist;

    /// <summary>
    /// 확보 거리
    /// </summary>
    public float TakeDist => _takeDist;

    /// <summary>
    /// 최대 탐색 시간
    /// </summary>
    public float SearchTime => _searchTime;

    /// <summary>
    /// 재탈취 확률
    /// </summary>
    public float RetryChance => _retryChance;
}