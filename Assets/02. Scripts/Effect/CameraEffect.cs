using DG.Tweening;
using UnityEngine;

/// <summary>
/// 카메라 연출 - 스턴 기울임과 시점 복구
/// </summary>
public class CameraEffect : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Transform _pivot;		//카메라 연출 피벗

    [Header( "----- 스턴 -----" )]
    [SerializeField, Range( -90f, 90f )] float _stunnedAngle = 90f;		//스턴 기울기
    [SerializeField, Min( 0.01f )] float _transitionDuration = 0.3f;		//기울임과 복구 시간

    EffectPlayer _effect;		//기울임 연출 수명
    Quaternion _defaultRotation;		//기본 로컬 회전
    float _currentAngle;		//현재 기울기
    bool _isInitialized;		//초기화 완료 여부
    bool _isStunned;		//요청받은 스턴 상태

    /// <summary>
    /// 연출 피벗의 기본 회전과 실행 모듈 준비
    /// </summary>
    public void Init ()
    {
        _effect = new EffectPlayer( gameObject );
        _defaultRotation = _pivot.localRotation;
        _currentAngle = 0f;
        _isStunned = false;
        _isInitialized = true;
    }

    private void OnEnable ()
    {
        if ( _isInitialized == true && _isStunned == true )
            RefreshEffect( );
    }

    private void OnDisable ()
    {
        RestorePose( );
    }

    private void OnDestroy ()
    {
        ResetEffect( );
    }


    /// <summary>
    /// 기울임 중단과 기본 회전 복구
    /// </summary>
    public void ResetEffect ()
    {
        _isStunned = false;
        RestorePose( );
    }

    /// <summary>
    /// 기울임 중단과 기본 회전 복구
    /// </summary>
    void RestorePose ()
    {
        if ( _isInitialized == false )
            return;

        _effect.Stop( );
        _currentAngle = 0f;

        //씬 해제 중 피벗이 먼저 파괴될 수 있음
        if ( _pivot != null )
            _pivot.localRotation = _defaultRotation;
    }


    /// <summary>
    /// 기본 회전에 카메라 기울기 적용
    /// </summary>
    /// <param name="angle">기울기 각도</param>
    void ApplyAngle ( float angle )
    {
        _currentAngle = angle;
        _pivot.localRotation = _defaultRotation *
            Quaternion.AngleAxis( angle, Vector3.forward );
    }

    /// <summary>
    /// 스턴 상태에 따른 카메라 기울임과 복구
    /// </summary>
    /// <param name="isStunned">스턴 여부</param>
    public void SetStunned ( bool isStunned )
    {
        //스턴 시간 연장에서는 진행 중인 연출 유지
        if ( _isStunned == isStunned )
            return;

        _isStunned = isStunned;

        if ( isActiveAndEnabled == true )
            RefreshEffect( );
    }

    /// <summary>
    /// 저장된 스턴 상태로 기울임 실행
    /// </summary>
    void RefreshEffect ()
    {
        float targetAngle = _isStunned == true
            ? _stunnedAngle
            : 0f;		//목표 기울기

        Tween tween = DOVirtual.Float(
            _currentAngle, targetAngle, _transitionDuration,
            ApplyAngle ).SetEase( Ease.InOutSine );

        _effect.Play( tween );
    }

}
