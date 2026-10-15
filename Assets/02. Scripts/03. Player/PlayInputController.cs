using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이 입력 제어 - 상호작용, 제작 화면과 상태 제한 연결
/// </summary>
public class PlayInputController
{
    Player _player;		//플레이어
    InputHandler _inputHandler;		//입력
    CameraFocus _cameraFocus;		//시점 대상
    PlayerHud _playerHud;		//플레이어 HUD
    ProduceHud _produceHud;		//제작 HUD

    bool _isPaused; //일시정지 입력 제한
    bool _isEnabled;		//입력 처리 허용 여부
    bool _isThrowInput;		//투척 입력 여부
    bool _isWorldInput;		//월드 상호작용 입력 여부
    bool _canInteract;		//상호작용 완료 허용 여부
    bool _isProduceUiOpen;		//제작 UI 표시 여부
    string _actionName = string.Empty;		//현재 누르기의 행동 이름

    RecipeInputController _recipeInput;		//레시피 대상과 제작 입력
    InteractionHighlight _highlight = new( );		//상호작용 대상 강조

    #region ----- 초기화 -----

    /// <summary>
    /// 입력과 상태 알림 연결
    /// </summary>
    /// <param name="player">플레이어</param>
    /// <param name="inputHandler">입력</param>
    /// <param name="cameraFocus">시점 대상</param>
    /// <param name="playerHud">플레이어 HUD</param>
    /// <param name="produceHud">제작 HUD</param>
    /// <param name="productPresenter">제작 목록</param>
    public PlayInputController (
        Player player, InputHandler inputHandler, CameraFocus cameraFocus,
        PlayerHud playerHud, ProduceHud produceHud, ProductionManager productPresenter )
    {
        _player = player;
        _inputHandler = inputHandler;
        _cameraFocus = cameraFocus;
        _playerHud = playerHud;
        _produceHud = produceHud;
        _recipeInput = new RecipeInputController(
            player, productPresenter, produceHud, () => _isEnabled && _isPaused == false );

        _player.OnStunChanged += HandleStunChanged;
        _player.OnCaptureChanged += HandleCaptureChanged;
        _player.OnCaptureYawChanged += _cameraFocus.ApplyCaptureYaw;


        _produceHud.OnUiVisibleChanged += HandleProduceUiVisibleChanged;
    }

    /// <summary>
    /// 일시정지에 따른 입력, 홀딩과 커서 적용
    /// </summary>
    /// <param name="paused">일시정지 여부</param>
    public void SetPaused ( bool paused )
    {
        _isPaused = paused;
        CancelActionInput( );
        _player.Move( Vector2.zero );
        _player.Run( false );
        _cameraFocus.SetLocked( _isEnabled == false || paused || _player.IsStunned );
        SetPlayInput( _isEnabled );
        UpdateCursor( );
    }

    /// <summary>
    /// 플레이 입력 시작
    /// </summary>
    public void Begin ()
    {
        _isEnabled = true;
        SetPlayInput( true );
    }

    /// <summary>
    /// 종료 중 입력 재연결 차단
    /// </summary>
    public void Suspend ()
    {
        _isEnabled = false;
        _highlight.Clear( );

        //씬 종료 시 HUD가 먼저 파괴됐어도 입력 종료는 계속 진행
        if ( _playerHud != null )
            _playerHud.SetActionName( string.Empty );
    }

    /// <summary>
    /// 입력 구독 해제와 취소
    /// </summary>
    public void Stop ()
    {
        Suspend( );
        SetPlayInput( false );
        CancelActionInput( );
    }

    /// <summary>
    /// 상태 알림 연결 해제
    /// </summary>
    public void Dispose ()
    {
        Stop( );

        if ( _player != null )
        {
            _player.OnStunChanged -= HandleStunChanged;
            _player.OnCaptureChanged -= HandleCaptureChanged;

            if ( _cameraFocus != null )
                _player.OnCaptureYawChanged -= _cameraFocus.ApplyCaptureYaw;
        }

        if ( _produceHud != null )
        {
            _produceHud.OnUiVisibleChanged -= HandleProduceUiVisibleChanged;
        }

        _recipeInput.Dispose( );
    }

    #endregion

    #region ----- 핸들 -----

