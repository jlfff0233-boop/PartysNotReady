using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게이지 연출 - 표시 진행률 보간과 완료 반응
/// </summary>
public class GaugeEffect : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Image _fill;		//채움 이미지
    [SerializeField] RectTransform _target;		//완료 강조 대상

    [Header( "----- 연출 -----" )]
    [SerializeField, Min( 0.01f )] float _duration = 0.1f;		//진행률 보간 시간
    [SerializeField, Min( 1f )] float _completedScale = 1.08f;		//완료 강조 배율
    [SerializeField, Min( 0.01f )] float _completedDuration = 0.2f;     //완료 강조 시간

    EffectPlayer _fillEffect;     //진행률 보간 실행
    EffectPlayer _completedEffect;        //완료 강조 실행
    float _targetProgress;		//최신 표시 목표값
    Vector3 _baseScale;		//기준 크기
    bool _isInitialized;        //기준 저장 여부

    /// <summary>
    /// 연출 모듈 준비와 표시 기준 저장
    /// </summary>
    void Init ()
    {
        //진행률과 완료 강조는 서로 중단하지 않도록 별도 실행
        _fillEffect ??= new EffectPlayer( gameObject );
        _completedEffect ??= new EffectPlayer( gameObject );

        if ( _isInitialized == true )
            return;

        _baseScale = _target.localScale;
        _targetProgress = _fill.fillAmount;
        _isInitialized = true;
    }

    private void OnDisable ()
    {
        ResetEffect( );
    }


    /// <summary>
    /// 진행률과 완료 강조 중단
    /// </summary>
    void StopEffect ()
    {
        _fillEffect?.Stop( );
        _completedEffect?.Stop( );
    }

    /// <summary>
    /// 완료 강조 대상의 기준 크기 복구
    /// </summary>
    void RestoreScale ()
    {
        if ( _isInitialized == true && _target != null )
            _target.localScale = _baseScale;
    }

    /// <summary>
    /// 연출 중단과 진행률 초기화
    /// </summary>
    public void ResetEffect ()
    {
        StopEffect( );
        _targetProgress = 0f;

        if ( _fill != null )
            _fill.fillAmount = 0f;

        RestoreScale( );
    }

    private void Reset ()
    {
        _fill = GetComponent<Image>( );
        _target = GetComponent<RectTransform>( );
    }


    /// <summary>
    /// 최신 목표 진행률로 표시 보간
    /// </summary>
    /// <param name="progress">0부터 1 사이의 진행률</param>
    public void SetProgress ( float progress )
    {
        Init( );

        _targetProgress = Mathf.Clamp01( progress );

        //실행 중에는 목표값만 갱신하고 Tween을 새로 만들지 않음
        if ( _fillEffect.IsPlaying == true )
            return;

        if ( Mathf.Approximately( _fill.fillAmount, _targetProgress ) == true )
            return;

        float previousRatio = 0f;       //이전 콜백의 보간 진행률

        Tween tween = DOVirtual.Float( 0f, 1f, _duration, ratio =>
        {
            //전체 진행률 증가분을 남은 구간에 대한 비율로 변환
            float remaining = 1f - previousRatio;       //이전 시점의 남은 구간
            float weight = remaining > 0f
                ? ( ratio - previousRatio ) / remaining
                : 1f;       //이번 콜백에서 적용할 비율

            //고정 시작값이 아닌 현재 표시값에서 최신 목표로 접근
            _fill.fillAmount = Mathf.Lerp(
                _fill.fillAmount, _targetProgress, weight );

            previousRatio = ratio;
        } ).SetEase( Ease.OutQuad );        //진행률 표시 연출

        _fillEffect.Play( tween );
    }

    /// <summary>
    /// 보간 없이 진행률 적용
    /// </summary>
    /// <param name="progress">0부터 1 사이의 진행률</param>
    public void SetProgressImmediate ( float progress )
    {
        Init( );
        _fillEffect.Stop( );

        //새 대상이나 새 시도의 시작값은 이전 보간과 분리
        _targetProgress = Mathf.Clamp01( progress );
        _fill.fillAmount = _targetProgress;
    }

    /// <summary>
    /// 완료 크기 강조
    /// </summary>
    public void PlayCompleted ()
    {
        Init( );

        _completedEffect.Stop( );
        RestoreScale( );

        Sequence sequence = DOTween.Sequence( );        //완료 반동

        //진행률 보간과 독립적으로 시각 루트만 확대
        sequence.Append( _target.DOScale(
            _baseScale * _completedScale,
            _completedDuration * 0.35f ) );

        sequence.Append( _target.DOScale(
            _baseScale,
            _completedDuration * 0.65f ).SetEase( Ease.OutBack ) );

        _completedEffect.Play( sequence );
    }

#if UNITY_EDITOR
    /// <summary>
    /// 진행률을 변경하지 않고 완료 반동 미리보기
    /// </summary>
    /// <returns>미리보기 연출</returns>
    public Tween PreviewEffect ()
    {
        PlayCompleted( );
        return _completedEffect.Current;
    }

    /// <summary>
    /// 미리보기 종료와 기준 재저장 준비
    /// </summary>
    public void EndPreview ()
    {
        //미리보기 종료는 실제 게이지 진행률을 초기화하지 않음
        StopEffect( );
        RestoreScale( );
        _isInitialized = false;
    }
#endif

}
