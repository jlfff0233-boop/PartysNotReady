using UnityEngine;

/// <summary>
/// 플레이어 사운드 - 이동, 포획과 아이템 행동음
/// </summary>
public class PlayerSound : CharacterSound
{
    [Header ( "----- 점프 / 착지 -----" )]
    [SerializeField] SoundData _jumpSound;		//점프
    [SerializeField] SoundData _landSound;		//착지

    [Header( "----- 포획 -----" )]
    [SerializeField] SoundData _capturedSound;       //포획 중 버둥거림 반복음

    [Header( "----- 아이템 행동 -----" )]
    [SerializeField] SoundData _pickUpSound;        //집기와 공급원 획득
    [SerializeField] SoundData _placeSound;        //정상 배치
    [SerializeField] SoundData _throwSound;        //투척

    Player _player;		//플레이어 포획 상태
    Mover _mover;		//플레이어 이동
    bool _hasMoveState;		//이전 이동 상태 기록 여부
    bool _wasGrounded;		//이전 접지 여부

    #region ----- 초기화 -----

    /// <summary>
    /// 플레이어 상태, 이동과 사운드 재생 모듈 연결
    /// </summary>
    /// <param name="player">플레이어</param>
    /// <param name="mover">플레이어 이동 모듈</param>
    /// <param name="soundPlayer">플레이어 전용 사운드 재생 모듈</param>
    public void Init (
        Player player , Mover mover , SoundPlayer soundPlayer )
    {
        Unsubscribe ( );
        base.Init ( soundPlayer );

        _player = player;
        _mover = mover;

        if ( isActiveAndEnabled == true )
        {
            Subscribe ( );
            RestoreCapturedSound ( );
        }
    }

    void OnEnable ( )
    {
        ResetMovement ( );
        Subscribe ( );
        RestoreCapturedSound ( );
    }

    protected override void OnDisable ( )
    {
        Unsubscribe ( );
        base.OnDisable ( );
    }


    /// <summary>
    /// 플레이어의 아이템 행동 성공음 재생
    /// </summary>
    /// <param name="action">완료한 행동</param>
    public void PlayItemAction ( ItemActionType action )
    {
        switch ( action )
        {
            case ItemActionType.PickUp:
                PlaySound( _pickUpSound );
                break;
            case ItemActionType.Place:
                PlaySound( _placeSound );
                break;
            case ItemActionType.Throw:
                PlaySound( _throwSound );
                break;
        }
    }


    /// <summary>
    /// 포획 반복음 재생 상태 적용
    /// </summary>
    /// <param name="isCaptured">포획 여부</param>
    public void SetCaptured ( bool isCaptured )
    {
        if ( isCaptured == true )
        {
            //포획에 따른 이동은 발소리 거리에서 제외
            ResetFootsteps( );
            StartLoop( _capturedSound );
        }
        else
        {
            StopLoop( );
        }
    }


    /// <summary>
    /// 현재 포획 상태에 맞춰 반복음 복구
    /// </summary>
    void RestoreCapturedSound ( )
    {
        if ( _player != null )
            SetCaptured ( _player.IsCaptured );
    }

    /// <summary>
    /// 이동과 포획 이벤트 구독
    /// </summary>
    void Subscribe ( )
    {
        Unsubscribe ( );

        //최초 활성화는 Init보다 먼저 호출될 수 있음
        if ( _mover != null )
        {
            _mover.OnMoveApplied += HandleMoveApplied;
            _mover.OnJumped += HandleJumped;
            _mover.OnMovementReset += HandleMovementReset;
        }

        if ( _player != null )
            _player.OnCaptureChanged += HandleCaptureChanged;
    }

    /// <summary>
    /// 이동과 포획 이벤트 구독 해제
    /// </summary>
    void Unsubscribe ( )
    {
        if ( _mover != null )
        {
            _mover.OnMoveApplied -= HandleMoveApplied;
            _mover.OnJumped -= HandleJumped;
            _mover.OnMovementReset -= HandleMovementReset;
        }

        if ( _player != null )
            _player.OnCaptureChanged -= HandleCaptureChanged;
    }

    #endregion

    #region ----- 핸들 -----

    /// <summary>
    /// 이동 기준 초기화 후 현재 포획음 복구
    /// </summary>
    void HandleMovementReset ( )
    {
        ResetSounds ( );
        RestoreCapturedSound ( );
    }

    /// <summary>
    /// 포획 상태 변경에 따른 반복음 처리
    /// </summary>
    /// <param name="isCaptured">포획 여부</param>
    void HandleCaptureChanged ( bool isCaptured )
    {
        ResetMovement ( );
        SetCaptured ( isCaptured );
    }

    /// <summary>
    /// 실제 이동 결과에 따른 발소리와 착지 처리
    /// </summary>
    /// <param name="result">이동 적용 결과</param>
    void HandleMoveApplied ( MoveResult result )
    {
        if ( result.CanMove == false )
        {
            ResetMovement ( );
            return;
        }

        bool landed =
            _hasMoveState == true &&
            _wasGrounded == false &&
            result.IsGrounded == true;		//이번 이동에서 착지 여부

        _hasMoveState = true;
        _wasGrounded = result.IsGrounded;

        if ( landed == true )
        {
            ResetFootsteps ( );
            PlayLand ( );

            //착지한 프레임에는 발소리 중복 방지
            return;
        }

        UpdateFootsteps (
            result.Distance , result.IsGrounded ,
            result.CanMove , result.IsRunning );
    }

    /// <summary>
    /// 실제 점프 시작 처리
    /// </summary>
    void HandleJumped ( )
    {
        ResetFootsteps ( );
        PlayJump ( );
    }

    #endregion

    #region ----- 재생 -----

    /// <summary>
    /// 점프음 재생
    /// </summary>
    public void PlayJump ( )
    {
        PlaySound ( _jumpSound );
    }

    /// <summary>
    /// 착지음 재생
    /// </summary>
    public void PlayLand ( )
    {
        PlaySound ( _landSound );
    }

    /// <summary>
    /// 사운드와 이동 / 접지 기록 초기화
    /// </summary>
    public override void ResetSounds ( )
    {
        base.ResetSounds ( );
        ResetMovement ( );
    }

    /// <summary>
    /// 이동과 접지 기록 초기화
    /// </summary>
    void ResetMovement ( )
    {
        ResetFootsteps ( );
        _hasMoveState = false;
        _wasGrounded = false;
    }

    #endregion
}
