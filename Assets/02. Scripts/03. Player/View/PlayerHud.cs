using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 플레이어 HUD - 상호작용 게이지, 탈출/구조 게이지, 안내 UI 표시
/// </summary>
public class PlayerHud : MonoBehaviour
{
    [Header( "----- 상호작용 -----" )]
    [SerializeField] Image _holdGauge;      //홀딩 진행률 이미지
    [SerializeField] GaugeEffect _holdEffect;		//홀딩 진행률 연출
    [SerializeField] TMP_Text _actionText;		//조준 또는 홀딩 중 행동 이름

    [Header( "----- 구조 -----" )]
    [SerializeField] Image _rescueGauge;        //구조 진행률 이미지
    [SerializeField] GameObject _rescueGuide;       //이미지와 TMP 안내 부모
    [SerializeField] GaugeEffect _rescueEffect;		//탈출/구조 진행률 연출
    [SerializeField] KeyPromptEffect _rescueKeyEffect;      //구조 키 이미지 연출

    [Header( "----- 레시피 -----" )]
    [SerializeField] RecipeGuide _recipeGuide;		//그릇 위치의 레시피 안내

    [Header( "----- 서포트 아이템 -----" )]
    [SerializeField] SupportItemSlotView [ ] _supportSlots;		//서포트 아이템 슬롯

    string _displayActionName;		//현재 표시한 행동 이름
    ICaptureTarget _rescueDisplayTarget;		//현재 구조 진행률 표시 대상

    private void Awake ()
    {
        //입력이 시작되기 전에는 진행 UI를 숨김
        ResetHoldGauge( );
        SetActionName( string.Empty );
        SetRescueHud( false, 0f );
        SetRecipeGuide( null );
        SetSupportItems( null );
    }

    private void OnDisable ()
    {
        //다시 활성화했을 때 이전 진행률과 안내가 남지 않도록 초기화
        ResetHoldGauge( );
        SetActionName( string.Empty );
        SetRescueHud( false, 0f );
        SetRecipeGuide( null );
        SetSupportItems( null );
    }


    /// <summary>
    /// 고정 슬롯에 서포트 아이템 표시
    /// </summary>
    /// <param name="items">보유한 서포트 아이템 목록</param>
    public void SetSupportItems ( PlayerSupportItems items )
    {
        if ( _supportSlots == null )
            return;

        for ( int i = 0; i < _supportSlots.Length; i++ )
        {
            SupportItemSlotView slot = _supportSlots [ i ];		//표시할 슬롯

            if ( slot == null )
                continue;

            SupportItem item = items != null && i < items.Count
                ? items.GetItem( i ) : null;		//슬롯에 표시할 아이템

            if ( item != null )
                slot.SetItem( item );
            else
                slot.Clear( );
        }
    }


    /// <summary>
    /// 레시피 안내 표시
    /// </summary>
    /// <param name="target">대상 그릇</param>
    public void SetRecipeGuide ( CookController target )
    {
        if ( _recipeGuide == null )
            return;

        _recipeGuide.SetTarget(
            isActiveAndEnabled == true ? target : null );
    }


    #region ----- UI 표시 -----

    /// <summary>
    /// E 키와 실행 가능한 행동 이름 표시 또는 숨김
    /// </summary>
    /// <param name="actionName">행동 이름, 빈 값이면 숨김</param>
    public void SetActionName ( string actionName )
    {
        bool visible = isActiveAndEnabled == true
            && string.IsNullOrEmpty( actionName ) == false;		//표시 여부

        string text = visible == true ? actionName : string.Empty;		//표시할 이름

        //행동 이름이 바뀔 때만 키 문구를 결합하여 표시
        if ( _displayActionName != text )
        {
            _displayActionName = text;
            _actionText.text = visible == true ? "[E] " + text : string.Empty;
        }

        _actionText.enabled = visible;
    }

    /// <summary>
    /// 홀딩 게이지만 숨김
    /// </summary>
    void ResetHoldGauge ()
    {
        _holdEffect.ResetEffect( );
        _holdGauge.enabled = false;
    }

    /// <summary>
    /// 홀딩 게이지 표시
    /// </summary>
    /// <param name="isHolding">게이지 표시 여부</param>
    /// <param name="progress">0~1 범위의 홀딩 진행률</param>
    public void SetHoldGauge ( bool isHolding, float progress )
    {
        if ( isHolding == false || isActiveAndEnabled == false )
        {
            ResetHoldGauge( );
            return;
        }

        //최초 표시와 새 시도의 시작값은 즉시 적용
        if ( _holdGauge.enabled == false || progress <= 0f )
            _holdEffect.SetProgressImmediate( progress );
        else
            _holdEffect.SetProgress( progress );

        _holdGauge.enabled = true;
    }


    /// <summary>
    /// 탈출/구조 게이지와 키 안내 표시
    /// </summary>
    /// <param name="isVisible">탈출/구조 UI 표시 여부</param>
    /// <param name="progress">0~1 범위의 탈출/구조 진행률</param>
    /// <param name="displayTarget">진행률 표시 대상</param>
    public void SetRescueHud (
        bool isVisible, float progress,
        ICaptureTarget displayTarget = null )
    {
        bool visible = isVisible && isActiveAndEnabled;		//실제 표시 여부

        if ( visible == false )
        {
            _rescueDisplayTarget = null;
            _rescueEffect.ResetEffect( );
            _rescueGauge.enabled = false;

            _rescueKeyEffect.SetVisible( false );
            _rescueGuide.SetActive( false );
            return;
        }

        bool targetChanged =
            ReferenceEquals( _rescueDisplayTarget, displayTarget ) == false;		//표시 대상 변경 여부

        //대상 변경과 진행률 초기화는 이전 표시값과 분리
        if ( _rescueGauge.enabled == false ||
             targetChanged == true ||
             progress <= 0f )
        {
            _rescueEffect.SetProgressImmediate( progress );
        }
        else
        {
            _rescueEffect.SetProgress( progress );
        }

        _rescueDisplayTarget = displayTarget;
        _rescueGauge.enabled = true;

        //부모를 표시한 뒤 키 이미지 연출 요청
        _rescueGuide.SetActive( true );
        _rescueKeyEffect.SetVisible( true );
    }

    #endregion


    /// <summary>
    /// 서포트 슬롯 표시 연결 확인
    /// </summary>
    /// <param name="count">서포트 아이템 보유 개수</param>
    /// <returns>표시 준비 여부</returns>
    public bool IsSupportReady ( int count )
    {
        if ( count == 0 )
            return true;

        if ( _supportSlots == null || _supportSlots.Length < count )
            return false;

        for ( int i = 0; i < count; i++ )
        {
            SupportItemSlotView slot = _supportSlots [ i ];		//표시할 슬롯

            if ( slot == null || slot.IsReady == false )
                return false;
        }

        return true;
    }

}
