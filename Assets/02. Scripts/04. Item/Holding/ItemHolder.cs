using System;
using UnityEngine;

/// <summary>
/// 아이템 보유 모듈 - 보유 동작과 참조 갱신, 외부 해제 연결
/// </summary>
public class ItemHolder : IDisposable
{
    bool _isExecuting;     //같은 보유자의 중복 요청 차단
    bool _isDisposed;     //사용 종료 여부

    ICarryable _holdItem;       //현재 보유한 아이템
    HoldType _holdType;     //보유 방식
    Transform _holdPoint;     //현재 보유 지점

    /// <summary>
    /// 현재 들고 있는 아이템
    /// </summary>
    public ICarryable HoldItem => _holdItem;

    /// <summary>
    /// 아이템 보유 여부
    /// </summary>
    public bool HasItem => _holdItem != null;

    /// <summary>
    /// 보유 종료 알림
    /// </summary>
    public event Action<ICarryable> OnHoldEnded;


    /// <summary>
    /// 보유 방식 설정
    /// </summary>
    /// <param name="holdType">보유 방식</param>
    public ItemHolder ( HoldType holdType = HoldType.View )
    {
        //None은 실제 보유 방식으로 사용할 수 없음
        if ( holdType != HoldType.View && holdType != HoldType.World )
            throw new ArgumentOutOfRangeException( nameof( holdType ) );

        _holdType = holdType;
    }


    /// <summary>
    /// 보유 참조와 구독 해제
    /// </summary>
    void ClearHoldItem ()
    {
        ICarryable item = _holdItem;

        if ( item == null )
            return;

        //이전 아이템 정리
        _holdItem = null;
        _holdPoint = null;

        item.OnHoldChanged -= HandleHoldChanged;
        item.OnUnavailable -= HandleUnavailable;

        //보유 종료 이벤트 발행
        OnHoldEnded?.Invoke( item );
    }

    /// <summary>
    /// 보유 변경 확인
    /// </summary>
    /// <param name="previous">이전 방식</param>
    /// <param name="current">현재 방식</param>
    void HandleHoldChanged (
        HoldType previous, HoldType current )
    {
        //다른 보유자의 물건 제외
        if ( CheckHeldItem( _holdItem ) == false )
            ClearHoldItem( );
    }

    /// <summary>
    /// 보유 대상 등록
    /// </summary>
    /// <param name="item">확보한 아이템</param>
    /// <param name="holdPoint">보유 지점</param>
    void SetHoldItem ( ICarryable item, Transform holdPoint )
    {
        _holdPoint = holdPoint;
        _holdItem = item;

        //실제 확보가 끝난 대상만 구독
        item.OnHoldChanged += HandleHoldChanged;
        item.OnUnavailable += HandleUnavailable;
    }

    /// <summary>
    /// 소비 또는 비활성화된 아이템의 참조 해제
    /// </summary>
    void HandleUnavailable ()
    {
        ClearHoldItem( );
    }

    /// <summary>
    /// 보유 종료
    /// </summary>
    public void Dispose ()
    {
        if ( _isDisposed == true )
            return;

        //종료 알림 중 새 물건을 받지 않도록 먼저 차단
        _isDisposed = true;

        ICarryable item = _holdItem;
        Transform holdPoint = _holdPoint;

        ClearHoldItem( );

        //종료 알림 이후에도 자신의 지점에 남은 물건만 해제
        if ( CheckOwnership( item, holdPoint ) == true )
            item.TryRelease( );
    }


    #region ----- 들기/배치/해제 -----

    /// <summary>
    /// 빈손일 때 아이템 들기
    /// </summary>
    /// <param name="item">집을 아이템</param>
    /// <param name="holdPoint">보유 지점</param>
    /// <param name="ownerColliders">보유자 충돌체</param>
    /// <returns>보유 성공 여부</returns>
    public bool TryPickUp (
        ICarryable item,
        Transform holdPoint,
        Collider [ ] ownerColliders = null )
    {
        if ( _isDisposed == true || _isExecuting == true || HasItem == true )
            return false;

        if ( CheckAvailable( item ) == false )
            return false;

        //재요청 차단
        _isExecuting = true;

        try
        {
            bool pickedUp = item.TryPickUp(
                holdPoint, _holdType, ownerColliders );

            //집기 도중 보유자나 대상이 종료될 수 있으므로 결과 재확인
            if ( pickedUp == false || _isDisposed == true ||
                 CheckAvailable( item ) == false ||
                 CheckOwnership( item, holdPoint ) == false )
            {
                //이 모듈의 지점에 남은 물건만 해제
                //다른 보유자에게 넘어간 물건은 건드리지 않음
                if ( CheckOwnership( item, holdPoint ) == true )
                    item.TryRelease( );

                return false;
            }

            SetHoldItem( item, holdPoint );

            return true;
        }
        finally
        {
            //보유 처리 해제
            _isExecuting = false;
        }
    }

