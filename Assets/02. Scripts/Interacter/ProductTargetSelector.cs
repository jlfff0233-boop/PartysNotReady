using UnityEngine;

/// <summary>
/// 제작 대상 조회 - 재료, 배치 표면, 그릇 확인
/// </summary>
public static class ProductTargetSelector
{
    /// <summary>
    /// 충돌한 대상의 제작 대상 조회
    /// </summary>
    /// <param name="hit">충돌 정보</param>
    /// <param name="product">제작 대상</param>
    /// <returns>대상 조회 여부</returns>
    public static bool TryGetProduct (
        RaycastHit hit , out CookController product )
    {
        product = null;

        if ( hit.collider == null )
            return false;

        //등록 재료의 소속 그릇 확인
        ProductIngredient ingredient =
            hit.collider.GetComponentInParent<ProductIngredient> ( );

        if ( ingredient != null && ingredient.Product != null )
        {
            product = ingredient.Product;
            return true;
        }

        //재료 배치 표면의 그릇 확인
        PlacementTarget target =
            hit.collider.GetComponentInParent<PlacementTarget> ( );

        if ( target != null )
        {
            CookController owner =
                target.GetComponentInParent<CookController> ( );

            if ( owner != null && owner.IsIngredientTarget ( target ) == true )
            {
                product = owner;
                return true;
            }
        }

        //그릇 몸체 확인
        CookController bowl =
            hit.collider.GetComponentInParent<CookController> ( );

        if ( bowl == null || bowl.IsBowlCollider ( hit.collider ) == false )
            return false;

        product = bowl;
        return true;
    }
}
