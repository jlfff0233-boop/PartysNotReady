using System;
using UnityEngine;

/// <summary>
/// 재료 상호작용 중재 - 그릇 재료 집기와 배치 입력 처리
/// </summary>
public class IngredientInteracter
{
    /// <summary>
    /// 재료 대상 행동 타입
    /// </summary>
    enum IngredientActionType
    {
        None,       //대상 없음
        PickUp,     //재료 집기
        Place       //재료 배치
    }

    PlayerItemHolder _interact;		//아이템 보유 처리

    /// <summary>
    /// 음식 배치 대상 허용 검사(아이템, 배치 대상, 가능 여부)
    /// </summary>
    Func<ICarryable , PlacementTarget , bool> _canPlaceItem;

    IngredientActionType _action;       //현재 조준 대상의 동작
    CookController _product;		//현재 제작 대상

    ProductIngredient _ingredient;		//현재 집기 대상 재료
    ItemCarry _targetItem;		//현재 집거나 배치할 아이템
    PlacementTarget _placementTarget;		//현재 배치 대상
    RaycastHit _placementHit;		//내려놓기 확정 시 다시 계산할 조준 정보
    bool _canInteract;		//현재 동작 가능 여부

    IngredientActionType _startedAction;		//입력 시작 동작
    ItemHolder _startedHolder;		//입력 시작 보유 모듈
    ItemCarry _startedItem;		//입력 시작 시 집거나 배치할 아이템
    PlacementTarget _startedPlacement;		//입력 시작 시 배치 대상
    ProductIngredient _startedIngredient;		//입력 시작 습득 재료
    CookController _startedProduct;		//입력 시작 재료 소속 그릇


    /// <summary>
    /// 재료 상호작용 대상 조준 여부
    /// </summary>
    public bool HasTarget => _action != IngredientActionType.None;

    /// <summary>
    /// 현재 재료 상호작용 가능 여부
    /// </summary>
    public bool CanInteract => _canInteract;

    /// <summary>
    /// 재료 상호작용 실패로 일반 집기와 투척이 차단된 상태
    /// </summary>
    public bool IsBlocked => HasTarget == true && _canInteract == false;


    /// <summary>
    /// 입력을 시작한 재료 상호작용 행동 이름
    /// </summary>
    public string ActionName
    {
        get
        {
            return GetActionName ( _startedAction );
        }
    }

    /// <summary>
    /// 현재 실행 가능한 재료 행동 이름
    /// </summary>
    public string PreviewActionName =>
        _canInteract == true ? GetActionName ( _action ) : string.Empty;

    /// <summary>
    /// 아이템 배치 완료 이벤트(배치 대상)
    /// </summary>
    public event Action<PlacementTarget> OnPlaced;


    /// <summary>
    /// 보유 처리와 배치 허용 검사 연결
    /// </summary>
    /// <param name="interact">아이템 보유 처리</param>
    /// <param name="canPlaceItem">음식 배치 대상 허용 검사</param>
    public IngredientInteracter (
        PlayerItemHolder interact ,
        Func<ICarryable , PlacementTarget , bool> canPlaceItem )
    {
        _interact = interact;
        _canPlaceItem = canPlaceItem;
    }


    #region ----- 상호작용 -----

    /// <summary>
    /// 입력 시작 대상과 동작 정리
    /// </summary>
    public void CancelInteract ( )
    {
        _startedAction = IngredientActionType.None;
        _startedItem = null;
        _startedHolder = null;
        _startedPlacement = null;
        _startedIngredient = null;
        _startedProduct = null;
    }

    /// <summary>
    /// 현재 재료 집기 또는 배치 입력 시작
    /// </summary>
    /// <param name="product">레시피 대상</param>
    /// <returns>재료 상호작용 시작 여부</returns>
    public bool BeginInteract ( CookController product )
    {
        CancelInteract ( );

        if ( _canInteract == false )
            return false;

        //레시피가 열려 있으면 해당 그릇의 재료만 처리
        if ( product != null && _product != product )
            return false;

        SaveStartedTarget ( );
        return true;
    }

    /// <summary>
    /// 현재 실행 가능한 재료 상호작용 대상 조회
    /// </summary>
    /// <param name="target">집을 재료 또는 재료를 넣을 그릇</param>
    /// <returns>유효 대상 존재 여부</returns>
    public bool TryGetInteractionTarget ( out Component target )
    {
        target = null;

        if ( _canInteract == false )
            return false;

        if ( _action == IngredientActionType.PickUp )
            target = _targetItem;
        else if ( _action == IngredientActionType.Place )
            target = _product;

        return target != null;
    }

