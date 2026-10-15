using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이 서포트 제어 - 아이템 준비와 효과 수명 관리
/// </summary>
public class PlaySupportController
{
    bool _isRunning;		//서포트 효과 진행 여부

    Player _player;		//효과 생성 대상
    PlayerSupportItems _items;		//이번 플레이의 보유 목록
    InputHandler _inputHandler;		//슬롯 입력 확인
    PlayerHud _playerHud;		//슬롯 표시
    IngredientStockManager _ingredientStock;		//급속 보급 재고

    /// <summary>
    /// 서포트 효과 진행 여부
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 플레이 서포트 기능 연결
    /// </summary>
    /// <param name="player">효과 생성 대상</param>
    /// <param name="items">보유 목록</param>
    /// <param name="inputHandler">슬롯 입력</param>
    /// <param name="playerHud">슬롯 표시</param>
    /// <param name="ingredientStock">보급 재고</param>
    public PlaySupportController (
        Player player,
        PlayerSupportItems items,
        InputHandler inputHandler,
        PlayerHud playerHud,
        IngredientStockManager ingredientStock )
    {
        _player = player;
        _items = items;
        _inputHandler = inputHandler;
        _playerHud = playerHud;
        _ingredientStock = ingredientStock;
    }

    /// <summary>
    /// 서포트 아이템 설정 확인과 효과 적용
    /// </summary>
    /// <param name="items">반입 또는 테스트 아이템 목록</param>
    /// <returns>시작 여부</returns>
    public bool Begin ( IReadOnlyList<SupportItemData> items )
    {
        if ( _items == null || items == null )
        {
            Debug.LogError(
                "[서포트 시작 실패] " +
                "서포트 아이템 목록을 확인해 주세요.", _player );
            return false;
        }

        if ( _playerHud.IsSupportReady( items.Count ) == false )
        {
            Debug.LogError(
                "[서포트 시작 실패] " +
                $"슬롯 HUD를 보유 개수 {items.Count}개만큼 연결해 주세요.", _player );
            return false;
        }

        for ( int i = 0; i < items.Count; i++ )
        {
            SupportItemData data = items [ i ];     //슬롯 아이템 설정

            //생성에 필요한 설정과 씬 연결 확인
            if ( data == null )
            {
                Debug.LogError(
                    "[서포트 시작 실패] 아이템 설정이 비어 있습니다.", _player );
                return false;
            }

            //액티브 슬롯의 사용 입력 확인
            if ( data is ActiveSupportItemData &&
                 _inputHandler.HasSupportAction( i ) == false )
            {
                Debug.LogError(
                    $"[서포트 시작 실패] {i + 1}번 슬롯의 " +
                    $"Player/Support{i + 1} 입력을 연결해 주세요.", _player );
                return false;
            }

            if ( data is QuickSupplyData && _ingredientStock == null )
            {
                Debug.LogError(
                    "[서포트 시작 실패] 재료 재고 기능을 연결해 주세요.", _player );
                return false;
            }

            SupportItem item = _player.CreateSupportItem( data, _ingredientStock );

            if ( _items.TryAdd( item ) == false )
                return false;
        }

        //목록 구성이 끝난 뒤 패시브 적용
        if ( _items.Begin( ) == false )
        {
            Debug.LogError(
                "[서포트 시작 실패] 아이템 효과를 시작하지 못했습니다.", _player );
            return false;
        }

        _isRunning = true;
        _playerHud.SetSupportItems( _items );
        return true;
    }

    /// <summary>
    /// 서포트 효과 시간 진행
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    public void Tick ( float deltaTime )
    {
        if ( IsRunning == true )
            _items.Tick( deltaTime );
    }

    /// <summary>
    /// 효과 해제와 서포트 목록, 슬롯 표시 정리
    /// </summary>
    public void End ()
    {
        _isRunning = false;
        _items.End( );

        if ( _playerHud != null )
            _playerHud.SetSupportItems( null );
    }
}
