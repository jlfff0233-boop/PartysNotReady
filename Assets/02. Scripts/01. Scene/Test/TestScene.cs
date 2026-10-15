using UnityEngine;

/// <summary>
/// 테스트 씬 중재 - 플레이어 초기화와 이동, 상호작용 입력 연결
/// </summary>
public class TestScene : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Player _player;		//플레이어
    [SerializeField] InputHandler _inputHandler;		//플레이어 입력
    [SerializeField] CameraFocus _cameraFocus;		//시점 대상
    [SerializeField] Camera _camera;		//상호작용 탐지 카메라

    PlayerItemHolder _interact;		//아이템 보유
    bool _isReady;		//초기화 완료 여부
    bool _canInteract;		//상호작용 완료 허용 여부
    bool _isWorldInput;		//월드 상호작용 입력 여부
    bool _cursorChanged;		//커서 설정 적용 여부
    bool _prevCursorVisible;		//시작 전 커서 표시 여부
    CursorLockMode _prevCursorLock;		//시작 전 커서 잠금

    private void Start ()
    {
        //각 컴포넌트의 Awake / OnEnable 이후 플레이어 연결
        if ( _player == null || _inputHandler == null ||
             _cameraFocus == null || _camera == null || _inputHandler.IsReady == false )
        {
            Debug.LogError( "[테스트 시작 실패] 플레이어 / 입력 / 시점 / 카메라 연결을 확인해 주세요.", this );
            return;
        }

        _interact = new PlayerItemHolder( _player.HoldPoint, _player.HoldGap );
        _player.Init( _interact, _cameraFocus.transform, _camera );

        //같은 씬에 선배치된 아이템의 기존 초기화 함수 사용
        foreach ( ItemCarry item in FindObjectsByType<ItemCarry>(
            FindObjectsInactive.Include, FindObjectsSortMode.None ) )
        {
            if ( item.gameObject.scene != gameObject.scene )
                continue;

            ItemWorldController view = item.GetComponent<ItemWorldController>( );
            if ( view != null )
                view.Init( );
        }

        _isReady = true;
        Subscribe( );
    }

    private void OnEnable ()
    {
        //최초 활성화에서는 Start가 초기화와 입력 연결 수행
        if ( _isReady == true )
            Subscribe( );
    }

    private void OnDisable ()
    {
        Unsubscribe( );
        CancelInteract( );

        if ( _isReady == true && _player != null )
        {
            _player.Move( Vector2.zero );
            _player.Run( false );
        }

        if ( _cursorChanged == true )
        {
            Cursor.lockState = _prevCursorLock;
            Cursor.visible = _prevCursorVisible;
            _cursorChanged = false;
        }
    }

    private void OnDestroy ()
    {
        _interact?.Dispose( );
    }


    /// <summary>
    /// 진행 중인 상호작용 취소
    /// </summary>
    void CancelInteract ()
    {
        _canInteract = false;
        _isWorldInput = false;

        if ( _player == null )
            return;

        _player.CancelWorldInteract( );
        _player.CancelItemInteract( );
    }

    /// <summary>
    /// 월드 상호작용, 아이템 상호작용, 투척 순서로 입력 시작
    /// </summary>
    void HandleInteractStarted ()
    {
        CancelInteract( );

        if ( _player == null || _player.IsActionBlocked == true )
            return;

        _isWorldInput = _player.BeginWorldInteract( );
        _canInteract = _isWorldInput == true || _player.BeginItemInteract( );

        if ( _canInteract == true || _player.IsIngredientInteractBlocked == true )
            return;

        if ( _player.HasThrowTarget( ) == true )
            _player.Throw( );
    }

    /// <summary>
    /// 홀딩 완료 시 시작한 상호작용 실행
    /// </summary>
    void HandleInteractCompleted ()
    {
        if ( _canInteract == false )
            return;

        if ( _player == null || _player.IsActionBlocked == true )
        {
            CancelInteract( );
            return;
        }

        //실행 전에 완료 권한을 해제하여 중복 처리 방지
        _canInteract = false;

        if ( _isWorldInput == true )
        {
            _isWorldInput = false;
            _player.WorldInteract( );
            return;
        }

        _player.CompleteItemInteract( );
    }

    /// <summary>
    /// 홀딩 해제 또는 행동 제한 시 상호작용 취소
    /// </summary>
    /// <param name="isHolding">홀딩 여부</param>
    /// <param name="progress">홀딩 진행률</param>
    void HandleInteractHold ( bool isHolding, float progress )
    {
        if ( isHolding == false || _player == null || _player.IsActionBlocked == true )
            CancelInteract( );
    }

    /// <summary>
    /// 테스트 입력 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _inputHandler == null )
            return;

        //Unity 객체가 파괴되어도 관리 참조의 델리게이트 연결은 해제
        if ( object.ReferenceEquals( _player, null ) == false )
        {
            _inputHandler.OnMoveInput -= _player.Move;
            _inputHandler.OnRunInput -= _player.Run;
            _inputHandler.OnJumpInput -= _player.Jump;
        }

        if ( object.ReferenceEquals( _cameraFocus, null ) == false )
            _inputHandler.OnLookInput -= _cameraFocus.Rotate;

        _inputHandler.OnInteractStarted -= HandleInteractStarted;
        _inputHandler.OnInteractInput -= HandleInteractCompleted;
        _inputHandler.OnInteractHoldInput -= HandleInteractHold;
    }

    /// <summary>
    /// 테스트 입력 연결과 커서 잠금
    /// </summary>
    void Subscribe ()
    {
        _inputHandler.OnMoveInput += _player.Move;
        _inputHandler.OnRunInput += _player.Run;
        _inputHandler.OnJumpInput += _player.Jump;
        _inputHandler.OnLookInput += _cameraFocus.Rotate;
        _inputHandler.OnInteractStarted += HandleInteractStarted;
        _inputHandler.OnInteractInput += HandleInteractCompleted;
        _inputHandler.OnInteractHoldInput += HandleInteractHold;

        _prevCursorLock = Cursor.lockState;
        _prevCursorVisible = Cursor.visible;
        _cursorChanged = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


}
