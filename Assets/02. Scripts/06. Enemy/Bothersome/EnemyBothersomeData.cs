using UnityEngine;

/// <summary>
/// 방해 적 설정 데이터 - 공 놀이, 추적과 회수 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Enemy/EnemyBothersomeData" )]
public class EnemyBothersomeData : ScriptableObject
{
    [Header( "----- 놀이 -----" )]
    [SerializeField] int _reqCount;		//필요 공 놀이 횟수
    [SerializeField] float _frontDist;		//정면 유지 거리
    [SerializeField] float _deliveryDist;		//전달 대기 거리
    [SerializeField] float _resumeDist;		//추적 재개 거리

    [Header( "----- 이동 -----" )]
    [SerializeField] float _followSpeed;		//추적 속력
    [SerializeField] float _fetchSpeed;		//회수 속력
    [SerializeField] float _rotateSpeed;		//초당 회전 각도
    [SerializeField] float _searchDistance;		//NavMesh 위치 탐색 거리
    [SerializeField] float _repathInterval;		//목적지 갱신 간격

    [Header( "----- 회수 -----" )]
    [SerializeField] float _fetchDist;		//보유 위치 기준 회수 거리
    [SerializeField] float _recoverDelay;		//이동 진행이 없을 때 복구 대기
    [SerializeField] float _progressDist;		//이동 진행 인정 거리


    /// <summary>
    /// 필요 공 놀이 횟수
    /// </summary>
    public int ReqCount => _reqCount;

    /// <summary>
    /// 정면 유지 거리
    /// </summary>
    public float FrontDist => _frontDist;

    /// <summary>
    /// 전달 대기 거리
    /// </summary>
    public float DeliveryDist => _deliveryDist;

    /// <summary>
    /// 추적 재개 거리
    /// </summary>
    public float ResumeDist => _resumeDist;

    /// <summary>
    /// 추적 속력
    /// </summary>
    public float FollowSpeed => _followSpeed;

    /// <summary>
    /// 회수 속력
    /// </summary>
    public float FetchSpeed => _fetchSpeed;

    /// <summary>
    /// 초당 회전 각도
    /// </summary>
    public float RotateSpeed => _rotateSpeed;

    /// <summary>
    /// NavMesh 위치 탐색 거리
    /// </summary>
    public float SearchDistance => _searchDistance;

    /// <summary>
    /// 목적지 갱신 간격
    /// </summary>
    public float RepathInterval => _repathInterval;

    /// <summary>
    /// 보유 위치 기준 회수 거리
    /// </summary>
    public float FetchDist => _fetchDist;

    /// <summary>
    /// 이동 진행이 없을 때 복구 대기
    /// </summary>
    public float RecoverDelay => _recoverDelay;

    /// <summary>
    /// 이동 진행 인정 거리
    /// </summary>
    public float ProgressDist => _progressDist;
}