using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 화면 효과 타입
/// </summary>
public enum EffectType
{
    None,       //기본 화면
    Captured,       //포획 화면
    Stunned     //스턴 화면
}

/// <summary>
/// 화면 연출 - 포획, 스턴 비네트와 표시 복구
/// </summary>
public class ScreenEffect : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Volume _volume;        //화면 연출 전용 볼륨

    [Header( "----- 상태 전환 -----" )]
    [SerializeField, Min( 0.01f )] float _transitionDuration = 0.2f;        //상태 전환 시간

    [Header( "----- 포획 -----" )]
    [SerializeField] Color _capturedColor = Color.red;      //포획 비네트 색상
    [SerializeField, Range( 0f, 1f )] float _capturedMinIntensity = 0.15f;      //포획 최소 강도
    [SerializeField, Range( 0f, 1f )] float _capturedMaxIntensity = 0.3f;       //포획 최대 강도
    [SerializeField, Min( 0.01f )] float _capturedDuration = 0.6f;      //포획 점멸 편도 시간

    [Header( "----- 스턴 -----" )]
    [SerializeField] Color _stunnedColor = new( 0.25f, 0.2f, 0.5f );        //스턴 비네트 색상
    [SerializeField, Range( 0f, 1f )] float _stunnedMinIntensity = 0.25f;        //스턴 최소 강도
    [SerializeField, Range( 0f, 1f )] float _stunnedMaxIntensity = 0.4f;        //스턴 최대 강도
    [SerializeField, Min( 0.01f )] float _stunnedDuration = 0.45f;        //스턴 점멸 편도 시간

    VolumeProfile _profile;        //직접 생성한 실행용 프로필
    Vignette _vignette;        //실행용 비네트
    EffectPlayer _effect;        //전환과 반복 연출 수명

    Color _defaultColor;        //기본 비네트 색상
    float _defaultIntensity;        //기본 비네트 강도
    bool _defaultActive;        //기본 비네트 활성 상태
    bool _defaultColorOverride;        //기본 색상 적용 여부
    bool _defaultIntensityOverride;        //기본 강도 적용 여부

    bool _isInitialized;        //초기화 완료 여부
    bool _isCaptured;        //포획 상태
    bool _isStunned;        //스턴 상태
    EffectType _displayedType;        //현재 표시 종류


    #region ----- 초기화 -----

    /// <summary>
    /// 실행용 볼륨 프로필과 비네트 준비
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ()
    {
        _effect = new EffectPlayer( gameObject );
        _profile = ScriptableObject.CreateInstance<VolumeProfile>( );

        //설정 원본과 각 효과 컴포넌트를 함께 분리
        foreach ( VolumeComponent component in _volume.sharedProfile.components )
            _profile.components.Add( Instantiate( component ) );

        _volume.profile = _profile;

        if ( _profile.TryGet( out _vignette ) == false )
        {
            Debug.LogError(
                "[화면 연출 초기화 실패] " +
                "전용 볼륨에 비네트를 설정해 주세요.", this );
            return false;
        }

        _defaultColor = _vignette.color.value;
        _defaultIntensity = _vignette.intensity.value;
        _defaultActive = _vignette.active;
        _defaultColorOverride = _vignette.color.overrideState;
        _defaultIntensityOverride = _vignette.intensity.overrideState;

        _isInitialized = true;
        ResetEffect( );
        return true;
    }

    private void OnEnable ()
    {
        if ( _isInitialized == true )
            RefreshEffect( );
    }

    private void OnDisable ()
    {
        StopEffect( );
    }

    private void OnDestroy ()
    {
        ResetEffect( );

        //초기화되지 않은 객체에는 생성한 프로필이 없음
        if ( _profile == null )
            return;

        if ( _volume != null )
            _volume.profile = null;

        foreach ( VolumeComponent component in _profile.components )
            Destroy( component );

        Destroy( _profile );
    }

    #endregion


    #region ----- 상태 연출 -----

    /// <summary>
    /// 비네트 강도 반복 변화
    /// </summary>
    /// <param name="minIntensity">최소 강도</param>
    /// <param name="maxIntensity">최대 강도</param>
    /// <param name="duration">편도 시간</param>
    void StartPulse (
        float minIntensity, float maxIntensity, float duration )
    {
        Tween tween = DOVirtual.Float(
            minIntensity, maxIntensity, duration,
            value => _vignette.intensity.value = value )
            .SetEase( Ease.InOutSine )
            .SetLoops( -1, LoopType.Yoyo );        //강도 반복 연출

        _effect.Play( tween );
    }

    /// <summary>
    /// 현재 표시값에서 지정한 비네트로 전환
    /// </summary>
    /// <param name="color">목표 색상</param>
    /// <param name="intensity">목표 강도</param>
    /// <param name="completed">전환 종료 처리</param>
    void Transition (
        Color color, float intensity, Action completed )
    {
        _effect.Stop( );

        Color previousColor = _vignette.color.value;        //전환 시작 색상
        float previousIntensity = _vignette.intensity.value;        //전환 시작 강도

        _vignette.active = true;
        _vignette.color.overrideState = true;
        _vignette.intensity.overrideState = true;

        Tween tween = DOVirtual.Float(
            0f, 1f, _transitionDuration, ratio =>
            {
                _vignette.color.value = Color.Lerp(
                    previousColor, color, ratio );

                _vignette.intensity.value = Mathf.Lerp(
                    previousIntensity, intensity, ratio );
            } ).SetEase( Ease.OutQuad );        //상태 전환 연출

        tween.OnComplete( () => completed?.Invoke( ) );
        _effect.Play( tween );
    }

    /// <summary>
    /// 상태에 맞는 화면 연출 적용
    /// </summary>
    void RefreshEffect ()
    {
        EffectType type = _isStunned == true
            ? EffectType.Stunned
            : _isCaptured == true
                ? EffectType.Captured
                : EffectType.None;        //표시할 효과 종류

        //같은 효과는 현재 반복 주기 유지
        if ( _displayedType == type )
            return;

        _displayedType = type;

        switch ( type )
        {
            case EffectType.Captured:
                Transition(
                    _capturedColor, _capturedMinIntensity,
                    () => StartPulse(
                        _capturedMinIntensity,
                        _capturedMaxIntensity,
                        _capturedDuration ) );
                break;

            case EffectType.Stunned:
                Transition(
                    _stunnedColor, _stunnedMinIntensity,
                    () => StartPulse(
                        _stunnedMinIntensity,
                        _stunnedMaxIntensity,
                        _stunnedDuration ) );
                break;

            case EffectType.None:
                Transition(
                    _defaultColor, _defaultIntensity,
                    RestoreDefaults );
                break;
        }
    }

    /// <summary>
    /// 포획과 스턴 상태 반영
    /// </summary>
    /// <param name="isCaptured">포획 여부</param>
    /// <param name="isStunned">스턴 여부</param>
    public void SetState ( bool isCaptured, bool isStunned )
    {
        _isCaptured = isCaptured;
        _isStunned = isStunned;

        if ( isActiveAndEnabled == true )
            RefreshEffect( );
    }

    /// <summary>
    /// 기본 비네트 설정 복구
    /// </summary>
    void RestoreDefaults ()
    {
        _vignette.color.value = _defaultColor;
        _vignette.intensity.value = _defaultIntensity;
        _vignette.color.overrideState = _defaultColorOverride;
        _vignette.intensity.overrideState = _defaultIntensityOverride;
        _vignette.active = _defaultActive;
    }

    /// <summary>
    /// 연출 중단과 기본 화면 복구
    /// </summary>
    public void ResetEffect ()
    {
        _isCaptured = false;
        _isStunned = false;
        StopEffect( );
    }

    /// <summary>
    /// 요청 상태를 유지하고 연출 중단과 기본 화면 복구
    /// </summary>
    void StopEffect ()
    {
        _displayedType = EffectType.None;

        //초기화 전 비활성화와 초기화 실패에서는 적용할 값 없음
        if ( _isInitialized == false )
            return;

        _effect.Stop( );
        RestoreDefaults( );
    }

    #endregion
}
