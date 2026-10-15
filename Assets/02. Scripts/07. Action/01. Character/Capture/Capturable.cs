using System;
using UnityEngine;

/// <summary>
/// 포획 대상 중재 - 포획자 관계, 구조 진행
/// </summary>
public class Capturable : MonoBehaviour
{
    [Header( "----- 구조 -----" )]
    [SerializeField, Min( 1 )] int _rescueCount = 10;		//구조 목표 진행도
    [SerializeField, Min( 0f )] float _decreaseSpeed = 1f;		//초당 감소량

    ICaptureTarget _target;		//포획당하는 대상
    CaptureController _owner;		//포획자

    ResistModel _resistModel;		//구조 진행 모델
    double _lastProgressTime;		//마지막 진행도 갱신 시각
    bool _releaseRequested;		//이번 포획의 종료 요청 전달 여부

    /// <summary>
    /// 포획자
    /// </summary>
    public CaptureController Owner => _owner;

    /// <summary>
    /// 포획 여부
    /// </summary>
    public bool IsCaptured => ReferenceEquals( _owner, null ) == false;

    /// <summary>
    /// 구조 입력 가능 여부
    /// </summary>
    public bool CanRescue => CheckCanRescue( );

    /// <summary>
    /// 구조 진행 비율
    /// </summary>
    public float RescueProgressRatio => GetRescueProgressRatio( );

    /// <summary>
    /// 포획 해제 요청 이벤트(포획 결과)
    /// </summary>
    public event Action<CaptureResult> OnReleaseRequested;


    /// <summary>
    /// 대상 연결, 저항 모델 생성
    /// </summary>
    /// <param name="target">포획 대상</param>
    public void Init ( ICaptureTarget target )
    {
        _target = target;

        if ( _resistModel == null )
            _resistModel = new ResistModel( _rescueCount, _decreaseSpeed );
    }

    private void Update ()
    {
        UpdateProgress( );
    }

    private void OnDisable ()
    {
        if ( IsCaptured == false )
            return;

        //포획 취소 이벤트 발행
        OnReleaseRequested?.Invoke( CaptureResult.Cancelled );
    }


    #region ----- 포획 -----

    /// <summary>
    /// 포획 관계와 시간 기록 초기화
    /// </summary>
    void ResetState ()
    {
        _owner = null;
        _lastProgressTime = 0d;
        _releaseRequested = false;
    }

    /// <summary>
    /// 포획 관계와 저항 진행 시작
    /// </summary>
    /// <param name="owner">포획자</param>
    /// <param name="timeLimit">제한 시간(초), 0이면 제한 없음</param>
    /// <returns>시작 여부</returns>
    public bool TryBegin ( CaptureController owner, float timeLimit = 0f )
    {
        //기존 포획 관계를 새 요청으로 덮지 않음
        if ( isActiveAndEnabled == false || IsCaptured == true ||
             _target == null || _target.TargetTransform == null ||
             owner == null || owner.isActiveAndEnabled == false ||
             ReferenceEquals( owner.Target, _target ) == false ||
             _resistModel == null )
            return false;

        _owner = owner;
        _releaseRequested = false;
        _resistModel.Begin( timeLimit );
        _lastProgressTime = Time.timeAsDouble;
        return true;
    }

    /// <summary>
    /// 포획 관계 정리
    /// </summary>
    public void End ()
    {
        ResetState( );
    }

    /// <summary>
    /// 포획자 확인
    /// </summary>
    /// <param name="owner">확인할 포획자</param>
    /// <returns>포획자 여부</returns>
    public bool IsOwner ( CaptureController owner )
    {
        return owner != null && ReferenceEquals( _owner, owner ) &&
            ReferenceEquals( owner.Target, _target );
    }

    #endregion

    #region ----- 구조 -----

    /// <summary>
    /// 구조 진행 갱신, 시간 초과 처리
    /// </summary>
    void UpdateProgress ()
    {
        if ( IsCaptured == false || _releaseRequested == true )
            return;

        //구조 진행의 포획 관계가 끊겼으면 시간 판정 대신 취소
        if ( IsOwner( _owner ) == false || _owner.isActiveAndEnabled == false )
        {
            _releaseRequested = true;
            OnReleaseRequested?.Invoke( CaptureResult.Cancelled );
            return;
        }

        //갱신과 여러 구조 입력이 연달아 호출돼도 지난 시간만 한 번 반영
        double now = Time.timeAsDouble;
        float deltaTime = ( float ) ( now - _lastProgressTime );

        _lastProgressTime = now;
        _resistModel.UpdateProgress( deltaTime );

        if ( _resistModel.IsExpired == false )
            return;

        //해제 처리 중 이벤트가 다시 들어와도 중복 요청 차단
        _releaseRequested = true;
        OnReleaseRequested?.Invoke( CaptureResult.TimedOut );
    }

    /// <summary>
    /// 저항 성공 시 해제 요청
    /// </summary>
    /// <param name="progressAmount">입력 진행량</param>
    /// <returns>저항 성공 여부</returns>
    public bool TryRescue ( float progressAmount = 1f )
    {
        //입력 시점까지의 시간을 먼저 반영해 제한시간 경계 판정 통일
        UpdateProgress( );

        if ( CanRescue == false )
            return false;

        bool completed = _resistModel.TryRescue( progressAmount );


        if ( completed == false )
            return false;

        //포획자가 충격량을 정해 대상에 해제를 요청하도록 알림
        _releaseRequested = true;
        OnReleaseRequested?.Invoke( CaptureResult.Rescued );
        return true;
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 구조 입력 가능 여부 확인
    /// </summary>
    /// <returns>구조 입력 가능 여부</returns>
    bool CheckCanRescue ()
    {
        return isActiveAndEnabled == true && _target != null &&
               _target.TargetTransform != null &&
               _target.TargetTransform.gameObject.activeInHierarchy == true &&
               IsOwner( _owner ) == true && _owner.isActiveAndEnabled == true &&
               _resistModel != null &&
               _resistModel.IsCompleted == false &&
               _resistModel.IsExpired == false &&
               _releaseRequested == false;
    }

    /// <summary>
    /// 구조 진행 비율 조회
    /// </summary>
    /// <returns>포획 중이면 진행 비율, 포획 정보가 없으면 0</returns>
    float GetRescueProgressRatio ()
    {
        return _owner == null || _resistModel == null
               ? 0f : _resistModel.ProgressRatio;
    }

    #endregion

}
