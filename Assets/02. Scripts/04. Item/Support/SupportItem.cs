using System;

/// <summary>
/// 서포트 아이템 - 횟수와 효과 연결
/// </summary>
public class SupportItem
{
    bool _started;		//효과 시작 완료
    bool _busy;     //사용 처리 중
    bool _ended;        //게임 종료

    SupportItemData _data;      //아이템 설정
    ISupportItemEffect _effect;     //효과 모듈
    SupportItemUseState _useState;      //액티브 사용 상태

    /// <summary>
    /// 시간 진행
    /// </summary>
    Action<float> _tick;
    /// <summary>
    /// 효과 정리
    /// </summary>
    Action _end;
    /// <summary>
    /// 남은 시간 조회
    /// </summary>
    Func<float> _getTime;

    /// <summary>
    /// 아이템 설정
    /// </summary>
    public SupportItemData Data => _data;

    /// <summary>
    /// 남은 효과 시간
    /// </summary>
    public float RemainingTime => _getTime == null ? 0f : _getTime( );

    /// <summary>
    /// 제작 예약 여부
    /// </summary>
    public bool IsReserved =>
        _effect is ProdDiffDownEffect effect && effect.IsReserved == true;

    /// <summary>
    /// 구조 입력으로 자동 사용하는 아이템 여부
    /// </summary>
    public bool IsAutomatic => _data is RescueSpeedUpData;

    /// <summary>
    /// 패시브 여부
    /// </summary>
    public bool IsPassive => _data is PassiveSupportItemData;

    /// <summary>
    /// 남은 사용 횟수
    /// </summary>
    public int RemainingUses =>
        _useState == null ? 0 : _useState.RemainingUses;

    /// <summary>
    /// 제작 효과 적용 여부
    /// </summary>
    public bool IsProductionActive =>
        _effect is ProdDiffDownEffect effect && effect.IsActive == true;



    /// <summary>
    /// 효과 연결
    /// </summary>
    /// <param name="data">설정</param>
    /// <param name="effect">효과 모듈</param>
    /// <param name="tick">시간 진행</param>
    /// <param name="end">효과 정리</param>
    /// <param name="getTime">시간 조회</param>
    public SupportItem (
        SupportItemData data,
        ISupportItemEffect effect,
        Action<float> tick = null,
        Action end = null,
        Func<float> getTime = null )
    {
        _data = data;
        _effect = effect;
        _tick = tick;
        _end = end;
        _getTime = getTime;

        if ( data is ActiveSupportItemData activeData )
            _useState = new SupportItemUseState( activeData.UseCount );
    }

    /// <summary>
    /// 서포트 아이템 시작
    /// </summary>
    /// <returns>시작 여부</returns>
    public bool Begin ()
    {
        if ( _started == true || _ended == true || _data.IsValid == false )
            return false;

        if ( IsPassive == true &&
             _effect.Use( ) != SupportItemUseResult.Applied )
            return false;

        _started = true;
        return true;
    }

    /// <summary>
    /// 아이템 사용
    /// </summary>
    /// <returns>사용 결과</returns>
    public SupportItemUseResult Use ()
    {
        //사용 가능 상태 확인
        if ( _started == false || _ended == true || _busy == true ||
             _useState == null || _useState.RemainingUses <= 0 ||
             _data.IsValid == false )
            return SupportItemUseResult.Rejected;

        _busy = true;

        try
        {
            //사용 결과 가져오기
            SupportItemUseResult result = _effect.Use( );

            //사용했다면 소모 처리
            if ( result == SupportItemUseResult.Applied )
                _useState.Consume( );

            return result;
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// 제작 시작
    /// </summary>
    /// <param name="session">제작 세션</param>
    /// <returns>시작 여부</returns>
    public bool TryBeginProduction ( ProductionSession session )
    {
        if ( _started == false || _ended == true || _busy == true ||
             _useState == null || _useState.RemainingUses <= 0 ||
             session == null ||
             _effect is not ProdDiffDownEffect effect ||
             effect.IsReserved == false )
            return false;

        _busy = true;

        try
        {
            if ( effect.TryBegin( session ) == false )
                return false;

            //제작 시작 성공 시 사용 횟수 차감
            _useState.Consume( );
            return true;
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// 효과 시간 진행
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    public void Tick ( float deltaTime )
    {
        if ( _ended == false && _tick != null )
            _tick( deltaTime );
    }

    /// <summary>
    /// 효과 종료
    /// </summary>
    public void End ()
    {
        if ( _ended == true )
            return;

        _ended = true;
        if ( _end != null )
            _end( );
    }
}
