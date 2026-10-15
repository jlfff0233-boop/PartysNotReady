using UnityEngine;

/// <summary>
/// 적 사운드 - 발소리, 행동 단발음과 반복음
/// </summary>
public class EnemySound : CharacterSound
{
    [Header ( "----- 공격 -----" )]
    [SerializeField] SoundData _attackSound;		//공격

    [Header ( "----- 먹기 -----" )]
    [SerializeField] SoundData _eatSound;		//먹기 반복음

    [Header ( "----- 수집 -----" )]
    [SerializeField] SoundData _collectSound;		//수집 반복음

    [Header ( "----- 포획 -----" )]
    [SerializeField] SoundData _captureSound;		//포획 성공음

    [Header ( "----- 비행 -----" )]
    [SerializeField] SoundData _flightSound;		//날갯짓 반복음

    [Header ( "----- 행동 표현 -----" )]
    [SerializeField] SoundData _requestSound;		//요구
    [SerializeField] SoundData _satisfiedSound;		//만족
    [SerializeField] SoundData _madSound;		//분노
    [SerializeField] SoundData _takeSound;		//음식 확보

    CharacterMover _mover;		//지상 이동 모듈
    FlightMover _flightMover;		//비행 이동 모듈
    bool _hasFlightMovement;		//이번 프레임 실제 비행 이동 여부
    bool _isKidnapping;		//포획 대상 운반 여부

    /// <summary>
    /// 기존 이동 연결 해제와 사운드 재생 모듈 연결
    /// </summary>
    /// <param name="soundPlayer">적 전용 사운드 재생 모듈</param>
    public override void Init ( SoundPlayer soundPlayer )
    {
        Unsubscribe( );
        _mover = null;
        _flightMover = null;
        base.Init( soundPlayer );
    }

    /// <summary>
    /// 지상 이동과 사운드 재생 모듈 연결
    /// </summary>
    /// <param name="mover">지상 이동 모듈</param>
    /// <param name="soundPlayer">적 전용 사운드 재생 모듈</param>
    public void Init ( CharacterMover mover , SoundPlayer soundPlayer )
    {
        Init( soundPlayer );
        _mover = mover;
    }

    /// <summary>
    /// 비행 이동과 사운드 재생 모듈 연결
    /// </summary>
    /// <param name="mover">비행 이동 모듈</param>
    /// <param name="soundPlayer">적 전용 사운드 재생 모듈</param>
    public void InitFlight ( FlightMover mover, SoundPlayer soundPlayer )
    {
        Init( soundPlayer );
        _flightMover = mover;

        if ( isActiveAndEnabled == true )
            Subscribe( );
    }

    /// <summary>
    /// 재활성화 시 비행 이동 기록과 연결 복구
    /// </summary>
    void OnEnable ()
    {
        _hasFlightMovement = false;
        Subscribe( );
    }

    /// <summary>
    /// 비활성화 시 비행 알림 연결과 사운드 정리
    /// </summary>
    protected override void OnDisable ()
    {
        Unsubscribe( );
        base.OnDisable( );
    }

    /// <summary>
    /// 파괴 시 비행 이동 알림 연결 해제
    /// </summary>
    void OnDestroy ()
    {
        Unsubscribe( );
    }


    /// <summary>
    /// 실제 비행 이동 기록
    /// </summary>
    /// <param name="displacement">실제 이동량</param>
    void HandleMoveApplied ( Vector3 displacement )
    {
        //한 프레임 안에서 한 번이라도 움직인 기록 유지
        if ( displacement.sqrMagnitude > 0f )
            _hasFlightMovement = true;
    }

    /// <summary>
    /// 정지에 따른 비행 이동 기록 초기화
    /// </summary>
    void HandleMovementReset ()
    {
        _hasFlightMovement = false;
    }

    /// <summary>
    /// 비행 이동 알림 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _flightMover == null )
            return;

