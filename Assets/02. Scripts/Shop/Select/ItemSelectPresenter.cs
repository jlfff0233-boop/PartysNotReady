using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 선택 중재자 - 보유 목록 조회와 반입 선택 전달
/// </summary>
public class ItemSelectPresenter : MonoBehaviour
{
    [Header( "----- 선택 UI -----" )]
    [SerializeField] ItemSelectPanel _panel;        //아이템 선택 패널

    SupportItemSelectionModel _model;        //반입 선택 관리
    SaveManager _save;		//아이템 보유량 조회
    bool _isOpen;        //선택 입력 허용 여부

    /// <summary>
    /// 선택 완료 알림(반입 순서별 아이템)
    /// </summary>
    public event Action<SupportItemData [ ]> OnConfirmed;

    /// <summary>
    /// 선택 취소 알림
    /// </summary>
    public event Action OnCancelled;


    /// <summary>
    /// 아이템 목록과 저장 관리자 연결, 선택 모델 초기화
    /// </summary>
    /// <param name="items">설정 확인을 마친 아이템 목록</param>
    /// <param name="save">저장 관리자</param>
    public void Init (
        IReadOnlyList<SupportItemData> items, SaveManager save )
    {
        _save = save;
        _model = new SupportItemSelectionModel( items );
        _isOpen = false;
        _panel.Close( true );
    }

    void OnEnable ()
    {
        _panel.OnSelectRequested += HandleSelectRequested;
        _panel.OnCompleteRequested += HandleCompleteRequested;
        _panel.OnCloseRequested += Close;
    }

    void OnDisable ()
    {
        _isOpen = false;
        _model?.ClearSelection( );

        //씬 해제 시 패널이 먼저 파괴될 수 있음
        if ( _panel == null )
            return;

        _panel.OnSelectRequested -= HandleSelectRequested;
        _panel.OnCompleteRequested -= HandleCompleteRequested;
        _panel.OnCloseRequested -= Close;
        _panel.Close( true );
    }

    /// <summary>
    /// 반입 선택 변경과 UI 갱신
    /// </summary>
    /// <param name="data">선택한 아이템</param>
    void HandleSelectRequested ( SupportItemData data )
    {
        if ( _isOpen == false )
            return;

        _model.TryToggle( data, out string message );
        _panel.RefreshSelection( _model.SelectedItems, message );
    }

    /// <summary>
    /// 선택 목록 복사 후 완료 알림
    /// </summary>
    void HandleCompleteRequested ()
    {
        if ( _isOpen == false )
            return;

        //선택 정리 전에 전달할 목록 복사
        SupportItemData [ ] selected = _model.CopySelectedItems( );

        EndSelection( );
        OnConfirmed?.Invoke( selected );
    }


    /// <summary>
    /// 보유 아이템을 조회해 선택 시작
    /// </summary>
    public void Open ()
    {
        if ( _model == null || _isOpen == true )
            return;

        SupportItemSaveData save = _save.LoadSupportItems( );
        _model.Reset( save.Inventory );

        //반입 가능한 보유 아이템이 없으면 선택창 생략
        if ( _model.OwnedItems.Count == 0 )
        {
            OnConfirmed?.Invoke( Array.Empty<SupportItemData>( ) );
            return;
        }

        _panel.Open( _model.OwnedItems, save.Inventory );
        _isOpen = true;
        _panel.RefreshSelection( _model.SelectedItems, string.Empty );
    }

    /// <summary>
    /// 선택 취소와 복귀 알림
    /// </summary>
    public void Close ()
    {
        if ( _isOpen == false )
            return;

        EndSelection( );
        OnCancelled?.Invoke( );
    }

    /// <summary>
    /// 선택 입력 종료와 패널 비활성화
    /// </summary>
    void EndSelection ()
    {
        _isOpen = false;
        _panel.Close( );
        _model.ClearSelection( );
    }
}