    /// <summary>
    /// 현재 대상과 동작을 입력 시작 정보로 저장
    /// </summary>
    void SaveStartedTarget ( )
    {
        //홀딩 중 시선이 바뀌어도 시작한 대상과 동작은 유지
        _startedAction = _action;
        _startedProduct = _product;
        _startedItem = _targetItem;
        _startedIngredient = _ingredient;
        _startedPlacement = _placementTarget;

        _startedHolder = _action == IngredientActionType.Place
            ? _interact.ActiveHolder : null;
    }

    /// <summary>
    /// 저장한 대상으로 재료 집기 또는 배치 실행
    /// </summary>
    public void Interact ( )
    {
        IngredientActionType action = _startedAction;
        ItemCarry item = _startedItem;
        ItemHolder holder = _startedHolder;

        bool canComplete = CanCompleteInteract ( );

        //실행할 요청을 보관한 뒤 비워 중복 실행 방지
        CancelInteract ( );

        if ( canComplete == false )
            return;

        if ( action == IngredientActionType.PickUp )
        {
            PickUp ( item );
            return;
        }

        PutDown ( holder , item );
    }


    /// <summary>
    /// 조준한 재료 습득
    /// </summary>
    /// <param name="item">입력 시작 습득 아이템</param>
    void PickUp ( ItemCarry item )
    {
        //보유 상태 변경 이벤트에서 재료 등록 해제
        if ( _interact.TryPickUp ( item ) == false )
            return;

        ClearTarget ( );
    }

    /// <summary>
    /// 들고 있는 아이템 내려놓기
    /// </summary>
    /// <param name="holder">입력 시작 보유 모듈</param>
    /// <param name="holdItem">입력 시작 아이템</param>
    void PutDown ( ItemHolder holder , ItemCarry holdItem )
    {
        //조준 갱신 이후 달라질 수 있는 제작 상태와 지지면을 확정 직전에 확인
        if ( CanPlaceIngredient ( holdItem , _product ) == false )
            return;

        if ( _product.TryGetIngredientPlacement (
            holdItem , _placementHit , out PlacementData placement ) == false )
            return;

        PlacementTarget target = _placementTarget;

        if ( _interact.TryPutDown ( holder , holdItem , placement ) == false )
            return;

        ClearTarget ( );

        //아이템의 재료 등록 처리가 끝난 뒤 화면에 배치 결과 전달
        OnPlaced?.Invoke ( target );
    }


    /// <summary>
    /// 시작한 동작과 대상이 유지되는지 확인
    /// </summary>
    /// <returns>저장한 입력 완료 가능 여부</returns>
    bool CanCompleteInteract ( )
    {
        if ( _canInteract == false || _action != _startedAction )
            return false;

        if ( _product != _startedProduct || _targetItem != _startedItem )
            return false;

        if ( _startedAction == IngredientActionType.PickUp )
        {
            //홀딩 중 다른 그릇으로 옮겨진 재료는 습득하지 않음
            return _ingredient == _startedIngredient;
        }

        if ( _startedAction == IngredientActionType.Place )
        {
            return _placementTarget == _startedPlacement &&
                   _interact.IsActive ( _startedHolder , _startedItem ) == true;
        }

        return false;
    }

    #endregion

    #region ----- 대상 탐지 -----

    /// <summary>
    /// 집기와 배치 대상 초기화
    /// </summary>
    public void ClearTarget ( )
    {
        _action = IngredientActionType.None;
        _product = null;
        _ingredient = null;
        _targetItem = null;
        _placementTarget = null;
        _placementHit = default;
        _canInteract = false;
    }

