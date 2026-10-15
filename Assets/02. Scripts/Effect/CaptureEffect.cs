using UnityEngine;

/// <summary>
/// 포획 연출 - 납치 자세 보정과 기본 자세 복구
/// </summary>
public class CaptureEffect : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Transform _pivot;		//납치 자세 전용 피벗

    [Header( "----- 납치 자세 -----" )]
    [SerializeField] Vector3 _capturedOffset;		//기본 위치 기준 보정
    [SerializeField] Vector3 _capturedRotation;		//기본 회전 기준 보정

    Vector3 _defaultPosition;		//기본 로컬 위치
    Quaternion _defaultRotation;		//기본 로컬 회전
    bool _isInitialized;		//초기화 완료 여부
    bool _isCaptured;		//요청받은 포획 상태

    /// <summary>
    /// 납치 자세 피벗의 기본 위치와 회전 저장
    /// </summary>
    public void Init ()
    {
        _defaultPosition = _pivot.localPosition;
        _defaultRotation = _pivot.localRotation;
        _isCaptured = false;
        _isInitialized = true;
    }

    private void OnEnable ()
    {
        if ( _isInitialized == true && _isCaptured == true )
            ApplyPose( );
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
    /// 기본 자세 복구
    /// </summary>
    public void ResetEffect ()
    {
        _isCaptured = false;
        RestorePose( );
    }

    /// <summary>
    /// 기본 자세 복구
    /// </summary>
    void RestorePose ()
    {
        if ( _isInitialized == false )
            return;

        //씬 해제 중 피벗이 먼저 파괴될 수 있음
        if ( _pivot == null )
            return;

        _pivot.localPosition = _defaultPosition;
        _pivot.localRotation = _defaultRotation;
    }


    /// <summary>
    /// 포획 상태에 따른 납치 자세 적용과 복구
    /// </summary>
    /// <param name="isCaptured">포획 여부</param>
    public void SetCaptured ( bool isCaptured )
    {
        if ( _isCaptured == isCaptured )
            return;

        _isCaptured = isCaptured;

        if ( isActiveAndEnabled == false )
            return;

        if ( _isCaptured == false )
        {
            RestorePose( );
            return;
        }

        ApplyPose( );
    }

    /// <summary>
    /// 납치 자세 적용
    /// </summary>
    void ApplyPose ()
    {
        _pivot.localPosition = _defaultPosition +
            _capturedOffset;

        _pivot.localRotation = _defaultRotation *
            Quaternion.Euler( _capturedRotation );
    }
}
