using System.Collections.Generic;

/// <summary>
/// 결과 집계 모델 - 안전과 확정 대응 기록
/// </summary>
public class ResultEvaluationModel
{
    int _successCount;		//확정 성공 수
    int _failureCount;		//확정 실패 수
    int _cancelledCount;		//확정 전 취소 수
    int _endedActiveCount;		//진행 중 종료 수

    /// <summary>
    /// 등록된 대응 건
    /// </summary>
    HashSet<object> _known = new( );

    /// <summary>
    /// 진행 중인 대응 건
    /// </summary>
    HashSet<object> _active = new( );


    /// <summary>
    /// 확정 성공 수
    /// </summary>
    public int SuccessCount => _successCount;

    /// <summary>
    /// 확정 실패 수
    /// </summary>
    public int FailureCount => _failureCount;

    /// <summary>
    /// 취소 수
    /// </summary>
    public int CancelledCount => _cancelledCount;

    /// <summary>
    /// 실제 등록된 대응 건수
    /// </summary>
    public int StartedCount => _known.Count;

    /// <summary>
    /// 진행 중 종료 수
    /// </summary>
    public int EndedActiveCount => _endedActiveCount;


    /// <summary>
    /// 실제 대응 시작 등록
    /// </summary>
    /// <param name="key">발생 건 키</param>
    /// <returns>새로 등록했는지 여부</returns>
    public bool TryBegin ( object key )
    {
        if ( key == null || _known.Add( key ) == false )
            return false;

        _active.Add( key );
        return true;
    }

    /// <summary>
    /// 대응 결과 확정
    /// </summary>
    /// <param name="key">발생 건 키</param>
    /// <param name="succeeded">성공 여부</param>
    /// <returns>이번에 확정했는지 여부</returns>
    public bool TryComplete ( object key, bool succeeded )
    {
        if ( key == null || _active.Remove( key ) == false )
            return false;

        if ( succeeded )
            _successCount++;
        else
            _failureCount++;

        return true;
    }

    /// <summary>
    /// 미확정 대응 취소
    /// </summary>
    /// <param name="key">발생 건 키</param>
    /// <returns>이번에 취소했는지 여부</returns>
    public bool TryCancel ( object key )
    {
        if ( key == null || _active.Remove( key ) == false )
            return false;

        _cancelledCount++;
        return true;
    }

    /// <summary>
    /// 진행 중인 대응을 종료 건으로 보관
    /// </summary>
    public void Finish ()
    {
        _endedActiveCount += _active.Count;
        _active.Clear( );
    }

    /// <summary>
    /// NPC 잔존 비율 계산
    /// </summary>
    /// <param name="initialCount">시작 NPC 수</param>
    /// <param name="remainingCount">종료 NPC 수</param>
    /// <param name="score">0~100 안전 점수</param>
    /// <returns>계산 가능 여부</returns>
    public bool TryGetSafetyScore (
        int initialCount, int remainingCount, out float score )
    {
        score = 0f;

        if ( initialCount <= 0 ||
             remainingCount < 0 ||
             remainingCount > initialCount )
            return false;

        score = ( float ) remainingCount / initialCount * 100f;
        return true;
    }

    /// <summary>
    /// 확정 대응 비율 계산
    /// </summary>
    /// <param name="score">0~100 대응 점수</param>
    /// <returns>확정 결과 존재 여부</returns>
    public bool TryGetPredatorScore ( out float score )
    {
        score = 0f;

        int confirmed = _successCount + _failureCount;

        if ( confirmed == 0 )
            return false;

        score = ( float ) _successCount / confirmed * 100f;
        return true;
    }
}