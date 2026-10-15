
/// <summary>
/// 존재 모델 - 존재 상태, 조사와 복구 상태 관리
/// </summary>
public class PresenceModel
{
    bool _isPresent;        //정식 등장 경로로 표시된 상태
    int _curTargetId;     //등장 또는 빈자리 조사의 ID
    PresenceInvestigationType _investigationType;     //현재 조사 목적
    bool _isRestoring;     //조사 후 정상 복구를 기다리는 상태

    /// <summary>
    /// 정상 복구 대기 여부
    /// </summary>
    public bool IsRestoring => _isRestoring;

    /// <summary>
    /// 물건 존재 여부
    /// </summary>
    public bool IsPresent => _isPresent;
    /// <summary>
    /// 현재 조사 ID
    /// </summary>
    public int CurTargetId => _curTargetId;

    /// <summary>
    /// 현재 조사 목적
    /// </summary>
    public PresenceInvestigationType InvestigationType => _investigationType;


    /// <summary>
    /// 초기 존재 상태 설정
    /// </summary>
    /// <param name="isPresent">초기 존재 여부</param>
    public PresenceModel ( bool isPresent )
    {
        //처음부터 있는 정상 물건은 조사 제거 대상이 아님
        _isPresent = isPresent;
        _investigationType = PresenceInvestigationType.None;
    }


    /// <summary>
    /// 존재 상태와 조사 목적 저장
    /// </summary>
    /// <param name="isPresent">물건 존재 여부</param>
    /// <param name="investigationType">새로 등장한 물건의 조사 목적</param>
    public void SetPresent ( bool isPresent,
        PresenceInvestigationType investigationType = PresenceInvestigationType.None )
    {
        if ( _isRestoring == true || _isPresent == isPresent )
            return;

        _isPresent = isPresent;
        _investigationType = ( isPresent == true )
            ? investigationType
            : PresenceInvestigationType.None;

        //새로 등장할 때만 ID 변경
        if ( isPresent == true )
        {
            unchecked
            {
                _curTargetId++;
            }
        }
    }


    #region ----- 복구 조사 -----

    /// <summary>
    /// 지정한 조사와 복구 대기 종료
    /// </summary>
    /// <param name="targetId">종료할 조사 ID</param>
    /// <returns>종료 처리 여부</returns>
    public bool TryEndInvestigation ( int targetId )
    {
        if ( _curTargetId != targetId ||
             ( _investigationType == PresenceInvestigationType.None && _isRestoring == false ) )
            return false;

        //조사만 종료하며 현재 존재 상태와 ID는 유지
        _investigationType = PresenceInvestigationType.None;
        _isRestoring = false;
        return true;
    }

    /// <summary>
    /// 숨겨진 물건의 복구 조사 시작
    /// </summary>
    /// <returns>복구 조사 시작 여부</returns>
    public bool TryBeginRestoreInvestigation ()
    {
        //물건이 없고 다른 조사가 진행 중이지 않을 때만 시작
        if ( _isRestoring == true || _isPresent == true ||
             _investigationType != PresenceInvestigationType.None )
            return false;

        _investigationType = PresenceInvestigationType.Restore;

        //이전 물건이나 빈자리에서 시작한 입력과 구분
        unchecked
        {
            _curTargetId++;
        }

        return true;
    }

    /// <summary>
    /// 빈자리 조사 완료, 복구 대기 시작
    /// </summary>
    /// <param name="targetId">조사 시작 시 저장한 ID</param>
    /// <param name="isReady">표시 기능 준비 여부</param>
    /// <param name="isVisible">현재 대상 표시 여부</param>
    /// <param name="isHeld">현재 보유 중인지 여부</param>
    /// <returns>복구 조사 완료 여부</returns>
    public bool TryCompleteRestoreInvestigation (
        int targetId, bool isReady, bool isVisible, bool isHeld )
    {
        if ( _isRestoring == true || _isPresent == true ||
             _investigationType != PresenceInvestigationType.Restore ||
             _curTargetId != targetId || isReady == false ||
             isVisible == false || isHeld == true )
            return false;

        //대기 물건이 보여도 정상 복구 전에는 존재 상태를 바꾸지 않음
        //조사부터 종료해 같은 입력의 중복 완료 방지
        _investigationType = PresenceInvestigationType.None;
        _isRestoring = true;
        return true;
    }