    /// <summary>
    /// 레시피 표시 중 오른쪽 드래그로 시점 회전
    /// </summary>
    /// <param name="direction">시점 입력</param>
    void HandleLookInput ( Vector2 direction )
    {
        if ( _produceHud.IsRecipeOpen == true &&
             ( Mouse.current == null || Mouse.current.rightButton.isPressed == false ) )
            return;

        _cameraFocus.Rotate( direction );
    }

    /// <summary>
    /// 상호작용 입력 시작과 행동 이름 저장
    /// </summary>
    void HandleInteractStarted ()
    {
        if ( _isEnabled == false || _isPaused == true )
            return;

        CancelActionInput( );

        //플레이어가 행동할 수 없는 상태면 종료
        if ( _player.IsActionBlocked == true )
            return;

        //레시피 패널이 열려 있다면
        if ( _isProduceUiOpen == true )
        {
            _canInteract = _recipeInput.BeginIngredientInteract( );

            if ( _canInteract == true )
                _actionName = _player.ItemActionName;

            return;
        }

        //진행 중인 제작 미니게임은 누른 순간 판정
        if ( _player.TryProduceMinigame( ) == true )
            return;

        //월드 상호작용 대상이 있다면 홀딩 입력으로 처리
        _isWorldInput = _player.BeginWorldInteract( );

        if ( _isWorldInput == true )
        {
            _canInteract = true;
            _actionName = _player.WorldActionName;
            return;
        }

        //일반 상호작용 대상이 있다면 홀딩 입력으로 처리
        if ( _player.BeginItemInteract( ) == true )
        {
            _canInteract = true;
            _actionName = _player.ItemActionName;
            return;
        }

        //재료 집기와 배치 실패는 투척으로 전환하지 않음
        if ( _player.IsIngredientInteractBlocked == true )
            return;

        //상호작용 대상이 없고 투척 가능하다면 즉시 던지기
        _isThrowInput = _player.HasThrowTarget( );

        if ( _isThrowInput == true )
            _player.Throw( );
    }


    /// <summary>
    /// 홀딩 완료 시 상호작용 실행
    /// </summary>
    void HandleInteractCompleted ()
    {
        CompleteActionInput( );
    }

    /// <summary>
    /// 입력 시작 동작을 확인하고 상호작용 완료
    /// </summary>
    void CompleteActionInput ()
    {
        if ( _isEnabled == false || _isPaused == true || _canInteract == false )
            return;

        if ( CanContinueActionInput( ) == false )
        {
            CancelActionInput( );
            return;
        }

        //같은 누르기의 완료를 중복 처리하지 않음
        _canInteract = false;
        _actionName = string.Empty;

        //실행 중 대상이 비활성화되더라도 완료한 표시가 남지 않도록 먼저 숨김
        _playerHud.SetHoldGauge( false, 0f );
        _playerHud.SetActionName( string.Empty );
        _recipeInput.CancelIngredientInteract( );

        if ( _isThrowInput == true )
            return;

        //음식 전달은 실패해도 아이템 배치로 넘어가지 않음
        if ( _isWorldInput == true )
        {
            _isWorldInput = false;
            _player.WorldInteract( );
            return;
        }

        _player.CompleteItemInteract( );
    }
    /// <summary>
    /// 홀딩 종료 처리
    /// </summary>
    /// <param name="isHolding">홀딩 여부</param>
    /// <param name="progress">홀딩 진행률</param>
    void HandleInteractHold ( bool isHolding, float progress )
    {
        UpdateActionHold( isHolding, progress );
    }

    /// <summary>
    /// 현재 입력의 홀딩 진행과 취소 처리
    /// </summary>
    /// <param name="isHolding">홀딩 여부</param>
    /// <param name="progress">홀딩 진행률</param>
    void UpdateActionHold ( bool isHolding, float progress )
    {
        if ( _isEnabled == false || _isPaused == true )
            return;

        if ( isHolding == false )
        {
            CancelActionInput( );
            return;
        }

        if ( _canInteract == true && CanContinueActionInput( ) == false )
        {
            CancelActionInput( );
            return;
        }

        _playerHud.SetHoldGauge(
            _canInteract == true && _isThrowInput == false &&
            string.IsNullOrEmpty( _actionName ) == false,
            progress );
    }

    /// <summary>
    /// 제작 화면에 따른 커서 설정
    /// </summary>
    void UpdateCursor ()
    {
        Cursor.lockState = _isPaused == true || _isProduceUiOpen == true
            ? CursorLockMode.None
            : CursorLockMode.Locked;

        Cursor.visible = _isPaused || _isProduceUiOpen;
    }

