using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 서포트 아이템 - 보유 목록과 사용 요청 관리
/// </summary>
public class PlayerSupportItems
{
    public const int MaxCount = 3;        //최대 반입 개수

    bool _running;      //사용 허용
    bool _ended;        //게임 종료

    /// <summary>
    /// 보유한 서포트 아이템 목록
    /// </summary>
    List<SupportItem> _items = new( );

    /// <summary>
    /// 서포트 아이템 보유 개수
    /// </summary>
    public int Count => _items.Count;


    /// <summary>
    /// 서포트 아이템 추가
    /// </summary>
    /// <param name="item">추가할 아이템</param>
    /// <returns>추가 성공 여부</returns>
    public bool TryAdd ( SupportItem item )
    {
        if ( _running == true || _ended == true ||
            item == null || item.Data == null || item.Data.IsValid == false )
        {
            Debug.LogError( "서포트 아이템을 추가할 수 없는 상태입니다." );
            return false;
        }

        if ( _items.Count >= MaxCount )
        {
            Debug.LogError(
                $"서포트 아이템은 최대 {MaxCount}개까지 보유할 수 있습니다." );
            return false;
        }

        foreach ( SupportItem held in _items )
        {
            if ( held.Data.Type == item.Data.Type )
            {
                Debug.LogError( "같은 종류의 서포트 아이템은 중복 보유할 수 없습니다." );
                return false;
            }
        }

        _items.Add( item );
        return true;
    }

    /// <summary>
    /// 보유 중 아이템 조회
    /// </summary>
    /// <param name="index">슬롯 번호</param>
    /// <returns>실행 객체</returns>
    public SupportItem GetItem ( int index )
    {
        return _items [ index ];
    }

    /// <summary>
    /// 게임 시작
    /// </summary>
    /// <returns>시작 여부</returns>
    public bool Begin ()
    {
        if ( _ended == true || _running == true )
            return false;

        foreach ( SupportItem item in _items )
        {
            if ( item.Begin( ) == false )
                return false;
        }

        _running = true;
        return true;
    }

    /// <summary>
    /// 슬롯 사용
    /// </summary>
    /// <param name="index">슬롯 번호</param>
    /// <returns>실행 결과</returns>
    public SupportItemUseResult Use ( int index )
    {
        if ( _running == false || index < 0 || index >= _items.Count )
            return SupportItemUseResult.Rejected;

        SupportItem item = _items [ index ];

        //자동 아이템은 해당 행동에서 사용
        if ( item.IsAutomatic == true )
            return SupportItemUseResult.Rejected;

        return item.Use( );
    }

    /// <summary>
    /// 유효한 구조 입력에 장갑 자동 사용
    /// </summary>
    public void TryUseRescueSupport ()
    {
        if ( _running == false )
            return;

        foreach ( SupportItem item in _items )
        {
            if ( item.Data.Type != SupportItemType.RescueGloves )
                continue;

            //횟수와 효과 중복 검사는 기존 사용 경로에서 처리
            item.Use( );
            return;
        }
    }

    /// <summary>
    /// 제작 시작
    /// </summary>
    /// <param name="session">제작 세션</param>
    /// <returns>시작 여부</returns>
    public bool TryBeginProduction ( ProductionSession session )
    {
        if ( _running == false || session == null )
            return false;

        //제작 유형과 관계없이 예약 효과 우선 적용
        foreach ( SupportItem item in _items )
        {
            if ( item.IsReserved == false )
                continue;

            return item.TryBeginProduction( session );
        }

        return session.TryBegin( out _ );
    }

    /// <summary>
    /// 효과 시간 진행
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    public void Tick ( float deltaTime )
    {
        if ( _running == false )
            return;

        foreach ( SupportItem item in _items )
            item.Tick( deltaTime );
    }

    /// <summary>
    /// 서포트 아이템 효과 해제와 목록 정리
    /// </summary>
    public void End ()
    {
        if ( _ended == true )
            return;

        _ended = true;
        _running = false;

        foreach ( SupportItem item in _items )
            item.End( );

        _items.Clear( );
    }
}