    /// <summary>
    /// 조준한 대상에 따라 재료 집기 또는 배치 선정
    /// </summary>
    /// <param name="hit">레이 충돌 정보</param>
    public void UpdateTarget ( RaycastHit hit )
    {
        ClearTarget ( );

        ProductIngredient ingredient =
            hit.collider.GetComponentInParent<ProductIngredient> ( );

        //등록된 재료를 조준하면 보유 여부와 관계없이 집기로 결정
        if ( ingredient != null && ingredient.Product != null )
        {
            SetPickUpTarget ( ingredient );
            return;
        }

        //들고 있는 아이템 가져오기
        ICarryable holdItem = _interact.HoldItem;

        if ( holdItem == null )
            return;

        //조준한 제작 대상 확인
        if ( ProductTargetSelector.TryGetProduct (
            hit , out CookController product ) == false )
            return;

        PlacementTarget target =
            hit.collider.GetComponentInParent<PlacementTarget> ( );

        bool isIngredientSurface = product.IsIngredientTarget ( target );

        //그릇 몸체는 재료를 보유 중일 때만 배치 대상으로 처리
        if ( isIngredientSurface == false )
        {
            if ( holdItem is not ItemCarry carry ||
                 carry.TryGetComponent<ProductIngredient> ( out _ ) == false )
                return;
        }

        SetPlacementTarget ( holdItem , product , hit );
    }

    /// <summary>
    /// 조준한 재료를 집기 대상으로 설정
    /// </summary>
    /// <param name="ingredient">조준한 등록 재료</param>
    void SetPickUpTarget ( ProductIngredient ingredient )
    {
        _action = IngredientActionType.PickUp;
        _product = ingredient.Product;
        _ingredient = ingredient;
        _targetItem = ingredient.Carry;
        _canInteract = CanPickUpIngredient ( ingredient );
    }

    /// <summary>
    /// 조준한 그릇을 재료 배치 대상으로 설정
    /// </summary>
    /// <param name="holdItem">보유 아이템</param>
    /// <param name="product">제작 대상</param>
    /// <param name="hit">레이 충돌 정보</param>
    void SetPlacementTarget (
        ICarryable holdItem , CookController product , RaycastHit hit )
    {
        //재료 배치 실패를 그릇 집기나 투척으로 바꾸지 않음
        _action = IngredientActionType.Place;
        _product = product;
        _placementTarget = product.IngredientTarget;
        _placementHit = hit;

        if ( holdItem is not ItemCarry item )
            return;

        _targetItem = item;

        if ( CanPlaceIngredient ( item , product ) == false )
            return;

        if ( product.TryGetIngredientPlacement (
            item , hit , out _ ) == false )
            return;

        _canInteract = true;
    }

    /// <summary>
    /// 제작 대상의 재료 이동 가능 여부 확인
    /// </summary>
    /// <param name="product">제작 대상</param>
    /// <returns>그릇을 내려놓은 상태인지 여부</returns>
    bool CanMoveIngredient ( CookController product )
    {
        return product != null && product.IsBowlHeld == false;
    }

    /// <summary>
    /// 그릇 재료의 습득 가능 여부 확인
    /// </summary>
    /// <param name="ingredient">조준한 재료</param>
    /// <returns>습득 가능 여부</returns>
    bool CanPickUpIngredient ( ProductIngredient ingredient )
    {
        if ( _interact.HasSpace == false )
            return false;

        if ( ingredient.isActiveAndEnabled == false )
            return false;

        CookController product = ingredient.Product;

        if ( CanMoveIngredient ( product ) == false )
            return false;

        //준비 상태에서만 재료 이동 허용
        if ( product.Session.CanReceiveIngredients == false )
            return false;

        ItemCarry carry = ingredient.Carry;

        if ( carry == null || carry.isActiveAndEnabled == false )
            return false;

        return carry.IsCarryLocked == false &&
               carry.CurrentType == HoldType.None;
    }

    /// <summary>
    /// 그릇에 재료 배치 가능 여부 확인
    /// </summary>
    /// <param name="item">보유 아이템</param>
    /// <param name="product">제작 대상</param>
    /// <returns>재료 배치 가능 여부</returns>
    bool CanPlaceIngredient ( ItemCarry item , CookController product )
    {
        if ( CanMoveIngredient ( product ) == false )
            return false;

        ProductIngredient ingredient =
            item.GetComponent<ProductIngredient> ( );

        if ( product.Session.CanAddIngredient (
            ingredient , product.IngredientTarget ) == false )
            return false;

        return _canPlaceItem ( item , product.IngredientTarget );
    }

    /// <summary>
    /// 재료 행동에 해당하는 안내 이름 조회
    /// </summary>
    /// <param name="action">재료 행동</param>
    /// <returns>행동 이름 또는 빈 문자열</returns>
    string GetActionName ( IngredientActionType action )
    {
        switch ( action )
        {
            case IngredientActionType.PickUp:
                return "들기";

            case IngredientActionType.Place:
                return "넣기";

            default:
                return string.Empty;
        }
    }

    #endregion
}
