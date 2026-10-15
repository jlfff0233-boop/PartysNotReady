using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 입력 처리기 - 입력 이벤트와 홀딩 진행률 전달
/// </summary>
public class InputHandler : MonoBehaviour
{
    const string MoveActionName = "Player/Move";		//이동 액션 경로
    const string LookActionName = "Player/Look";		//시점 액션 경로
    const string InteractActionName = "Player/Interact";		//상호작용 액션 경로
    const string JumpActionName = "Player/Jump";		//점프 액션 경로
    const string RunActionName = "Player/Run";		//달리기 액션 경로
    const string RescueActionName = "Player/Rescue";		//구조 액션 경로

    InputAction _moveAction;		//이동 입력
    InputAction _lookAction;		//시점 입력
    InputAction _interactAction;		//상호작용 입력
    InputAction _jumpAction;		//점프 입력
    InputAction _runAction;		//달리기 입력
    InputAction _rescueAction;		//구조 입력

    InputAction _support1Action;		//서포트 아이템 슬롯 1
    InputAction _support2Action;		//서포트 아이템 슬롯 2
    InputAction _support3Action;		//서포트 아이템 슬롯 3

    bool _isInteractHolding;     //상호작용 키 홀딩 여부


    /// <summary>
    /// 일반 상호작용 홀딩 여부
    /// </summary>
    public bool IsInteractHolding => _isInteractHolding;

    /// <summary>
    /// 필수 입력 액션의 활성 연결 준비 여부
    /// </summary>
    public bool IsReady => isActiveAndEnabled &&
        _moveAction != null && _lookAction != null && _interactAction != null &&
        _jumpAction != null && _runAction != null && _rescueAction != null;

    /// <summary>
    /// 서포트 사용 입력(슬롯 번호)
    /// </summary>
    public event Action<int> OnSupportInput;

    /// <summary>
    /// 이동 입력값 변경(이동 방향)
    /// </summary>
    public event Action<Vector2> OnMoveInput;
    /// <summary>
    /// 시점 입력값 변경(회전 입력값)
    /// </summary>
    public event Action<Vector2> OnLookInput;
    /// <summary>
    /// 상호작용 홀딩 완료
    /// </summary>
    public event Action OnInteractInput;
    /// <summary>
    /// 상호작용 입력 시작
    /// </summary>
    public event Action OnInteractStarted;
    /// <summary>
    /// 점프 입력
    /// </summary>
    public event Action OnJumpInput;
    /// <summary>
    /// 상호작용 홀딩 인풋(홀딩 여부, 진행률)
    /// </summary>
    public event Action<bool , float> OnInteractHoldInput;
    /// <summary>
    /// 달리기 입력 상태 변경(누름 여부)
    /// </summary>
    public event Action<bool> OnRunInput;
    /// <summary>
    /// 구조 입력
    /// </summary>
    public event Action OnRescueInput;

    /// <summary>
    /// 일시정지 메뉴 입력
    /// </summary>
    public event Action OnPauseInput;

#if UNITY_EDITOR
    /// <summary>
    /// 플레이 검증용 재시작 입력
    /// </summary>
    public event Action OnRestartInput;

    /// <summary>
    /// 촬영용 디버그 입력(단축키 번호)
    /// </summary>
    public event Action<int> OnDebugInput;
#endif

    /// <summary>
    /// 필수 액션 확인 후 입력 구독
    /// </summary>
    void InitActions ( )
    {
        //인풋 액션 가져오기
        InputActionAsset actions = InputSystem.actions;

        _moveAction = actions?.FindAction ( MoveActionName , false );
        _lookAction = actions?.FindAction ( LookActionName , false );
        _interactAction = actions?.FindAction ( InteractActionName , false );
        _jumpAction = actions?.FindAction ( JumpActionName , false );
        _runAction = actions?.FindAction ( RunActionName , false );
        _rescueAction = actions?.FindAction ( RescueActionName , false );

        _support1Action = actions?.FindAction ( "Player/Support1" , false );
        _support2Action = actions?.FindAction ( "Player/Support2" , false );
        _support3Action = actions?.FindAction ( "Player/Support3" , false );

        if ( IsReady == false )
        {
            Debug.LogError ( "[입력 준비 실패] 필수 Player 액션 연결을 확인해 주세요." , this );
            return;
        }

        SubscribeActions ( );
    }

