using UnityEngine;

/// <summary>
/// 월드 상호작용 중재자 - 음식 전달, 조사와 일반 상호작용
/// </summary>
public class WorldInteracter : MonoBehaviour
{
    [Header( "----- 설정 데이터 -----" )]
    [SerializeField] float _interactDist = 3f;      //상호작용 거리
    [SerializeField] LayerMask _detectionLayerMask;     //상호작용 탐지 레이어

    Camera _camera;     //카메라
    PlayerItemHolder _interact;     //아이템 보유 처리

    ItemHolder _startedHolder;		//입력 시작 보유 모듈

    Food _currentFood;      //홀딩 시작 시 보유 음식
    IFoodReceiver _currentReceiver;     //홀딩 시작 시 음식 수락 대상
    ProductDisposal _currentDisposal;     //홀딩 시작 시 조준한 쓰레기통
    ItemCarry _currentDisposalItem;     //홀딩 시작 시 폐기할 보유 아이템
    IInteractable _currentTarget;               //홀딩 시작 시 일반 상호작용 대상

    IInvestigatable _curInvestigationTarget;        //홀딩 시작 시 조사 대상
    WorldTargetSelector _targetSelector = new( );     //조준한 대상의 조사 기능 선택
    int _curInvestigationId;        //조사 시작 시 저장한 ID

    IngredientSource _currentSource;     //홀딩 시작 시 조준한 공급원
    string _actionName = string.Empty;		//입력 시작 행동 이름

    /// <summary>
    /// 입력을 시작한 월드 상호작용 행동 이름
    /// </summary>
    public string ActionName => _actionName;

    /// <summary>
    /// 월드 상호작용 중재자 초기화
    /// </summary>
    /// <param name="interact">아이템 보유 처리</param>
    /// <param name="camera">상호작용 탐지 카메라</param>
    public void Init ( PlayerItemHolder interact, Camera camera )
    {
        _interact = interact;
        _camera = camera;
    }

    private void OnDisable ()
    {
        CancelInteract( );
    }


    #region ----- 상호작용 -----

    /// <summary>
    /// 상호작용 취소
    /// </summary>
    public void CancelInteract ()
    {
        _currentFood = null;
        _startedHolder = null;
        _currentReceiver = null;
        _currentDisposal = null;
        _currentDisposalItem = null;
        _currentTarget = null;
        _curInvestigationTarget = null;
        _curInvestigationId = 0;
        _currentSource = null;
        _actionName = string.Empty;
    }

    /// <summary>
    /// 월드 상호작용 대상과 행동 이름 저장
    /// </summary>
    /// <returns>상호작용 시작 여부</returns>
    public bool BeginInteract ()
    {
        CancelInteract( );

        if ( isActiveAndEnabled == false || _interact == null )
            return false;

        //쓰레기통은 배치하지 않고 보유 음식만 폐기 대상으로 저장
        ItemCarry heldItem = _interact.HoldItem as ItemCarry;
        _startedHolder = _interact.ActiveHolder;
        if ( heldItem != null &&
             TryGetHit( out RaycastHit disposalHit ) == true )
        {
            ProductDisposal disposal =
                disposalHit.collider.GetComponentInParent<ProductDisposal>( );

            //폐기 불가 아이템도 여기서 입력을 소비해 쓰레기통으로 투척하지 않음
            if ( disposal != null )
            {
                _currentDisposal = disposal;
                _currentDisposalItem = heldItem;
                _actionName = disposal.CanDispose( heldItem ) == true
                    ? "버리기" : string.Empty;
                return true;
            }
        }

        //음식 전달이 가능하면 우선 처리
        if ( TryGetHeldFood( out Food food ) == true &&
             TryGetReceiver( out IFoodReceiver receiver ) == true &&
             receiver.CanReceive( food.Data ) == true )
        {
            _currentFood = food;
            _currentReceiver = receiver;
            _actionName = "전달";
            return true;
        }

        //조사 가능한 대상이면 해당 조사 ID를 함께 저장
        if ( TrySelectInvestigation(
            out IInvestigatable investigation ) == true )
        {
            _curInvestigationTarget = investigation;
            _curInvestigationId = investigation.InvestigationId;
            _actionName = investigation.ActionName;
            return true;
        }

        //공급원 입력은 공급 가능 여부와 관계없이 여기서 처리
        if ( TryGetSource( out IngredientSource source ) == true )
        {
            //공급 불가 입력을 배치, 집기나 투척으로 넘기지 않음
            if ( _interact.HasSpace == false || source.CanTake == false )
            {
                _actionName = string.Empty;
                return true;
            }

            _currentSource = source;
            _actionName = "들기";
            return true;
        }

        //음식 전달과 조사 대상이 없으면 일반 상호작용 확인
        if ( TryGetWorldTarget( out IInteractable target ) == false )
            return false;

        _currentTarget = target;
        _actionName = target.ActionName;
        return true;
    }

