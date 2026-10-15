using UnityEngine;

/// <summary>
/// 공 놀이 모델 - 진행과 완료 횟수 관리
/// </summary>
public class FetchPlayModel
{
    //일반 회차는 회수 후 전달, 마지막 회차는 회수 시 완료
    int _reqCount;		//퇴장에 필요한 공 놀이 횟수
    int _currentCount;		//완료한 공 놀이 횟수
    bool _isRoundStarted;		//놀이 회차 진행 여부
    bool _isRecovered;		//공 회수 여부
    bool _hasLeftRange;		//공 회수 범위 이탈 여부

    /// <summary>
    /// 목표 완료 횟수
    /// </summary>
    public int ReqCount => _reqCount;
    /// <summary>
    /// 놀이 완료 횟수
    /// </summary>
    public int CurrentCount => _currentCount;
    /// <summary>
    /// 목표 횟수 충족 여부
    /// </summary>
    public bool IsSatisfied => _currentCount >= _reqCount;


    /// <summary>
    /// 놀이 목표 횟수 설정
    /// </summary>
    /// <param name="reqCount">퇴장에 필요한 공 놀이 횟수</param>
    public FetchPlayModel ( int reqCount = 3 )
    {
        _reqCount = reqCount;
    }

    /// <summary>
    /// 새 등장 놀이 횟수와 회차 초기화
    /// </summary>
    public void Reset ()
    {
        _currentCount = 0;
        CancelRound( );
    }


    /// <summary>
    /// 놀이 회차 시작
    /// </summary>
    public void BeginRound ()
    {
        _isRoundStarted = true;
        _isRecovered = false;
        _hasLeftRange = false;
    }

    /// <summary>
    /// 공 회수 범위 이탈 기록
    /// </summary>
    /// <param name="ballDist">보유 위치와 공 사이 거리</param>
    /// <param name="fetchDist">회수 거리</param>
    public void CheckRecoveryRange ( float ballDist, float fetchDist )
    {
        //가까이서 바로 다시 집은 공을 제외하도록 범위 이탈 기록
        if ( _isRoundStarted == true && ballDist > fetchDist )
            _hasLeftRange = true;
    }

    /// <summary>
    /// 공 회수 완료 기록
    /// </summary>
    public void MarkRecovered ()
    {
        if ( _isRoundStarted == false || _hasLeftRange == false )
            return;

        _isRecovered = true;
    }

    /// <summary>
    /// 놀이 회차 취소
    /// </summary>
    public void CancelRound ()
    {
        _isRoundStarted = false;
        _isRecovered = false;
        _hasLeftRange = false;
    }


    /// <summary>
    /// 회수한 공의 놀이 회차 완료
    /// </summary>
    /// <returns>공 놀이 완료 반영 여부</returns>
    public bool TryCompleteRound ()
    {
        if ( _isRoundStarted == false || _isRecovered == false )
            return false;

        _currentCount++;

        //완료한 회차의 중복 집계 방지
        CancelRound( );

        return true;
    }

    /// <summary>
    /// 마지막 회수 시 놀이 완료
    /// </summary>
    /// <returns>놀이 완료 여부</returns>
    public bool TryCompleteFinalRound ()
    {
        //마지막 회차만 전달 없이 회수 시점에 완료 처리
        if ( _currentCount != _reqCount - 1 )
            return false;

        return TryCompleteRound( );
    }


}
