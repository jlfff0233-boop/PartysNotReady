using DG.Tweening;
using UnityEngine;

/// <summary>
/// 키 안내 연출 - 키 이미지의 반복 눌림
/// </summary>
[RequireComponent( typeof( CanvasGroup ) )]
public class KeyPromptEffect : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] RectTransform _target;		//키 이미지
    [SerializeField] CanvasGroup _group;		//키 이미지 표시

    [Header( "----- 눌림 -----" )]
    [SerializeField] Vector2 _pressedOffset = new( 0f, -3f );		//눌림 위치 보정
    [SerializeField, Range( 0.1f, 1f )] float _pressedScale = 0.92f;		//눌림 크기
    [SerializeField, Min( 0.01f )] float _pressDuration = 0.08f;		//누름 시간
    [SerializeField, Min( 0.01f )] float _releaseDuration = 0.08f;		//복귀 시간
    [SerializeField, Min( 0f )] float _interval = 0.06f;		//다음 누름 대기

    EffectPlayer _effect;		//반복 연출 실행과 수명 관리
    Vector2 _basePosition;		//기준 위치
    Vector3 _baseScale;		//기준 크기
    float _baseAlpha;		//기준 알파
    bool _isInitialized;		//기준 저장 여부
    bool _isVisible;        //표시 요청 여부

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

    private void OnDisable ()
    {
        ResetEffect( );
    }


    /// <summary>
    /// 눌림 중단과 위치, 크기 복구
    /// </summary>
    public void StopEffect ()
    {
        _effect?.Stop( );

        if ( _isInitialized == false || _target == null )
            return;

        _target.anchoredPosition = _basePosition;
        _target.localScale = _baseScale;
    }

    /// <summary>
    /// 안내 연출 종료와 숨김
    /// </summary>
    public void ResetEffect ()
    {
        StopEffect( );
        _isVisible = false;

        if ( _isInitialized == true && _group != null )
            _group.alpha = 0f;
    }

    private void Reset ()
    {
        _target = GetComponent<RectTransform>( );
        _group = GetComponent<CanvasGroup>( );
    }


    /// <summary>
    /// 키 눌림과 복귀 반복
    /// </summary>
    void PlayPress ()
    {
        StopEffect( );

        Sequence sequence = DOTween.Sequence( );        //한 번의 눌림 흐름

        AppendPress( sequence, true, _pressDuration );
        AppendPress( sequence, false, _releaseDuration );

        //짧은 대기 뒤 처음부터 반복
        sequence.AppendInterval( _interval );
        sequence.SetLoops( -1, LoopType.Restart );

        _effect.Play( sequence );
    }

    /// <summary>
    /// 눌림 또는 복귀 구간 추가
    /// </summary>
    /// <param name="sequence">추가할 연출</param>
    /// <param name="isPressed">눌림 여부</param>
    /// <param name="duration">구간 시간</param>
    void AppendPress ( Sequence sequence, bool isPressed, float duration )
    {
        Vector2 position = isPressed == true
            ? _basePosition + _pressedOffset
            : _basePosition;        //구간 목표 위치

        Vector3 scale = isPressed == true
            ? _baseScale * _pressedScale
            : _baseScale;       //구간 목표 크기

        //위치와 크기를 같은 구간에 변경하여 눌림 표현을 맞춤
        sequence.Append( _target.DOAnchorPos(
            position, duration ).SetEase( Ease.OutQuad ) );

        sequence.Join( _target.DOScale(
            scale, duration ).SetEase( Ease.OutQuad ) );
    }

    /// <summary>
    /// 키 이미지 표시와 반복 눌림 설정
    /// </summary>
    /// <param name="isVisible">표시 여부</param>
    public void SetVisible ( bool isVisible )
    {
        Init( );

        //최초 숨김 요청도 반복 요청 검사 전에 알파를 적용
        _group.alpha = isVisible == true ? _baseAlpha : 0f;

        //구조 HUD의 매 프레임 갱신에서도 같은 연출을 유지
        if ( _isVisible == isVisible &&
             ( isVisible == false || _effect.IsPlaying == true ) )
            return;

        _isVisible = isVisible;

        if ( isVisible == true )
            PlayPress( );
        else
            StopEffect( );
    }

#if UNITY_EDITOR
    /// <summary>
    /// 활성 상태를 변경하지 않고 눌림 미리보기
    /// </summary>
    /// <returns>미리보기 연출</returns>
    public Tween PreviewEffect ()
    {
        Init( );
        _group.alpha = _baseAlpha;

        PlayPress( );
        return _effect.Current;
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
