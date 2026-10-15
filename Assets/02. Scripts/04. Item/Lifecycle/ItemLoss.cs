using UnityEngine;

/// <summary>
/// 아이템 손실 처리 - 보유 해제와 비활성화
/// </summary>
[RequireComponent( typeof( ItemCarry ) )]
public class ItemLoss : MonoBehaviour
{
    bool _isLost;     //손실 확정 여부

    ItemCarry _carry;     //아이템 보유 기능
    ItemWorldController _view;     //아이템 뷰

    /// <summary>
    /// 손실 확정 여부
    /// </summary>
    public bool IsLost => _isLost;


    private void Awake ()
    {
        //아이템 루트의 보유 기능과 비활성화 기능 연결
        _carry = GetComponent<ItemCarry>( );
        _view = GetComponent<ItemWorldController>( );
    }


    /// <summary>
    /// 보유 아이템 손실 처리
    /// </summary>
    /// <param name="holdPoint">손실을 요청한 보유자의 지점</param>
    /// <returns>손실 처리 성공 여부</returns>
    public bool TryLose ( Transform holdPoint )
    {
        if ( CanLose( holdPoint ) == false )
            return false;

        //부모와 충돌 설정을 복구하고 보유자 목록에서 해제
        //적의 자식으로 남겨두면 적의 삭제나 재활성화에 영향을 받음
        if ( _carry.TryRelease( ) == false )
            return false;

        //기존 보유 해제 후 재확인 유지
        if ( this == null || isActiveAndEnabled == false ||
             _carry == null || _carry.IsHold == true )
            return false;

        if ( _carry.IsStockRegistered == true )
        {
            //등록 재료는 물리와 배치 상태까지 정리한 뒤 손실 확정
            if ( _carry.TryChangeConsumeState( out string reason ) == false )
            {
                Debug.LogWarning( $"[재료 손실 실패] {reason}", this );
                return false;
            }
        }
        else
        {
            //미등록 아이템은 기존 숨김 처리 유지
            _view.Hide( );
        }

        //손실 확정 처리
        _isLost = true;

        if ( _carry.IsStockRegistered == true )
            _carry.NotifyConsumeConfirmed( IngredientConsumeReason.ExternalLoss );

        return true;
    }


    /// <summary>
    /// 손실 기록 초기화
    /// </summary>
    public void ResetForReuse ()
    {
        _isLost = false;
    }

    /// <summary>
    /// 손실 요청과 보유 상태 확인
    /// </summary>
    /// <param name="holdPoint">요청한 보유자의 지점</param>
    /// <returns>손실 처리 가능 여부</returns>
    bool CanLose ( Transform holdPoint )
    {
        if ( _isLost || isActiveAndEnabled == false ||
             _carry == null || _view == null || holdPoint == null )
            return false;

        if ( _carry.isActiveAndEnabled == false )
            return false;

        //플레이어에게 회수됐거나 다른 지점으로 옮겨진 물건 제외
        return _carry.CurrentType == HoldType.World &&
               transform.parent == holdPoint;
    }

}
