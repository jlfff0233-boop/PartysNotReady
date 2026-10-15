using DG.Tweening;
using UnityEngine;
using System;

/// <summary>
/// 패널 연출 종류
/// </summary>
public enum PanelEffectType
{
    None,       //즉시 표시
    Slide,      //이동
    Fade,       //페이드
    SlideFade,  //이동과 페이드
    Pop,        //반동 확대
    ZoomFade    //확대와 페이드
}

/// <summary>
/// 패널 연출 - 등장, 퇴장과 입력 차단
/// </summary>
[RequireComponent( typeof( CanvasGroup ) )]
public class PanelEffect : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] RectTransform _target;		//시각 연출 대상
    [SerializeField] CanvasGroup _group;		//표시와 입력 제어

    [Header( "----- 연출 -----" )]
    [SerializeField] PanelEffectType _showEffect = PanelEffectType.SlideFade;		//등장 종류
    [SerializeField] PanelEffectType _hideEffect = PanelEffectType.SlideFade;		//퇴장 종류
    [SerializeField] Vector2 _hiddenOffset = new( 0f, -80f );		//숨김 위치 보정
    [SerializeField, Range( 0.01f, 1f )] float _hiddenScale = 0.85f;		//숨김 크기 배율
    [SerializeField, Min( 0.01f )] float _showDuration = 0.25f;		//등장 시간
    [SerializeField, Min( 0.01f )] float _hideDuration = 0.18f;		//퇴장 시간
    [SerializeField] Ease _showEase = Ease.OutCubic;		//등장 보간
    [SerializeField] Ease _hideEase = Ease.InCubic;     //퇴장 보간

    EffectPlayer _effect;     //연출 실행과 수명 관리
    Vector2 _basePosition;		//표시 기준 위치
    Vector3 _baseScale;		//표시 기준 크기
    float _baseAlpha;		//표시 기준 알파
    bool _isInitialized;		//기준 저장 여부
    bool _hasRequest;		//표시 요청 여부
    bool _isVisible;		//논리 표시 여부
    bool _isShowCompleted;		//등장 완료 여부

    /// <summary>
    /// 논리 표시 여부
    /// </summary>
    public bool IsVisible => _isVisible;

    /// <summary>
    /// 패널 등장 완료 여부
    /// </summary>
    public bool IsShowCompleted => _isVisible == true && _isShowCompleted == true;

    /// <summary>
    /// 패널 등장 완료 알림 이벤트
    /// </summary>
    public event Action OnOpened;

    /// <summary>
    /// 연출 모듈 준비와 표시 기준 저장
    /// </summary>
    void Init ()
    {
        _effect ??= new EffectPlayer( gameObject );

        if ( _isInitialized == true )
            return;

        _basePosition = _target.anchoredPosition;
        _baseScale = _target.localScale;
        _baseAlpha = _group.alpha;
        _isInitialized = true;
    }

    /// <summary>
    /// 패널 활성화 시 등장 연출 자동 재생
    /// </summary>
    private void OnEnable ()
    {
        Show( );
    }

    private void OnDisable ()
    {
        ResetEffect( );
    }


    /// <summary>
    /// 부착된 패널 연출로 표시 상태 적용
    /// </summary>
    /// <param name="target">표시할 패널</param>
    /// <param name="visible">표시 여부</param>
    /// <param name="immediate">초기화처럼 즉시 적용할지 여부</param>
    public static void SetVisible ( GameObject target, bool visible, bool immediate = false )
    {
        //연출이 없는 화면은 기존 즉시 표시를 유지
        if ( target.TryGetComponent( out PanelEffect effect ) == false )
        {
            target.SetActive( visible );
            return;
        }

        if ( immediate == true )
            effect.SetVisibleImmediate( visible );
        else if ( visible == true )
        {
            //등장은 OnEnable에서 실행하며, 아직 활성 상태인 퇴장 중 재열기만 이어서 처리
            target.SetActive( true );
            if ( target.activeInHierarchy == true && effect.IsVisible == false )
                effect.Show( );
        }
        else
            effect.Hide( );
    }


    /// <summary>
    /// 표시 기준 복구
    /// </summary>
    void RestoreVisual ()
    {
        _target.anchoredPosition = _basePosition;
        _target.localScale = _baseScale;
        _group.alpha = _baseAlpha;
    }

    /// <summary>
    /// 현재 연출 중단
    /// </summary>
    public void StopEffect ()
    {
        //초기화 전에 비활성화된 경우에는 실행 모듈이 없음
        _effect?.Stop( );
    }

    /// <summary>
    /// 숨김 상태의 위치 계산
    /// </summary>
    /// <param name="type">연출 종류</param>
    /// <returns>숨김 위치</returns>
    Vector2 GetHiddenPosition ( PanelEffectType type )
    {
        bool moves = type == PanelEffectType.Slide ||
            type == PanelEffectType.SlideFade;      //이동 적용 여부

        return moves == true
            ? _basePosition + _hiddenOffset
            : _basePosition;
    }

    /// <summary>
    /// 숨김 상태의 크기 계산
    /// </summary>
    /// <param name="type">연출 종류</param>
    /// <returns>숨김 크기</returns>
    Vector3 GetHiddenScale ( PanelEffectType type )
    {
        bool scales = type == PanelEffectType.Pop ||
            type == PanelEffectType.ZoomFade;       //크기 적용 여부

        //원래 크기에 배율을 곱해 비정규 크기의 UI도 유지
        return scales == true
            ? _baseScale * _hiddenScale
            : _baseScale;
    }

    /// <summary>
    /// 숨김 상태의 알파 계산
    /// </summary>
    /// <param name="type">연출 종류</param>
    /// <returns>숨김 알파</returns>
    float GetHiddenAlpha ( PanelEffectType type )
    {
        bool fades = type == PanelEffectType.Fade ||
            type == PanelEffectType.SlideFade ||
            type == PanelEffectType.ZoomFade;       //페이드 적용 여부

        return fades == true ? 0f : _baseAlpha;
    }

    /// <summary>
    /// 비활성화 경계에서 연출과 표시 복구
    /// </summary>
    void ResetEffect ()
    {
        StopEffect( );

        if ( _isInitialized == true )
            RestoreVisual( );

        _hasRequest = false;
        _isVisible = false;
        _isShowCompleted = false;
    }

    private void Reset ()
    {
        _target = GetComponent<RectTransform>( );
        _group = GetComponent<CanvasGroup>( );
    }


    /// <summary>
    /// 지정 효과의 위치, 크기와 알파 전환
    /// </summary>
    /// <param name="type">연출 종류</param>
    /// <param name="isVisible">등장 여부</param>
    /// <param name="duration">전환 시간</param>
    /// <param name="ease">보간 종류</param>
    /// <returns>생성한 연출</returns>
    Sequence CreateEffect (
        PanelEffectType type,
        bool isVisible,
        float duration,
        Ease ease )
    {
        Vector2 position = isVisible == true
            ? _basePosition : GetHiddenPosition( type );        //목표 위치

        Vector3 scale = isVisible == true
            ? _baseScale : GetHiddenScale( type );      //목표 크기

        float alpha = isVisible == true
            ? _baseAlpha : GetHiddenAlpha( type );      //목표 알파

        Ease scaleEase = type == PanelEffectType.Pop && isVisible == true
            ? Ease.OutBack : ease;      //등장 반동 보간

        Sequence sequence = DOTween.Sequence( );        //이번 패널 전환

        //세 표시값을 같은 시간에 전환하여 효과 교체 시에도 기준값으로 복귀
        sequence.Join( _target.DOAnchorPos( position, duration ).SetEase( ease ) );
        sequence.Join( _target.DOScale( scale, duration ).SetEase( scaleEase ) );
        sequence.Join( _group.DOFade( alpha, duration ).SetEase( ease ) );

        return sequence;
    }

    /// <summary>
    /// 등장 효과의 시작값 적용
    /// </summary>
    /// <param name="type">등장 종류</param>
    void SetHiddenVisual ( PanelEffectType type )
    {
        //전환과 시작값 설정이 같은 계산 함수를 사용
        _target.anchoredPosition = GetHiddenPosition( type );
        _target.localScale = GetHiddenScale( type );
        _group.alpha = GetHiddenAlpha( type );
    }

    /// <summary>
    /// 패널 등장
    /// </summary>
    public void Show ()
    {
        //비활성 패널은 활성화로 OnEnable이 등장 연출을 시작
        if ( gameObject.activeSelf == false )
        {
            gameObject.SetActive( true );
            return;
        }

        Init( );

        //같은 표시 요청으로 연출을 다시 시작하지 않음
        if ( _hasRequest == true && _isVisible == true &&
             ( _isShowCompleted == true || _effect.IsPlaying == true ) )
            return;

        bool wasHidden = _hasRequest == false;       //숨김 상태에서 시작 여부

        StopEffect( );

        _hasRequest = true;
        _isVisible = true;
        _isShowCompleted = false;

        //버튼 입력은 등장 완료 후 허용
        _group.interactable = false;
        _group.blocksRaycasts = true;

        if ( _showEffect == PanelEffectType.None )
        {
            SetVisibleImmediate( true );
            return;
        }

        //퇴장 중 재열기는 현재 표시값에서 이어감
        if ( wasHidden == true )
            SetHiddenVisual( _showEffect );

        Sequence sequence = CreateEffect(
            _showEffect, true, _showDuration, _showEase );      //등장 연출

        sequence.OnComplete( () =>
        {
            RestoreVisual( );
            _group.interactable = true;
            _isShowCompleted = true;
            OnOpened?.Invoke( );
        } );

        _effect.Play( sequence );
    }

    /// <summary>
    /// 입력 차단 후 패널 퇴장
    /// </summary>
    public void Hide ()
    {
        Init( );

        if ( gameObject.activeSelf == false ||
             ( _hasRequest == true && _isVisible == false ) )
            return;

        StopEffect( );

        _hasRequest = true;
        _isVisible = false;
        _isShowCompleted = false;

        //퇴장 중 버튼 실행과 아래 UI로의 클릭 통과를 차단
        _group.interactable = false;
        _group.blocksRaycasts = true;

        if ( _hideEffect == PanelEffectType.None )
        {
            SetVisibleImmediate( false );
            return;
        }

        Sequence sequence = CreateEffect(
            _hideEffect, false, _hideDuration, _hideEase );     //퇴장 연출

        //새 Show 요청은 이 연출을 Kill하므로 이전 완료는 실행되지 않음
        sequence.OnComplete( () => gameObject.SetActive( false ) );

        _effect.Play( sequence );
    }


    /// <summary>
    /// 연출 없이 표시 상태 적용
    /// </summary>
    /// <param name="isVisible">표시 여부</param>
    public void SetVisibleImmediate ( bool isVisible )
    {
        Init( );
        StopEffect( );
        RestoreVisual( );

        //활성화 전에 즉시 표시 상태를 지정하여 OnEnable에서 등장하지 않도록 처리
        _hasRequest = isVisible;
        _isVisible = isVisible;
        _isShowCompleted = isVisible;
        _group.interactable = isVisible;
        _group.blocksRaycasts = isVisible;

        gameObject.SetActive( isVisible );

        if ( isVisible == true && gameObject.activeInHierarchy == true )
            OnOpened?.Invoke( );
    }

#if UNITY_EDITOR
    /// <summary>
    /// 기능 상태를 변경하지 않고 등장 미리보기
    /// </summary>
    /// <returns>미리보기 연출</returns>
    public Tween PreviewEffect ()
    {
        Init( );
        StopEffect( );
        SetHiddenVisual( _showEffect );

        if ( _showEffect == PanelEffectType.None )
        {
            RestoreVisual( );
            return null;
        }

        _effect.Play( CreateEffect(
            _showEffect, true, _showDuration, _showEase ) );

        return _effect.Current;
    }

    /// <summary>
    /// 미리보기 종료와 기준 재저장 준비
    /// </summary>
    public void EndPreview ()
    {
        ResetEffect( );

        //Editor가 원래 표시값을 복구한 뒤 다음 호출에서 기준을 다시 저장
        _isInitialized = false;
    }
#endif
}
