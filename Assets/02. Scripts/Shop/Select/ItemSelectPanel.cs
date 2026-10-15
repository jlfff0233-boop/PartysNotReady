using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 선택 패널 - 보유 목록과 선택 결과 출력
/// </summary>
public class ItemSelectPanel : MonoBehaviour
{
    [Header( "----- 선택 UI -----" )]
    [SerializeField] TMP_Text _infoText;        //선택 안내

    [Header( "----- 아이템 슬롯 -----" )]
    [SerializeField] ItemSelectSlot _slotPrefab;        //선택 슬롯 프리팹
    [SerializeField] Transform _content;        //슬롯 생성 위치

    [Header( "----- 버튼 -----" )]
    [SerializeField] Button _completeButton;        //선택 완료
    [SerializeField] Button _closeButton;        //선택 취소

    /// <summary>
    /// 보유 아이템 순서별 생성 슬롯
    /// </summary>
    List<ItemSelectSlot> _slots = new( );

    /// <summary>
    /// 아이템 선택 요청
    /// </summary>
    public event Action<SupportItemData> OnSelectRequested;

    /// <summary>
    /// 선택 완료 요청
    /// </summary>
    public event Action OnCompleteRequested;

    /// <summary>
    /// 선택 취소 요청
    /// </summary>
    public event Action OnCloseRequested;



    void OnEnable ()
    {
        _completeButton.onClick.AddListener( HandleCompleteClicked );
        _closeButton.onClick.AddListener( HandleCloseClicked );
    }

    void OnDisable ()
    {
        _closeButton.onClick.RemoveListener( HandleCloseClicked );

        //패널 밖 버튼은 씬 해제 시 먼저 파괴될 수 있음
        if ( _completeButton != null )
        {
            _completeButton.onClick.RemoveListener( HandleCompleteClicked );
            _completeButton.gameObject.SetActive( false );
        }
    }


    void OnDestroy ()
    {
        ClearSlots( );
    }


    /// <summary>
    /// 슬롯의 선택 요청 전달
    /// </summary>
    /// <param name="data">선택한 아이템</param>
    void HandleSelectRequested ( SupportItemData data )
    {
        OnSelectRequested?.Invoke( data );
    }

    /// <summary>
    /// 선택 완료 요청 전달
    /// </summary>
    void HandleCompleteClicked ()
    {
        OnCompleteRequested?.Invoke( );
    }

    /// <summary>
    /// 선택 취소 요청 전달
    /// </summary>
    void HandleCloseClicked ()
    {
        OnCloseRequested?.Invoke( );
    }


    /// <summary>
    /// 보유 아이템 슬롯 생성과 선택 이벤트 연결
    /// </summary>
    /// <param name="items">보유 아이템 목록</param>
    /// <param name="inventory">현재 보유량</param>
    void CreateSlots (
        IReadOnlyList<SupportItemData> items,
        SupportItemInventory inventory )
    {
        foreach ( SupportItemData data in items )
        {
            ItemSelectSlot slot = Instantiate( _slotPrefab, _content, false );
            _slots.Add( slot );

            slot.Init( data, inventory.GetCount( data.Id ) );
            slot.OnSelectRequested += HandleSelectRequested;
            slot.gameObject.SetActive( true );
        }
    }

    /// <summary>
    /// 생성한 선택 슬롯 제거
    /// </summary>
    void ClearSlots ()
    {
        foreach ( ItemSelectSlot slot in _slots )
        {
            //씬 해제 시 먼저 파괴된 슬롯 제외
            if ( slot == null )
                continue;

            slot.OnSelectRequested -= HandleSelectRequested;

            //파괴 대기 중 입력과 레이아웃 참여 차단
            slot.gameObject.SetActive( false );
            Destroy( slot.gameObject );
        }

        _slots.Clear( );
    }

    /// <summary>
    /// 보유 슬롯 생성 후 선택 패널 활성화
    /// </summary>
    /// <param name="items">보유 아이템 목록</param>
    /// <param name="inventory">현재 보유량</param>
    public void Open (
        IReadOnlyList<SupportItemData> items,
        SupportItemInventory inventory )
    {
        //이전 생성분을 정리하고 최신 보유 목록 반영
        ClearSlots( );
        CreateSlots( items, inventory );

        _completeButton.gameObject.SetActive( true );
        _completeButton.interactable = true;
        _closeButton.interactable = true;
        PanelEffect.SetVisible( gameObject, true );
    }

    /// <summary>
    /// 선택 순서와 안내 갱신
    /// </summary>
    /// <param name="selected">선택한 아이템 목록</param>
    /// <param name="message">추가 안내</param>
    public void RefreshSelection (
        IReadOnlyList<SupportItemData> selected, string message )
    {
        foreach ( ItemSelectSlot slot in _slots )
        {
            int order = GetSelectionOrder( slot.Data, selected );
            slot.SetSelection( order );
        }

        _infoText.text =
            $"아이템을 선택하세요. " +
            $"({selected.Count}/{PlayerSupportItems.MaxCount}, 중복 선택 불가)";

        if ( string.IsNullOrEmpty( message ) == false )
            _infoText.text += $"\n{message}";
    }

    /// <summary>
    /// 선택 패널과 완료 버튼 비활성화
    /// </summary>
    /// <param name="immediate">초기 숨김을 즉시 적용할지 여부</param>
    public void Close ( bool immediate = false )
    {
        //패널 밖 완료 버튼도 퇴장 중에는 입력만 차단하고 OnDisable에서 숨김
        _completeButton.interactable = false;
        _closeButton.interactable = false;
        if ( immediate == true )
            _completeButton.gameObject.SetActive( false );

        PanelEffect.SetVisible( gameObject, false, immediate );
    }

    /// <summary>
    /// 아이템 선택 순서 조회
    /// </summary>
    /// <param name="data">조회할 아이템</param>
    /// <param name="selected">선택한 아이템 목록</param>
    /// <returns>선택 순서, 미선택은 0</returns>
    int GetSelectionOrder (
        SupportItemData data,
        IReadOnlyList<SupportItemData> selected )
    {
        for ( int i = 0; i < selected.Count; i++ )
        {
            if ( selected [ i ] == data )
                return i + 1;
        }

        return 0;
    }
}
