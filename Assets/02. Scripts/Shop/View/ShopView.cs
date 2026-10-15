using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 상점 뷰 - 상품 목록, 구매 결과 출력
/// </summary>
public class ShopView : MonoBehaviour
{
    [Header( "----- 상점 UI -----" )]
    [SerializeField] TMP_Text _goldText;        //보유 코인
    [SerializeField] TMP_Text _messageText;        //구매 결과 안내
    [SerializeField] Button _closeButton;        //상점 닫기

    [Header( "----- 구매 결과 연출 -----" )]
    [SerializeField] TextEffect _goldEffect;		//구매 후 잔액 강조
    [SerializeField] TextEffect _messageEffect;		//구매 결과 안내 연출

    [Header( "----- 상품 슬롯 -----" )]
    [SerializeField] ShopItemSlot _slotPrefab;        //상품 슬롯 프리팹
    [SerializeField] Transform _content;        //슬롯 생성 위치

    [Header( "----- 수량 팝업 -----" )]
    [SerializeField] SliderPopup _popupPrefab;        //공통 슬라이더 프리팹
    [SerializeField] RectTransform _viewport;        //상품 목록 영역

    SliderPopup _popup;        //재사용 팝업
    ShopItemSlot _editingSlot;        //현재 수량 편집 슬롯

    /// <summary>
    /// 판매 순서별 생성 슬롯
    /// </summary>
    List<ShopItemSlot> _slots = new( );

    /// <summary>
    /// 구매 요청(아이템, 수량)
    /// </summary>
    public event Action<SupportItemData, int> OnPurchaseRequested;

    /// <summary>
    /// 상점 닫기 요청
    /// </summary>
    public event Action OnCloseRequested;

    #region ----- 초기화 -----

    /// <summary>
    /// 판매 목록으로 슬롯 생성
    /// </summary>
    /// <param name="items">판매 목록</param>
    public void Init ( IReadOnlyList<SupportItemData> items )
    {
        if ( _popup == null )
        {
            _popup = Instantiate( _popupPrefab, transform, false );
            _popup.Init( ShopModel.MinPurchaseCount, ShopModel.MaxPurchaseCount );
            _popup.OnValueChanged += HandlePopupValueChanged;
        }

        ClosePopup( );
        ClearSlots( );
        CreateSlots( items );
    }

    void OnEnable ()
    {
        _closeButton.onClick.AddListener( HandleCloseClicked );
    }

    void OnDisable ()
    {
        ClosePopup( );
        ResetResultEffect( );
        _closeButton.onClick.RemoveListener( HandleCloseClicked );

        //다시 열 때 이전 입력 필드의 선택 상태가 남지 않도록 해제
        EventSystem events = EventSystem.current;

        if ( events == null || events.currentSelectedGameObject == null )
            return;

        if ( events.currentSelectedGameObject.transform.IsChildOf( transform ) == true )
            events.SetSelectedGameObject( null );
    }

    void OnDestroy ()
    {
        ClearSlots( );
        if ( _popup != null )
        {
            _popup.OnValueChanged -= HandlePopupValueChanged;
            Destroy( _popup.gameObject );
        }
    }

    /// <summary>
    /// 수량 영역 밖 선택과 목록 영역 이탈 시 팝업 비활성화
    /// </summary>
    void LateUpdate ()
    {
        if ( _editingSlot == null )
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        bool popupSelected = selected != null &&
            selected.transform.IsChildOf( _popup.transform );

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            _viewport, _editingSlot.CountAnchor );
        Rect area = new Rect( bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y );

