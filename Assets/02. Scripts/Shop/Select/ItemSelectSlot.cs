using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 선택 슬롯 - 보유량과 선택 상태 출력
/// </summary>
public class ItemSelectSlot : MonoBehaviour
{
    [Header( "----- 아이템 정보 -----" )]
    [SerializeField] Image _icon;        //아이템 아이콘
    [SerializeField] TMP_Text _nameText;        //아이템 이름
    [SerializeField] TMP_Text _ownedText;        //보유 수량

    [Header( "----- 선택 -----" )]
    [SerializeField] Button _button;        //아이템 선택
    [SerializeField] Outline _selectionOutline;        //선택 테두리

    SupportItemData _data;        //슬롯 아이템

    /// <summary>
    /// 슬롯 아이템
    /// </summary>
    public SupportItemData Data => _data;

    /// <summary>
    /// 아이템 선택 요청
    /// </summary>
    public event Action<SupportItemData> OnSelectRequested;


    /// <summary>
    /// 아이템 정보 초기화
    /// </summary>
    /// <param name="data">슬롯 아이템</param>
    /// <param name="count">보유 수량</param>
    public void Init ( SupportItemData data, int count )
    {
        _data = data;
        _icon.sprite = data.Icon;
        _ownedText.text = $"보유 {count:N0}개";
        SetSelection( 0 );
    }

    void OnEnable ()
    {
        _button.onClick.AddListener( HandleClicked );
    }

    void OnDisable ()
    {
        _button.onClick.RemoveListener( HandleClicked );
    }


    /// <summary>
    /// 아이템 선택 요청 전달
    /// </summary>
    void HandleClicked ()
    {
        OnSelectRequested?.Invoke( _data );
    }

    /// <summary>
    /// 선택 테두리와 순서 갱신
    /// </summary>
    /// <param name="order">선택 순서</param>
    public void SetSelection ( int order )
    {
        _selectionOutline.enabled = order > 0;
        _nameText.text = order > 0
            ? $"{order}. {_data.ItemName}"
            : _data.ItemName;
    }
}