using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 아이템 슬롯 - 상품 정보와 구매 수량 입력
/// </summary>
public class ShopItemSlot : MonoBehaviour
{
    [Header( "----- 상품 정보 -----" )]
    [SerializeField] Image _icon;        //아이템 아이콘
    [SerializeField] TMP_Text _nameText;        //아이템 이름
    [SerializeField] TMP_Text _ownedText;        //보유 수량
    [SerializeField] TMP_Text _buyText;        //구매 버튼 텍스트

    [Header( "----- 구매 결과 연출 -----" )]
    [SerializeField] TextEffect _nameEffect;		//상품 이름 강조
    [SerializeField] TextEffect _ownedEffect;		//보유 수량 강조

    [Header( "----- 수량 입력 -----" )]
    [SerializeField] TMP_InputField _countInput;        //구매 수량 입력
    [SerializeField] Button _minusButton;        //수량 감소
    [SerializeField] Button _plusButton;        //수량 증가

    [Header( "----- 구매 -----" )]
    [SerializeField] Button _buyButton;        //구매 요청

    SupportItemData _data;        //판매 아이템
    int _count;        //구매 수량
    int _maxCount = ShopModel.MaxPurchaseCount;        //현재 구매 가능 수량

    /// <summary>
    /// 판매 아이템
    /// </summary>
    public SupportItemData Data => _data;

    /// <summary>
    /// 현재 구매 수량
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 남은 보유 공간을 반영한 구매 상한
    /// </summary>
    public int MaxCount => _maxCount;

    /// <summary>
    /// 수량 팝업 위치 기준
    /// </summary>
    public RectTransform CountAnchor => ( RectTransform ) _countInput.transform;

    /// <summary>
    /// 수량 편집 요청
    /// </summary>
    public event Action<ShopItemSlot> OnCountSelected;

    /// <summary>
    /// 구매 수량 변경 알림
    /// </summary>
    public event Action<ShopItemSlot> OnCountChanged;

    /// <summary>
    /// 구매 요청(아이템, 수량)
    /// </summary>
    public event Action<SupportItemData, int> OnPurchaseRequested;


    /// <summary>
    /// 상품 정보 초기화
    /// </summary>
    /// <param name="data">판매 아이템</param>
    public void Init ( SupportItemData data )
    {
        _data = data;
        _icon.sprite = data.Icon;
        _nameText.text = data.ItemName;
        ResetCount( );
    }

    void Awake ()
    {
        //정수 수량 입력 설정
        _countInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        _countInput.characterLimit = 3;

        //수량 입력 연결
        _countInput.onSelect.AddListener( HandleCountSelected );
        _countInput.onValueChanged.AddListener( HandleCountInput );
        _countInput.onEndEdit.AddListener( HandleCountEndEdit );

        //버튼도 공통 수량 변경 함수 사용
        _minusButton.onClick.AddListener( () => SetCount( _count - 1 ) );
        _plusButton.onClick.AddListener( () => SetCount( _count + 1 ) );
        _buyButton.onClick.AddListener( HandleBuyClicked );

    }


    #region ----- 핸들 -----

    /// <summary>
    /// 수량 입력 선택 요청 전달
    /// </summary>
    /// <param name="value">현재 입력값</param>
    void HandleCountSelected ( string value )
    {
        OnCountSelected?.Invoke( this );
    }

    /// <summary>
    /// 직접 입력한 수량 반영
    /// </summary>
    /// <param name="value">입력값</param>
    void HandleCountInput ( string value )
    {
        //편집 중 빈 입력은 완료 시 처리
        if ( int.TryParse( value, out int count ) == true )
            SetCount( count );
    }

    /// <summary>
    /// 편집 완료 후 구매 수량 확정
    /// </summary>
    /// <param name="value">입력값</param>
    void HandleCountEndEdit ( string value )
    {
        if ( int.TryParse( value, out int count ) == false )
            count = ShopModel.MinPurchaseCount;

        SetCount( count );
    }

    /// <summary>
    /// 현재 수량으로 구매 요청
    /// </summary>
    void HandleBuyClicked ()
    {
        //편집 중인 입력도 확정한 뒤 요청
        HandleCountEndEdit( _countInput.text );
        OnPurchaseRequested?.Invoke( _data, _count );
    }

    #endregion

    #region ----- 수량 갱신 -----

    /// <summary>
    /// 수량과 구매 총가격 갱신
    /// </summary>
    /// <param name="count">구매 수량</param>
    public void SetCount ( int count )
    {
        bool canPurchase = _maxCount > 0;
        _count = canPurchase
            ? Mathf.Clamp( count, ShopModel.MinPurchaseCount, _maxCount )
            : 0;

        //입력 이벤트를 다시 호출하지 않고 값 동기화
        _countInput.SetTextWithoutNotify( _count.ToString( ) );

        //수량 범위에 따라 증감 버튼 입력 제한
        _minusButton.interactable = _count > ShopModel.MinPurchaseCount;
        _plusButton.interactable = _count < _maxCount;
        _countInput.interactable = canPurchase;
        _buyButton.interactable = canPurchase;

        long totalCost = ShopModel.GetTotalCost( _data, _count );
        _buyText.text = canPurchase ? $"{totalCost:N0} 코인" : "보유 한도 도달";
        OnCountChanged?.Invoke( this );
    }

    /// <summary>
    /// 구매 수량 초기화
    /// </summary>
    public void ResetCount ()
    {
        SetCount( ShopModel.MinPurchaseCount );
    }

    /// <summary>
    /// 보유 수량과 구매 상한 갱신
    /// </summary>
    /// <param name="count">보유 수량</param>
    public void SetOwnedCount ( int count )
    {
        _ownedText.text = $"보유 {count:N0} / {SupportItemInventory.MaxCount}";
        _maxCount = Mathf.Clamp(
            SupportItemInventory.MaxCount - count, 0, ShopModel.MaxPurchaseCount );
        SetCount( _count );
    }

    #endregion


    /// <summary>
    /// 구매 결과 연출 중단과 표시 기준 복구
    /// </summary>
    public void ResetPurchaseEffect ()
    {
        _nameEffect.ResetEffect( );
        _ownedEffect.ResetEffect( );
    }

    /// <summary>
    /// 구매한 상품과 보유 수량 강조
    /// </summary>
    public void PlayPurchaseCompleted ()
    {
        ResetPurchaseEffect( );

        _nameEffect.PlayEmphasis( );
        _ownedEffect.PlayEmphasis( );
    }


    /// <summary>
    /// 수량 조절 UI 선택 여부 확인
    /// </summary>
    /// <param name="selected">선택된 오브젝트</param>
    /// <returns>수량 조절 UI 여부</returns>
    public bool IsCountSelected ( GameObject selected )
    {
        if ( selected == null )
            return false;

        Transform target = selected.transform;

        return target.IsChildOf( _countInput.transform ) ||
               selected == _minusButton.gameObject ||
               selected == _plusButton.gameObject;
    }


}