    /// <summary>
    /// 상호작용 실행
    /// </summary>
    public void Interact ()
    {
        Food currentFood = _currentFood;
        ItemHolder startedHolder = _startedHolder;
        IFoodReceiver currentReceiver = _currentReceiver;
        ProductDisposal startedDisposal = _currentDisposal;
        ItemCarry startedDisposalItem = _currentDisposalItem;
        IngredientSource startedSource = _currentSource;
        IInteractable currentTarget = _currentTarget;
        IInvestigatable startedInvestigation = _curInvestigationTarget;
        int startedInvestigationId = _curInvestigationId;

        //같은 요청이 다시 실행되지 않도록 호출 전에 정리
        CancelInteract( );

        if ( isActiveAndEnabled == false || _interact == null )
            return;

        //시작할 때 보유한 음식과 조준한 쓰레기통을 완료 순간 다시 확인
        if ( startedDisposal != null )
        {
            if ( _interact.IsActive( startedHolder, startedDisposalItem ) == false ||
                 TryGetHit( out RaycastHit disposalHit ) == false ||
                 disposalHit.collider.GetComponentInParent<ProductDisposal>( ) !=
                     startedDisposal )
                return;

            startedDisposal.TryDispose( startedDisposalItem, startedHolder );
            return;
        }

        //조사로 시작한 입력 처리
        if ( startedInvestigation != null )
        {
            if ( CanCompleteInvestigation(
                startedInvestigation, startedInvestigationId ) == false )
                return;

            startedInvestigation.TryInvestigate( startedInvestigationId );
            return;
        }

        //일반 상호작용으로 시작한 입력 처리
        if ( currentTarget != null )
        {
            //완료 순간의 거리와 시선 및 상호작용 가능 여부 확인
            if ( TryGetWorldTarget( out IInteractable target ) == false )
                return;

            //저장한 참조는 비교에만 사용
            if ( object.ReferenceEquals( target, currentTarget ) == false )
                return;

            //현재 다시 탐지한 대상으로 실행
            target.Interact( );
            return;
        }

        //음식 전달로 시작한 입력 처리
        if ( currentFood == null )
            return;

        if ( TryGetHeldFood( out Food food ) == false ||
             TryGetReceiver( out IFoodReceiver receiver ) == false )
            return;

        //홀딩 시작 때와 다른 음식이나 대상이면 취소
        if ( _interact.IsActive( startedHolder, currentFood.GetComponent<ItemCarry>( ) ) == false ||
             food != currentFood ||
             object.ReferenceEquals( receiver, currentReceiver ) == false )
            return;

        ItemCarry item = ( ItemCarry ) _interact.HoldItem;

        //전달 실패 시 음식과 보유 상태 유지
        if ( receiver.TryReceive( food.Data ) == false )
            return;

        //전달 성공 시에만 보유 해제와 소비
        startedHolder.TryRelease( item );
        if ( food != null )
            food.Consume( );
    }

    /// <summary>
    /// 현재 조준한 제작 미니게임 즉시 실행
    /// </summary>
    /// <returns>입력 처리 여부</returns>
    public bool TryProduceMinigame ()
    {
        if ( isActiveAndEnabled == false || _interact == null )
            return false;

        if ( TryGetHit( out RaycastHit hit ) == false )
            return false;

        //제작 대상 가져오기
        if ( WorldTargetSelector.TryGetProduceMinigameTarget(
            hit, out ProduceMinigameInteractable target ) == false )
            return false;

        return target.TryPlayMinigame( );
    }

