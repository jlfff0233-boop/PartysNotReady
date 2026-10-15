using UnityEngine;

/// <summary>
/// 절도 적 사운드 - 실제 비행에 따른 날갯짓과 음식 확보음
/// </summary>
public class StealSound : SoundBase
{
    [Header( "----- 행동 사운드 -----" )]
    [SerializeField] SoundData _flightSound;       //날갯짓 반복음
    [SerializeField] SoundData _takeSound;       //음식 확보음

    FlightMover _mover;       //비행 이동 모듈
    bool _hasMovement;       //이번 프레임 실제 비행 이동 여부

    /// <summary>
    /// 기존 비행 연결 정리와 재생 모듈 연결
    /// </summary>
    /// <param name="soundPlayer">적 전용 사운드 재생 모듈</param>
    public override void Init ( SoundPlayer soundPlayer )
    {
        Unsubscribe( );
        _mover = null;
        base.Init( soundPlayer );
    }

    /// <summary>
    /// 비행 이동과 재생 모듈 연결
    /// </summary>
    /// <param name="mover">비행 이동 모듈</param>
    /// <param name="soundPlayer">적 전용 사운드 재생 모듈</param>
    public void InitFlight ( FlightMover mover, SoundPlayer soundPlayer )
    {
        Init( soundPlayer );
        _mover = mover;

        if ( isActiveAndEnabled == true )
            Subscribe( );
    }

    void OnEnable ()
    {
        _hasMovement = false;
        Subscribe( );
    }

    protected override void OnDisable ()
    {
        Unsubscribe( );
        base.OnDisable( );
    }

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
        //같은 프레임에서 한 번이라도 움직인 기록 유지
        if ( displacement.sqrMagnitude > 0f )
            _hasMovement = true;
    }

    /// <summary>
    /// 정지에 따른 이동 기록 초기화
    /// </summary>
    void HandleMovementReset ()
    {
        _hasMovement = false;
    }

    /// <summary>
    /// 비행 이동 알림 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _mover == null )
            return;

        _mover.OnMoveApplied -= HandleMoveApplied;
        _mover.OnMovementReset -= HandleMovementReset;
    }

    /// <summary>
    /// 비행 이동 알림 연결
    /// </summary>
    void Subscribe ()
    {
        Unsubscribe( );

        //최초 활성화에서는 이동 모듈이 아직 연결되지 않을 수 있음
        if ( _mover == null )
            return;

        _mover.OnMoveApplied += HandleMoveApplied;
        _mover.OnMovementReset += HandleMovementReset;
    }

    /// <summary>
    /// 사운드와 비행 이동 기록 초기화
    /// </summary>
    public override void ResetSounds ()
    {
        base.ResetSounds( );
        _hasMovement = false;
    }


    /// <summary>
    /// 음식 확보음 재생
    /// </summary>
    public void PlayTake ()
    {
        PlaySound( _takeSound );
    }

    /// <summary>
    /// 실제 비행 이동에 따른 반복음 갱신
    /// </summary>
    /// <param name="canMove">비행음 허용 여부</param>
    public void UpdateFlight ( bool canMove )
    {
        bool isFlying =
            canMove == true &&
            isActiveAndEnabled == true &&
            _mover != null &&
            _mover.isActiveAndEnabled == true &&
            _hasMovement == true;       //이번 프레임 비행음 재생 여부

        //다음 프레임은 새로운 이동 결과로 판단
        _hasMovement = false;

        if ( isFlying == true )
            StartLoop( _flightSound );
        else
            StopLoop( );
    }
}