    /// <summary>
    /// 입력 액션 초기화
    /// </summary>
    void OnEnable ( )
    {
        InitActions ( );
    }

    /// <summary>
    /// 입력 구독 해제와 잔여 입력 초기화
    /// </summary>
    void OnDisable ( )
    {
        UnsubscribeActions ( );

        //비활성화 후에도 홀딩과 달리기 상태가 남지 않도록 해제 알림
        _isInteractHolding = false;
        OnInteractHoldInput?.Invoke ( false , 0f );

        OnRunInput?.Invoke ( false );
        OnMoveInput?.Invoke ( Vector2.zero );
        OnLookInput?.Invoke ( Vector2.zero );
    }

    private void Update ( )
    {
#if UNITY_EDITOR
        //정식 조작 설정과 분리한 에디터 검증용 단축키
        if ( Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame )
        {
            OnRestartInput?.Invoke ( );
            return;
        }

        if ( OnDebugInput != null && Keyboard.current != null )
        {
            if ( Keyboard.current.digit1Key.wasPressedThisFrame ) OnDebugInput.Invoke ( 1 );
            else if ( Keyboard.current.digit2Key.wasPressedThisFrame ) OnDebugInput.Invoke ( 2 );
            else if ( Keyboard.current.digit3Key.wasPressedThisFrame ) OnDebugInput.Invoke ( 3 );
            else if ( Keyboard.current.digit4Key.wasPressedThisFrame ) OnDebugInput.Invoke ( 4 );
        }
#endif
        //설정에서 Player 액션이 중단되어도 메뉴 입력은 유지
        if ( Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame )
        {
            OnPauseInput?.Invoke( );
            return;
        }

        UpdateInteractHold ( );
    }


    /// <summary>
    /// 준비된 액션의 입력 이벤트 연결
    /// </summary>
    void SubscribeActions ( )
    {
        //이동과 시점은 키를 떼거나 입력이 취소될 때도 갱신
        _moveAction.performed += HandleMoveAction;
        _moveAction.canceled += HandleMoveAction;

        _lookAction.performed += HandleLookAction;
        _lookAction.canceled += HandleLookAction;

        _interactAction.started += HandleInteractHoldAction;
        _interactAction.canceled += HandleInteractHoldAction;
        _interactAction.performed += HandleInteractAction;
        _jumpAction.performed += HandleJumpAction;

        _runAction.performed += HandleRunAction;
        _runAction.canceled += HandleRunAction;
        _rescueAction.performed += HandleRescueAction;

        if ( _support1Action != null ) _support1Action.performed += HandleSupport1;
        if ( _support2Action != null ) _support2Action.performed += HandleSupport2;
        if ( _support3Action != null ) _support3Action.performed += HandleSupport3;

    }

    /// <summary>
    /// 초기화 실패와 선택 액션 누락을 고려한 입력 구독 해제
    /// </summary>
    void UnsubscribeActions ( )
    {
        if ( _support1Action != null ) _support1Action.performed -= HandleSupport1;
        if ( _support2Action != null ) _support2Action.performed -= HandleSupport2;
        if ( _support3Action != null ) _support3Action.performed -= HandleSupport3;

        if ( _moveAction != null )
        {
            _moveAction.performed -= HandleMoveAction;
            _moveAction.canceled -= HandleMoveAction;
        }

        if ( _lookAction != null )
        {
            _lookAction.performed -= HandleLookAction;
            _lookAction.canceled -= HandleLookAction;
        }

        if ( _interactAction != null )
        {
            _interactAction.started -= HandleInteractHoldAction;
            _interactAction.canceled -= HandleInteractHoldAction;
            _interactAction.performed -= HandleInteractAction;
        }

        if ( _jumpAction != null ) _jumpAction.performed -= HandleJumpAction;

        if ( _runAction != null )
        {
            _runAction.performed -= HandleRunAction;
            _runAction.canceled -= HandleRunAction;
        }

        if ( _rescueAction != null ) _rescueAction.performed -= HandleRescueAction;
    }


    /// <summary>
    /// 첫 서포트 슬롯 입력
    /// </summary>
    /// <param name="context">입력 정보</param>
    void HandleSupport1 ( InputAction.CallbackContext context )
    {
#if UNITY_EDITOR
        if ( OnDebugInput != null && context.control.device is Keyboard )
            return;
#endif
        OnSupportInput?.Invoke ( 0 );
    }