    /// <summary>
    /// 제작 화면 전환에 따른 입력과 커서 설정
    /// </summary>
    /// <param name="isOpen">제작 UI 열림 여부</param>
    void HandleProduceUiVisibleChanged ( bool isOpen )
    {
        //닫기와 레시피/미니게임 전환 모두 이전 입력 취소
        CancelActionInput( );

        _isProduceUiOpen = isOpen;

        _recipeInput.HandleUiVisibleChanged( isOpen );

        //게임 종료 중에는 플레이 입력을 다시 연결하지 않음
        if ( _isEnabled == false || _isPaused == true )
            return;

        SetPlayInput( true );

        //레시피에서는 이동을 유지하고 미니게임 진입 시에만 정지
        if ( isOpen == true && _produceHud.IsRecipeOpen == false )
        {
            _player.Move( Vector2.zero );
            _player.Run( false );
        }

        UpdateCursor( );
    }

    /// <summary>
    /// 포획 시작 시 이전 누르기의 완료 처리 차단
    /// </summary>
    /// <param name="isCaptured">포획 여부</param>
    void HandleCaptureChanged ( bool isCaptured )
    {
        if ( isCaptured == true )
        {
            _recipeInput.CancelProduction( );

            CancelActionInput( );
        }
    }

    /// <summary>
    /// 스턴 상태에 따른 시점 잠금과 상호작용 취소
    /// </summary>
    /// <param name="isStunned">스턴 적용 여부</param>
    void HandleStunChanged ( bool isStunned )
    {
        _cameraFocus.SetLocked( _isEnabled == false || _isPaused == true || isStunned );

        if ( isStunned == true )
        {
            _recipeInput.CancelProduction( );

            CancelActionInput( );
        }
    }

    #endregion

    #region ----- 플레이 입력 -----

    /// <summary>
    /// 게임 진행과 제작 화면에 따라 플레이 입력 연결
    /// </summary>
    /// <param name="isEnabled">플레이 입력 허용 여부</param>
    void SetPlayInput ( bool isEnabled )
    {
        //파괴된 플레이어의 입력 연결도 먼저 해제
        Unsubscribe( );

        if ( _inputHandler == null || _player == null )
            return;

        if ( isEnabled == false || _isPaused == true )
            return;

        //F 입력은 화면 상태에 따라 분기
        _inputHandler.OnRescueInput += HandleRescueInput;

        //미니게임 화면에서는 이동과 월드 상호작용 제한
        if ( _isProduceUiOpen == true && _produceHud.IsRecipeOpen == false )
            return;

        //레시피에서는 이동과 시점을 유지
        _inputHandler.OnMoveInput += _player.Move;
        _inputHandler.OnLookInput += HandleLookInput;
        _inputHandler.OnJumpInput += _player.Jump;
        _inputHandler.OnRunInput += _player.Run;

        //레시피에서는 같은 그릇의 재료 배치와 습득만 처리
        _inputHandler.OnInteractStarted += HandleInteractStarted;
        _inputHandler.OnInteractInput += HandleInteractCompleted;
        _inputHandler.OnInteractHoldInput += HandleInteractHold;


        //제작 화면에서는 서포트 사용 제한
        if ( _isProduceUiOpen == true )
            return;

        _inputHandler.OnSupportInput += HandleSupportInput;
    }

    /// <summary>
    /// 현재 누르기의 상호작용과 관련 HUD 취소
    /// </summary>
    void CancelActionInput ()
    {
        _highlight.Clear( );

        //제한이 풀려도 이전 누르기의 완료 입력은 받지 않음
        _canInteract = false;
        _isThrowInput = false;
        _isWorldInput = false;
        _actionName = string.Empty;
        _recipeInput.CancelIngredientInteract( );

        if ( _player != null )
        {
            _player.CancelWorldInteract( );
            _player.CancelItemInteract( );
        }

        if ( _playerHud != null )
        {
            _playerHud.SetHoldGauge( false, 0f );
            _playerHud.SetActionName( string.Empty );
            _playerHud.SetRescueHud( false, 0f );
            _playerHud.SetRecipeGuide( null );
        }
    }


