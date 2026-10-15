using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 재료 소비 원인 - 제작 확정/외부 소실 구분
/// </summary>
public enum IngredientConsumeReason
{
    Production,     //제작 성공, 실패 또는 미완성 재료 폐기
    ExternalLoss        //제작 외 실제 소비와 소실
}

/// <summary>
/// 재료 재고 관리 - 공급원별 재고와 재료 객체 소속 관리
/// </summary>
public class IngredientStockManager : MonoBehaviour
{
    [SerializeField] IngredientSource [ ] _sources;     //이번 게임에 등록할 공급원 목록

    bool _isStarting;     //공급원 등록 진행 여부
    bool _isRunning;     //게임 진행 중 재고 처리 허용 여부
    bool _isReturning;		//반환 순회 진행 여부
    string _returnFailure;		//중복 출력을 막을 최근 반환 실패 사유

    /// <summary>
    /// 재고 딕셔너리(공급원, 재고 상태)
    /// </summary>
    Dictionary<Object, IngredientStockModel> _stocks = new( );
    /// <summary>
    /// 소속 딕셔너리(재료 객체 ID, 대여 재료와 공급원)
    /// </summary>
    Dictionary<int, IngredientStockItem> _owners = new( );

    /// <summary>
    /// 반환 대기 딕셔너리(재료 객체 ID, 반환할 재료)
    /// </summary>
    Dictionary<int, ItemCarry> _pendingReturns = new( );


    /// <summary>
    /// 게임의 공급원 목록 검사, 재고 등록
    /// </summary>
#if UNITY_EDITOR
    /// <param name="allowMissingSources">에디터 테스트에서 공급원 누락 허용 여부</param>
#endif

    /// <returns>전체 공급원 등록 여부</returns>
    public bool Init (
#if UNITY_EDITOR
        bool allowMissingSources = false
#endif
        )
    {
        if ( _isRunning == true )
            return true;

        if ( _isStarting == true ||
             _stocks.Count != 0 || _owners.Count != 0 ||
             _pendingReturns.Count != 0 || _isReturning == true )
            return false;

        bool hasSources = _sources != null && _sources.Length > 0;		//공급원 목록 존재 여부

        if ( hasSources == false )
        {
#if UNITY_EDITOR
            if ( allowMissingSources == false )
#endif
                return false;
        }

        //등록 전에 빈 참조와 중복 공급원을 확인
        HashSet<IngredientSource> uniqueSources = new( );
        List<IngredientSource> sources = new( );		//초기화할 유효 공급원

        if ( hasSources == true )
        {
            foreach ( IngredientSource source in _sources )
            {
                if ( source == null )
                {
#if UNITY_EDITOR
                    if ( allowMissingSources == true )
                        continue;
#endif
                    return false;
                }

                if ( uniqueSources.Add( source ) == false )
                    return false;

                sources.Add( source );
            }
        }

#if UNITY_EDITOR
        if ( allowMissingSources == true &&
             ( _sources == null || sources.Count != _sources.Length || sources.Count == 0 ) )
            Debug.LogWarning( "[에디터 테스트] 누락된 공급원을 제외하고 재고를 준비합니다.", this );
#endif

        _isStarting = true;

        try
        {
            foreach ( IngredientSource source in sources )
            {
                if ( source.Init( this ) == true )
                    continue;

                Debug.LogError(
                    $"[재료 재고 시작 실패] " +
                    $"공급원 설정과 재료 프리팹의 ItemPoolHandler를 확인해 주세요: {source.name}", source );

                return false;
            }

            _isRunning = true;
            return true;
        }
        finally
        {
            _isStarting = false;
            if ( _isRunning == false )
                ClearStock( );
        }
    }

    private void Update ()
    {
        //소비 알림 전체가 끝난 후 반환하며 종료 중 실패분도 재시도
        ProcessPendingReturns( out string reason );
        ReportReturnFailure( reason );

        if ( _isRunning == false )
            return;

        foreach ( KeyValuePair<Object, IngredientStockModel> entry in _stocks )
        {
            IngredientStockModel stock = entry.Value;
            stock.Tick( Time.deltaTime );

            bool refilled = stock.TryCompleteRefill( );
            if ( entry.Key is IngredientSource source && source != null )
            {
                if ( refilled == true )
                    source.SetStockCount( stock.CurrentCount, stock.MaxCount );
                source.SetRefillProgress( stock.IsRefilling, stock.RefillProgress );
            }
        }
    }

