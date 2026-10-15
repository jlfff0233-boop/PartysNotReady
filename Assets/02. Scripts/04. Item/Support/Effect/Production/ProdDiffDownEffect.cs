
/// <summary>
/// 제작 난이도 완화 효과 - 제작 미니게임 난이도 완화
/// </summary>
public class ProdDiffDownEffect : ISupportItemEffect
{
    bool _reserved;     //다음 예약
    bool _ended;        //게임 종료

    ProdDiffDownData _data;		//효과 설정
    ProductionSession _session;     //적용한 세션


    /// <summary>
    /// 예약 여부
    /// </summary>
    public bool IsReserved => _reserved;

    /// <summary>
    /// 적용 여부
    /// </summary>
    public bool IsActive => _session != null;

    /// <summary>
    /// 효과 설정 연결
    /// </summary>
    /// <param name="data">효과 설정</param>
    public ProdDiffDownEffect ( ProdDiffDownData data )
    {
        _data = data;
    }

    /// <summary>
    /// 세션 구독 해제
    /// </summary>
    void ClearSession ()
    {
        if ( _session == null )
            return;

        _session.OnProductionEnded -= ClearSession;
        _session = null;
    }

    /// <summary>
    /// 사용 예약
    /// </summary>
    /// <returns>실행 결과</returns>
    public SupportItemUseResult Use ()
    {
        //사용 가능 여부 확인
        if ( _ended == true || _reserved == true ||
             _session != null || _data.IsValid == false )
            return SupportItemUseResult.Rejected;

        //사용 예약
        _reserved = true;

        //사용 결과 반환
        return SupportItemUseResult.Reserved;
    }

    /// <summary>
    /// 예약한 제작 시작
    /// </summary>
    /// <param name="session">제작 세션</param>
    /// <returns>시작 여부</returns>
    public bool TryBegin ( ProductionSession session )
    {
        //사용 가능 여부 확인 후 제작 미니게임 시작
        if ( _ended == true || _reserved == false || _data.IsValid == false )
            return false;

        //현재 제작 유형에 맞는 배율 선택
        float multiplier = session.Minigame.MinigameType == ProduceMinigameType.Timing
            ? _data.TimingMultiplier
            : _data.TargetLifetimeMultiplier;

        if ( session.TryBegin( multiplier, out _ ) == false )
            return false;

        //예약 해제
        _reserved = false;

        //시작 알림 중 종료된 세션은 구독하지 않음
        if ( _ended == false && session.IsProductionActive == true )
        {
            _session = session;
            _session.OnProductionEnded += ClearSession;
        }

        return true;
    }

    /// <summary>
    /// 예약과 구독 종료
    /// </summary>
    public void End ()
    {
        _ended = true;
        _reserved = false;
        ClearSession( );
    }
}
