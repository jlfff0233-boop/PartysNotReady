using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 재료 소비 실행 - 준비, 확정과 부분 실패 정리
/// </summary>
public class IngredientConsumption
{
    ItemCarry _bowl;        //그릇 아이템
    Transform _root;        //재료 부모
    PlacementTarget _target;        //재료 배치 대상

    CookController _product;     //제작 대상
    /// <summary>
    /// 실제 소비분과 모델 손실 반영(소비량, 손실 여부)
    /// </summary>
    Action<int, bool> _removeIngredient;
    ProductIngredient _applying;		//소비 적용 대상

    int _appliedCount;		//소비 성공 수
    bool _hasStarted;		//소비 확정 진입 여부
    bool _ended;        //소비 실행 종료 여부


    /// <summary>
    /// 세션 등록 재료 딕셔너리(재료 ID, 재료)
    /// </summary>
    IReadOnlyDictionary<int, ProductIngredient> _ingredients;

    /// <summary>
    /// 소비 준비 재료 목록
    /// </summary>
    List<IngredientConsumptionEntry> _entries = new( );

    /// <summary>
    /// 제작 외 손실 재료 목록(재료 ID)
    /// </summary>
    HashSet<int> _externalLosses = new( );

    /// <summary>
    /// 소비 중단 요청 여부
    /// </summary>
    public bool StopRequested { get; private set; }


    /// <summary>
    /// 소비 대상 연결
    /// </summary>
    /// <param name="product">제작 대상</param>
    /// <param name="ingredients">현재 등록 목록</param>
    /// <param name="removeIngredient">재료 제거와 모델 손실 반영</param>
    /// <param name="bowl">그릇 아이템</param>
    /// <param name="root">재료 부모</param>
    /// <param name="target">재료 배치 대상</param>
    public IngredientConsumption (
        CookController product,
        IReadOnlyDictionary<int, ProductIngredient> ingredients,
        Action<int, bool> removeIngredient,
        ItemCarry bowl, Transform root, PlacementTarget target )
    {
        _removeIngredient = removeIngredient;
        _product = product;
        _ingredients = ingredients;
        _bowl = bowl;
        _root = root;
        _target = target;
    }


    #region ----- 준비 -----

