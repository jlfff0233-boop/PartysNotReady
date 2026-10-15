using UnityEngine;

/// <summary>
/// 음식 폐기 - 보유 중인 그릇 내용물 또는 단독 음식 처리
/// </summary>
public class ProductDisposal : MonoBehaviour
{

    /// <summary>
    /// 그릇 내용물/단독 음식 폐기
    /// </summary>
    /// <param name="item">보유 아이템</param>
    /// <param name="holder">보유 상태 모듈</param>
    /// <returns>폐기 완료 여부</returns>
    public bool TryDispose ( ItemCarry item, ItemHolder holder )
    {
        //현재 보유 중인 아이템만 폐기
        if ( holder == null || holder.IsHolding( item ) == false )
            return false;

        //폐기 불가면 종료
        if ( CanDispose( item ) == false )
            return false;

        if ( item.TryGetComponent( out CookController cook ) == true )
        {
            if ( cook.TryDispose( out string reason ) == true )
                return true;

            Debug.LogWarning( $"[음식 폐기 실패] {reason}", this );
            return false;
        }

        Food food = item.GetComponent<Food>( );

        //단독 음식은 보유 해제 후 기존 소비 경로로 비활성화
        if ( holder.TryRelease( item ) == false )
            return false;

        food.Consume( );
        return true;
    }

    /// <summary>
    /// 폐기 가능 여부 확인
    /// </summary>
    /// <param name="item">보유 아이템</param>
    /// <returns>폐기 가능 여부</returns>
    public bool CanDispose ( ItemCarry item )
    {
        if ( isActiveAndEnabled == false ) return false;
        if ( item == null ||
             item.isActiveAndEnabled == false ||
             item.CurrentType != HoldType.View )
        {
            return false;
        }

        //그릇 내용물 폐기 가능 여부
        if ( item.TryGetComponent( out CookController cook ) == true )
            return cook.Session.IsActive && cook.HasDiscardable;

        //단독 음식 아이템만 폐기
        return item.TryGetComponent( out Food food ) &&
               food.Data != null;
    }

}