    /// <summary>
    /// 탈출, 구조, 레시피 입력 처리
    /// </summary>
    void HandleRescueInput ()
    {
        if ( _isEnabled == false || _isPaused == true )
            return;

        //자신의 탈출 우선
        if ( _player.IsCaptured == true )
        {
            _player.Rescue( );
            return;
        }

        //행동할 수 없는 상태면 종료
        if ( _player.IsActionBlocked == true )
            return;

        if ( _isProduceUiOpen == true )
        {
            _recipeInput.TryCloseRecipe( );
            return;
        }

        //구조가 끝나지 않아도 이번 입력은 구조만 처리
        if ( _player.TryGetRescueTarget( out _ ) == true )
        {
            _player.Rescue( );
            return;
        }

        _recipeInput.TryOpen( );
    }


    /// <summary>
    /// 플레이 입력 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _inputHandler == null )
            return;

        _inputHandler.OnMoveInput -= _player.Move;
        _inputHandler.OnLookInput -= HandleLookInput;
        _inputHandler.OnJumpInput -= _player.Jump;
        _inputHandler.OnInteractStarted -= HandleInteractStarted;
        _inputHandler.OnInteractInput -= HandleInteractCompleted;
        _inputHandler.OnInteractHoldInput -= HandleInteractHold;
        _inputHandler.OnRunInput -= _player.Run;
        _inputHandler.OnRescueInput -= HandleRescueInput;
        _inputHandler.OnSupportInput -= HandleSupportInput;

    }

    /// <summary>
    /// 레시피 대상과 상호작용 안내 갱신
    /// </summary>
    public void Tick ()
    {
        UpdateHighlight( );

        if ( _isEnabled == false || _isPaused == true )
            return;

        _recipeInput.Tick( );
        _playerHud.SetSupportItems( _player.SupportItems );

        //제작 화면에서는 월드 안내 숨김
        if ( _isProduceUiOpen == true )
        {
            _playerHud.SetRescueHud( false, 0f );
            _playerHud.SetRecipeGuide( null );
            return;
        }

        //탈출/구조 표시 가능 여부
        bool isRescueVisible = _player.TryGetRescueProgress(
            out ICaptureTarget displayTarget,
            out float progress );       

        _playerHud.SetRescueHud(
            isRescueVisible, progress, displayTarget );

        CookController recipeTarget = null;     //레시피 안내 대상

        if ( isRescueVisible == false )
            _recipeInput.TryGetTarget( out recipeTarget );

        _playerHud.SetRecipeGuide( recipeTarget );
    }


    /// <summary>
    /// 현재 대상의 외곽선과 조준 또는 입력 중 행동 안내 갱신
    /// </summary>
    void UpdateHighlight ()
    {
        if ( _isEnabled == false || _isPaused == true ||
             _player.IsActionBlocked == true )
        {
            _highlight.Clear( );
            _playerHud.SetActionName( string.Empty );
            return;
        }

        Component target = null;		//강조할 대상
        string actionName = string.Empty;		//조준 행동 이름

        //제작 화면에서는 월드 대상 안내를 조회하지 않음
        if ( _isProduceUiOpen == false )
        {
            if ( _player.TryGetInteractionTarget(
                out target, out actionName ) == false )
            {
                //F 안내는 기존 구조 및 레시피 UI에서 표시
                if ( _player.TryGetRescueTarget( out Npc npc ) == true )
                    target = npc;
                else if ( _recipeInput.TryGetTarget( out CookController product ) == true )
                    target = product;
            }
        }

        _highlight.SetTarget( target );

        //입력 중에는 시선이 바뀌어도 시작한 행동 이름 유지
        _playerHud.SetActionName(
            _canInteract == true ? _actionName : actionName );
    }

    /// <summary>
    /// 서포트 사용 입력
    /// </summary>
    /// <param name="index">서포트 아이템 슬롯 번호</param>
    void HandleSupportInput ( int index )
    {
        //효과 변경 방지
        if ( _isEnabled == false || _isPaused == true || _isProduceUiOpen == true ||
             _player.IsActionBlocked == true || _inputHandler.IsInteractHolding == true ||
             _player.SupportItems == null )
            return;

        _player.SupportItems.Use( index );
        _playerHud.SetSupportItems( _player.SupportItems );
    }


    /// <summary>
    /// 현재 상호작용 입력을 유지할 수 있는지 확인
    /// </summary>
    /// <returns>입력 유지 가능 여부</returns>
    bool CanContinueActionInput ()
    {
        if ( _player.IsActionBlocked == true )
            return false;

        if ( _recipeInput.HasIngredientInput == false && _isProduceUiOpen == false )
            return true;

        return _recipeInput.CanContinueIngredientInteract( );
    }

    #endregion
}
