using UnityEngine;

/// <summary>
/// 음식 중재 - 데이터 제공과 소비 처리
/// </summary>
public class Food : MonoBehaviour
{
    FoodData _data;     //이전 완료 전까지 보존할 기존 음식 참조

    ItemCarry _carry;     //공통 아이템 데이터 조회
    ItemWorldController _view;      //아이템 뷰

    /// <summary>
    /// 음식 데이터
    /// </summary>
    public FoodData Data => GetFoodData( );


    private void Awake ()
    {
        _carry = GetComponent<ItemCarry>( );
        _view = GetComponent<ItemWorldController>( );
    }

    /// <summary>
    /// 공통 아이템 데이터에서 음식 설정 조회
    /// </summary>
    /// <returns>음식 설정 데이터</returns>
    FoodData GetFoodData ()
    {
        //다른 컴포넌트의 Awake에서 조회해도 연결 가능
        if ( _carry == null )
            _carry = GetComponent<ItemCarry>( );

        if ( _carry != null && _carry.ItemData != null )
            return _carry.ItemData as FoodData;

        //기존 참조 이전 전에도 음식 기능은 유지
        return _data;
    }

    /// <summary>
    /// 음식 소비
    /// </summary>
    /// <returns>실제 소비 완료 여부</returns>
    public bool Consume ()
    {
        if ( isActiveAndEnabled == false || _view == null )
            return false;

        if ( _carry == null || _carry.IsStockRegistered == false )
        {
            //일반 음식과 미등록 재료는 기존 숨김 처리
            _view.Hide( );
            return gameObject.activeSelf == false;
        }

        //등록 재료는 비활성화 성공 후에만 소비 확정
        if ( _carry.TryChangeConsumeState( out string reason ) == false )
        {
            Debug.LogWarning( $"[재료 소비 실패] {reason}", this );
            return false;
        }

        _carry.NotifyConsumeConfirmed( IngredientConsumeReason.ExternalLoss );
        return true;
    }
}