    /// <summary>
    /// 두 번째 서포트 슬롯 입력
    /// </summary>
    /// <param name="context">입력 정보</param>
    void HandleSupport2 ( InputAction.CallbackContext context )
    {
#if UNITY_EDITOR
        if ( OnDebugInput != null && context.control.device is Keyboard )
            return;
#endif
        OnSupportInput?.Invoke ( 1 );
    }

    /// <summary>
    /// 세 번째 서포트 슬롯 입력
    /// </summary>
    /// <param name="context">입력 정보</param>
    void HandleSupport3 ( InputAction.CallbackContext context )
    {
#if UNITY_EDITOR
        if ( OnDebugInput != null && context.control.device is Keyboard )
            return;
#endif
        OnSupportInput?.Invoke ( 2 );
    }


    /// <summary>
    /// 이동 입력값 전달
    /// </summary>
    /// <param name="context">입력 콜백 정보</param>
    void HandleMoveAction ( InputAction.CallbackContext context )
    {
        OnMoveInput?.Invoke ( context.ReadValue<Vector2> ( ) );
    }

    /// <summary>
    /// 시점 입력값 전달
    /// </summary>
    /// <param name="context">입력 콜백 정보</param>
    void HandleLookAction ( InputAction.CallbackContext context )
    {
        OnLookInput?.Invoke ( context.ReadValue<Vector2> ( ) );
    }

    /// <summary>
    /// 점프 입력 전달
    /// </summary>
    /// <param name="context">입력 콜백 정보</param>
    void HandleJumpAction ( InputAction.CallbackContext context )
    {
        OnJumpInput?.Invoke ( );
    }

    /// <summary>
    /// 달리기 키 누름과 해제 전달
    /// </summary>
    /// <param name="context">달리기 입력 콜백 정보</param>
    void HandleRunAction ( InputAction.CallbackContext context )
    {
        OnRunInput?.Invoke ( context.ReadValueAsButton ( ) );
    }

    /// <summary>
    /// 구조 키를 누른 순간 입력 전달
    /// </summary>
    /// <param name="context">구조 입력 콜백 정보</param>
    void HandleRescueAction ( InputAction.CallbackContext context )
    {
        OnRescueInput?.Invoke ( );
    }

    /// <summary>
    /// 홀딩 완료와 상호작용 입력 전달
    /// </summary>
    /// <param name="context">상호작용 입력 콜백 정보</param>
    void HandleInteractAction ( InputAction.CallbackContext context )
    {
        //홀딩 완료 후에도 키를 떼기 전까지 완성된 게이지 유지
        OnInteractHoldInput?.Invoke ( true , 1f );
        OnInteractInput?.Invoke ( );
    }

    /// <summary>
    /// 상호작용 입력 시작 및 홀딩 취소 처리
    /// </summary>
    /// <param name="context">상호작용 입력 콜백 정보</param>
    void HandleInteractHoldAction ( InputAction.CallbackContext context )
    {
        _isInteractHolding = context.started;

        //홀딩 완료와 별개로 누른 순간을 먼저 전달
        if ( context.started == true )
            OnInteractStarted?.Invoke ( );

        OnInteractHoldInput?.Invoke ( _isInteractHolding , 0f );
    }

    /// <summary>
    /// 홀딩 진행률 전달
    /// </summary>
    void UpdateInteractHold ( )
    {
        if ( _isInteractHolding == false || _interactAction == null ) return;

        //Hold 완료 시간 대비 진행률을 0~1 범위로 제한
        float progress = Mathf.Clamp01 (
            _interactAction.GetTimeoutCompletionPercentage ( ) );

        OnInteractHoldInput?.Invoke ( true , progress );
    }

    /// <summary>
    /// 슬롯의 서포트 입력 연결 확인
    /// </summary>
    /// <param name="index">0부터 시작하는 슬롯 번호</param>
    /// <returns>입력 연결 여부</returns>
    public bool HasSupportAction ( int index )
    {
        switch ( index )
        {
            case 0: return _support1Action != null;
            case 1: return _support2Action != null;
            case 2: return _support3Action != null;
            default: return false;
        }
    }
}