    /// <summary>
    /// 소비 준비
    /// </summary>
    /// <param name="ids">소비 재료 ID 목록</param>
    /// <returns>모든 재료의 준비 성공 여부</returns>
    public bool TryPrepare ( List<int> ids )
    {
        if ( _ended || _hasStarted ||
            _entries.Count > 0 || CanContinue( ) == false )
            return false;

        foreach ( int id in ids )
        {
            if ( _ingredients.TryGetValue(
                id, out ProductIngredient ingredient ) == false ||
                 ingredient == null )
                return false;

            //소비 대상 후보 등록
            if ( TryPrepareIngredient( ingredient, out string reason ) == false )
            {
                Debug.LogWarning( $"[제작 재료 준비 실패] {id}\n{reason}", _product );
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 재료 소비 준비
    /// </summary>
    /// <param name="ingredient">소비 대상</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>준비 여부</returns>
    bool TryPrepareIngredient (
        ProductIngredient ingredient, out string reason )
    {
        reason = "소비 가능한 제작 실행이 없습니다.";
        if ( ingredient.CanConsume( _product ) == false )
            return false;

        reason = "현재 실행에 재료를 준비할 수 없습니다.";

        if ( _ended || _hasStarted || Contains( ingredient ) ||
             IsAvailable( ingredient ) == false ||
             _ingredients.TryGetValue(
                 ingredient.InstanceId, out ProductIngredient current ) == false ||
             ReferenceEquals( current, ingredient ) == false ||
             ingredient.Carry.CheckConsumeState( out reason ) == false )
            return false;

        _entries.Add( new IngredientConsumptionEntry(
            ingredient.InstanceId , ingredient , ingredient.Carry ) );

        return true;
    }


    #endregion

    #region ----- 소비 확정 -----

    /// <summary>
    /// 소비 확정
    /// </summary>
    /// <returns>소비 성공 여부</returns>
    public bool Commit ()
    {
        if ( _hasStarted || _ended )
            return false;

        _hasStarted = true;

        if ( CanContinue( ) == false )
            return false;

        //첫 월드 변경 전에 준비된 모든 재료를 다시 검사
        foreach ( IngredientConsumptionEntry entry in _entries )
        {
            if ( entry.Item == null ||
                entry.Item.CheckConsumeState( out _ ) == false )
                return false;
        }

        bool notified = false;

        try
        {
            foreach ( IngredientConsumptionEntry entry in _entries )
            {
                //앞선 소비 콜백의 중단/손실 확인
                if ( CanContinue( ) == false )
                    return false;

                bool applied;
                string reason;
                //적용 대상 한정
                _applying = entry.Ingredient;

                try
                {
                    applied = entry.Ingredient.ApplyConsume( _product, out reason );
                }
                finally
                {
                    _applying = null;
                }

                if ( applied == false )
                {
                    Debug.LogWarning(
                        $"[제작 재료 확정 실패] {entry.Id}\n{reason}", _product );
                    return false;
                }

                _appliedCount++;
                //제작 결과 판정 전까지 모델 재료 유지
                _removeIngredient( entry.Id, false );
            }
        }
        finally
        {
            //부분 실패에도 성공분 알림 유지
            notified = NotifySuccessful( );
        }
        //소비 알림 후 상태 재확인
        return notified && CanContinue( );
    }

    /// <summary>
    /// 소비 종료 정리
    /// </summary>
    /// <param name="modelApplied">소비 결과의 모델 반영 여부</param>
    /// <returns>상태 갱신 필요 여부</returns>
    public bool End ( bool modelApplied )
    {
        if ( _ended )
            return false;

        //등록 정리를 허용하도록 준비 상태 종료
        _ended = true;
        bool changed = _hasStarted && modelApplied == false;

        try
        {
            if ( modelApplied == false )
            {
                //실제 소비분만 모델에 반영
                for ( int i = 0; i < _appliedCount; i++ )
                    _removeIngredient( _entries [ i ].Id, true );
            }

            //소비 대상 외 손실도 반영
            if ( _hasStarted || _externalLosses.Count > 0 )
            {
                //순회 중 등록 목록 변경 방지
                List<int> lost = new( );

                foreach ( KeyValuePair<int, ProductIngredient> pair in _ingredients )
                {
                    if ( _externalLosses.Contains( pair.Key ) ||
                        IsAvailable( pair.Value ) == false )
                        lost.Add( pair.Key );
                }

                foreach ( int id in lost )
                {
                    _removeIngredient( id, true );
                    changed = true;
                }
            }
            return changed;
        }
        finally
        {
            //월드 복원 없이 준비 기록 해제
            _entries.Clear( );
            _externalLosses.Clear( );
        }
    }

    /// <summary>
    /// 소비 중단 요청
    /// </summary>
    public void RequestStop ()
    {
        StopRequested = true;
    }

    /// <summary>
    /// 외부 손실 기록
    /// </summary>
    /// <param name="instanceId">소실된 재료 ID</param>
    /// <returns>손실 접수 여부</returns>
    public bool RecordExternalLoss ( int instanceId )
    {
        //종료 후 손실은 세션에서 처리
        if ( _ended )
            return false;

        _externalLosses.Add( instanceId );

        return true;
    }

    /// <summary>
    /// 성공 재료 소비 알림
    /// </summary>
    /// <returns>알림 대상 유지 여부</returns>
    bool NotifySuccessful ()
    {
        bool notified = true;

        for ( int i = 0; i < _appliedCount; i++ )
        {
            IngredientConsumptionEntry entry = _entries [ i ];

            if ( entry.Item != null )
                entry.Item.NotifyConsumeConfirmed(
                    IngredientConsumeReason.Production );
            else
                notified = false;
        }
        return notified;
    }


    #endregion

    #region ----- 검사 -----

    /// <summary>
    /// 소비 준비 목록 포함 검사
    /// </summary>
    /// <param name="ingredient">확인할 재료</param>
    /// <returns>종료되지 않은 준비 여부</returns>
    public bool Contains ( ProductIngredient ingredient )
    {
        return _ended == false &&
            _entries.Exists( entry => ReferenceEquals( entry.Ingredient, ingredient ) );
    }

    /// <summary>
    /// 재료 배치 연결 검사
    /// </summary>
    /// <param name="ingredient">현재 등록된 재료</param>
    /// <returns>유효한 배치 연결 여부</returns>
    bool IsAvailable ( ProductIngredient ingredient )
    {
        return ingredient != null &&
            ingredient.isActiveAndEnabled &&
            ingredient.Carry != null &&
            ingredient.Carry.isActiveAndEnabled &&
            _root != null &&
            ingredient.transform.IsChildOf( _root );
    }

    /// <summary>
    /// 소비 적용 대상 검사
    /// </summary>
    /// <param name="ingredient">적용할 재료</param>
    /// <returns>소비 적용 대상 여부</returns>
    public bool IsApplying ( ProductIngredient ingredient )
    {
        return _ended == false && ingredient != null &&
            ReferenceEquals( _applying, ingredient );
    }

    /// <summary>
    /// 소비 진행 가능 여부
    /// </summary>
    /// <returns>소비 진행 가능 여부</returns>
    bool CanContinue ()
    {
        if ( _ended || StopRequested ||
            _externalLosses.Count > 0 ||
             _product == null ||
             _product.isActiveAndEnabled == false ||
             _bowl == null ||
             _bowl.isActiveAndEnabled == false ||
             _target == null )
            return false;

        foreach ( ProductIngredient ingredient in _ingredients.Values )
        {
            if ( IsAvailable( ingredient ) == false )
                return false;
        }
        return true;
    }

    #endregion
}
