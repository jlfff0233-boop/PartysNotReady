using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 월드 상호작용 대상 선택 - 음식 전달, 일반 상호작용과 조사 기능 확인
/// </summary>
public class WorldTargetSelector
{
    List<MonoBehaviour> _investigationCandidates = new ( );     //조사 기능 탐색 목록


    /// <summary>
    /// 음식 수락 대상 확인
    /// </summary>
    /// <param name="hit">탐지한 충돌 정보</param>
    /// <param name="receiver">음식 수락 대상</param>
    /// <returns>대상 확인 여부</returns>
    public static bool TryGetReceiver ( RaycastHit hit , out IFoodReceiver receiver )
    {
        receiver = null;

        if ( hit.collider == null )
            return false;

        receiver = hit.collider.GetComponentInParent<IFoodReceiver> ( );

        return receiver != null;
    }

    /// <summary>
    /// 현재 조준한 일반 상호작용 대상 확인
    /// </summary>
    /// <param name="hit">탐지한 충돌 정보</param>
    /// <param name="target">상호작용 대상</param>
    /// <returns>상호작용 가능한 대상 확인 여부</returns>
    public static bool TryGetWorldTarget ( RaycastHit hit , out IInteractable target )
    {
        target = null;

        if ( hit.collider == null )
            return false;

        target = hit.collider.GetComponentInParent<IInteractable> ( );

        //컴포넌트 활성 상태와 동작 가능 여부는 대상에서 판단
        return target != null && target.CanInteract;
    }

    /// <summary>
    /// 조준한 대상에서 사용 가능한 조사 기능 선택
    /// </summary>
    /// <param name="hit">탐지한 충돌 정보</param>
    /// <param name="target">선택한 조사 기능</param>
    /// <returns>조사 가능한 기능 확인 여부</returns>
    public bool TrySelectInvestigation ( RaycastHit hit , out IInvestigatable target )
    {
        target = null;
        if ( hit.collider == null )
            return false;

        //충돌한 오브젝트부터 부모로 올라가며 컴포넌트 순서로 선택
        for ( Transform current = hit.collider.transform ;
              current != null ; current = current.parent )
        {
            current.GetComponents ( _investigationCandidates );
            for ( int i = 0 ; i < _investigationCandidates.Count ; i++ )
            {
                MonoBehaviour component = _investigationCandidates [ i ];
                if ( component == null || component.isActiveAndEnabled == false ||
                     component is not IInvestigatable candidate )
                    continue;

                if ( candidate.CanInvestigate == false )
                    continue;

                target = candidate;
                _investigationCandidates.Clear ( );
                return true;
            }
        }

        _investigationCandidates.Clear ( );
        return false;
    }


    /// <summary>
    /// 시작한 조사 기능의 조준 상태와 완료 조건 확인
    /// </summary>
    /// <param name="hit">현재 탐지한 충돌 정보</param>
    /// <param name="target">홀딩 시작 시 선택한 조사 기능</param>
    /// <param name="investigationId">홀딩 시작 시 저장한 조사 ID</param>
    /// <returns>같은 조사를 완료할 수 있는지 여부</returns>
    public static bool CanCompleteInvestigation (
        RaycastHit hit , IInvestigatable target , int investigationId )
    {
        //인터페이스 참조가 남아 있어도 파괴되거나 비활성화된 컴포넌트는 제외
        MonoBehaviour component = target as MonoBehaviour;
        if ( component == null || component.isActiveAndEnabled == false )
            return false;

        if ( hit.collider == null )
            return false;

        //후보를 다시 선택하지 않고 시작 기능에 속한 충돌체인지 확인
        if ( hit.collider.transform.IsChildOf ( component.transform ) == false )
            return false;

        //같은 기능이라도 홀딩 중 다른 조사로 바뀌었다면 완료하지 않음
        return target.CanInvestigate == true &&
               target.InvestigationId == investigationId;
    }

    /// <summary>
    /// 현재 조준한 제작 미니게임 대상 확인
    /// </summary>
    /// <param name="hit">탐지한 충돌 정보</param>
    /// <param name="target">제작 미니게임 대상</param>
    /// <returns>제작 미니게임 가능한 대상 확인 여부</returns>
    public static bool TryGetProduceMinigameTarget (
        RaycastHit hit , out ProduceMinigameInteractable target )
    {
        target = null;

        if ( hit.collider == null )
            return false;

        target =
            hit.collider.GetComponentInParent<ProduceMinigameInteractable> ( );

        return target != null && target.CanPlayMinigame;
    }
}
