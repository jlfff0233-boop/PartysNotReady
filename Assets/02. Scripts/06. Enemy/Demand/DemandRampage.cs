using UnityEngine;

/// <summary>
/// 아이템 요구 적 난동 모듈 - 재료 소실과 아이템 밀어내기
/// </summary>
public class DemandRampage
{
    Transform _origin;     //공격 방향 기준
    RampageLossCounter _model;     //공격당 소실 수량

    /// <summary>
    /// 난동 피해 처리 초기화
    /// </summary>
    /// <param name="origin">공격 방향 기준</param>
    /// <param name="lossLimit">공격당 재료 소실 상한</param>
    public DemandRampage ( Transform origin, int lossLimit )
    {
        _origin = origin;
        _model = new RampageLossCounter( lossLimit );
    }

    /// <summary>
    /// 이번 공격의 소실 수량 초기화
    /// </summary>
    public void BeginAttack ()
    {
        _model.BeginAttack( );
    }

    /// <summary>
    /// 피격 아이템 소실 또는 밀어내기
    /// </summary>
    /// <param name="item">공격할 아이템</param>
    /// <param name="pushPower">수평 밀치기 힘</param>
    /// <param name="pushUpPower">위쪽 밀치기 힘</param>
    public void ApplyHit ( ItemCarry item, float pushPower, float pushUpPower )
    {
        //실제 피해 적용 직전 상태 재확인
        if ( item == null || item.isActiveAndEnabled == false || item.IsHold == true )
            return;

        //활성 재료이고 소실 상한이 남았다면 실제 소비를 먼저 확인
        if ( item.TryGetComponent<Food>( out Food food ) &&
             food.isActiveAndEnabled == true &&
             food.Data != null &&
             _model.CanLoseIngredient( food.Data.Category ) == true &&
             food.Consume( ) == true )
        {
            _model.RecordIngredientLoss( );
            return;
        }

        //소실하지 않은 아이템은 밀어내기
        Vector3 dir = item.transform.position - _origin.position;
        dir.y = 0f;

        if ( dir.sqrMagnitude <= 0.0001f )
            dir = _origin.forward;

        dir.y = 0f;
        dir.Normalize( );

        Vector3 impulse = dir * pushPower + Vector3.up * pushUpPower;

        item.TryPush( impulse );
    }
}
