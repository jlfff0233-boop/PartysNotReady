using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 텍스트 자동 재생 연출 종류
/// </summary>
public enum TextEffectType
{
    Emphasis,       //크기 강조
    Failure,        //실패 표시
    Blink,          //반복 점멸
    None            //자동 재생 없음
}

/// <summary>
/// 텍스트 연출 - 강조, 실패와 점멸
/// </summary>
[RequireComponent( typeof( CanvasGroup ) )]
public class TextEffect : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] RectTransform _target;		//텍스트 시각 대상
    [SerializeField] CanvasGroup _group;		//텍스트 알파

    [Header( "----- 자동 재생 -----" )]
    [SerializeField] TextEffectType _autoEffect = TextEffectType.Emphasis;     //활성화 시 연출

    [Header( "----- 강조 -----" )]
    [SerializeField, Min( 1f )] float _emphasisScale = 1.2f;		//강조 크기 배율
    [SerializeField, Min( 0.01f )] float _emphasisDuration = 0.25f;		//강조 시간

    [Header( "----- 실패 -----" )]
    [SerializeField] float _failureAngle = -12f;		//실패 기울기
    [SerializeField, Min( 0f )] float _shakeAngle = 6f;		//흔들림 각도
    [SerializeField, Min( 1 )] int _shakeCount = 3;		//흔들림 횟수
    [SerializeField, Min( 0.01f )] float _shakeDuration = 0.3f;		//흔들림 시간
    [SerializeField, Min( 0.01f )] float _fadeDuration = 0.2f;		//사라짐 시간

    [Header( "----- 점멸 -----" )]
    [SerializeField, Range( 0f, 1f )] float _blinkAlpha = 0.35f;		//점멸 최저 알파
    [SerializeField, Min( 0.01f )] float _blinkDuration = 0.35f;		//점멸 편도 시간

    EffectPlayer _effect;		//연출 실행과 수명 관리
    Vector3 _baseScale;		//기준 크기
    Quaternion _baseRotation;		//기준 회전
    float _baseAlpha;		//기준 알파
    bool _isInitialized;		//기준 저장 여부
    bool _isBlinking;		//점멸 요청 여부
    bool _isPlayingFailure;		//알림을 전달할 실패 연출 진행 여부

    /// <summary>
    /// 강조 연출 시간
    /// </summary>
    public float EmphasisDuration => _emphasisDuration;

    /// <summary>
    /// 실패 연출 종료 알림 이벤트
    /// </summary>
    public event Action OnFailEnd;

    /// <summary>
    /// 실패 연출 취소 알림 이벤트
    /// </summary>
    public event Action OnFailCancel;

    /// <summary>
    /// 연출 모듈 준비와 표시 기준 저장
    /// </summary>
    void Init ()
    {
        _effect ??= new EffectPlayer( gameObject );

        if ( _isInitialized == true )
            return;

        _baseScale = _target.localScale;
        _baseRotation = _target.localRotation;
        _baseAlpha = _group.alpha;
        _isInitialized = true;
    }

    private void OnEnable ()
    {
        PlayAutoEffect( true );
    }

    private void OnDisable ()
    {
        ResetEffect( );
    }


    /// <summary>
    /// 현재 연출 중단
    /// </summary>
    public void StopEffect ()
    {
        bool wasPlayingFailure = _isPlayingFailure;		//취소할 실패 연출 여부
        _isPlayingFailure = false;
        _effect?.Stop( );
        _isBlinking = false;

        if ( wasPlayingFailure == true )
            OnFailCancel?.Invoke( );
    }

    /// <summary>
    /// 연출 중단과 표시 기준 복구
    /// </summary>
    public void ResetEffect ()
    {
        StopEffect( );

        if ( _isInitialized == false )
            return;

        if ( _target != null )
        {
            _target.localScale = _baseScale;
            _target.localRotation = _baseRotation;
        }

        if ( _group != null )
            _group.alpha = _baseAlpha;
    }

    private void Reset ()
    {
        _target = GetComponent<RectTransform>( );
        _group = GetComponent<CanvasGroup>( );
    }


    /// <summary>
    /// 설정한 자동 연출 재생
    /// </summary>
    /// <param name="notifyFailure">실패 완료 이벤트 호출 여부</param>
    void PlayAutoEffect ( bool notifyFailure )
    {
        switch ( _autoEffect )
        {
            case TextEffectType.Emphasis:
                PlayEmphasis( );
                break;
            case TextEffectType.Failure:
                if ( notifyFailure == true )
                    PlayFailure( );
                else
                {
                    Init( );
                    ResetEffect( );
                    _effect.Play( CreateFailure( ) );
                }
                break;
            case TextEffectType.Blink:
                StartBlink( );
                break;
            case TextEffectType.None:
                ResetEffect( );
                break;
        }
    }

    /// <summary>
    /// 텍스트 크기 강조
    /// </summary>
    /// <param name="completed">강조 종료 처리</param>
    public void PlayEmphasis ( Action completed )
    {
        Init( );
        ResetEffect( );

        Sequence sequence = CreateEmphasis( );		//강조 흐름
        sequence.OnComplete( () => completed?.Invoke( ) );

        _effect.Play( sequence );
    }

    /// <summary>
    /// 텍스트 크기 강조
    /// </summary>
    public void PlayEmphasis ()
    {
        PlayEmphasis( null );
    }

    /// <summary>
    /// 실패 기울임, 흔들림과 사라짐 재생
    /// </summary>
    public void PlayFailure ()
    {
        Init( );
        ResetEffect( );

        Sequence sequence = CreateFailure( );     //실패 표시 흐름
        _isPlayingFailure = true;
        sequence.OnComplete( () =>
        {
            _isPlayingFailure = false;
            OnFailEnd?.Invoke( );
        } );
        _effect.Play( sequence );
    }

    /// <summary>
    /// 실패 기울임, 흔들림과 사라짐 연출 생성
    /// </summary>
    /// <returns>생성한 연출</returns>
    Sequence CreateFailure ()
    {
        Sequence sequence = DOTween.Sequence( );        //실패 표시 흐름

        //원래 회전을 기준으로 실패 방향에 기울임
        sequence.Append( _target.DOLocalRotateQuaternion(
            GetRotation( _failureAngle ), 0.08f ).SetEase( Ease.OutQuad ) );

        int steps = _shakeCount * 2;        //좌우 편도 전환 수
        float stepDuration = _shakeDuration / steps;        //편도 흔들림 시간

        for ( int i = 0; i < steps; i++ )
        {
            //짝수와 홀수에서 기울기의 부호를 번갈아 적용
            float offset = i % 2 == 0
                ? _shakeAngle : -_shakeAngle;       //이번 흔들림 보정

            sequence.Append( _target.DOLocalRotateQuaternion(
                GetRotation( _failureAngle + offset ),
                stepDuration ).SetEase( Ease.InOutSine ) );
        }

        //흔들림이 끝난 다음 알파를 줄임
        sequence.Append( _group.DOFade( 0f, _fadeDuration ) );

        return sequence;
    }

    /// <summary>
    /// 알파 점멸 시작
    /// </summary>
    public void StartBlink ()
    {
        Init( );

        //같은 반복 요청으로 점멸 주기를 다시 시작하지 않음
        if ( _isBlinking == true && _effect.IsPlaying == true )
            return;

        ResetEffect( );
        _isBlinking = true;

        //최저 알파까지 감소한 뒤 Yoyo로 원래 알파에 복귀
        _effect.Play( _group.DOFade(
            _baseAlpha * _blinkAlpha, _blinkDuration )
            .SetEase( Ease.InOutSine )
            .SetLoops( -1, LoopType.Yoyo ) );
    }

    /// <summary>
    /// 기준 회전에 기울기 적용
    /// </summary>
    /// <param name="angle">기준 회전에 더할 각도</param>
    /// <returns>목표 회전</returns>
    Quaternion GetRotation ( float angle )
    {
        //텍스트의 원래 회전을 유지한 채 로컬 Z축 기울기만 추가
        return _baseRotation * Quaternion.Euler( 0f, 0f, angle );
    }

    /// <summary>
    /// 강조 확대와 복귀 연출 생성
    /// </summary>
    /// <returns>생성한 연출</returns>
    Sequence CreateEmphasis ()
    {
        Sequence sequence = DOTween.Sequence( );        //강조 흐름

        //짧게 확대하고 남은 시간에 기준 크기로 복귀
        sequence.Append( _target.DOScale(
            _baseScale * _emphasisScale,
            _emphasisDuration * 0.35f ).SetEase( Ease.OutQuad ) );

        sequence.Append( _target.DOScale(
            _baseScale,
            _emphasisDuration * 0.65f ).SetEase( Ease.OutBack ) );

        return sequence;
    }


#if UNITY_EDITOR
    /// <summary>
    /// 기능 이벤트 없이 자동 재생 연출 미리보기
    /// </summary>
    /// <returns>미리보기 연출</returns>
    public Tween PreviewEffect ()
    {
        PlayAutoEffect( false );
        return _effect == null ? null : _effect.Current;
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