    /// <summary>
    /// 보유 종료 동작 실행과 참조 정리
    /// </summary>
    /// <param name="item">보유 아이템</param>
    /// <param name="action">실제 적용 결과를 반환할 동작</param>
    /// <returns>동작 성공 여부</returns>
    bool TryEndHold ( ICarryable item, Func<bool> action )
    {
        if ( _isDisposed == true || _isExecuting == true )
            return false;

        if ( CheckHeldItem( item ) == false )
            return false;

        _isExecuting = true;

        try
        {
            if ( action( ) == false )
                return false;

            //상태 알림에서 이미 정리됐을 수 있으므로 시작한 대상만 해제
            if ( ReferenceEquals( _holdItem, item ) == true )
                ClearHoldItem( );

            return true;
        }
        finally
        {
            //보유 처리 해제
            _isExecuting = false;
        }
    }

    /// <summary>
    /// 현재 보유 아이템 배치
    /// </summary>
    /// <param name="data">배치 정보</param>
    /// <returns>배치 성공 여부</returns>
    public bool TryPutDown ( PlacementData data )
    {
        ICarryable item = _holdItem;
        return TryEndHold( item, () => item.TryPutDown( data ) );
    }

    /// <summary>
    /// 지정한 보유 아이템 해제
    /// </summary>
    /// <param name="item">해제할 아이템</param>
    /// <returns>해제 성공 여부</returns>
    public bool TryRelease ( ICarryable item )
    {
        return TryEndHold( item, () => item.TryRelease( ) );
    }

    #endregion

    #region ----- 투척 -----

    /// <summary>
    /// 현재 보유 아이템의 투척 기능 조회
    /// </summary>
    /// <returns>활성 투척 기능</returns>
    public IThrowable GetThrowTarget ()
    {
        if ( CheckHeldItem( _holdItem ) == false )
            return null;

        Component item = ( Component ) _holdItem;
        IThrowable target = item.GetComponent<IThrowable>( );

        return target is Behaviour behaviour && behaviour.isActiveAndEnabled
            ? target : null;
    }

    /// <summary>
    /// 현재 보유 아이템 투척
    /// </summary>
    /// <param name="velocity">월드 초기 속도</param>
    /// <param name="ownerColliders">투척자 콜라이더</param>
    /// <returns>투척 성공 여부</returns>
    public bool TryThrow (
        Vector3 velocity, Collider [ ] ownerColliders )
    {
        ICarryable item = _holdItem;
        IThrowable target = GetThrowTarget( );

        return target != null &&
               TryEndHold( item, () => target.TryThrow( velocity, ownerColliders ) );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 활성 운반 대상인지 확인
    /// </summary>
    /// <param name="item">확인할 아이템</param>
    /// <returns>활성 컴포넌트 여부</returns>
    bool CheckAvailable ( ICarryable item )
    {
        return item is Behaviour behaviour && behaviour != null &&
               behaviour.isActiveAndEnabled;
    }

    /// <summary>
    /// 보유 방식과 지점 확인
    /// </summary>
    /// <param name="item">확인할 아이템</param>
    /// <param name="holdPoint">보유 지점</param>
    /// <returns>보유 일치 여부</returns>
    bool CheckOwnership ( ICarryable item, Transform holdPoint )
    {
        if ( item is not Component component || component == null )
            return false;

        if ( holdPoint == null )
            return false;

        //비활성화된 보유자를 정리할 때도 부모 관계는 확인
        return item.CurrentType == _holdType &&
               component.transform.parent == holdPoint;
    }

    /// <summary>
    /// 현재 보유 상태 확인
    /// </summary>
    /// <param name="item">확인할 아이템</param>
    /// <returns>보유 여부</returns>
    bool CheckHeldItem ( ICarryable item )
    {
        return ReferenceEquals( _holdItem, item ) &&
               CheckAvailable( item ) &&
               CheckOwnership( item, _holdPoint );
    }

    /// <summary>
    /// 지정한 아이템의 보유 여부 확인
    /// </summary>
    /// <param name="item">확인할 아이템</param>
    /// <returns>보유 여부</returns>
    public bool IsHolding ( ICarryable item )
    {
        return CheckHeldItem( item );
    }

    #endregion
}
