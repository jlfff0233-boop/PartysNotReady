using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서포트 아이템 반입 요청 - 씬 전달, 차감과 실패 복원
/// </summary>
public class SupportItemRunRequest
{
    /// <summary>
    /// 반입 처리 상태
    /// </summary>
    enum RequestState
    {
        Ready,        //차감 전
        Consumed,        //수량 차감 완료
        Started,        //게임 시작 완료
        Cancelled        //시작 취소
    }

    static SupportItemRunRequest _pending;        //플레이 씬에 전달할 요청

    SupportItemData [ ] _items;        //반입 순서별 아이템
    SaveManager _save;		//반입 차감과 실패 복원 저장
    RequestState _state;        //반입 처리 상태

    /// <summary>
    /// 반입 순서별 아이템
    /// </summary>
    public IReadOnlyList<SupportItemData> Items => _items;


    /// <summary>
    /// 저장 관리자 연결과 반입 목록 복사
    /// </summary>
    /// <param name="items">선택을 마친 아이템 목록</param>
    /// <param name="save">저장 관리자</param>
    SupportItemRunRequest (
        IReadOnlyList<SupportItemData> items, SaveManager save )
    {
        _save = save;
        _items = new SupportItemData [ items.Count ];

        for ( int i = 0; i < items.Count; i++ )
            _items [ i ] = items [ i ];

        _state = RequestState.Ready;
    }

    /// <summary>
    /// 플레이 모드 진입 시 전달 요청 초기화
    /// </summary>
    [RuntimeInitializeOnLoadMethod( 
        RuntimeInitializeLoadType.SubsystemRegistration )]
    static void ResetPending ()
    {
        _pending = null;
    }


    #region ----- 요청 전달 -----

    /// <summary>
    /// 다음 플레이 씬의 반입 요청 준비
    /// </summary>
    /// <param name="items">선택을 마친 아이템 목록</param>
    /// <param name="save">저장 관리자</param>
    /// <param name="request">생성한 요청</param>
    /// <returns>요청 준비 여부</returns>
    public static bool TryPrepare (
        IReadOnlyList<SupportItemData> items,
        SaveManager save,
        out SupportItemRunRequest request )
    {
        request = null;

        //아직 전달하지 않은 요청 덮어쓰기 차단
        if ( _pending != null )
            return false;

        request = new SupportItemRunRequest( items, save );
        _pending = request;
        return true;
    }

    /// <summary>
    /// 플레이 씬에서 반입 요청 인수
    /// </summary>
    /// <returns>전달 요청, 없으면 null</returns>
    public static SupportItemRunRequest TakePending ()
    {
        SupportItemRunRequest request = _pending;
        _pending = null;
        return request;
    }

    #endregion

    #region ----- 수량 처리 -----

    /// <summary>
    /// 반입 아이템별 보유량 1개 차감
    /// </summary>
    /// <param name="message">차감 실패 안내</param>
    /// <returns>차감 성공 여부</returns>
    public bool TryConsume ( out string message )
    {
        message = string.Empty;

        if ( _state != RequestState.Ready )
        {
            message = "이미 처리한 반입 요청입니다.";
            return false;
        }

        if ( TryChangeCounts( -1, out message ) == false )
            return false;

        _state = RequestState.Consumed;
        return true;
    }

    /// <summary>
    /// 게임 시작 완료 확정
    /// </summary>
    public void ConfirmStart ()
    {
        if ( _state == RequestState.Consumed )
            _state = RequestState.Started;
    }

    /// <summary>
    /// 시작 요청 취소와 차감 수량 복원
    /// </summary>
    public void Cancel ()
    {
        //씬 이동 실패로 남은 전달 요청도 해제
        if ( ReferenceEquals( _pending, this ) == true )
            _pending = null;

        if ( _state == RequestState.Started ||
             _state == RequestState.Cancelled )
            return;

        if ( _state == RequestState.Consumed &&
             TryChangeCounts( 1, out string message ) == false )
        {
            Debug.LogError( $"[반입 복원 실패] {message}" );
            return;
        }

        _state = RequestState.Cancelled;
    }

    /// <summary>
    /// 반입 목록의 보유량 변경 후 저장
    /// </summary>
    /// <param name="amount">아이템별 변화량</param>
    /// <param name="message">변경 실패 안내</param>
    /// <returns>변경 성공 여부</returns>
    bool TryChangeCounts ( int amount, out string message )
    {
        message = string.Empty;

        if ( _items.Length == 0 )
            return true;

        SupportItemSaveData save = _save.LoadSupportItems( );

        foreach ( SupportItemData data in _items )
        {
            if ( save.Inventory.TryChangeCount( data.Id, amount ) == true )
                continue;

            message = amount < 0
                ? $"{data.ItemName} 보유 수량이 부족합니다."
                : $"{data.ItemName} 보유 수량을 복원할 수 없습니다.";

            //현재 불러온 데이터만 변경했으므로 저장하지 않고 종료
            return false;
        }

        _save.SaveSupportItems( save );
        return true;
    }

    #endregion
}