    /// <summary>
    /// 시작한 재료 공급원 상호작용의 완료 대상 확인
    /// </summary>
    /// <param name="source">완료 시점에도 유효한 공급원</param>
    /// <returns>공급원 입력으로 시작했는지 여부</returns>
    public bool TryCompleteSourceInteract ( out IngredientSource source )
    {
        source = null;
        IngredientSource startedSource = _currentSource;

        if ( startedSource == null )
            return false;

        //실패한 공급원 입력도 일반 상호작용으로 넘기지 않음
        CancelInteract( );

        if ( isActiveAndEnabled == false || _interact == null ||
             _interact.HasSpace == false ||
             TryGetSource( out IngredientSource current ) == false ||
             current != startedSource || current.CanTake == false )
            return true;

        source = current;
        return true;
    }

    #endregion

    #region ----- 대상 탐지 -----

    /// <summary>
    /// 현재 실행 대상의 컴포넌트 조회
    /// </summary>
    /// <param name="target">월드 상호작용 대상</param>
    /// <returns>유효 대상 존재 여부</returns>
    public bool TryGetInteractionTarget ( out Component target )
    {
        target = null;
        if ( isActiveAndEnabled == false || _interact == null ||
             TryGetHit( out RaycastHit hit ) == false )
            return false;

        //입력 시 즉시 처리하는 제작 미니게임 우선
        if ( WorldTargetSelector.TryGetProduceMinigameTarget(
            hit, out ProduceMinigameInteractable minigame ) == true )
        {
            target = minigame;
            return true;
        }

        ItemCarry heldItem = _interact.HoldItem as ItemCarry;		//보유 아이템
        if ( heldItem != null )
        {
            ProductDisposal disposal =
                hit.collider.GetComponentInParent<ProductDisposal>( );
            if ( disposal != null )
            {
                //폐기 불가여도 입력 대상은 유지하고 행동 안내만 숨김
                target = disposal;
                return true;
            }
        }

        if ( TryGetHeldFood( out Food food ) == true &&
             WorldTargetSelector.TryGetReceiver(
                 hit, out IFoodReceiver receiver ) == true &&
             receiver.CanReceive( food.Data ) == true )
        {
            target = receiver as Component;
            return target != null;
        }

        if ( _targetSelector.TrySelectInvestigation(
            hit, out IInvestigatable investigation ) == true )
        {
            target = investigation as Component;
            return target != null;
        }

        IngredientSource source =
            hit.collider.GetComponentInParent<IngredientSource>( );

        //공급 불가 상태에서도 표시용 그릇 대신 공급원을 대상으로 유지
        if ( source != null && source.isActiveAndEnabled == true )
        {
            target = source;
            return true;
        }

        if ( WorldTargetSelector.TryGetWorldTarget(
            hit, out IInteractable worldTarget ) == true )
        {
            target = worldTarget as Component;
            return target != null;
        }

        return false;
    }

    /// <summary>
    /// 현재 월드 대상과 실행 가능한 행동 이름 조회
    /// </summary>
    /// <param name="target">선택된 월드 대상</param>
    /// <param name="actionName">실행 가능하면 행동 이름, 불가하면 빈 문자열</param>
    /// <returns>월드 입력 대상으로 선택됐는지 여부</returns>
    public bool TryGetInteractionTarget (
        out Component target, out string actionName )
    {
        actionName = string.Empty;

        if ( TryGetInteractionTarget( out target ) == false )
            return false;

        if ( target is ProduceMinigameInteractable )
        {
            actionName = "조리";
        }
        else if ( target is ProductDisposal disposal )
        {
            ItemCarry heldItem = _interact.HoldItem as ItemCarry;		//폐기할 아이템

            if ( heldItem != null && disposal.CanDispose( heldItem ) == true )
                actionName = "버리기";
        }
        else if ( target is IngredientSource source )
        {
            if ( _interact.HasSpace == true && source.CanTake == true )
                actionName = "들기";
        }
        else if ( target is IFoodReceiver receiver &&
                  TryGetHeldFood( out Food food ) == true &&
                  receiver.CanReceive( food.Data ) == true )
        {
            actionName = "전달";
        }
        else if ( target is IInvestigatable investigation )
        {
            actionName = investigation.ActionName;
        }
        else if ( target is IInteractable interactable )
        {
            actionName = interactable.ActionName;
        }

        //안내가 없어도 월드 대상은 유지하여 다른 행동으로 넘어가지 않음
        return true;
    }

