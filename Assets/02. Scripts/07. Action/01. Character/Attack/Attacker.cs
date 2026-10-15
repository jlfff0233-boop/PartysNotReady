using UnityEngine;

/// <summary>
/// 공격 공통 - 범위 탐지와 공격 진행
/// </summary>
public abstract class Attacker : MonoBehaviour
{
    [Header ( "----- 공격 지점 -----" )]
    [SerializeField] Transform [ ] _atkPoints;      //공격 지점 배열

    [Header ( "----- 탐지 설정 -----" )]
    [SerializeField, Min ( 0.01f )] float _attackRadius = 0.3f;
    [SerializeField] LayerMask _targetLayerMask;   //공격 대상 레이어

    bool _isAttacking;                       //공격 판정 진행 여부

#if UNITY_EDITOR
    int _debugDetectedCount;		//최근 명중 검사에서 감지한 콜라이더 수
#endif


    protected virtual void OnDisable ( )
    {
        EndAttack ( );
    }


    /// <summary>
    /// 이번 공격의 명중 기록 초기화
    /// </summary>
    protected abstract void ClearHits ( );

    /// <summary>
    /// 대상별 명중 확인과 전달
    /// </summary>
    /// <param name="target">범위에 들어온 콜라이더</param>
    protected abstract void CheckTarget ( Collider target );

    /// <summary>
    /// 공격 시작
    /// </summary>
    public void BeginAttack ()
    {
        //새 공격에서는 이전에 맞은 대상도 다시 명중할 수 있도록 기록 초기화
        ClearHits( );
#if UNITY_EDITOR
        _debugDetectedCount = 0;
#endif
        _isAttacking = true;
    }

    /// <summary>
    /// 범위 내 대상 탐지와 명중 처리
    /// </summary>
    /// <param name="atkPoint">공격 지점</param>
    void CheckAttackRange ( Transform atkPoint )
    {
        //트리거 영역은 제외하고 실제 충돌체를 대상으로 탐지
        Collider [ ] hits = Physics.OverlapSphere(
            atkPoint.position, _attackRadius, _targetLayerMask,
            QueryTriggerInteraction.Ignore );

#if UNITY_EDITOR
        _debugDetectedCount += hits.Length;
#endif

        for ( int i = 0; i < hits.Length; i++ )
        {
            //이벤트 처리 중 공격이 종료됐다면 중단
            if ( _isAttacking == false )
                return;

            CheckTarget( hits [ i ] );
        }
    }


    /// <summary>
    /// 전체 공격 지점의 명중 확인
    /// </summary>
    public void CheckHit ()
    {
#if UNITY_EDITOR
        _debugDetectedCount = 0;
#endif
        for ( int i = 0; i < _atkPoints.Length; i++ )
        {
            //이전 공격 지점의 명중 처리에서 종료된 경우도 확인
            if ( _isAttacking == false )
                return;

            CheckAttackRange( _atkPoints [ i ] );
        }
    }

    /// <summary>
    /// 공격 종료
    /// </summary>
    public void EndAttack ( )
    {
        //명중 알림 중 호출돼도
        //남은 대상 처리가 중단되도록 먼저 판정 종료
        _isAttacking = false;
        ClearHits ( );
    }

    /// <summary>
    /// 실제 명중 검사에서 사용하는 충돌체 조건 확인
    /// </summary>
    /// <param name="target">검사할 충돌체</param>
    /// <returns>공격 레이어의 활성 일반 충돌체 여부</returns>
    public bool IsTargetCollider ( Collider target )
    {
        return target != null && target.enabled == true &&
            target.gameObject.activeInHierarchy == true &&
            target.isTrigger == false &&
            ( _targetLayerMask.value & ( 1 << target.gameObject.layer ) ) != 0;
    }


#if UNITY_EDITOR
    /// <summary>
    /// 공격 지점과 최근 범위 검사 상태 조회
    /// </summary>
    /// <returns>공격 반경, 레이어와 지점별 월드 위치</returns>
    public string GetDebugDetails ()
    {
        string details = $"공격 반경: {_attackRadius:F2} / 레이어: {_targetLayerMask.value} / " +
            $"최근 감지 콜라이더: {_debugDetectedCount}";
        for ( int i = 0; i < _atkPoints.Length; i++ )
            details += $" / 공격 지점 {i}: {(_atkPoints [ i ] != null ? _atkPoints [ i ].position.ToString( ) : "없음")}";
        return details;
    }

    /// <summary>
    /// 각 공격 지점에서 대상 충돌체까지의 거리 조회
    /// </summary>
    /// <param name="target">목표 충돌체</param>
    /// <returns>공격 지점별 충돌체 표면까지 거리</returns>
    public string GetTargetDebugDetails ( Collider target )
    {
        string details = string.Empty;
        for ( int i = 0; i < _atkPoints.Length; i++ )
        {
            if ( _atkPoints [ i ] == null )
                continue;
            Vector3 position = _atkPoints [ i ].position;
            float distance = Vector3.Distance( position, target.ClosestPoint( position ) );
            details += $" / 공격 지점 {i}에서 표면까지: {distance:F3}";
        }
        return details;
    }
#endif

    private void OnDrawGizmosSelected ()
    {
        if ( _atkPoints == null ) return;

        Gizmos.color = Color.yellow;

        //각 공격 지점의 실제 탐지 반경 표시
        for ( int i = 0; i < _atkPoints.Length; i++ )
        {
            if ( _atkPoints [ i ] == null ) continue;

            Gizmos.DrawWireSphere( _atkPoints [ i ].position, _attackRadius );
        }
    }
}