    private void OnDestroy ()
    {
        ClearStock( );
    }


    /// <summary>
    /// 등록된 공급원에 제작 관리자 전달
    /// </summary>
    /// <param name="manager">이번 플레이의 제작 관리자</param>
    public void BindProduction ( ProductionManager manager )
    {
        foreach ( KeyValuePair<Object, IngredientStockModel> entry in _stocks )
        {
            if ( entry.Key is IngredientSource source && source != null )
                source.BindProduction( manager );
        }
    }

    /// <summary>
    /// 이번 플레이에 등록된 공급원 조회
    /// </summary>
    /// <param name="sources">조회 결과를 채울 목록</param>
    public void GetSources ( List<IngredientSource> sources )
    {
        sources.Clear( );
        if ( _isRunning == false )
            return;

        foreach ( KeyValuePair<Object, IngredientStockModel> entry in _stocks )
        {
            if ( entry.Key is IngredientSource source && source != null )
                sources.Add( source );
        }
    }


    /// <summary>
    /// 재료의 공급원 소속과 소비 구독 해제
    /// </summary>
    /// <param name="instanceId">등록한 객체 ID</param>
    /// <param name="item">등록한 재료 또는 파괴된 참조</param>
    void UnregisterItem ( int instanceId, ItemCarry item )
    {
        _owners.Remove( instanceId );

        if ( item == null )
            return;

        item.OnConsumeConfirmed -= HandleConsumeConfirmed;
        item.SetStockRegistered( false );
    }

    /// <summary>
    /// 소비 또는 대여 실패 객체의 반환 예약
    /// </summary>
    /// <param name="item">반환할 대여 재료</param>
    public void QueueReturn ( ItemCarry item )
    {
        if ( item != null )
            _pendingReturns.TryAdd( item.gameObject.GetInstanceID( ), item );
    }

    /// <summary>
    /// 소비 기록을 해제하고 대기 객체를 각자의 풀에 반환
    /// </summary>
    /// <param name="reason">첫 반환 실패 사유</param>
    /// <returns>대기 객체 전체 반환 여부</returns>
    bool ProcessPendingReturns ( out string reason )
    {
        reason = string.Empty;

        if ( _isReturning == true )
        {
            reason = "재료 반환이 이미 진행 중입니다.";
            return false;
        }

        if ( _pendingReturns.Count == 0 )
            return true;

        _isReturning = true;

        try
        {
            //반환 콜백에서 추가된 객체는 다음 처리에서 반환
            List<KeyValuePair<int, ItemCarry>> pending = new( _pendingReturns );

            foreach ( var entry in pending )
            {
                int instanceId = entry.Key;
                ItemCarry item = entry.Value;

                if ( _owners.TryGetValue( instanceId, out IngredientStockItem owner ) == true )
                {
                    //일반 비활성화와 실제 소비를 구분하고 공급 수량은 유지
                    if ( ( item != null && item.gameObject.activeSelf == true ) ||
                         _stocks [ owner.Source ].TryUnregisterConsumed( instanceId ) == false )
                    {
                        if ( string.IsNullOrEmpty( reason ) == true )
                            reason = $"소비 완료를 확인하지 못했습니다: {instanceId}";
                        continue;
                    }

                    UnregisterItem( instanceId, item );
                }

                if ( item == null )
                {
                    _pendingReturns.Remove( instanceId );
                    continue;
                }

                //실패하면 소속 없는 대기 객체로 남겨 다음 프레임에 재시도
                Poolable poolable = item.GetComponent<Poolable>( );
                if ( poolable.TryReturnToPool( out string itemReason ) == false )
                {
                    if ( string.IsNullOrEmpty( reason ) == true )
                        reason = $"{item.name}: {itemReason}";
                    continue;
                }

                _pendingReturns.Remove( instanceId );
            }

            return _pendingReturns.Count == 0;
        }
        finally
        {
            _isReturning = false;
        }
    }

    /// <summary>
    /// 달라진 반환 실패 사유만 출력
    /// </summary>
    /// <param name="reason">이번 반환 실패 사유 또는 빈 문자열</param>
    void ReportReturnFailure ( string reason )
    {
        if ( string.IsNullOrEmpty( reason ) == false && _returnFailure != reason )
            Debug.LogWarning( $"[재료 풀 반환 대기] {reason}", this );

        _returnFailure = reason;
    }