    /// <summary>
    /// 가장 먼저 닿은 월드 대상 확인
    /// </summary>
    /// <param name="hit">충돌 정보</param>
    /// <returns>대상 탐지 여부</returns>
    bool TryGetHit ( out RaycastHit hit )
    {
        return InteractionRaycaster.TryGetHit(
            _camera, _interactDist, _detectionLayerMask,
            QueryTriggerInteraction.Ignore, out hit );
    }

    /// <summary>
    /// 음식 수락 대상 확인
    /// </summary>
    /// <param name="receiver">음식 수락 대상</param>
    /// <returns>대상 확인 여부</returns>
    bool TryGetReceiver ( out IFoodReceiver receiver )
    {
        receiver = null;
        if ( TryGetHit( out RaycastHit hit ) == false )
            return false;

        return WorldTargetSelector.TryGetReceiver( hit, out receiver );
    }

    /// <summary>
    /// 현재 조준한 일반 상호작용 대상 확인
    /// </summary>
    /// <param name="target">상호작용 대상</param>
    /// <returns>상호작용 가능한 대상 확인 여부</returns>
    bool TryGetWorldTarget ( out IInteractable target )
    {
        target = null;
        if ( TryGetHit( out RaycastHit hit ) == false )
            return false;

        return WorldTargetSelector.TryGetWorldTarget( hit, out target );
    }

    /// <summary>
    /// 조준한 대상에서 사용 가능한 조사 기능 선택
    /// </summary>
    /// <param name="target">선택한 조사 기능</param>
    /// <returns>조사 가능한 기능 확인 여부</returns>
    bool TrySelectInvestigation ( out IInvestigatable target )
    {
        target = null;
        if ( TryGetHit( out RaycastHit hit ) == false )
            return false;

        return _targetSelector.TrySelectInvestigation( hit, out target );
    }

    /// <summary>
    /// 현재 조준한 재료 공급원 확인
    /// </summary>
    /// <param name="source">조준한 공급원</param>
    /// <returns>공급원 탐지 여부</returns>
    bool TryGetSource ( out IngredientSource source )
    {
        source = null;

        if ( TryGetHit( out RaycastHit hit ) == false )
            return false;

        source = hit.collider.GetComponentInParent<IngredientSource>( );
        return source != null && source.isActiveAndEnabled;
    }

    #endregion

    /// <summary>
    /// 조준한 보급 대상 조회
    /// </summary>
    /// <returns>활성 공급원 또는 null</returns>
    public IngredientSource GetSupplyTarget ()
    {
        if ( isActiveAndEnabled == false || _camera == null )
            return null;

        return TryGetSource( out IngredientSource source ) == true ? source : null;
    }


    #region ----- 상태 검사 -----

    /// <summary>
    /// 보유 음식 확인
    /// </summary>
    /// <param name="food">보유 음식</param>
    /// <returns>보유 음식 확인 여부</returns>
    bool TryGetHeldFood ( out Food food )
    {
        food = null;

        ItemCarry item = _interact.HoldItem as ItemCarry;

        //플레이어가 실제로 보유한 아이템인지 확인
        if ( item == null ||
             item.isActiveAndEnabled == false ||
             item.CurrentType != HoldType.View )
            return false;

        return item.TryGetComponent<Food>( out food );
    }

    /// <summary>
    /// 시작한 조사 기능의 조준 상태와 완료 조건 확인
    /// </summary>
    /// <param name="target">홀딩 시작 시 선택한 조사 기능</param>
    /// <param name="investigationId">홀딩 시작 시 저장한 조사 ID</param>
    /// <returns>같은 조사를 완료할 수 있는지 여부</returns>
    bool CanCompleteInvestigation ( IInvestigatable target, int investigationId )
    {
        if ( TryGetHit( out RaycastHit hit ) == false )
            return false;

        return WorldTargetSelector.CanCompleteInvestigation(
            hit, target, investigationId );
    }

    #endregion
}
