using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>
/// 포획 제어 - 대상 확보와 해제, 결과 전달
/// </summary>
public class CaptureController : MonoBehaviour
{
    [Header( "----- 포획 -----" )]
    [SerializeField] Transform _carryPoint;		//보유 지점
    [SerializeField, Min( 0f )] float _releasePower;		//수평 충격량

    [Header( "----- 테스트 -----" )]
    [SerializeField] Npc _testTarget;		//수동 포획 확인 대상
    [SerializeField] Player _testPlayer;		//수동 포획 확인 플레이어

    bool _isReportingResult;		//포획 결과 전달 중
    ICaptureTarget _target;		//현재 포획 대상

    /// <summary>
    /// 현재 포획 대상
    /// </summary>
    public ICaptureTarget Target => _target;

    /// <summary>
    /// 포획 대상 보유 여부
    /// </summary>
    public bool HasTarget => _target != null;

    /// <summary>
    /// 포획 종료 이벤트(대상, 결과)
    /// </summary>
    public event Action<ICaptureTarget, CaptureResult> OnResult;


    private void OnDisable ()
    {
        //포획자 비활성화 시 대상에도 취소를 전달해 포획 상태 해제
        TryRelease( CaptureResult.Cancelled );
    }


    #region ----- 핸들 -----

    /// <summary>
    /// 포획 해제 요청 처리
    /// </summary>
    /// <param name="result">요청 결과</param>
    void HandleReleaseRequested ( CaptureResult result )
    {
        TryRelease( result );
    }

    /// <summary>
    /// 포획 종료 결과 전달
    /// </summary>
    /// <param name="result">포획 종료 결과</param>
    void HandleCaptureEnded ( CaptureResult result )
    {
        ICaptureTarget target = _target;

        if ( target == null )
            return;

        //이전 대상 정리
        ClearTarget( );

        _isReportingResult = true;

        try
        {
            OnResult?.Invoke( target, result );
        }
        finally
        {
            _isReportingResult = false;
        }
    }

    #endregion

    #region ----- 포획 -----

    /// <summary>
    /// 포획 시도
    /// </summary>
    /// <param name="target">포획할 대상</param>
    /// <returns>포획 성공 여부</returns>
    public bool TryCapture ( ICaptureTarget target )
    {
        if ( isActiveAndEnabled == false || HasTarget == true || _isReportingResult == true )
            return false;

        if ( _carryPoint == null || _carryPoint.gameObject.activeInHierarchy == false ||
             target == null || ( target is Object item && item == null ) ||
             target.TargetTransform == null ||
             target.CanCapture == false )
            return false;

        //대상의 포획자 확인과 적용 중
        //종료 알림을 받을 수 있도록 먼저 연결
        _target = target;
        _target.OnCaptureEnded += HandleCaptureEnded;
        _target.OnReleaseRequested += HandleReleaseRequested;

        if ( target.TryCapture( this, _carryPoint ) == false )
        {
            //포획 적용 실패 시 미리 연결한 구독과 참조 해제
            if ( ReferenceEquals( _target, target ) == true )
                ClearTarget( );
            return false;
        }

        //적용 중 종료되거나 대상이 바뀌었으면
        //이번 포획은 성공으로 반환하지 않음
        if ( ReferenceEquals( _target, target ) == false )
            return false;

        if ( target.IsCapturedBy( this ) == false )
        {
            HandleCaptureEnded( CaptureResult.Cancelled );
            return false;
        }


        return true;
    }

    /// <summary>
    /// 포획 해제 요청
    /// </summary>
    /// <param name="result">해제 결과</param>
    /// <returns>해제 성공 여부</returns>
    public bool TryRelease ( CaptureResult result )
    {
        //대상이 없으면 종료
        if ( HasTarget == false )
            return false;

        //납치 완료는 별도 경로에서 처리
        if ( result == CaptureResult.Kidnapped )
            return false;

        //대상이 사라졌으면 취소 처리
        if ( ( _target is Object item && item == null ) ||
             _target.TargetTransform == null || _target.IsCapturedBy( this ) == false )
        {
            HandleCaptureEnded( CaptureResult.Cancelled );
            return true;
        }

        //실제 해제와 충격량 적용 여부는 대상이 결과에 따라 처리
        return _target.TryRelease( this, GetReleaseImpulse( ), result );
    }

    /// <summary>
    /// 대상 구독/참조 정리
    /// </summary>
    void ClearTarget ()
    {
        if ( _target != null )
        {
            _target.OnCaptureEnded -= HandleCaptureEnded;
            _target.OnReleaseRequested -= HandleReleaseRequested;
        }

        _target = null;
    }

    /// <summary>
    /// 좌우 또는 뒤쪽 해제 충격량 계산
    /// </summary>
    /// <returns>월드 기준 해제 충격량</returns>
    Vector3 GetReleaseImpulse ()
    {
        //포획자 기준 앞쪽은 선택하지 않음
        Vector3 localDirection = Random.Range( 0, 3 ) switch
        {
            0 => Vector3.left,
            1 => Vector3.right,
            _ => Vector3.back
        };

        //포획자가 기울어져 있어도 수평 방향으로 밀어냄
        Quaternion rotation = Quaternion.Euler(
            0f, transform.eulerAngles.y, 0f );

        return rotation * localDirection * _releasePower;
    }

    #endregion

    #region ----- 테스트 -----

    /// <summary>
    /// 플레이 중 수동 포획 확인
    /// </summary>
    [ContextMenu( "Test Capture" )]
    void TestCapture ()
    {
        if ( Application.isPlaying == false )
            return;

        Debug.Log(
            $"[포획 테스트] 성공: {TryCapture( _testTarget )}", this );
    }

    /// <summary>
    /// 플레이 중 지정한 플레이어의 수동 포획 확인
    /// </summary>
    [ContextMenu( "Test Capture Player" )]
    void TestCapturePlayer ()
    {
        if ( Application.isPlaying == false )
            return;

        Debug.Log(
            $"[플레이어 포획 테스트] 성공: {TryCapture( _testPlayer )}", this );
    }

    /// <summary>
    /// 플레이 중 구조 해제 확인
    /// </summary>
    [ContextMenu( "Test Rescue" )]
    void TestRescue ()
    {
        if ( Application.isPlaying == false )
            return;

        TryRelease( CaptureResult.Rescued );
    }

    /// <summary>
    /// 플레이 중 취소 해제 확인
    /// </summary>
    [ContextMenu( "Test Cancel" )]
    void TestCancel ()
    {
        if ( Application.isPlaying == false )
            return;

        TryRelease( CaptureResult.Cancelled );
    }

    #endregion

}
