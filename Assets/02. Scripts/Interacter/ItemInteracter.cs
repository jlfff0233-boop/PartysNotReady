using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 아이템 상호작용 중재자 - 대상 탐지, 들기, 내려놓기
/// </summary>
public class ItemInteracter : MonoBehaviour
{
    [Header ( "----- 설정 데이터 -----" )]
    [SerializeField] float _interactDist;       //상호작용 거리
    [SerializeField] LayerMask _detectionLayerMask;   //상호작용 탐지 레이어 마스크

    Camera _camera;           //카메라 
    PlayerItemHolder _interact;     //아이템 보유 처리

    Transform _holdPoint;       //아이템 들기 위치
    ICarryable _carryTarget;       //현재 들기 대상
    PlacementTarget _placementTarget;       //현재 배치 대상
    PlacementData _placementData;        //현재 배치 정보
    ItemHolder _startedHolder;		//입력 시작 보유 모듈

    bool _canPlace;     //배치 가능 여부
    bool _isIngredientInput;		//재료 집기 또는 배치로 시작한 입력 여부
    bool _isPlacementInput;     //배치로 시작한 입력 여부
    ICarryable _startedItem;     //입력 시작 시 집거나 배치할 아이템
    IngredientInteracter _ingredientInteracter;		//그릇 재료 상호작용 처리
    PlacementTarget _startedPlacement;     //입력 시작 시 배치 대상


    /// <summary>
    /// 아이템 상호작용 거리
    /// </summary>
    public float InteractDistance => _interactDist;

    /// <summary>
    /// 재료 상호작용 실패로 일반 집기와 투척이 차단된 상태
    /// </summary>
    public bool IsIngredientInteractBlocked =>
        _ingredientInteracter != null && _ingredientInteracter.IsBlocked;

    /// <summary>
    /// 입력을 시작한 아이템 상호작용 행동 이름
    /// </summary>
    public string ActionName
    {
        get
        {
            if ( _isIngredientInput == true )
                return _ingredientInteracter.ActionName;

            if ( _startedItem == null )
                return string.Empty;

            return _isPlacementInput == true ? "놓기" : "들기";
        }
    }

    /// <summary>
    /// 들기 대상 변경 이벤트(변경 대상)
    /// </summary>
    public event Action<ICarryable> OnCarryTargetChanged;
    /// <summary>
    /// 아이템 배치 완료 이벤트(배치 대상)
    /// </summary>
    public event Action<PlacementTarget> OnPlaced;

    /// <summary>
    /// 아이템 상호작용 중재자 초기화
    /// </summary>
    /// <param name="interact">아이템 보유 처리</param>
    /// <param name="camera">카메라</param>
    /// <param name="holdPoint">아이템을 들고 있을 지점</param>
    public void Init (
        PlayerItemHolder interact , Camera camera , Transform holdPoint )
    {
        _interact = interact;
        _camera = camera;
        _holdPoint = holdPoint;

        if ( _ingredientInteracter != null )
            _ingredientInteracter.OnPlaced -= HandleIngredientPlaced;

        _ingredientInteracter = new IngredientInteracter ( interact , CanPlace );
        _ingredientInteracter.OnPlaced += HandleIngredientPlaced;
    }

    private void Update ( )
    {
        UpdateTarget ( );
    }

    private void OnDisable ( )
    {
        CancelInteract ( );
        ClearTargets ( );
    }


    #region ----- 상호작용 -----

    /// <summary>
    /// 입력 시작 대상과 동작 정리
    /// </summary>
    public void CancelInteract ( )
    {
        _startedItem = null;
        _startedHolder = null;
        _startedPlacement = null;
        _isPlacementInput = false;
        _isIngredientInput = false;
        _ingredientInteracter?.CancelInteract ( );
    }

    /// <summary>
    /// 들기 또는 배치 대상 저장
    /// </summary>
    /// <returns>상호작용 시작 여부</returns>
    public bool BeginInteract ( )
    {
        return TryBeginInteract ( null , false );
    }

    /// <summary>
    /// 레시피 대상의 재료 집기 또는 배치 입력 시작
    /// </summary>
    /// <param name="product">레시피를 표시 중인 제작 대상</param>
    /// <returns>재료 상호작용 시작 여부</returns>
    public bool BeginIngredientInteract ( CookController product )
    {
        return TryBeginInteract ( product , true );
    }