    /// <summary>
    /// 공급원 설정으로 독립 재고 등록
    /// </summary>
    /// <param name="source">원래 공급원</param>
    /// <param name="data">재료 설정</param>
    /// <returns>전체 등록 성공 여부</returns>
    public bool TryRegisterSource (
        Object source,
        IngredientStockData data )
    {
        //유효한 값인지 확인
        if ( _isStarting == false ||
             source == null || data == null ||
             data.MaxCount < 1 ||
             float.IsNaN( data.RefillDuration ) ||
             float.IsInfinity( data.RefillDuration ) ||
             data.RefillDuration < 0f ||
             _stocks.ContainsKey( source ) )
            return false;

        //최대 수량으로 재고 생성
        IngredientStockModel stock =
            new( data.MaxCount, data.RefillDuration );

        _stocks.Add( source, stock );

        return true;
    }

    /// <summary>
    /// 소비 객체를 반환 대기로 등록
    /// </summary>
    /// <param name="item">소비가 확정된 재료</param>
    /// <param name="reason">소비 원인</param>
    void HandleConsumeConfirmed (
        ItemCarry item, IngredientConsumeReason reason )
    {
        //공급원 및 재고 수량 가져오기
        if ( _isRunning == false || item == null ||
             _owners.TryGetValue( item.gameObject.GetInstanceID( ), out IngredientStockItem entry ) == false ||
             _stocks.TryGetValue( entry.Source, out IngredientStockModel stock ) == false )
            return;

        //소비 확정 처리
        if ( stock.TryMarkConsumed( item.gameObject.GetInstanceID( ) ) == false )
            return;

        QueueReturn( item );
    }


    /// <summary>
    /// 재료 객체를 획득 시도 중으로 예약
    /// </summary>
    /// <param name="source">원래 공급원</param>
    /// <param name="item">예약할 재료 객체</param>
    /// <returns>예약 성공 여부</returns>
    public bool TryReserve ( Object source, ItemCarry item )
    {
        //예약은 아직 소비가 아니므로 현재 수량은 유지
        if ( _isRunning == false || source == null || item == null ||
             _stocks.TryGetValue( source, out IngredientStockModel stock ) == false )
            return false;

        int instanceId = item.gameObject.GetInstanceID( );
        if ( _owners.ContainsKey( instanceId ) == true ||
             _pendingReturns.ContainsKey( instanceId ) == true ||
             stock.TryReserve( instanceId ) == false )
            return false;

        _owners.Add( instanceId, new IngredientStockItem( item , source ) );
        item.OnConsumeConfirmed += HandleConsumeConfirmed;
        item.SetStockRegistered( true );
        return true;
    }

    /// <summary>
    /// 실패한 획득 예약 취소
    /// </summary>
    /// <param name="source">원래 공급원</param>
    /// <param name="instanceId">예약한 재료 아이디</param>
    /// <returns>예약 취소 여부</returns>
    public bool TryCancelReserve ( Object source, int instanceId )
    {
        if ( source == null ||
             _owners.TryGetValue( instanceId, out IngredientStockItem entry ) == false ||
             entry.Source != source ||
             _stocks.TryGetValue( source, out IngredientStockModel stock ) == false ||
             stock.TryCancelReserve( instanceId ) == false )
            return false;

        UnregisterItem( instanceId, entry.Item );
        QueueReturn( entry.Item );
        if ( entry.Item != null )
            entry.Item.gameObject.SetActive( false );
        return true;
    }

    /// <summary>
    /// 손 전달 성공 후 재고 차감과 UI 반영
    /// </summary>
    /// <param name="source">원래 공급원</param>
    /// <param name="item">획득한 객체</param>
    /// <returns>확정 여부</returns>
    public bool TryConfirmTaken ( IngredientSource source, ItemCarry item )
    {
        if ( _isRunning == false || item == null ||
             _owners.TryGetValue( item.gameObject.GetInstanceID( ), out IngredientStockItem entry ) == false ||
             entry.Source != source ||
             _stocks.TryGetValue( source, out IngredientStockModel stock ) == false ||
             stock.TryConfirmTaken( item.gameObject.GetInstanceID( ) ) == false )
            return false;
        source.SetStockCount( stock.CurrentCount, stock.MaxCount );
        source.SetRefillProgress( stock.IsRefilling, stock.RefillProgress );
#if UNITY_EDITOR
        PlayDebugHistory.Record( "공급원 획득", source,
            $"공급원 {source.name} / 획득 1개 / 남은 {stock.CurrentCount} / {stock.MaxCount}" );
#endif
        return true;
    }


