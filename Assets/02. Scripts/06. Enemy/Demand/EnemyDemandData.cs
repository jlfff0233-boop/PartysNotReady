using UnityEngine;

/// <summary>
/// 요구 적 설정 데이터 - 음식 취향, 이동, 난동과 납치 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Enemy/EnemyDemandData" )]
public class EnemyDemandData : ScriptableObject
{
    [Header( "----- 음식 -----" )]
    [SerializeField] FoodDataMap _likedFoods;		//좋아하는 음식 맵
    [SerializeField] FoodDataMap _dislikedFoods;		//싫어하는 음식 맵
    [SerializeField, Min( 1f )] float _maxSatisfaction = 100f;		//최대 만족도
    [SerializeField, Min( 0f )] float _eatTime = 2f;		//먹기 시간

    [Header( "----- 이동 -----" )]
    [SerializeField] float _moveSpeed = 2f;		//이동 속력
    [SerializeField] float _rotateSpeed = 360f;		//회전 속력
    [SerializeField, Min( 0.01f )] float _stopDist = 0.2f;		//정지 거리

    [Header( "----- 난동 -----" )]
    [SerializeField, Min( 1 )] int _attackCount = 3;		//공격 횟수
    [SerializeField, Min( 0 )] int _ingredientLossCount = 1;		//공격당 재료 소실 상한
    [SerializeField, Min( 0f )] float _attackDelay = 0.5f;		//공격 전 대기 시간
    [SerializeField, Min( 0.01f )] float _attackDuration = 0.3f;		//공격 지속 시간
    [SerializeField, Min( 0.1f )] float _approachTimeout = 10f;		//접근 제한 시간
    [SerializeField, Min( 0f )] float _pushPower = 3f;		//밀치기 힘
    [SerializeField, Min( 0f )] float _pushUpPower = 1f;		//띄우기 힘

    [Header( "----- 납치 -----" )]
    [SerializeField, Range( 0f, 1f )] float _kidnapChance = 0.2f;		//분노 시 납치 선택 확률
    [SerializeField, Min( 0.01f )] float _kidnapDist = 1f;		//표적 접근 완료 거리
    [SerializeField, Min( 0.01f )] float _kidnapCheckDist = 3f;		//표적 주변 NavMesh 탐색 거리
    [SerializeField, Min( 0.01f )] float _kidnapRefreshInterval = 0.3f;		//접근 목적지 갱신 간격
    [SerializeField, Min( 0f )] float _kidnapHeight = 1f;		//표적과 허용할 높이 차이
    [SerializeField, Min( 0f )] float _kidnapTolerance = 0.1f;		//정지 위치 오차 허용 거리


    /// <summary>
    /// 좋아하는 음식 맵
    /// </summary>
    public FoodDataMap LikedFoods => _likedFoods;

    /// <summary>
    /// 싫어하는 음식 맵
    /// </summary>
    public FoodDataMap DislikedFoods => _dislikedFoods;

    /// <summary>
    /// 최대 만족도
    /// </summary>
    public float MaxSatisfaction => _maxSatisfaction;

    /// <summary>
    /// 먹기 시간
    /// </summary>
    public float EatTime => _eatTime;

    /// <summary>
    /// 이동 속력
    /// </summary>
    public float MoveSpeed => _moveSpeed;

    /// <summary>
    /// 회전 속력
    /// </summary>
    public float RotateSpeed => _rotateSpeed;

    /// <summary>
    /// 정지 거리
    /// </summary>
    public float StopDist => _stopDist;

    /// <summary>
    /// 공격 횟수
    /// </summary>
    public int AttackCount => _attackCount;

    /// <summary>
    /// 공격당 재료 소실 상한
    /// </summary>
    public int IngredientLossCount => _ingredientLossCount;

    /// <summary>
    /// 공격 전 대기 시간
    /// </summary>
    public float AttackDelay => _attackDelay;

    /// <summary>
    /// 공격 지속 시간
    /// </summary>
    public float AttackDuration => _attackDuration;

    /// <summary>
    /// 접근 제한 시간
    /// </summary>
    public float ApproachTimeout => _approachTimeout;

    /// <summary>
    /// 밀치기 힘
    /// </summary>
    public float PushPower => _pushPower;

    /// <summary>
    /// 띄우기 힘
    /// </summary>
    public float PushUpPower => _pushUpPower;

    /// <summary>
    /// 분노 시 납치 선택 확률
    /// </summary>
    public float KidnapChance => _kidnapChance;

    /// <summary>
    /// 표적 접근 완료 거리
    /// </summary>
    public float KidnapDist => _kidnapDist;

    /// <summary>
    /// 표적 주변 NavMesh 탐색 거리
    /// </summary>
    public float KidnapCheckDist => _kidnapCheckDist;

    /// <summary>
    /// 접근 목적지 갱신 간격
    /// </summary>
    public float KidnapRefreshInterval => _kidnapRefreshInterval;

    /// <summary>
    /// 표적과 허용할 높이 차이
    /// </summary>
    public float KidnapHeight => _kidnapHeight;

    /// <summary>
    /// 정지 위치 오차 허용 거리
    /// </summary>
    public float KidnapTolerance => _kidnapTolerance;
}