    /// <summary>
    /// 대상 갱신 후 처리 모듈과 입력 시작 정보 저장
    /// </summary>
    /// <param name="product">상호작용을 제한할 제작 대상</param>
    /// <param name="ingredientOnly">재료 상호작용만 허용할지 여부</param>
    /// <returns>상호작용 시작 여부</returns>
    bool TryBeginInteract ( CookController product , bool ingredientOnly )
    {
        CancelInteract ( );

        if ( IsInteractReady ( ) == false )
            return false;

        if ( ingredientOnly == true && product == null )
            return false;

        UpdateTarget ( );

        if ( _ingredientInteracter.HasTarget == true )
        {
            _isIngredientInput = _ingredientInteracter.BeginInteract ( product );
            return _isIngredientInput;
        }

        if ( ingredientOnly == true || HasInteractTarget ( ) == false )
            return false;

        SaveStartedTarget ( );
        return true;
    }


    /// <summary>
    /// 현재 대상과 동작을 입력 시작 정보로 저장
    /// </summary>
    void SaveStartedTarget ( )
    {
        //홀딩 중 시선이 바뀌어도 시작한 대상과 동작은 유지
        _isPlacementInput = _canPlace;

        _startedHolder = _isPlacementInput == true
            ? _interact.ActiveHolder : null;

        _startedItem = _isPlacementInput == true
            ? _interact.HoldItem : _carryTarget;

        _startedPlacement = _isPlacementInput == true
            ? _placementTarget : null;
    }

    /// <summary>
    /// 저장한 대상으로 상호작용 실행
    /// </summary>
    public void Interact ( )
    {
        if ( IsInteractReady ( ) == false )
        {
            CancelInteract ( );
            return;
        }

        if ( _isIngredientInput == true )
        {
            _isIngredientInput = false;

            //완료 순간의 시선과 거리 및 상호작용 가능 여부 재확인
            UpdateTarget ( );
            _ingredientInteracter.Interact ( );
            return;
        }

        ICarryable startedItem = _startedItem;
        ItemHolder startedHolder = _startedHolder;
        PlacementTarget startedPlacement = _startedPlacement;
        bool isPlacementInput = _isPlacementInput;

        //실행할 요청을 보관한 뒤 비워 중복 실행 방지
        CancelInteract ( );

        if ( startedItem is not Component component || component == null ||
             component.gameObject.activeInHierarchy == false ||
             ( component is Behaviour behaviour && behaviour.isActiveAndEnabled == false ) )
            return;

        //완료 순간의 시선과 거리 및 배치 가능 여부 재확인
        UpdateTarget ( );

        if ( isPlacementInput == true )
        {
            string reason = string.Empty;       //놓기 취소 사유

            if ( _interact.IsActive ( startedHolder , startedItem ) == false )
                reason = "입력 시작 아이템과 현재 보유 아이템이 다릅니다.";
            else if ( startedPlacement == null )
                reason = "입력 시작 배치 대상이 없습니다.";
            else if ( _placementTarget != startedPlacement )
                reason = "입력 완료 시 배치 대상이 변경됐습니다.";
            else if ( _canPlace == false )
                reason = "입력 완료 시 배치 가능한 위치가 없습니다.";

            if ( reason.Length > 0 )
            {
#if UNITY_EDITOR
                PlayDebugHistory.Record( "놓기 취소", component, reason );
#endif
                return;
            }

            PutDown ( startedHolder , startedItem );
            return;
        }

        if ( _interact.HasSpace == false ||
             object.ReferenceEquals ( _carryTarget , startedItem ) == false )
            return;

        PickUp ( );
    }

    /// <summary>
    /// 재료 배치 완료 알림 전달
    /// </summary>
    /// <param name="target">배치 완료 대상</param>
    void HandleIngredientPlaced ( PlacementTarget target )
    {
        OnPlaced?.Invoke ( target );
    }


    /// <summary>
    /// 아이템 상호작용의 실행 준비 여부 확인
    /// </summary>
    /// <returns>상호작용 실행 준비 여부</returns>
    bool IsInteractReady ( )
    {
        return isActiveAndEnabled == true &&
               _interact != null &&
               _camera != null;
    }

    #endregion

    #region ----- 대상 탐지 -----