    /// <summary>
    /// 획득/재고 변경을 막고 공급원 구독 해제
    /// </summary>
    /// <returns>대여 재료 전체 반환 여부</returns>
    public bool ClearStock ()
    {
        //종료 중 발생하는 콜백부터 먼저 차단
        _isRunning = false;
        _isStarting = false;

        foreach ( KeyValuePair<Object, IngredientStockModel> entry in _stocks )
        {
            if ( entry.Key is IngredientSource source && source != null )
                source.ClearStock( this );
        }

        //공급원 소속과 구독을 모두 끊은 뒤 반환 콜백 실행
        List<KeyValuePair<int, IngredientStockItem>> owners = new( _owners );
        foreach ( var entry in owners )
        {
            UnregisterItem( entry.Key, entry.Value.Item );
            QueueReturn( entry.Value.Item );
        }

        //진행 중인 재보충 모델도 게임 종료 시 폐기
        _stocks.Clear( );

        bool completed = ProcessPendingReturns( out string reason );
        ReportReturnFailure( reason );
        return completed;
    }

    /// <summary>
    /// 지정 공급원 재보충 단축
    /// </summary>
    /// <param name="source">선택한 공급원</param>
    /// <param name="ratio">감소 비율</param>
    /// <returns>단축 여부</returns>
    public bool TryReduceRefill ( IngredientSource source, float ratio )
    {
        if ( _isRunning == false || source == null || source.isActiveAndEnabled == false ||
             _stocks.TryGetValue( source, out IngredientStockModel stock ) == false ||
             stock.TryReduceRefill( ratio ) == false )
            return false;
        stock.TryCompleteRefill( );
        source.SetStockCount( stock.CurrentCount, stock.MaxCount );
        source.SetRefillProgress( stock.IsRefilling, stock.RefillProgress );
        return true;
    }


#if UNITY_EDITOR
    /// <summary>
    /// 공급원 재고와 재보충 상태 조회
    /// </summary>
    /// <param name="source">조회 공급원</param>
    /// <returns>현재 상태</returns>
    public string GetDebugStockStatus ( IngredientSource source )
    {
        if ( _isRunning == false || source == null ||
             _stocks.TryGetValue( source, out IngredientStockModel stock ) == false )
            return "플레이 중 등록된 공급원이 필요합니다.";

        return $"재고 {stock.CurrentCount}/{stock.MaxCount} / {stock.GetDebugInstanceStatus( )} / " +
            $"재보충 {stock.IsRefilling} / 남은 {stock.RemainingTime:F2}초";
    }

    /// <summary>
    /// 공급 수량을 소진하여 재보충 테스트 준비
    /// </summary>
    /// <param name="source">소비할 공급원</param>
    /// <param name="reason">실행 결과</param>
    /// <returns>소비 완료 여부</returns>
    public bool TryDebugConsumeStock ( IngredientSource source, out string reason )
    {
        reason = string.Empty;
        if ( Application.isPlaying == false || _isRunning == false || source == null ||
             _stocks.TryGetValue( source, out IngredientStockModel stock ) == false )
        {
            reason = "플레이 중 등록된 공급원이 필요합니다.";
            return false;
        }
        if ( stock.TryDebugDeplete( ) == false )
        {
            reason = "재고가 이미 0이거나 획득 처리 중입니다. " + GetDebugStockStatus( source );
            return false;
        }
        source.SetRefillProgress( stock.IsRefilling, stock.RefillProgress );
        reason = $"재고 소진. {GetDebugStockStatus( source )}";
        source.SetStockCount( stock.CurrentCount, stock.MaxCount );
        return true;
    }
#endif


    /// <summary>
    /// 공급원의 획득 가능 수량 존재 여부
    /// </summary>
    /// <param name="source">확인할 공급원</param>
    /// <returns>획득 가능 객체 존재 여부</returns>
    public bool HasAvailableStock ( Object source )
    {
        return _isRunning == true &&
               source != null &&
               _stocks.TryGetValue( source, out IngredientStockModel stock ) &&
               stock.HasAvailableStock;
    }

}
