using System;
using System.Collections.Generic;

/// <summary>
/// 변칙 사건 디버그 기록 - 세션별 종료 결과와 요청 실패 보관
/// </summary>
public class AnomalyDebugHistory
{
    const int RecentCapacity = 20;     //종료 기록 보관 한도
    readonly List<AnomalyDebugRecord> _recentResults = new( RecentCapacity );
    readonly List<string> _failures = new( );     //최근 요청의 실패 설명
    Guid _debugSession;     //기록이 속한 플레이 세션
    int _failureCount;     //표시 한도를 포함한 실패 횟수

    /// <summary>
    /// 최근 요청의 전체 실패 횟수
    /// </summary>
    public int FailureCount => _failureCount;

    /// <summary>
    /// 보관한 실패 설명 수
    /// </summary>
    public int FailureDetailCount => _failures.Count;

    /// <summary>
    /// 현재 세션의 종료 기록 수
    /// </summary>
    public int RecentResultCount
    {
        get
        {
            EnsureDebugSession( );
            return _recentResults.Count;
        }
    }


    /// <summary>
    /// 플레이 세션 변경 시 이전 기록 제거
    /// </summary>
    void EnsureDebugSession ()
    {
        if ( _debugSession == AnomalyDebugSession.Id )
            return;

        _debugSession = AnomalyDebugSession.Id;
        _recentResults.Clear( );
        _failures.Clear( );
        _failureCount = 0;
    }

    /// <summary>
    /// 최근 요청에서 실제 검사 함수가 반환한 실패 설명 조회
    /// </summary>
    /// <param name="index">보관한 설명 번호</param>
    public string GetDebugFailure ( int index )
    {
        return _failures[index];
    }

    /// <summary>
    /// 최근 종료 결과 읽기
    /// </summary>
    /// <param name="index">조회할 종료 기록 번호, 오래된 기록부터 0</param>
    public AnomalyDebugRecord GetRecentResult ( int index )
    {
        EnsureDebugSession( );
        return _recentResults [ index ];
    }

    /// <summary>
    /// 최근 종료 기록만 지우기
    /// </summary>
    public void ClearRecentResults ()
    {
        EnsureDebugSession( );
        _recentResults.Clear( );
    }

    /// <summary>
    /// 새 요청에 사용할 실패 목록 초기화
    /// </summary>
    public void BeginRequest ()
    {
        EnsureDebugSession( );
        _failures.Clear( );
        _failureCount = 0;
    }


    /// <summary>
    /// 사건 정리 전에 종료 결과를 값으로 보관
    /// </summary>
    /// <param name="debugName">사건 표시 이름</param>
    /// <param name="occurrence">종료 상태를 기록할 발생 건</param>
    public void RecordStatus ( string debugName, IAnomalyDebugOccurrence occurrence )
    {
        //실패 상태는 사후 처리가 남아 있으므로 최종 종료 기록에서 제외
        if ( occurrence.Status != AnomalyStatus.Resolved &&
             occurrence.Status != AnomalyStatus.Recovered &&
             occurrence.Status != AnomalyStatus.Expired &&
             occurrence.Status != AnomalyStatus.Cancelled )
            return;

        EnsureDebugSession( );
        //보관 한도에 도달하면 가장 오래된 기록부터 제거
        if ( _recentResults.Count == RecentCapacity )
            _recentResults.RemoveAt( 0 );

        _recentResults.Add( new AnomalyDebugRecord( debugName, occurrence ) );
    }

    /// <summary>
    /// 발생 검사에서 반환한 실패 저장
    /// </summary>
    /// <param name="target">실패한 사건 대상</param>
    /// <param name="failure">실패 코드와 설명</param>
    public void RecordFailure ( AnomalyTarget target, AnomalyStartFailure failure )
    {
        _failureCount++;
        //대상 삭제 이후에도 조회할 수 있게 문자열로 보관하고 표시량 제한
        if ( _failures.Count < 50 )
        {
            string name = target != null ? target.name : "대상 미지정";
            _failures.Add( $"{name} [{failure.Category}/{failure.Code}] {failure.Message}" );
        }
    }

}