        if ( _editingSlot.gameObject.activeInHierarchy == false ||
             _viewport.rect.Overlaps( area ) == false ||
             ( popupSelected == false && _editingSlot.IsCountSelected( selected ) == false ) )
            ClosePopup( );
    }


    /// <summary>
    /// 상품 슬롯 생성과 구매 이벤트 연결
    /// </summary>
    /// <param name="items">판매 목록</param>
    void CreateSlots ( IReadOnlyList<SupportItemData> items )
    {
        foreach ( SupportItemData data in items )
        {
            ShopItemSlot slot = Instantiate( _slotPrefab, _content, false );
            _slots.Add( slot );

            slot.Init( data );
            slot.OnPurchaseRequested += HandlePurchaseRequested;
            slot.OnCountSelected += HandleCountSelected;
            slot.OnCountChanged += HandleCountChanged;
            slot.gameObject.SetActive( true );
        }
    }

    /// <summary>
    /// 생성한 상품 슬롯 제거
    /// </summary>
    void ClearSlots ()
    {
        foreach ( ShopItemSlot slot in _slots )
        {
            //씬 해제 시 먼저 파괴된 슬롯 제외
            if ( slot == null )
                continue;

            slot.OnPurchaseRequested -= HandlePurchaseRequested;
            slot.OnCountSelected -= HandleCountSelected;
            slot.OnCountChanged -= HandleCountChanged;

            //파괴 대기 중 입력과 레이아웃 참여 차단
            slot.gameObject.SetActive( false );
            Destroy( slot.gameObject );
        }

        _slots.Clear( );
    }

    #endregion

    #region ----- 핸들 -----

    /// <summary>
    /// 편집 슬롯의 선택 상한과 위치로 팝업 활성화
    /// </summary>
    /// <param name="slot">편집할 슬롯</param>
    void HandleCountSelected ( ShopItemSlot slot )
    {
        _editingSlot = slot;
        _popup.SetLimit( slot.MaxCount );
        _popup.Open( slot.CountAnchor, slot.Count );
    }

    /// <summary>
    /// 변경된 선택 상한과 수량을 팝업에 반영
    /// </summary>
    /// <param name="slot">수량이 변경된 슬롯</param>
    void HandleCountChanged ( ShopItemSlot slot )
    {
        if ( slot != _editingSlot )
            return;

        if ( slot.MaxCount == 0 )
        {
            ClosePopup( );
            return;
        }

        _popup.SetLimit( slot.MaxCount );
        _popup.SetValue( slot.Count );
    }

    /// <summary>
    /// 팝업 입력을 편집 슬롯에 반영
    /// </summary>
    /// <param name="value">변경된 수량</param>
    void HandlePopupValueChanged ( int value )
    {
        _editingSlot.SetCount( value );
    }

    /// <summary>
    /// 슬롯의 구매 요청 전달
    /// </summary>
    /// <param name="data">판매 아이템</param>
    /// <param name="count">구매 수량</param>
    void HandlePurchaseRequested ( SupportItemData data, int count )
    {
        ClosePopup( );
        OnPurchaseRequested?.Invoke( data, count );
    }

    /// <summary>
    /// 상점 닫기 요청 전달
    /// </summary>
    void HandleCloseClicked ()
    {
        OnCloseRequested?.Invoke( );
    }

    #endregion

    #region ----- 화면 갱신 -----

    /// <summary>
    /// 편집 대상 해제와 팝업 비활성화
    /// </summary>
    void ClosePopup ()
    {
        _editingSlot = null;
        if ( _popup == null )
            return;

        //씬 해제 시 EventSystem이 먼저 파괴될 수 있음
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if ( selected != null &&
             selected.transform.IsChildOf( _popup.transform ) == true )
            events.SetSelectedGameObject( null );

        _popup.Close( );
    }

    /// <summary>
    /// 구매한 상품의 입력 수량 초기화
    /// </summary>
    /// <param name="data">구매한 상품</param>
    public void ResetPurchaseCount ( SupportItemData data )
    {
        foreach ( ShopItemSlot slot in _slots )
        {
            if ( slot.Data != data )
                continue;

            slot.ResetCount( );
            return;
        }
    }

    /// <summary>
    /// 코인과 아이템 보유량 갱신
    /// </summary>
    /// <param name="save">현재 저장 데이터</param>
    public void Refresh ( SupportItemSaveData save )
    {
        _goldText.text = save.Balance.ToString( "N0" );

        foreach ( ShopItemSlot slot in _slots )
        {
            int count = save.Inventory.GetCount( slot.Data.Id );
            slot.SetOwnedCount( count );
        }
    }

    /// <summary>
    /// 잔액과 결과 안내 연출 초기화
    /// </summary>
    void ResetResultEffect ()
    {
        _goldEffect.ResetEffect( );
        _messageEffect.ResetEffect( );
    }

    /// <summary>
    /// 구매 결과와 상품 연출 초기화
    /// </summary>
    void ResetPurchaseEffects ()
    {
        ResetResultEffect( );

        foreach ( ShopItemSlot slot in _slots )
            slot.ResetPurchaseEffect( );
    }

    /// <summary>
    /// 구매 연출
    /// </summary>
    /// <param name="data">구매를 요청한 상품</param>
    /// <param name="purchased">구매 성공 여부</param>
    /// <param name="message">구매 결과 안내</param>
    public void PlayPurchaseResult (
        SupportItemData data, bool purchased, string message )
    {
        ResetPurchaseEffects( );
        SetMessage( message );

        if ( purchased == false )
        {
            _messageEffect.PlayFailure( );
            return;
        }

        _messageEffect.PlayEmphasis( );
        _goldEffect.PlayEmphasis( );

        foreach ( ShopItemSlot slot in _slots )
        {
            if ( slot.Data != data )
                continue;

            slot.PlayPurchaseCompleted( );
            return;
        }
    }

    /// <summary>
    /// 구매 결과 안내 갱신
    /// </summary>
    /// <param name="message">안내 문구</param>
    public void SetMessage ( string message )
    {
        _messageText.text = message;
    }

    /// <summary>
    /// 상점 활성화
    /// </summary>
    /// <param name="save">현재 저장 데이터</param>
    public void Open ( SupportItemSaveData save )
    {
        ResetPurchaseEffects( );
        Refresh( save );
        SetMessage( string.Empty );

        foreach ( ShopItemSlot slot in _slots )
            slot.ResetCount( );

        PanelEffect.SetVisible( gameObject, true );
    }

    /// <summary>
    /// 상점 비활성화
    /// </summary>
    /// <param name="immediate">초기 숨김을 즉시 적용할지 여부</param>
    public void Close ( bool immediate = false )
    {
        ClosePopup( );
        ResetPurchaseEffects( );
        PanelEffect.SetVisible( gameObject, false, immediate );
    }

    #endregion
}
