using System;
using UnityEngine;

/// <summary>
/// 플레이어 - 이동, 상호작용, 스턴, 포획
/// </summary>
[RequireComponent (
    typeof ( CharacterController ) , typeof ( Capturable ) , typeof ( PlayerCapture ) )]
public class Player : MonoBehaviour,
    IAttackTarget, IStunTarget, ICaptureTarget
{
    Transform _camRef;		//카메라 참조

    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] Mover _mover;		//이동
    [SerializeField] PlayerAnimatorHandler _animatorHandler;		//이동과 피포획 애니메이션
    [SerializeField] PlayerSound _playerSound;		//플레이어 사운드
    [SerializeField] SoundPlayer _soundPlayer;		//플레이어 전용 사운드 재생
    [SerializeField] ItemInteracter _interacter;		//아이템 상호작용
    [SerializeField] WorldInteracter _worldInteracter;		//월드 상호작용 중재자
    [SerializeField] RescueInteracter _rescueInteracter;		//구조 상호작용 중재자
    [SerializeField] Thrower _thrower;		//아이템 투척

    [Header ( "----- 아이템 보유 -----" )]
    [SerializeField] Transform _holdPoint;		//들기 지점
    [SerializeField, Min ( 0f )] float _holdGap;		//보유 표시 간격

    [Header ( "----- 포획 탈출 -----" )]
    [SerializeField, Min ( 0.01f )] float _escapeTimeLimit = 5f;		//탈출 제한시간
    [SerializeField, Min ( 0f )] float _escapeStunDuration = 2f;		//실패 시 스턴 시간
    [SerializeField] Transform _kidnapRespawnPoint;		//납치 완료 후 리스폰 지점
    [SerializeField, Min ( 0.01f )] float _kidnapExtraStunDuration = 1f;		//납치 완료 시 추가 스턴 시간

    Stunner _stunner = new ( );		//스턴 시간과 상태 변경
    PlayerCapture _capture;		//보유 지점 따라가기와 컨트롤러 복구
    Capturable _capturable;		//공통 포획 관계와 저항 진행
    PlayerItemHolder _itemHolder;		//아이템 보유 처리
    PlayerSupportItems _supportItems;		//보유한 서포트 아이템
    SupportItemFactory _supportFactory;		//서포트 효과 생성

    Vector2 _moveInput;		//현재 이동 키 입력
    bool _isEndingCapture;		//포획 결과 전달 중
    bool _runInput;		//현재 달리기 키 입력 여부


    Room _currentRoom;     //현재 위치한 방

    #region ----- 프로퍼티/이벤트 -----

    /// <summary>
    /// 보유 표시 기준
    /// </summary>
    public Transform HoldPoint => _holdPoint;

    /// <summary>
    /// 보유 표시 간격
    /// </summary>
    public float HoldGap => _holdGap;

    /// <summary>
    /// 보유한 서포트 아이템
    /// </summary>
    public PlayerSupportItems SupportItems => _supportItems;

    /// <summary>
    /// 아이템 상호작용 거리
    /// </summary>
    public float ItemInteractDistance => _interacter.InteractDistance;

    /// <summary>
    /// 재료 상호작용 실패로 일반 집기와 투척이 차단된 상태
    /// </summary>
    public bool IsIngredientInteractBlocked =>
        _interacter.IsIngredientInteractBlocked;

    /// <summary>
    /// 입력을 시작한 아이템 상호작용 행동 이름
    /// </summary>
    public string ItemActionName => _interacter.ActionName;

    /// <summary>
    /// 입력을 시작한 월드 상호작용 행동 이름
    /// </summary>
    public string WorldActionName => _worldInteracter.ActionName;


    /// <summary>
    /// 공격 대상 트랜스폼
    /// </summary>
    public Transform TargetTransform => transform;

    /// <summary>
    /// 피격 가능 여부
    /// </summary>
    public bool CanTakeHit => isActiveAndEnabled;

    /// <summary>
    /// 스턴 적용 여부
    /// </summary>
    public bool IsStunned => _stunner.IsStunned;

    /// <summary>
    /// 포획 여부
    /// </summary>
    public bool IsCaptured =>
        _capturable != null && _capturable.IsCaptured;

    /// <summary>
    /// 일반 행동 제한 여부
    /// </summary>
    public bool IsActionBlocked => CheckActionBlocked ( );

    /// <summary>
    /// 포획 가능 여부
    /// </summary>
    public bool CanCapture => CheckCanCapture ( );


    /// <summary>
    /// 현재 위치한 방
    /// </summary>
    public Room CurrentRoom => _currentRoom;

    /// <summary>
    /// 현재 방 변경 알림
    /// </summary>
    public event Action<Room> OnRoomChanged;

    /// <summary>
    /// 스턴 상태 변경 이벤트(스턴 여부)
    /// </summary>
    public event Action<bool> OnStunChanged;

    /// <summary>
    /// 포획 상태 변경 이벤트(포획 여부)
    /// </summary>
    public event Action<bool> OnCaptureChanged;

    /// <summary>
    /// 포획 회전 변경 이벤트(수평 각도 변화량)
    /// </summary>
    public event Action<float> OnCaptureYawChanged;

    /// <summary>
    /// 포획 종료 결과 이벤트(포획 결과)
    /// </summary>
    public event Action<CaptureResult> OnCaptureEnded;

    /// <summary>
    /// 포획 해제 요청 이벤트(포획 결과)
    /// </summary>
    public event Action<CaptureResult> OnReleaseRequested;

    /// <summary>
    /// 현재 조준한 아이템 변경 이벤트(현재 아이템)
    /// </summary>
    public event Action<ICarryable> OnItemTargetChanged
    {
        add => _interacter.OnCarryTargetChanged += value;
        remove => _interacter.OnCarryTargetChanged -= value;
    }

    /// <summary>
    /// 아이템 배치 완료 이벤트(배치 대상)
    /// </summary>
    public event Action<PlacementTarget> OnItemPlaced
    {
        add => _interacter.OnPlaced += value;
        remove => _interacter.OnPlaced -= value;
    }

    #endregion

    /// <summary>
    /// 초기화(카메라, 이동, 사운드, 상호작용)
    /// </summary>
    /// <param name="interact">아이템 보유 처리</param>
    /// <param name="camRef">이동 기준 카메라 트랜스폼</param>
    /// <param name="camera">상호작용 탐지 카메라</param>
    public void Init ( PlayerItemHolder interact , Transform camRef , Camera camera )
    {
        SetCamRef ( camRef );
        _mover.SetCamRef ( _camRef );
        _playerSound.Init ( this , _mover , _soundPlayer );
        _interacter.Init ( interact , camera , _holdPoint );
        //재초기화 시 이전 보유 모듈의 성공 알림 해제
        if ( _itemHolder != null )
            _itemHolder.OnItemActionCompleted -= HandleItemActionCompleted;

        _itemHolder = interact;
        _itemHolder.OnItemActionCompleted += HandleItemActionCompleted;
        _worldInteracter.Init ( interact , camera );
        _rescueInteracter.Init ( camera , ApplyRescueSupport );
        _thrower.Init ( interact , camera );
        _supportFactory = new SupportItemFactory (
            interact , _mover , _worldInteracter.GetSupplyTarget , _rescueInteracter );
    }

    private void OnEnable ( )
    {
        _stunner.OnChanged += HandleStunChanged;
    }

    private void OnDisable ( )
    {
        //입력과 포획을 정리한 뒤 스턴 해제 알림까지 전달하고 구독 해제
        _moveInput = Vector2.zero;
        _runInput = false;

        if ( _capture.IsFollowing == true )
            FinishRelease ( Vector3.zero , CaptureResult.Cancelled );

        _stunner.Clear ( );
        _stunner.OnChanged -= HandleStunChanged;

        _mover.SetReleaseVelocity ( Vector3.zero );
        ApplyMovementState ( );
    }

    private void OnDestroy ( )
    {
        if ( _itemHolder != null )
            _itemHolder.OnItemActionCompleted -= HandleItemActionCompleted;

        if ( _capturable != null )
            _capturable.OnReleaseRequested -= HandleReleaseRequested;
        if ( _capture != null )
            _capture.OnCaptureInterrupted -= HandleCaptureInterrupted;
    }

    private void Awake ( )
    {
        //따라가기와 포획 관계의 중단 요청 연결
        _capture = GetComponent<PlayerCapture> ( );
        _capturable = GetComponent<Capturable> ( );

        _capture.Init ( );
        _capture.OnCaptureInterrupted += HandleCaptureInterrupted;
        _capturable.Init ( this );
        _capturable.OnReleaseRequested += HandleReleaseRequested;
    }

    private void Update ( )
    {
        _stunner.Update ( Time.deltaTime );
    }

    private void LateUpdate ( )
    {
        if ( _capture.IsFollowing == false )
        {
            RefreshRoom( );
            return;
        }

        //보유 지점 소실 시에도 관계와 이동 설정 복구
        if ( _capture.TryFollow ( out float yawDelta ) == false )
        {
            FinishRelease ( Vector3.zero , CaptureResult.Cancelled );
            RefreshRoom( );
            return;
        }

        //몸체가 따라간 회전 변화량을 카메라에 전달해 시점 기준도 맞춤
        OnCaptureYawChanged?.Invoke ( yawDelta );
        RefreshRoom( );
    }


    /// <summary>
    /// 현재 위치의 방 확인과 변경 알림
    /// </summary>
    public void RefreshRoom ()
    {
        RoomRegister.TryGetRoom( transform.position, out Room room );
        if ( _currentRoom == room )
            return;

        _currentRoom = room;
        OnRoomChanged?.Invoke( room );
    }


    /// <summary>
    /// 구조 입력에 서포트 효과 적용
    /// </summary>
    void ApplyRescueSupport ( )
    {
        _supportItems?.TryUseRescueSupport( );
    }

    /// <summary>
    /// 이동 기준 카메라 참조 저장
    /// </summary>
    /// <param name="camRef">카메라 기준 트랜스폼</param>
    public void SetCamRef ( Transform camRef )
    {
        _camRef = camRef;
    }

    /// <summary>
    /// 아이템 행동 성공음을 플레이어 사운드에 전달
    /// </summary>
    /// <param name="action">완료한 행동</param>
    void HandleItemActionCompleted ( ItemActionType action )
    {
        if ( _playerSound != null )
            _playerSound.PlayItemAction( action );
    }


    #region ----- 이동 -----

    /// <summary>
    /// 포획과 스턴에 따른 이동 제한 및 현재 입력 반영
    /// </summary>
    void ApplyMovementState ( )
    {
        //포획 따라가기 중에는 중력과 일반 이동 갱신을 함께 중단
        _mover.SetSuspended ( _capture.IsFollowing );

        bool canMove = IsActionBlocked == false;		//일반 이동 가능 여부

        _mover.SetMoveAllowed ( canMove );
        _mover.Move ( canMove == true ? _moveInput : Vector2.zero );
        _mover.SetRunning ( canMove == true && _runInput );
    }

    /// <summary>
    /// 현재 입력 저장과 행동 제한에 따른 이동
    /// </summary>
    /// <param name="dir">이동 방향</param>
    public void Move ( Vector2 dir )
    {
        _moveInput = dir;
        ApplyMovementState ( );
    }

    /// <summary>
    /// 달리기 입력 저장과 행동 제한에 따른 적용
    /// </summary>
    /// <param name="isPressed">달리기 키 입력 여부</param>
    public void Run ( bool isPressed )
    {
        _runInput = isPressed;
        ApplyMovementState ( );
    }

    /// <summary>
    /// 행동 가능한 상태에서 점프
    /// </summary>
    public void Jump ( )
    {
        if ( IsActionBlocked == true )
            return;

        _mover.Jump ( );
    }

    #endregion

    #region ----- 상호작용 -----


    /// <summary>
    /// 상호작용 대상 저장
    /// </summary>
    /// <returns>저장 성공 여부</returns>
    public bool BeginItemInteract ( )
    {
        return IsActionBlocked == false && _interacter.BeginInteract ( );
    }

    /// <summary>
    /// 레시피 대상의 재료 집기 또는 배치 입력 시작
    /// </summary>
    /// <param name="product">레시피를 표시 중인 제작 대상</param>
    /// <returns>재료 상호작용 시작 여부</returns>
    public bool BeginIngredientInteract ( CookController product )
    {
        return IsActionBlocked == false &&
               _interacter.BeginIngredientInteract ( product );
    }

    /// <summary>
    /// 상호작용 취소
    /// </summary>
    public void CancelItemInteract ( )
    {
        if ( _interacter != null )
            _interacter.CancelInteract ( );
    }

    /// <summary>
    /// 아이템 상호작용 완료
    /// </summary>
    public void CompleteItemInteract ( )
    {
        if ( IsActionBlocked == true )
            return;

        _interacter.Interact ( );
    }

    /// <summary>
    /// 월드 상호작용 시작
    /// </summary>
    /// <returns>상호작용 시작 여부</returns>
    public bool BeginWorldInteract ( )
    {
        return IsActionBlocked == false && _worldInteracter.BeginInteract ( );
    }

    /// <summary>
    /// 월드 상호작용 완료
    /// </summary>
    public void WorldInteract ( )
    {
        if ( IsActionBlocked == true )
            return;

        if ( _worldInteracter.TryCompleteSourceInteract (
            out IngredientSource source ) == true )
        {
            if ( source != null )
                _itemHolder.TryTake ( source );

            return;
        }

        _worldInteracter.Interact ( );
    }

    /// <summary>
    /// 진행 중인 월드 상호작용 취소
    /// </summary>
    public void CancelWorldInteract ( )
    {
        if ( _worldInteracter != null )
            _worldInteracter.CancelInteract ( );
    }

    /// <summary>
    /// 조준한 제작 미니게임 즉시 실행
    /// </summary>
    /// <returns>입력 처리 여부</returns>
    public bool TryProduceMinigame ( )
    {
        return IsActionBlocked == false &&
               _worldInteracter.TryProduceMinigame ( );
    }


    /// <summary>
    /// 현재 실행 가능한 월드 또는 아이템 상호작용 대상 조회
    /// </summary>
    /// <param name="target">선택된 상호작용 대상</param>
    /// <returns>유효 대상 존재 여부</returns>
    public bool TryGetInteractionTarget ( out Component target )
    {
        target = null;
        if ( IsActionBlocked == true )
            return false;

        if ( _worldInteracter.TryGetInteractionTarget( out target ) == true )
            return true;

        return _interacter.TryGetInteractionTarget( out target );
    }

    /// <summary>
    /// 월드 우선순위에 따라 상호작용 대상과 행동 이름 조회
    /// </summary>
    /// <param name="target">선택된 대상</param>
    /// <param name="actionName">실행 가능한 행동 이름</param>
    /// <returns>대상 존재 여부</returns>
    public bool TryGetInteractionTarget (
        out Component target , out string actionName )
    {
        target = null;
        actionName = string.Empty;

        if ( IsActionBlocked == true )
            return false;

        if ( _worldInteracter.TryGetInteractionTarget (
            out target , out actionName ) == true )
            return true;

        return _interacter.TryGetInteractionTarget (
            out target , out actionName );
    }

    /// <summary>
    /// 조준한 레시피 대상 조회
    /// </summary>
    /// <param name="product">제작 대상</param>
    /// <returns>대상 조회 여부</returns>
    public bool TryGetRecipeTarget ( out CookController product )
    {
        product = null;

        //행동할 수 없는 상태면 조회 차단
        if ( IsActionBlocked == true )
            return false;

        return _interacter.TryGetRecipeTarget ( out product );
    }


    /// <summary>
    /// 상호작용 가능 대상 확인
    /// </summary>
    /// <returns>상호작용 가능 여부</returns>
    public bool HasInteractTarget ( )
    {
        return IsActionBlocked == false && _interacter.HasInteractTarget ( );
    }

    /// <summary>
    /// 지정 아이템 영역을 조준 중인지 확인
    /// </summary>
    /// <param name="target">확인할 대상의 루트</param>
    /// <returns>조준 여부</returns>
    public bool IsLookingAtItem ( Transform target )
    {
        return _interacter.IsLookingAt ( target );
    }

    #endregion

    #region ----- 아이템 -----


    /// <summary>
    /// 보유 아이템 즉시 투척
    /// </summary>
    public void Throw ( )
    {
        if ( IsActionBlocked == true )
            return;

        _thrower.Throw ( );
    }

    /// <summary>
    /// 서포트 아이템 생성
    /// </summary>
    /// <param name="data">아이템 설정</param>
    /// <param name="stock">재고 기능</param>
    /// <returns>생성한 아이템</returns>
    public SupportItem CreateSupportItem (
        SupportItemData data , IngredientStockManager stock )
    {
        return _supportFactory.Create ( data , stock );
    }

    /// <summary>
    /// 서포트 아이템 연결
    /// </summary>
    /// <param name="items">보유한 서포트 아이템 목록</param>
    public void SetSupportItems ( PlayerSupportItems items )
    {
        _supportItems = items;
    }


    /// <summary>
    /// 보유 아이템의 투척 기능 확인
    /// </summary>
    /// <returns>투척 가능 여부</returns>
    public bool HasThrowTarget ( )
    {
        return IsActionBlocked == false && _thrower.HasThrowTarget ( );
    }

    #endregion

    #region ----- 포획 -----

    /// <summary>
    /// 포획 종료와 이동 설정 복구
    /// </summary>
    /// <param name="impulse">포획 해제 충격량</param>
    /// <param name="result">종료 결과</param>
    /// <returns>포획 종료 처리 여부</returns>
    bool FinishRelease ( Vector3 impulse , CaptureResult result )
    {
        if ( _isEndingCapture == true )
            return false;

        //종료 알림이 모두 끝나기 전에는 새 포획 차단
        _isEndingCapture = true;
        try
        {
            //중복 종료와 포획자 소실을 함께 처리
            if ( _capture.IsFollowing == false )
                return false;

            //납치 완료 시 리스폰 위치가 없으면 완료 처리하지 않음
            bool kidnapped = result == CaptureResult.Kidnapped;

            if ( kidnapped == true && _kidnapRespawnPoint == null )
                return false;

            _capture.EndCapture ( kidnapped == true ? _kidnapRespawnPoint : null );
            _capturable.End ( );

            if ( _animatorHandler != null )
                _animatorHandler.SetCaptured ( false );

            //납치 완료 시 시간 초과보다 긴 스턴 적용
            if ( kidnapped == true )
            {
                TakeStun (
                    _escapeStunDuration + Mathf.Max ( 0.01f , _kidnapExtraStunDuration ) );
            }
            else if ( result == CaptureResult.TimedOut )
            {
                TakeStun ( _escapeStunDuration );
            }

            //해제 후 이동 설정
            ApplyMovementState ( );

            //리스폰 또는 취소 시에는 해제 충격량을 적용하지 않음
            _mover.SetReleaseVelocity (
                kidnapped == true || result == CaptureResult.Cancelled
                    ? Vector3.zero : impulse );

            //몸체 기울기 복구 후 기존 시점 유지
            OnCaptureYawChanged?.Invoke ( 0f );

            //이전 포획자의 참조 정리
            OnCaptureEnded?.Invoke ( result );
            OnCaptureChanged?.Invoke ( false );

            return true;
        }
        finally
        {
            _isEndingCapture = false;
        }
    }


    /// <summary>
    /// 포획자의 출구 도착에 따른 리스폰과 추가 스턴 처리
    /// </summary>
    /// <param name="owner">납치 완료를 요청한 포획자</param>
    /// <returns>납치 완료 처리 여부</returns>
    public bool TryCompleteKidnap ( CaptureController owner )
    {
        if ( IsCapturedBy ( owner ) == false )
            return false;

        if ( _capture.CanFollow == false )
        {
            FinishRelease ( Vector3.zero , CaptureResult.Cancelled );
            return false;
        }

        return FinishRelease ( Vector3.zero , CaptureResult.Kidnapped );
    }

    /// <summary>
    /// 따라가기 중단에 따른 포획 취소
    /// </summary>
    void HandleCaptureInterrupted ( )
    {
        FinishRelease ( Vector3.zero , CaptureResult.Cancelled );
    }

    /// <summary>
    /// 포획 해제 요청
    /// </summary>
    /// <param name="result">요청 결과</param>
    void HandleReleaseRequested ( CaptureResult result )
    {
        if ( _capture.IsFollowing == false )
            return;

        if ( result == CaptureResult.Cancelled )
            FinishRelease ( Vector3.zero , result );
        else
            OnReleaseRequested?.Invoke ( result );
    }

    /// <summary>
    /// 포획당하기
    /// </summary>
    /// <param name="owner">포획자</param>
    /// <param name="carryPoint">보유 지점</param>
    /// <returns>포획 성공 여부</returns>
    public bool TryCapture ( CaptureController owner , Transform carryPoint )
    {
        if ( CanCapture == false ||
             _capturable.TryBegin ( owner , Mathf.Max ( 0.01f , _escapeTimeLimit ) ) == false )
            return false;

        if ( _capture.TryBeginCapture ( carryPoint ) == false )
        {
            _capturable.End ( );
            return false;
        }

        //이동 상태 변경 및 진행 중인 상호작용 취소
        ApplyMovementState ( );

        if ( _animatorHandler != null )
            _animatorHandler.SetCaptured ( true );

        _worldInteracter.CancelInteract ( );
        OnCaptureChanged?.Invoke ( true );

        //최초 포획 위치와 시점 적용
        if ( IsCapturedBy ( owner ) == false )
            return false;

        if ( _capture.TryFollow ( out float yawDelta ) == false )
        {
            FinishRelease ( Vector3.zero , CaptureResult.Cancelled );
            return false;
        }

        OnCaptureYawChanged?.Invoke ( yawDelta );
        return IsCapturedBy ( owner );
    }

    /// <summary>
    /// 탈출 성공, 시간 초과 또는 취소 요청 처리
    /// </summary>
    /// <param name="owner">해제를 요청한 포획자</param>
    /// <param name="impulse">해제 충격량</param>
    /// <param name="result">종료 결과</param>
    /// <returns>해제 성공 여부</returns>
    public bool TryRelease (
        CaptureController owner , Vector3 impulse , CaptureResult result )
    {
        if ( IsCapturedBy ( owner ) == false )
            return false;

        //탈출 성공, 시간 초과 또는 취소만 허용
        if ( result != CaptureResult.Rescued &&
             result != CaptureResult.TimedOut &&
             result != CaptureResult.Cancelled )
            return false;

        if ( _capture.CanFollow == false )
            result = CaptureResult.Cancelled;

        return FinishRelease ( impulse , result );
    }


    /// <summary>
    /// 포획자 확인
    /// </summary>
    /// <param name="owner">확인할 포획자</param>
    /// <returns>포획자 여부</returns>
    public bool IsCapturedBy ( CaptureController owner )
    {
        return _capture != null && _capture.IsFollowing &&
            _capturable != null && _capturable.IsOwner ( owner );
    }

    #endregion

    #region ----- 구조 -----

    /// <summary>
    /// 탈출/구조
    /// </summary>
    public void Rescue ( )
    {
        if ( isActiveAndEnabled == false )
            return;

        //자신이 포획당한 상태라면 탈출 시도
        if ( IsCaptured == true )
        {
            _capturable.TryRescue ( );
            return;
        }

        //행동할 수 없는 상태면 종료
        if ( IsActionBlocked == true )
            return;

        //구조 시도
        _rescueInteracter.TryRescue ( );
    }

    /// <summary>
    /// 조준한 구조 대상 조회
    /// </summary>
    /// <param name="target">구조 대상</param>
    /// <returns>대상 조회 여부</returns>
    public bool TryGetRescueTarget ( out Npc target )
    {
        target = null;

        //행동할 수 없는 상태면 조회 차단
        if ( IsActionBlocked == true )
            return false;

        return _rescueInteracter.TryGetTarget ( out target );
    }

    /// <summary>
    /// 탈출/구조 대상과 진행률 조회
    /// </summary>
    /// <param name="displayTarget">진행률 표시 대상</param>
    /// <param name="progress">탈출/구조 진행률, 0~1 범위</param>
    /// <returns>탈출/구조 UI 표시 가능 여부</returns>
    public bool TryGetRescueProgress (
        out ICaptureTarget displayTarget, out float progress )
    {
        displayTarget = null;
        progress = 0f;

        if ( isActiveAndEnabled == false )
            return false;

        //포획당한 상태라면 자신의 탈출 진행도를 우선 표시
        if ( IsCaptured == true )
        {
            if ( _capturable.CanRescue == false )
                return false;

            displayTarget = this;
            progress = _capturable.RescueProgressRatio;
            return true;
        }

        //현재 구조 대상 확인
        if ( TryGetRescueTarget ( out Npc target ) == false )
            return false;

        //구조 대상과 진행률 설정
        displayTarget = target;
        progress = target.RescueProgressRatio;
        return true;
    }

    /// <summary>
    /// 탈출/구조 진행률 조회
    /// </summary>
    /// <param name="progress">탈출/구조 진행률, 0~1 범위</param>
    /// <returns>탈출/구조 UI 표시 가능 여부</returns>
    public bool TryGetRescueProgress ( out float progress )
    {
        return TryGetRescueProgress( out _, out progress );
    }

    #endregion

    #region ----- 스턴 -----

    /// <summary>
    /// 스턴 상태에 따른 행동 제한, 복구
    /// </summary>
    /// <param name="isStunned">스턴 적용 여부</param>
    void HandleStunChanged ( bool isStunned )
    {
        //스턴이 끝나도 포획 중이면 이동 제한 유지
        ApplyMovementState ( );

        if ( isStunned == true )
            _worldInteracter.CancelInteract ( );


        OnStunChanged?.Invoke ( isStunned );
    }

    /// <summary>
    /// 지정 시간 동안 스턴 적용
    /// </summary>
    /// <param name="duration">스턴 지속 시간</param>
    public void TakeStun ( float duration )
    {
        if ( isActiveAndEnabled == false )
            return;

        _stunner.Apply ( duration );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 비활성, 스턴, 포획에 따른 행동 제한 확인
    /// </summary>
    /// <returns>행동 제한 여부</returns>
    bool CheckActionBlocked ( )
    {
        return isActiveAndEnabled == false ||
               IsStunned == true ||
               IsCaptured == true ||
               ( _capture != null && _capture.IsFollowing == true );
    }

    /// <summary>
    /// 포획 가능 여부 확인
    /// </summary>
    /// <returns>포획 가능 여부</returns>
    bool CheckCanCapture ( )
    {
        return isActiveAndEnabled == true && _isEndingCapture == false &&
               _kidnapRespawnPoint != null &&
               _capturable != null &&
               _capturable.isActiveAndEnabled == true &&
               IsCaptured == false &&
               _capture != null &&
               _capture.CanBeginCapture == true &&
               _mover.isActiveAndEnabled == true;
    }

    #endregion

}
