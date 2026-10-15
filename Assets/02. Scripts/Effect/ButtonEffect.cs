using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼 연출 - 선택, 누름과 클릭 크기 반응
/// </summary>
[RequireComponent( typeof( Button ) )]
public class ButtonEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Button _button;		//입력 버튼
    [SerializeField] RectTransform _target;		//시각 연출 대상

    [Header( "----- 연출 -----" )]
    [SerializeField, Min( 1f )] float _selectedScale = 1.06f;		//선택 크기 배율
    [SerializeField, Range( 0.1f, 1f )] float _pressedScale = 0.94f;		//누름 크기 배율
    [SerializeField, Min( 1f )] float _clickScale = 1.12f;		//클릭 반동 배율
    [SerializeField, Min( 0.01f )] float _duration = 0.12f;     //전환 시간

    EffectPlayer _effect;     //연출 실행과 수명 관리
    Vector3 _baseScale;		//기준 크기
    bool _isInitialized;		//기준 저장 여부
    bool _isHovered;		//마우스 진입 여부
    bool _isSelected;		//키보드 선택 여부
    bool _isPressed;		//누름 여부
    bool _wasInteractable;      //이전 입력 허용 여부

    /// <summary>
    /// 연출 모듈 준비와 크기 기준 저장
    /// </summary>
    void Init ()
    {
        _effect ??= new EffectPlayer( gameObject );

        if ( _isInitialized == true )
            return;

        _baseScale = _target.localScale;
        _isInitialized = true;
    }

    private void OnEnable ()
    {
        Init( );

        _button.onClick.AddListener( PlayClick );
        _wasInteractable = _button.IsInteractable( );
    }

    private void OnDisable ()
    {
        _button.onClick.RemoveListener( PlayClick );
        ResetEffect( );
    }

    private void Update ()
    {
        bool canInteract = _button.IsInteractable( );		//현재 입력 허용 여부

        if ( _wasInteractable == canInteract )
            return;

        _wasInteractable = canInteract;

        if ( canInteract == false )
            ResetEffect( );
    }


    /// <summary>
    /// 현재 연출 중단
    /// </summary>
    void StopEffect ()
    {
        _effect?.Stop( );
    }

    /// <summary>
    /// 크기와 입력 상태 복구
    /// </summary>
    public void ResetEffect ()
    {
        StopEffect( );

        _isHovered = false;
        _isSelected = false;
        _isPressed = false;

        if ( _isInitialized == true && _target != null )
            _target.localScale = _baseScale;
    }

    private void Reset ()
    {
        _button = GetComponent<Button>( );
        _target = GetComponent<RectTransform>( );
    }


    /// <summary>
    /// 상태에 따른 표시 크기 전환
    /// </summary>
    void RefreshScale ()
    {
        if ( _button.IsInteractable( ) == false )
        {
            ResetEffect( );
            return;
        }

        float scale = _isPressed == true
            ? _pressedScale
            : ( _isHovered == true || _isSelected == true )
                ? _selectedScale : 1f;      //현재 입력 상태의 크기 배율

        //새 입력은 이전 전환을 교체하며 현재 크기에서 이어감
        _effect.Play( _target.DOScale(
            _baseScale * scale, _duration ).SetEase( Ease.OutQuad ) );
    }

    /// <summary>
    /// 마우스 진입 처리
    /// </summary>
    /// <param name="eventData">포인터 정보</param>
    public void OnPointerEnter ( PointerEventData eventData )
    {
        _isHovered = true;
        RefreshScale( );
    }

    /// <summary>
    /// 마우스 이탈 처리
    /// </summary>
    /// <param name="eventData">포인터 정보</param>
    public void OnPointerExit ( PointerEventData eventData )
    {
        _isHovered = false;
        _isPressed = false;
        RefreshScale( );
    }

    /// <summary>
    /// 왼쪽 버튼 누름 처리
    /// </summary>
    /// <param name="eventData">포인터 정보</param>
    public void OnPointerDown ( PointerEventData eventData )
    {
        if ( eventData.button != PointerEventData.InputButton.Left )
            return;

        _isPressed = true;
        RefreshScale( );
    }

    /// <summary>
    /// 왼쪽 버튼 해제 처리
    /// </summary>
    /// <param name="eventData">포인터 정보</param>
    public void OnPointerUp ( PointerEventData eventData )
    {
        if ( eventData.button != PointerEventData.InputButton.Left )
            return;

        _isPressed = false;
        RefreshScale( );
    }

    /// <summary>
    /// 키보드 선택 처리
    /// </summary>
    /// <param name="eventData">선택 정보</param>
    public void OnSelect ( BaseEventData eventData )
    {
        _isSelected = true;
        RefreshScale( );
    }

    /// <summary>
    /// 키보드 선택 해제 처리
    /// </summary>
    /// <param name="eventData">선택 정보</param>
    public void OnDeselect ( BaseEventData eventData )
    {
        _isSelected = false;
        _isPressed = false;
        RefreshScale( );
    }

    /// <summary>
    /// 클릭 확대와 복귀 연출 생성
    /// </summary>
    /// <returns>생성한 연출</returns>
    Tween CreateClickEffect ()
    {
        Init( );
        StopEffect( );

        float scale = _isHovered == true || _isSelected == true
            ? _selectedScale : 1f;      //클릭 후 복귀 크기

        Sequence sequence = DOTween.Sequence( );        //클릭 반동

        //짧게 확대하고 현재 선택 상태의 크기로 복귀
        sequence.Append( _target.DOScale(
            _baseScale * _clickScale, _duration * 0.4f ) );

        sequence.Append( _target.DOScale(
            _baseScale * scale, _duration ).SetEase( Ease.OutBack ) );

        _effect.Play( sequence );
        return _effect.Current;
    }

    /// <summary>
    /// 정상 클릭 반동 재생
    /// </summary>
    public void PlayClick ()
    {
        //앞선 클릭 처리에서 패널이 닫혔다면 연출을 다시 시작하지 않음
        if ( isActiveAndEnabled == false || _button.IsInteractable( ) == false )
            return;

        _isPressed = false;
        CreateClickEffect( );
    }

#if UNITY_EDITOR
    /// <summary>
    /// 실제 클릭 이벤트 없이 반동 미리보기
    /// </summary>
    /// <returns>미리보기 연출</returns>
    public Tween PreviewEffect ()
    {
        return CreateClickEffect( );
    }

    /// <summary>
    /// 미리보기 종료와 기준 재저장 준비
    /// </summary>
    public void EndPreview ()
    {
        ResetEffect( );
        _isInitialized = false;
    }
#endif
}
