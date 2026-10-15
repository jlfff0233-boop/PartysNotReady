using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 서포트 아이템 슬롯 뷰 - 아이콘과 사용 횟수 표시
/// </summary>
public class SupportItemSlotView : MonoBehaviour
{
    [Header ( "----- 아이템 표시 -----" )]
    [SerializeField] Image _icon;		//아이템 아이콘
    [SerializeField] TMP_Text _usesCountText;		//남은 사용 횟수

    [Header ( "----- 소진 표시 -----" )]
    [SerializeField] Color _exhaustedColor = Color.gray;		//소진 아이콘 색상

    Color _baseColor;		//기본 아이콘 색상
    bool _isInitialized;		//기준 저장 여부

    /// <summary>
    /// 슬롯 표시 연결 여부
    /// </summary>
    public bool IsReady =>
        _icon != null && _usesCountText != null;


    /// <summary>
    /// 기본 아이콘 색상 저장
    /// </summary>
    void Init ( )
    {
        if ( _isInitialized == true )
            return;

        _baseColor = _icon.color;
        _isInitialized = true;
    }

    private void Awake ( )
    {
        Init ( );
        Clear ( );
    }

    private void OnDisable ( )
    {
        Clear ( );
    }


    /// <summary>
    /// 아이템 아이콘과 사용 횟수 표시
    /// </summary>
    /// <param name="item">표시할 서포트 아이템</param>
    public void SetItem ( SupportItem item )
    {
        if ( item == null )
        {
            Clear ( );
            return;
        }

        Init ( );

        if ( _icon.sprite != item.Data.Icon )
            _icon.sprite = item.Data.Icon;

        _icon.enabled = item.Data.Icon != null;

        //횟수가 소진되어도 진행 중인 효과는 기본 색상 유지
        bool hasEffect = item.IsReserved == true ||
            item.IsProductionActive == true || item.RemainingTime > 0f;

        bool isExhausted = item.IsPassive == false &&
            item.RemainingUses <= 0 && hasEffect == false;

        Color color = isExhausted == true
            ? _exhaustedColor : _baseColor;		//현재 아이콘 색상

        if ( _icon.color != color )
            _icon.color = color;

        SetText ( _usesCountText , item.IsPassive == true
            ? string.Empty : $"{item.RemainingUses}" );
    }

    /// <summary>
    /// 슬롯 아이템 표시 초기화
    /// </summary>
    public void Clear ( )
    {
        _icon.enabled = false;
        _icon.sprite = null;

        if ( _isInitialized == true )
            _icon.color = _baseColor;

        SetText ( _usesCountText , string.Empty );
    }

    /// <summary>
    /// 변경된 텍스트만 반영
    /// </summary>
    /// <param name="label">표시할 텍스트</param>
    /// <param name="text">표시 내용</param>
    void SetText ( TMP_Text label , string text )
    {
        if ( label.text != text )
            label.text = text;
    }

}
