using System;
using UnityEngine;

/// <summary>
/// 변칙 사건 진행 상태
/// </summary>
public enum AnomalyStatus
{
    Ready,       //발생 전
    Active,      //진행 중
    Resolved,    //시간 내 해결
    Failed,      //시간 초과
    Recovered,   //실패 후 환경 복구
    Cancelled,   //대상 무효화로 종료
    Expired      //사후 처리 만료로 종료, 현재 상태 유지
}

/// <summary>
/// 사후 복구 방식
/// </summary>
public enum AnomalyRecoveryType
{
    None,        //복구 완료 전 또는 복구 없이 종료
    Manual,      //상호작용 등 수동 대응으로 복구
    Automatic    //이전 자동 복구 결과 값
}

/// <summary>
/// 변칙 사건 - 상태, 제한시간, 종료 규칙
/// </summary>
public abstract class AnomalyEvent
{
    AnomalyStatus _status = AnomalyStatus.Ready;        //현재 사건 상태
    AnomalyRecoveryType _recoveryType;       //실제 복구 완료 방식
    AnomalyEventTimes _times;       //발생 때 복사한 시간 설정
    float _elapsedTime;             //경과 시간
    float _recoveryElapsedTime;     //실패 이후 경과 시간


    #region ----- 프로퍼티/이벤트 -----

    /// <summary>
    /// 현재 상태
    /// </summary>
    public AnomalyStatus Status => _status;

    /// <summary>
    /// 남은 제한 시간
    /// </summary>
    public float RemainingTime => Math.Max ( 0f , _times.TimeLimit - _elapsedTime );

    /// <summary>
    /// 남은 사후 복구 시간
    /// </summary>
    public float RecoveryRemainingTime =>
        Math.Max ( 0f , _times.RecoveryTimeLimit - _recoveryElapsedTime );

    /// <summary>
    /// 사후 처리 만료 대기 여부
    /// </summary>
    public bool RequiresAutomaticRecovery =>
        _status == AnomalyStatus.Failed &&
        _recoveryElapsedTime >= _times.RecoveryTimeLimit;

    /// <summary>
    /// 실제 복구 완료 방식
    /// </summary>
    public AnomalyRecoveryType RecoveryType => _recoveryType;

    /// <summary>
    /// 정상 상태 갱신을 잠가야 하는지 여부
    /// </summary>
    public bool RequiresNormalLock =>
        _status == AnomalyStatus.Active || _status == AnomalyStatus.Failed;

    /// <summary>
    /// 상태 변경 알림(이전 상태, 현재 상태)
    /// </summary>
    public event Action<AnomalyStatus, AnomalyStatus> OnStatusChanged;

    #endregion



    #region ----- 사건 진행 -----

    /// <summary>
    /// 발생 건에 사용할 시간 설정 저장
    /// </summary>
    /// <param name="times">해결 시간/사후 복구 시간</param>
    protected AnomalyEvent ( AnomalyEventTimes times )
    {
        _times = times;
    }

    /// <summary>
    /// 사건 시작
    /// </summary>
    /// <returns>시작 성공 여부</returns>
    public bool TryStart ( )
    {
        //준비 상태가 아니면 종료
        if ( _status != AnomalyStatus.Ready )
            return false;

        //잘못된 설정으로 생성 시 사건 시작과 상태 변경 막기
        if ( _times.CheckValid ( out string reason ) == false )
        {
            Debug.LogError ( $"[{GetType ( ).Name}] 사건 시작 실패: {reason}" );
            return false;
        }

        _elapsedTime = 0f;
        _recoveryElapsedTime = 0f;
        _recoveryType = AnomalyRecoveryType.None;
        ChangeStatus ( AnomalyStatus.Active );
        return true;
    }

    /// <summary>
    /// 해결, 실패 이후 복구 시간 진행
    /// </summary>
    /// <param name="deltaTime">이번 갱신의 경과 시간</param>
    public void Tick ( float deltaTime )
    {
        if ( float.IsNaN ( deltaTime ) == true ||
             float.IsInfinity ( deltaTime ) == true || deltaTime <= 0f )
            return;

        if ( _status == AnomalyStatus.Active )
        {
            float remaining = RemainingTime;

            if ( deltaTime < remaining )
            {
                _elapsedTime += deltaTime;
                return;
            }

            //해결 시간을 넘긴 초과분을 복구 시간에 먼저 반영
            _elapsedTime = _times.TimeLimit;
            _recoveryElapsedTime = Math.Min (
                deltaTime - remaining , _times.RecoveryTimeLimit );

            //콜백에서 취소나 복구가 발생해도 이후 상태를 덮어쓰지 않음
            ChangeStatus ( AnomalyStatus.Failed );
            return;
        }

        if ( _status != AnomalyStatus.Failed )
            return;

        _recoveryElapsedTime += Math.Min ( deltaTime , RecoveryRemainingTime );
    }

    /// <summary>
    /// 사후 처리 시간이 끝난 사건을 원상복원 없이 종료
    /// </summary>
    /// <returns>만료 종료 여부</returns>
    public bool TryExpire ()
    {
        if ( _status != AnomalyStatus.Failed || RecoveryRemainingTime > 0f )
            return false;

        //복구 완료와 달리 현재 환경을 유지한 채 사후 처리만 종료
        ChangeStatus( AnomalyStatus.Expired );
        return true;
    }

    /// <summary>
    /// 수동 대응 결과로 해결 또는 사후 복구 완료
    /// </summary>
    /// <param name="isCorrect">개별 사건의 대응 조건 충족 여부</param>
    /// <returns>해결 또는 사후 복구 성공 여부</returns>
    public bool TryComplete ( bool isCorrect )
    {
        if ( isCorrect == false )
            return false;

        if ( _status == AnomalyStatus.Active )
        {
            ChangeStatus ( AnomalyStatus.Resolved );
            return true;
        }

        if ( _status == AnomalyStatus.Failed )
        {
            _recoveryType = AnomalyRecoveryType.Manual;
            ChangeStatus ( AnomalyStatus.Recovered );
            return true;
        }

        //이미 끝난 사건은 중복 처리하지 않음
        return false;
    }

    /// <summary>
    /// 대상 무효화로 사건 종료
    /// </summary>
    /// <returns>취소 성공 여부</returns>
    public bool TryCancel ( )
    {
        if ( _status != AnomalyStatus.Active && _status != AnomalyStatus.Failed )
            return false;

        ChangeStatus ( AnomalyStatus.Cancelled );
        return true;
    }

    /// <summary>
    /// 상태 변경 및 결과 알림
    /// </summary>
    /// <param name="status">새 상태</param>
    void ChangeStatus ( AnomalyStatus status )
    {
        AnomalyStatus previous = _status;
        _status = status;

        //상태를 먼저 변경해 재진입에 의한 중복 완료 방지
        OnStatusChanged?.Invoke ( previous , status );
    }

    #endregion

}
