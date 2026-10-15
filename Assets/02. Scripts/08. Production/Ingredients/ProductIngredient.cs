using UnityEngine;

/// <summary>
/// 제작 재료 - 제작 대상 등록, 제거 요청
/// </summary>
[RequireComponent( typeof( ItemCarry ) )]
public class ProductIngredient : MonoBehaviour
{
    ItemCarry _carry;		//아이템
    CookController _product;		//현재 등록된 제작 대상

    /// <summary>
    /// 재료 객체 ID
    /// </summary>
    public int InstanceId => gameObject.GetInstanceID( );

    /// <summary>
    /// 재료 데이터
    /// </summary>
    public ItemData Data =>
        _carry == null ? null : _carry.ItemData;

    /// <summary>
    /// 제작 대상 등록 여부
    /// </summary>
    public bool IsRegistered => _product != null;

    /// <summary>
    /// 재료 아이템
    /// </summary>
    public ItemCarry Carry => _carry;

    /// <summary>
    /// 재료가 등록된 제작 대상
    /// </summary>
    public CookController Product => _product;

    /// <summary>
    /// 소비 준비 여부
    /// </summary>
    bool IsPrepared => _product != null &&
        _product.Session.IsIngredientPrepared( this );


    /// <summary>
    /// 아이템 연결과 이벤트 구독
    /// </summary>
    void Awake ()
    {
        _carry = GetComponent<ItemCarry>( );

        _carry.OnPlaced += HandlePlaced;
        _carry.OnHoldChanged += HandleHoldChanged;
        _carry.OnUnavailable += HandleUnavailable;
        _carry.OnConsumeConfirmed += HandleConsumeConfirmed;
    }

    /// <summary>
    /// 아이템 이벤트 구독 해제
    /// </summary>
    void OnDestroy ()
    {
        _carry.OnPlaced -= HandlePlaced;
        _carry.OnHoldChanged -= HandleHoldChanged;
        _carry.OnUnavailable -= HandleUnavailable;
        _carry.OnConsumeConfirmed -= HandleConsumeConfirmed;

        Unregister( );
    }


    #region ----- 핸들 -----

    /// <summary>
    /// 제작 대상 배치 시 재료 등록
    /// </summary>
    /// <param name="target">실제 배치 대상</param>
    void HandlePlaced ( PlacementTarget target )
    {
        //대상이나 데이터가 없으면 종료
        if ( IsPrepared || target == null || Data == null )
            return;

        CookController product =
            target.GetComponentInParent<CookController>( );

        //제작품이 없으면 종료
        if ( product == null )
            return;

        //다른 제작 대상의 등록이 남아 있으면 먼저 해제
        if ( _product != null && _product != product )
            Unregister( );

        //재료 추가
        if ( product.Session.TryAddIngredient( this, target ) == true )
            _product = product;
    }

    /// <summary>
    /// 제작 대상에서 제거
    /// </summary>
    /// <param name="previousType">이전 보유 방식</param>
    /// <param name="currentType">현재 보유 방식</param>
    void HandleHoldChanged (
        HoldType previousType, HoldType currentType )
    {
        if ( previousType == HoldType.None &&
             currentType != HoldType.None )
        {
            Unregister( );
        }
    }

    /// <summary>
    /// 재료 손실 전달
    /// </summary>
    /// <param name="item">소비가 확정된 아이템</param>
    /// <param name="reason">소비 원인</param>
    void HandleConsumeConfirmed (
        ItemCarry item, IngredientConsumeReason reason )
    {
        if ( item != _carry ) return;

        if ( reason != IngredientConsumeReason.ExternalLoss ) return;

        if ( _product == null ) return;

        _product.Session.TryHandleExternalLoss( this );
    }

    /// <summary>
    /// 일반 비활성화 시 재료 등록 상태 확인
    /// </summary>
    void HandleUnavailable ()
    {
        //재고 재료의 손실은 소비 확정 알림에서만 처리
        if ( IsPrepared == true ) return;
        if ( _carry != null && _carry.IsStockRegistered == true )
            return;

        Unregister( );
    }

    #endregion

    #region ----- 등록/정리 -----

    /// <summary>
    /// 재료 집기 잠금 설정
    /// </summary>
    /// <param name="product"> 제작 대상</param>
    /// <param name="isLocked">집기 잠금 여부</param>
    /// <returns>잠금 적용 여부</returns>
    public bool SetLocked ( CookController product, bool isLocked )
    {
        if ( IsPrepared == true || _product != product || _carry == null )
            return false;

        _carry.SetCarryLocked( isLocked );
        return true;
    }

    /// <summary>
    /// 현재 제작 대상에서 재료 제거
    /// </summary>
    void Unregister ()
    {
        if ( _product == null )
            return;

        CookController product = _product;

        if ( product.Session.TryRemoveIngredient( this ) == true )
            _product = null;
    }

    /// <summary>
    /// 소비 진행 여부를 확인하고 제작 등록 해제
    /// </summary>
    /// <param name="reason">반환 준비 실패 사유</param>
    /// <returns>반환 준비 성공 여부</returns>
    public bool TryPreparePoolReturn ( out string reason )
    {
        reason = string.Empty;

        if ( _product == null )
            return true;

        if ( _product.Session.IsConsuming == true )
        {
            reason = "제작 재료 소비가 진행 중입니다.";
            return false;
        }

        Unregister( );

        if ( _product != null )
        {
            reason = "제작 재료 등록을 해제하지 못했습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 등록 참조 정리
    /// </summary>
    /// <param name="product">등록을 해제한 제작 대상</param>
    public void ClearProduct ( CookController product )
    {
        if ( IsPrepared == true || _product != product )
            return;

        if ( _carry != null )
            _carry.SetCarryLocked( false );

        _product = null;
    }

    #endregion

    #region ----- 소비 -----

    /// <summary>
    /// 재료 소비 적용
    /// </summary>
    /// <param name="product">제작 대상</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>실제 소비 성공 여부</returns>
    public bool ApplyConsume (
        CookController product, out string reason )
    {
        reason = "소비 준비 또는 제작 연결이 변경됐습니다.";

        if ( product == null || _product != product ) return false;

        if ( product.Session.IsApplyingIngredient( this ) == false ) return false;

        if ( _carry == null ) return false;

        //실제 비활성화에 성공한 재료만 소비 처리
        if ( _carry.TryDeactivateForConsume( out reason ) == false )
            return false;

        _product = null;
        return true;
    }

    /// <summary>
    /// 재료 소비 가능 여부
    /// </summary>
    /// <param name="product">제작 대상</param>
    /// <returns>소비 가능 여부</returns>
    public bool CanConsume ( CookController product )
    {
        return product != null && IsPrepared == false &&
               _product == product &&
               _carry != null &&
               _carry.CanChangeConsumeState;
    }

    #endregion
}