    /// <summary>
    /// 화면 중앙 Raycast 결과 확인
    /// </summary>
    /// <param name="hit">레이 충돌 정보</param>
    /// <returns>충돌 여부</returns>
    bool TryGetTargetHit ( out RaycastHit hit )
    {
        return InteractionRaycaster.TryGetHit (
            _camera , _interactDist , _detectionLayerMask ,
            QueryTriggerInteraction.Ignore , out hit );
    }

    /// <summary>
    /// 현재 실행 가능한 집기 또는 배치 대상 조회
    /// </summary>
    /// <param name="target">상호작용 대상</param>
    /// <returns>유효 대상 존재 여부</returns>
    public bool TryGetInteractionTarget ( out Component target )
    {
        target = null;

        if ( IsInteractReady( ) == false )
            return false;

        UpdateTarget( );
        if ( HasInteractTarget( ) == false )
            return false;

        //재료 동작이 차단된 경우 일반 집기로 전환하지 않음
        if ( _ingredientInteracter.HasTarget == true )
            return _ingredientInteracter.TryGetInteractionTarget( out target );

        target = _canPlace == true
            ? _placementTarget : _carryTarget as Component;
        return target != null;
    }

    /// <summary>
    /// 현재 실행 가능한 아이템 대상과 행동 이름 조회
    /// </summary>
    /// <param name="target">상호작용 대상</param>
    /// <param name="actionName">실행 가능한 행동 이름</param>
    /// <returns>유효 대상 존재 여부</returns>
    public bool TryGetInteractionTarget (
        out Component target , out string actionName )
    {
        actionName = string.Empty;

        if ( TryGetInteractionTarget ( out target ) == false )
            return false;

        //등록 재료 동작은 재료 중재자의 현재 판정 사용
        actionName = _ingredientInteracter.HasTarget == true
            ? _ingredientInteracter.PreviewActionName
            : _canPlace == true ? "놓기" : "들기";

        return true;
    }

    /// <summary>
    /// 조준한 레시피 대상 조회
    /// </summary>
    /// <param name="product">제작 대상</param>
    /// <returns>대상 조회 여부</returns>
    public bool TryGetRecipeTarget ( out CookController product )
    {
        product = null;

        if ( isActiveAndEnabled == false )
            return false;

        //현재 시선과 상호작용 거리로 탐지
        if ( TryGetTargetHit ( out RaycastHit hit ) == false )
            return false;

        return ProductTargetSelector.TryGetProduct ( hit , out product );
    }

    /// <summary>
    /// 들기 대상 설정
    /// </summary>
    /// <param name="target">들기 대상</param>
    void SetCarryTarget ( ICarryable target )
    {
        //이미 설정돼 있으면 종료
        if ( _carryTarget == target ) return;

        _carryTarget = target;
        OnCarryTargetChanged?.Invoke ( _carryTarget );
    }

    /// <summary>
    /// 배치 대상 초기화
    /// </summary>
    void ClearPlacementTarget ( )
    {
        _placementTarget = null;
        _placementData = default;
        _canPlace = false;
    }

    /// <summary>
    /// 들기 및 배치 대상 초기화
    /// </summary>
    void ClearTargets ( )
    {
        SetCarryTarget ( null );
        ClearPlacementTarget ( );
        _ingredientInteracter?.ClearTarget ( );
    }

    /// <summary>
    /// 들기 대상 갱신
    /// </summary>
    /// <param name="hit">레이 충돌 정보</param>
    void UpdateCarryTarget ( RaycastHit hit )
    {
        ICarryable target = hit.collider.GetComponentInParent<ICarryable> ( );

        SetCarryTarget ( target );
    }

    /// <summary>
    /// 배치 대상 갱신
    /// </summary>
    /// <param name="hit">레이 충돌 정보</param>
    void UpdatePlacementTarget ( RaycastHit hit )
    {
        //들고 있는 아이템 가져오기
        ICarryable holdItem = _interact.HoldItem;

        if ( holdItem == null )
            return;

        //일반 배치 대상 가져오기
        PlacementTarget target =
            hit.collider.GetComponentInParent<PlacementTarget> ( );

        if ( target == null )
            return;

        if ( CanPlace ( holdItem , target ) == false )
            return;

        if ( target.TryGetPlacement (
            hit , holdItem.PlacementBoundsSize ,
            holdItem.PlacementRotationOffset ,
            out PlacementData placementData ) == false )
            return;

        _placementTarget = target;
        _placementData = placementData;
        _canPlace = true;
    }

