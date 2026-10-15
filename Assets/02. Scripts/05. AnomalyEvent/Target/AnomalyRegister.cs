using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 변칙 대상 등록 목록 - 활성 대상 등록과 해제
/// </summary>
public static class AnomalyRegister
{
    /// <summary>
    /// 활성화된 대상
    /// </summary>
    static readonly HashSet<AnomalyTarget> _targets = new( ); 

    /// <summary>
    /// 등록된 대상 목록
    /// </summary>
    public static IReadOnlyCollection<AnomalyTarget> Targets => _targets;

    [RuntimeInitializeOnLoadMethod( 
        RuntimeInitializeLoadType.SubsystemRegistration )]
    private static void Reset ()
    {
        //도메인 재로드를 꺼도
        //이전 플레이의 대상 참조가 남지 않도록 초기화
        _targets.Clear( );
    }

    /// <summary>
    /// 활성화된 대상 등록
    /// </summary>
    /// <param name="target">등록할 대상</param>
    public static void Register ( AnomalyTarget target )
    {
        //이미 등록된 대상은 중복으로 추가하지 않음
        _targets.Add( target );
    }

    /// <summary>
    /// 비활성화된 대상 등록 해제
    /// </summary>
    /// <param name="target">해제할 대상</param>
    public static void Unregister ( AnomalyTarget target )
    {
        _targets.Remove( target );
    }
}
