using UnityEngine;

/// <summary>
/// 아이템 풀 반환 중재 - 재고 소속 확인과 제작, 보유 상태 정리
/// </summary>
[DisallowMultipleComponent]
[RequireComponent( typeof( ItemCarry ) )]
public class ItemPoolHandler : MonoBehaviour, IPoolReturnHandler
{
    ItemCarry _carry;		//아이템 보유와 상태 정리
    ProductIngredient _ingredient;		//선택적 제작 재료 기능
    ItemLoss _loss;		//선택적 손실 기록
    CookController _product;		//선택적 조리 그릇 기능


    /// <summary>
    /// 비활성 객체에서도 반환에 필요한 컴포넌트 확보
    /// </summary>
    void Init ()
    {
        if ( _carry != null )
            return;

        _carry = GetComponent<ItemCarry>( );
        _ingredient = GetComponent<ProductIngredient>( );
        _loss = GetComponent<ItemLoss>( );
        _product = GetComponent<CookController>( );
    }

    /// <summary>
    /// 공급받을 그릇의 제작 관리자 연결
    /// </summary>
    /// <param name="manager">이번 플레이의 제작 관리자</param>
    /// <param name="reason">등록 실패 사유</param>
    /// <returns>대여 준비 성공 여부</returns>
    public bool TryPreparePoolRent (
        ProductionManager manager, out string reason )
    {
        reason = string.Empty;
        Init( );

        //일반 아이템과 재료에는 제작 등록이 필요하지 않음
        if ( _product == null )
            return true;

        if ( manager == null )
        {
            reason = "공급 그릇의 제작 관리자가 연결되지 않았습니다.";
            return false;
        }

        if ( manager.Register( _product ) == false )
        {
            reason = "공급 그릇의 제작 등록에 실패했습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 재고 소속 해제 확인 후 제작과 아이템 상태 정리
    /// </summary>
    /// <param name="reason">반환 준비 실패 사유</param>
    /// <returns>반환 준비 성공 여부</returns>
    public bool TryPreparePoolReturn ( out string reason )
    {
        reason = string.Empty;
        Init( );

        //재고 중재자가 소속과 소비 구독을 해제한 뒤 반환
        if ( _carry.IsStockRegistered == true )
        {
            reason = "공급원 재고 등록이 남아 있습니다.";
            return false;
        }

        //소비 진행 중인 재료는 제작 처리가 끝날 때까지 반환하지 않음
        if ( _ingredient != null &&
             _ingredient.TryPreparePoolReturn( out reason ) == false )
            return false;

        //보유, 물리, 배치와 실행 중 데이터 정리
        if ( _carry.TryPreparePoolReturn( out reason ) == false )
            return false;

        //보유와 비활성화 정리 후 그릇의 완료 결과와 등록 해제
        if ( _product != null &&
             _product.TryPreparePoolReturn( out reason ) == false )
            return false;

        if ( _loss != null )
            _loss.ResetForReuse( );

        return true;
    }
}