    /// <summary>
    /// 집기와 배치 대상 갱신
    /// </summary>
    void UpdateTarget ( )
    {
        if ( _interact == null || TryGetTargetHit ( out RaycastHit hit ) == false )
        {
            ClearTargets ( );
            return;
        }

        ClearTargets ( );

        //등록 재료 집기 또는 그릇 배치를 먼저 확인
        _ingredientInteracter.UpdateTarget ( hit );

        //재료 대상은 실행 불가여도 일반 집기와 배치로 전환하지 않음
        if ( _ingredientInteracter.HasTarget == true )
            return;

        //일반 대상에서는 활성 보유 아이템의 배치를 추가 집기보다 먼저 확인
        if ( _interact.HasItem == true )
        {
            UpdatePlacementTarget ( hit );

            if ( _canPlace == true )
                return;
        }

        if ( _interact.HasSpace == false )
            return;

        ItemCarry item = hit.collider.GetComponentInParent<ItemCarry> ( );

        if ( item == null )
            return;

        if ( item.isActiveAndEnabled == false ||
             item.IsCarryLocked == true ||
             item.CurrentType == HoldType.View )
            return;

        SetCarryTarget ( item );
    }

    /// <summary>
    /// 지정 오브젝트 또는 자식을 조준 중인지 확인
    /// </summary>
    /// <param name="target">확인할 대상의 루트</param>
    /// <returns>상호작용 거리 안에서 조준 중인지 여부</returns>
    public bool IsLookingAt ( Transform target )
    {
        if ( isActiveAndEnabled == false || target == null )
            return false;

        if ( TryGetTargetHit ( out RaycastHit hit ) == false )
            return false;

        //대상 아래에 배치된 재료를 바라보는 경우도 포함
        return hit.collider.transform.IsChildOf ( target );
    }

    #endregion

    #region ----- 들기/배치 -----

    /// <summary>
    /// 들기 대상 아이템 들기
    /// </summary>
    void PickUp ( )
    {
        //대상이 없거나 들 지점이 없으면 종료
        if ( _carryTarget == null || _holdPoint == null ) return;

        //실제 집기에 성공했을 때만 보유 참조 등록
        if ( _interact.TryPickUp ( _carryTarget as ItemCarry ) == false )
            return;

        //들기 처리 후 조준 대상 해제
        SetCarryTarget ( null );
    }

    /// <summary>
    /// 들고 있는 아이템 내려놓기
    /// </summary>
    /// <param name="holder">입력 시작 보유 모듈</param>
    /// <param name="holdItem">입력 시작 아이템</param>
    void PutDown ( ItemHolder holder , ICarryable holdItem )
    {

        if ( holdItem == null || _canPlace == false )
            return;

        PlacementTarget target = _placementTarget;

        if ( _interact.TryPutDown ( holder , holdItem , _placementData ) == false )
            return;

        ClearPlacementTarget ( );

        //아이템의 재료 등록 처리가 끝난 뒤 화면에 배치 결과 전달
        OnPlaced?.Invoke ( target );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 현재 상호작용 가능한 대상이 있는지 확인
    /// </summary>
    /// <returns>상호작용 가능 여부</returns>
    public bool HasInteractTarget ( )
    {
        if ( _ingredientInteracter != null &&
             _ingredientInteracter.HasTarget == true )
            return _ingredientInteracter.CanInteract;

        if ( _canPlace == true )
            return true;

        if ( _interact == null || _interact.HasSpace == false )
            return false;

        return _holdPoint != null &&
               _carryTarget is ItemCarry item &&
               item != null;
    }

    /// <summary>
    /// 음식의 배치 대상 허용 여부 확인
    /// </summary>
    /// <param name="holdItem">보유 아이템</param>
    /// <param name="target">배치 대상</param>
    /// <returns>배치 가능 여부</returns>
    bool CanPlace ( ICarryable holdItem , PlacementTarget target )
    {
        //쓰레기통은 배치가 아닌 보유 음식의 월드 상호작용으로 처리
        if ( target.GetComponent<ProductDisposal> ( ) != null )
            return false;

        //보유 아이템의 음식 컴포넌트 확인
        if ( holdItem is Component component &&
             component.TryGetComponent<Food> ( out Food food ) )
        {
            return food.Data.CanPlace ( target.FoodPlacementType );
        }

        //일반 아이템에는 음식 배치 종류 제한을 적용하지 않음
        return true;
    }

    #endregion
}
