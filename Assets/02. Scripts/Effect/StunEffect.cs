using DG.Tweening;
using UnityEngine;

/// <summary>
/// 스턴 연출 - 모델 넘어짐과 기본 자세 복구
/// </summary>
public class StunEffect : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Transform _pivot;		//시각 모델 전용 피벗

    [Header( "----- 스턴 -----" )]
    [SerializeField, Range( -90f, 90f )] float _stunnedAngle = 90f;		//넘어짐 각도
    [SerializeField] Vector3 _stunnedOffset;		//넘어진 자세의 위치 보정
    [SerializeField, Min( 0.01f )] float _transitionDuration = 0.3f;		//넘어짐과 복구 시간

    EffectPlayer _effect;		//자세 전환 연출 수명
    Vector3 _defaultPosition;		//기본 로컬 위치
    Quaternion _defaultRotation;		//기본 로컬 회전
    float _currentRatio;		//현재 넘어짐 정도
    bool _isInitialized;		//초기화 완료 여부
    bool _isStunned;		//요청받은 스턴 상태

    /// <summary>
    /// 모델의 기본 자세와 실행 모듈 준비
    /// </summary>
    public void Init ()
    {
        _effect = new EffectPlayer( gameObject );
        _defaultPosition = _pivot.localPosition;
        _defaultRotation = _pivot.localRotation;
        _currentRatio = 0f;
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
    /// 연출 중단과 기본 자세 복구
    /// </summary>
    public void ResetEffect ()
    {
        _isStunned = false;
        RestorePose( );
    }

    /// <summary>
    /// 연출 중단과 기본 자세 복구
    /// </summary>
    void RestorePose ()
    {
        if ( _isInitialized == false )
            return;

        _effect.Stop( );
        _currentRatio = 0f;

        //씬 해제 중 피벗이 먼저 파괴될 수 있음
        if ( _pivot == null )
            return;

        _pivot.localPosition = _defaultPosition;
        _pivot.localRotation = _defaultRotation;
    }


    /// <summary>
    /// 넘어짐 정도에 따른 모델 자세 적용
    /// </summary>
    /// <param name="ratio">넘어짐 정도</param>
    void ApplyPose ( float ratio )
    {
        _currentRatio = ratio;

        _pivot.localPosition = _defaultPosition +
            _stunnedOffset * ratio;

        _pivot.localRotation = _defaultRotation *
            Quaternion.AngleAxis(
                _stunnedAngle * ratio, Vector3.forward );
    }

    /// <summary>
    /// 스턴 상태에 따른 넘어짐과 자세 복구
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
    /// 저장된 스턴 상태로 넘어짐 실행
    /// </summary>
    void RefreshEffect ()
    {
        float targetRatio = _isStunned == true
            ? 1f
            : 0f;		//목표 넘어짐 정도

        Tween tween = DOVirtual.Float(
            _currentRatio, targetRatio, _transitionDuration,
            ApplyPose ).SetEase( Ease.InOutSine );

        _effect.Play( tween );
    }
}