    /// <summary>
    /// 복구 완료 상태 반영
    /// </summary>
    /// <param name="targetId">복구를 완료한 조사 ID</param>
    /// <returns>정상 상태 반영 여부</returns>
    public bool TryCompleteRestore ( int targetId )
    {
        if ( _isRestoring == false || _curTargetId != targetId )
            return false;

        _isRestoring = false;
        SetPresent( true, PresenceInvestigationType.None );
        return true;
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 물건 등장 가능 여부 확인
    /// </summary>
    /// <param name="isReady">준비 여부</param>
    /// <param name="isVisible">대상 표시 여부</param>
    /// <param name="hasSpace">등장 공간이 비어 있는지 여부</param>
    /// <param name="isHeld">현재 보유 중인지 여부</param>
    public bool CanShow ( bool isReady, bool isVisible, bool hasSpace, bool isHeld )
    {
        return _isRestoring == false &&
               _isPresent == false &&
               isReady == true &&
               isVisible == false &&
               hasSpace == true &&
               isHeld == false;
    }

    /// <summary>
    /// 표시된 물건 제거 가능 여부 확인
    /// </summary>
    /// <param name="isReady">준비 여부</param>
    /// <param name="isVisible">현재 대상 표시 여부</param>
    /// <param name="isHeld">현재 보유 중인지 여부</param>
    public bool CanRemove ( bool isReady, bool isVisible, bool isHeld )
    {
        return _isRestoring == false &&
               _isPresent == true &&
               isReady == true &&
               isVisible == true &&
               isHeld == false;
    }

    /// <summary>
    /// 조사 가능 여부 확인
    /// </summary>
    /// <param name="targetId">조사 시작 시 저장한 ID</param>
    /// <param name="isReady">준비 여부</param>
    /// <param name="isVisible">현재 대상 표시 여부</param>
    /// <param name="isHeld">현재 보유 중인지 여부</param>
    /// <returns>조사 가능 여부</returns>
    public bool CanInvestigate (
        int targetId,
        bool isReady,
        bool isVisible,
        bool isHeld )
    {
        if ( _isRestoring == true || _curTargetId != targetId ||
             isReady == false ||
             isHeld == true )
            return false;

        switch ( _investigationType )
        {
            case PresenceInvestigationType.Remove:
                return CanRemove( isReady, isVisible, isHeld );

            case PresenceInvestigationType.Restore:
                return _isPresent == false && isVisible == false;

            default:
                return false;
        }
    }

    /// <summary>
    /// 복구 가능 여부 확인
    /// </summary>
    /// <param name="targetId">재충전을 시작한 조사 ID</param>
    /// <param name="isReady">표시 기능 준비 여부</param>
    /// <param name="isVisible">대기 물건 표시 여부</param>
    /// <param name="isHeld">현재 보유 중인지 여부</param>
    /// <returns>정상 복구 가능 여부</returns>
    public bool CanRestore ( int targetId, bool isReady, bool isVisible, bool isHeld )
    {
        return _isRestoring == true && _isPresent == false &&
               _curTargetId == targetId && isReady == true &&
               isVisible == true && isHeld == false;
    }

    /// <summary>
    /// 즉시 복구 가능 여부 확인
    /// </summary>
    /// <param name="targetId">발생 당시 조사 ID</param>
    /// <param name="isReady">표시 기능 준비 여부</param>
    /// <param name="isHeld">현재 보유 여부</param>
    public bool CanRestoreImmediately ( int targetId, bool isReady, bool isHeld )
    {
        return _curTargetId == targetId && _isPresent == false &&
               isReady == true && isHeld == false &&
               ( _isRestoring == true || _investigationType == PresenceInvestigationType.Restore );
    }

    #endregion

}
