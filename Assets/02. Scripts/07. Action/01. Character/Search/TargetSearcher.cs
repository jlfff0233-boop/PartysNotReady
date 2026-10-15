using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 목표 탐색 모듈 - 범위 내 캐릭터 조회와 가까운 유효 대상 선정
/// </summary>
public class TargetSearcher : MonoBehaviour
{
    [SerializeField] LayerMask _targetLayerMask;        //탐색 대상 레이어

    /// <summary>
    /// 중복 후보 제외
    /// </summary>
    HashSet<IAttackTarget> _checkedTargets = new( );

    /// <summary>
    /// 탐지한 대상 목록
    /// </summary>
    List<IAttackTarget> _targets = new( );

    /// <summary>
    /// 현재 탐색 기준 위치
    /// </summary>
    public Vector3 Position => transform.position;


    /// <summary>
    /// 가까운 유효 목표 탐색
    /// </summary>
    /// <param name="searchDist">최대 탐색 거리</param>
    /// <param name="target">선정한 목표</param>
    /// <param name="condition">추가 후보 조건, 없으면 기본 조건만 확인</param>
    /// <returns>목표 선정 여부</returns>
    public bool TryFindTarget (
        float searchDist, out IAttackTarget target,
        Predicate<IAttackTarget> condition = null )
    {
        target = null;
        float nearestDist = float.MaxValue;

        FindTargets( searchDist, _targets );

        for ( int i = 0; i < _targets.Count; i++ )
        {
            //후보와 대상 위치 가져오기
            IAttackTarget candidate = _targets [ i ];
            Transform targetTransform = candidate.TargetTransform;

            bool isAvailable =
                targetTransform != null &&
                candidate.CanTakeHit == true;

            //대상 위치와 피격 가능 여부를 확인한 뒤 추가 조건 검사
            if ( isAvailable == true && condition != null )
                isAvailable = condition( candidate );

            //콜라이더 탐지 거리와 별개로 대상 Transform까지의 직선 거리 계산
            float dist = targetTransform == null
                ? float.MaxValue
                : Vector3.Distance( Position, targetTransform.position );

            //선정 불가, 거리 제한 초과, 현재 후보보다 멀거나 같은 거리면 제외
            if ( isAvailable == false ||
                 dist > searchDist || dist >= nearestDist )
                continue;

            //더 가까운 후보만 선택
            nearestDist = dist;
            target = candidate;
        }

        return target != null;
    }

    /// <summary>
    /// 범위 내 캐릭터 후보 조회
    /// </summary>
    /// <param name="searchDist">구형 탐지 반경</param>
    /// <param name="targets">초기화 후 후보를 채울 목록</param>
    public void FindTargets (
        float searchDist, List<IAttackTarget> targets )
    {
        //목록 초기화
        targets.Clear( );
        _checkedTargets.Clear( );

        //트리거를 제외하고 구형 범위에 겹치는 콜라이더 조회
        Collider [ ] hits = Physics.OverlapSphere(
            Position, searchDist, _targetLayerMask,
            QueryTriggerInteraction.Ignore );

        for ( int i = 0; i < hits.Length; i++ )
        {
            IAttackTarget target =
                hits [ i ].GetComponentInParent<IAttackTarget>( );

            //한 대상의 여러 콜라이더가 탐지돼도 후보는 한 번만 등록
            if ( target == null || _checkedTargets.Add( target ) == false )
                continue;

            targets.Add( target );
        }
    }

}