        _flightMover.OnMoveApplied -= HandleMoveApplied;
        _flightMover.OnMovementReset -= HandleMovementReset;
    }

    /// <summary>
    /// 비행 이동 알림 연결
    /// </summary>
    void Subscribe ()
    {
        Unsubscribe( );

        //지상 적 또는 초기화 전에는 비행 알림을 연결하지 않음
        if ( _flightMover == null )
            return;

        _flightMover.OnMoveApplied += HandleMoveApplied;
        _flightMover.OnMovementReset += HandleMovementReset;
    }


    /// <summary>
    /// 사운드와 포획 및 비행 이동 기록 정리
    /// </summary>
    public override void ResetSounds ( )
    {
        base.ResetSounds ( );
        _isKidnapping = false;
        _hasFlightMovement = false;
    }


    /// <summary>
    /// 실제 지상 이동에 따른 발소리 갱신
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    /// <param name="canMove">이동음 허용 여부</param>
    public void UpdateMovement ( float deltaTime , bool canMove )
    {
        //초기화 전, 행동 종료와 이동 불가 상태에서는 거리 기록 정리
        if ( canMove == false ||
             _mover == null ||
             _mover.CanNavigate == false ||
             deltaTime <= 0f )
        {
            ResetFootsteps ( );
            return;
        }

        Vector3 velocity = _mover.Velocity;		//실제 이동 속도
        velocity.y = 0f;

        //지상 에이전트의 실제 수평 속도로 이동 거리 계산
        float distance = velocity.magnitude * deltaTime;		//이번 프레임 이동 거리
        UpdateFootsteps ( distance , true , true , false );
    }

    /// <summary>
    /// 공격음 재생
    /// </summary>
    public void PlayAttack ( )
    {
        PlaySound ( _attackSound );
    }

    /// <summary>
    /// 요구음 재생
    /// </summary>
    public void PlayRequest ( )
    {
        PlaySound ( _requestSound );
    }

    /// <summary>
    /// 만족음 재생
    /// </summary>
    public void PlaySatisfied ( )
    {
        PlaySound ( _satisfiedSound );
    }

    /// <summary>
    /// 분노음 재생
    /// </summary>
    public void PlayMad ( )
    {
        PlaySound ( _madSound );
    }

    /// <summary>
    /// 음식 확보음 재생
    /// </summary>
    public void PlayTake ( )
    {
        PlaySound ( _takeSound );
    }

    /// <summary>
    /// 먹기 반복음 재생 상태 적용
    /// </summary>
    /// <param name="isEating">먹기 진행 여부</param>
    public void SetEating ( bool isEating )
    {
        if ( isEating == true )
            StartLoop ( _eatSound );
        else
            StopLoop ( );
    }

    /// <summary>
    /// 수집 반복음 재생 상태 적용
    /// </summary>
    /// <param name="isCollecting">수집 진행 여부</param>
    public void SetCollecting ( bool isCollecting )
    {
        if ( isCollecting == true )
            StartLoop ( _collectSound );
        else
            StopLoop ( );
    }

    /// <summary>
    /// 비행 반복음 재생 상태 적용
    /// </summary>
    /// <param name="isFlying">비행 이동 여부</param>
    public void SetFlying ( bool isFlying )
    {
        if ( isFlying == true )
            StartLoop ( _flightSound );
        else
            StopLoop ( );
    }

    /// <summary>
    /// 실제 비행 이동 기록에 따른 날갯짓 반복음 갱신
    /// </summary>
    /// <param name="canMove">비행음 허용 여부</param>
    public void UpdateFlight ( bool canMove )
    {
        bool isFlying =
            canMove == true &&
            isActiveAndEnabled == true &&
            _flightMover != null &&
            _flightMover.isActiveAndEnabled == true &&
            _hasFlightMovement == true;		//이번 프레임 비행음 재생 여부

        //다음 프레임은 새 이동 결과로 판단
        _hasFlightMovement = false;
        SetFlying( isFlying );
    }

    /// <summary>
    /// 포획 대상 운반 상태와 성공음 적용
    /// </summary>
    /// <param name="isKidnapping">포획 대상 운반 여부</param>
    public void SetKidnap ( bool isKidnapping )
    {
        if ( _isKidnapping == isKidnapping )
            return;

        _isKidnapping = isKidnapping;

        //실제 대상 확보로 운반을 시작한 경우에만 재생
        if ( isKidnapping == true )
            PlaySound ( _captureSound );
    }
}
