using System;
using UnityEngine;

/// <summary>
/// 상점 중재자 - 구매, 저장, 화면 갱신 연결
/// </summary>
public class ShopPresenter : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] ShopView _view;        //상점 화면
    [SerializeField] ShopSound _sound;		//구매 결과 사운드

    ShopModel _model;        //구매 처리
    SaveManager _save;		//코인과 아이템 저장

    /// <summary>
    /// 상점 닫힘 알림
    /// </summary>
    public event Action OnClosed;

    /// <summary>
    /// 구매 완료 알림 이벤트(구매 성공 여부)
    /// </summary>
    public event Action<bool> OnPurchaseCompleted;


    /// <summary>
    /// 판매 목록과 저장 관리자 연결
    /// </summary>
    /// <param name="dataMap">판매 목록</param>
    /// <param name="save">저장 관리자</param>
    public void Init ( ShopDataMap dataMap, SaveManager save )
    {
        _save = save;

        _view.Init( dataMap.Data );
        _model = new ShopModel( dataMap );
        _view.Close( true );
        _sound.Init( this );
    }

    /// <summary>
    /// 구매와 상점 종료 요청 연결
    /// </summary>
    private void OnEnable ()
    {
        _view.OnPurchaseRequested += HandlePurchaseRequested;
        _view.OnCloseRequested += Close;
    }


    /// <summary>
    /// 구매 처리 후 저장과 결과 표시
    /// </summary>
    /// <param name="data">판매 아이템</param>
    /// <param name="count">구매 수량</param>
    void HandlePurchaseRequested ( SupportItemData data, int count )
    {
        //구매할 때마다 최신 잔액과 보유량 확인
        SupportItemSaveData save = _save.LoadSupportItems( );

        bool purchased = _model.TryPurchase(
            data.Id, count, save, out string message );		//구매 성공 여부

        //코인과 보유량 변경을 한 번에 저장
        if ( purchased == true )
            _save.SaveSupportItems( save );

        _view.Refresh( save );

        if ( purchased == true )
            _view.ResetPurchaseCount( data );

        //확정된 값으로 화면을 갱신한 뒤 결과 연출 요청
        _view.PlayPurchaseResult( data, purchased, message );

        //구매 결과 사운드는 기존 알림 경로 유지
        OnPurchaseCompleted?.Invoke( purchased );
    }

    /// <summary>
    /// 최신 저장 데이터로 잔액과 보유량 갱신
    /// </summary>
    public void Refresh ()
    {
        if ( _model == null )
            return;

        _view.Refresh( _save.LoadSupportItems( ) );
    }

    /// <summary>
    /// 저장 데이터를 불러와 상점 열기
    /// </summary>
    public void Open ()
    {
        //초기화 실패 시 상점 진입 차단
        if ( _model == null )
            return;

        SupportItemSaveData save = _save.LoadSupportItems( );
        _view.Open( save );
    }

    /// <summary>
    /// 상점 닫기와 복귀 알림
    /// </summary>
    public void Close ()
    {
        _view.Close( );
        OnClosed?.Invoke( );
    }

    private void OnDisable ()
    {
        //씬 해제 시 상점 화면이 먼저 파괴될 수 있음
        if ( _view == null )
            return;

        _view.OnPurchaseRequested -= HandlePurchaseRequested;
        _view.OnCloseRequested -= Close;
        _view.Close( true );
    }

}
