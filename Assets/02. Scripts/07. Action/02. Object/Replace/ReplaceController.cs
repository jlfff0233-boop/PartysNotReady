using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 대체 제어 - 색과 외형 변경, 조사 복원과 유지 처리
/// </summary>
public class ReplaceController : MonoBehaviour, IInvestigatable
{
    [SerializeField] ReplaceTypeWeight [ ] _types = Array.Empty<ReplaceTypeWeight>( );     //유형별 발생 가중치

    bool _isRunning;     //변경과 복원 도중 중복 요청 차단
    string _failureReason = string.Empty;     //최근 요청 실패 사유

    ItemCarry _item;		//보유 상태 조회
    ReplaceType _activeType;     //발생 당시 대체 유형
    ReplaceModel _model = new( );   //대체 모델
    MaterialChangePresenter _materialChange;        //매트리얼 대체 기능
    AppearanceChangeController _appearanceChange;     //외형 대체 기능

    /// <summary>
    /// 마지막으로 발생한 대체 유형
    /// </summary>
    public ReplaceType Type => _activeType;

    /// <summary>
    /// 시도 가능한 대체 유형 존재 여부
    /// </summary>
    public bool CanChange => CheckCandidates( );

    /// <summary>
    /// 최근 요청 실패 사유
    /// </summary>
    public string FailureReason => _failureReason;

    /// <summary>
    /// 관리 중인 대체 여부
    /// </summary>
    public bool IsChanged => _model.IsChanged;

    /// <summary>
    /// 현재 조사 ID
    /// </summary>
    public int InvestigationId => _model.InvestigationId;

    /// <summary>
    /// 현재 대체의 조사 가능 여부
    /// </summary>
    public bool CanInvestigate => CheckCanInvestigate( InvestigationId );

    /// <summary>
    /// 조사 행동 이름
    /// </summary>
    public string ActionName => "조사";

    /// <summary>
    /// 현재 보유 여부
    /// </summary>
    bool IsHeld => _item != null && _item.IsHold;

    /// <summary>
    /// 조사 복원 전 알림(조사 ID)
    /// </summary>
    public event Action<int> OnActionRequested;

    /// <summary>
    /// 조사 복원 완료 알림(조사 ID)
    /// </summary>
    public event Action<int> OnInvestigated;


    private void Awake ()
    {
        //같은 대상의 색/외형 기능 연결
        _materialChange = GetComponent<MaterialChangePresenter>( );
        _appearanceChange = GetComponent<AppearanceChangeController>( );
        _item = GetComponent<ItemCarry>( );
    }


    #region ----- 기능 요청 -----

    /// <summary>
    /// 지정한 유형의 색 또는 외형 변경
    /// </summary>
    /// <param name="type">시작할 대체 유형</param>
    /// <returns>변경 성공 여부</returns>
    bool TryChangeFeature ( ReplaceType type )
    {
        switch ( type )
        {
            case ReplaceType.Color:
                return _materialChange.TryChange( );

            case ReplaceType.Appearance:
                return _appearanceChange.TryChange( );

            default:
                return false;
        }
    }

    /// <summary>
    /// 발생 당시 유형에 맞춰 색 또는 외형 복원
    /// </summary>
    /// <returns>복원 성공 여부</returns>
    bool TryRestoreFeature ()
    {
        switch ( _activeType )
        {
            case ReplaceType.Color:
                return _materialChange.TryRestore( );

            case ReplaceType.Appearance:
                return _appearanceChange.TryRestore( );

            default:
                return false;
        }
    }

    /// <summary>
    /// 현재 색 또는 외형을 유지하도록 요청
    /// </summary>
    /// <returns>유지 요청 성공 여부</returns>
    bool TryKeepFeature ()
    {
        switch ( _activeType )
        {
            case ReplaceType.Color:
                //기능이 제거됐거나 이미 정리됐다면 조사 관리 상태만 종료
                return _materialChange == null ||
                       _materialChange.IsChanged == false ||
                       _materialChange.TryKeep( );

            case ReplaceType.Appearance:
                return _appearanceChange == null ||
                       _appearanceChange.IsChanged == false ||
                       _appearanceChange.TryKeep( );

            default:
                return false;
        }
    }

    #endregion

    #region ----- 변경/종료 -----

    /// <summary>
    /// 대체 후보를 선택하고 가능한 유형 적용
    /// </summary>
    /// <returns>대체 시작 여부</returns>
    bool TryChangeCandidate ()
    {
        while ( _model.TryTakeCandidate( Random.value, out ReplaceType requestedType ) == true )
        {
            //이전 시도의 콜백이 보유나 기능 상태를 바꿨다면 더 시도하지 않음
            if ( CheckRequestState( ) == false )
                return Fail( "대체 시도 중 대상 상태가 변경됐거나 외형 참조 복구에 실패했습니다." );
            if ( CheckFeatureReady( requestedType ) == false )
                continue;

            if ( TryChangeFeature( requestedType ) == false )
            {
                //공간 부족이나 후보 부족은 남은 유형으로 재시도
                _failureReason = GetFeatureFailure( requestedType );
                //참조 복구 실패는 다음 유형으로 넘어가 숨기지 않음
                if ( _appearanceChange != null && _appearanceChange.HasBindingFailure )
                    return false;
                continue;
            }

            //성공한 유형만 저장하며 같은 유형의 연속 발생도 허용
            _activeType = requestedType;
            _model.BeginChange( );
            _failureReason = string.Empty;
            return true;
        }

        return Fail( string.IsNullOrEmpty( _failureReason )
            ? "시도 가능한 대체 유형이 없습니다." : _failureReason );
    }

    /// <summary>
    /// 가중치로 유형을 골라 대체 시작
    /// </summary>
    /// <returns>변경 성공 여부</returns>
    public bool TryChange ()
    {
        if ( _isRunning == true )
            return false;

        _failureReason = string.Empty;
        if ( CheckCandidates( ) == false )
            return Fail( "유형별 가중치, 연결한 기능, 기존 변경과 보유 상태를 확인해 주세요." );

        _model.ClearCandidates( );
        //설정 목록을 이번 요청의 후보로 복사해 실행 중 설정 변경과 분리
        foreach ( ReplaceTypeWeight candidate in _types )
        {
            if ( CheckCandidate( candidate ) == true )
                _model.AddCandidate( candidate );
        }

        _isRunning = true;

        try
        {
            return TryChangeCandidate( );
        }
        finally
        {
            //성공 여부와 관계없이 이번 요청의 후보와 실행 상태 정리
            _isRunning = false;
            _model.ClearCandidates( );
        }
    }

    /// <summary>
    /// 조사한 대상을 발생 전 상태로 복원
    /// </summary>
    /// <param name="targetId">조사 시작 당시 ID</param>
    /// <returns>정식 조사 복원 성공 여부</returns>
    public bool TryInvestigate ( int targetId )
    {
        if ( _isRunning == true )
            return false;

        _failureReason = string.Empty;

        if ( CheckCanInvestigate( targetId ) == false )
            return Fail( "조사 ID, 변경 기능 또는 보유 상태를 확인해 주세요." );

        //사후 처리 시간이 끝났다면 이 콜백에서 현재 상태를 확정하고 조사 종료
        OnActionRequested?.Invoke( targetId );

        if ( CheckCanInvestigate( targetId ) == false )
            return Fail( "대체가 종료됐거나 조사 가능한 상태가 변경됐습니다." );

        _isRunning = true;

        try
        {
            //복원 실패 시 재조사를 위해 조사 ID와 진행 상태 유지
            if ( TryRestoreFeature( ) == false )
                return Fail( GetFeatureFailure( _activeType ) );

            if ( _model.TryEnd( targetId ) == false )
                return Fail( "복원한 대체의 조사 ID가 변경됐습니다." );
        }
        finally
        {
            _isRunning = false;
        }

        //정식 복원과 관리 상태 정리가 끝난 뒤에만 사건 해결 알림
        OnInvestigated?.Invoke( targetId );
        return true;
    }

    /// <summary>
    /// 현재 색 또는 외형을 유지하고 조사 종료
    /// </summary>
    /// <param name="targetId">종료할 조사 ID</param>
    /// <returns>종료 성공 여부</returns>
    public bool TryKeep ( int targetId )
    {
        if ( _isRunning == true || _model.Matches( targetId ) == false )
            return false;

        _failureReason = string.Empty;
        _isRunning = true;

        try
        {
            //만료와 취소 정리는 보유 중 또는 비활성화 중에도 시도
            //외형 대체는 현재 외형을 남기고 보관한 이전 외형을 정리
            if ( TryKeepFeature( ) == false )
                return Fail( GetFeatureFailure( _activeType ) );

            return _model.TryEnd( targetId );
        }
        finally
        {
            _isRunning = false;
        }
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 새 대체를 시작할 수 있는 상태인지 확인
    /// </summary>
    /// <returns>요청 가능 여부</returns>
    bool CheckRequestState ()
    {
        return isActiveAndEnabled == true && _model.IsChanged == false && IsHeld == false &&
               CheckFeatureChanged( ReplaceType.Color ) == false &&
               CheckFeatureChanged( ReplaceType.Appearance ) == false &&
               ( _appearanceChange == null || _appearanceChange.HasBindingFailure == false );
    }

    /// <summary>
    /// 유형의 가중치와 기능 준비 상태 확인
    /// </summary>
    /// <param name="candidate">유형과 가중치</param>
    /// <returns>후보 사용 가능 여부</returns>
    bool CheckCandidate ( ReplaceTypeWeight candidate )
    {
        if ( float.IsNaN( candidate.Weight ) || float.IsInfinity( candidate.Weight ) ||
             candidate.Weight <= 0f || CheckFeatureReady( candidate.Type ) == false ||
             _model.CanChange( candidate.Type, CheckFeatureChanged( candidate.Type ), IsHeld ) == false )
            return false;

        return candidate.Type == ReplaceType.Color
            ? _materialChange.CanChange
            : candidate.Type == ReplaceType.Appearance && _appearanceChange.CanChange;
    }

    /// <summary>
    /// 시도 가능한 대체 유형이 있는지 확인
    /// </summary>
    /// <returns>후보 존재 여부</returns>
    bool CheckCandidates ()
    {
        if ( _isRunning == true || CheckRequestState( ) == false || _types == null )
            return false;

        foreach ( ReplaceTypeWeight candidate in _types )
        {
            if ( CheckCandidate( candidate ) == true )
                return true;
        }

        return false;
    }

    /// <summary>
    /// 지정한 기능의 변경 여부 확인
    /// </summary>
    /// <param name="type">확인할 대체 유형</param>
    /// <returns>변경 진행 여부</returns>
    bool CheckFeatureChanged ( ReplaceType type )
    {
        switch ( type )
        {
            case ReplaceType.Color:
                return _materialChange != null && _materialChange.IsChanged;

            case ReplaceType.Appearance:
                return _appearanceChange != null && _appearanceChange.IsChanged;

            default:
                return false;
        }
    }

    /// <summary>
    /// 지정한 대체 기능이 활성 상태인지 확인
    /// </summary>
    /// <param name="type">확인할 대체 유형</param>
    /// <returns>기능 활성 여부</returns>
    bool CheckFeatureReady ( ReplaceType type )
    {
        if ( isActiveAndEnabled == false )
            return false;

        switch ( type )
        {
            case ReplaceType.Color:
                return _materialChange != null &&
                       _materialChange.isActiveAndEnabled == true;

            case ReplaceType.Appearance:
                return _appearanceChange != null &&
                       _appearanceChange.isActiveAndEnabled == true;

            default:
                return false;
        }
    }

    /// <summary>
    /// 진행 중인 같은 대체를 조사할 수 있는지 확인
    /// </summary>
    /// <param name="expectedId">조사 시작 당시 ID</param>
    /// <returns>조사 가능 여부</returns>
    bool CheckCanInvestigate ( int expectedId )
    {
        return _isRunning == false && CheckFeatureReady( _activeType ) == true &&
               _model.CanInvestigate(
                   expectedId, CheckFeatureChanged( _activeType ), IsHeld );
    }

    /// <summary>
    /// 변경 기능의 실패 사유 조회
    /// </summary>
    /// <param name="type">실패한 대체 유형</param>
    /// <returns>실패 사유</returns>
    string GetFeatureFailure ( ReplaceType type )
    {
        if ( type == ReplaceType.Appearance &&
             _appearanceChange != null &&
             string.IsNullOrEmpty( _appearanceChange.FailureReason ) == false )
            return _appearanceChange.FailureReason;

        return type == ReplaceType.Color
            ? "매트리얼 후보, Renderer와 슬롯 또는 기능 상태를 확인해 주세요."
            : "외형 후보, 배치 공간 또는 참조 연결 상태를 확인해 주세요.";
    }

    /// <summary>
    /// 요청 실패 사유 저장
    /// </summary>
    /// <param name="reason">실패 설명</param>
    /// <returns>항상 false</returns>
    bool Fail ( string reason )
    {
        _failureReason = reason;
        return false;
    }

    #endregion


    /// <summary>
    /// 플레이 중 대체 발생
    /// </summary>
    [ContextMenu( "Test Change" )]
    void TestChange ()
    {
        if ( Application.isPlaying == true && TryChange( ) == false )
            Debug.LogWarning( _failureReason, this );
    }

    /// <summary>
    /// 플레이 중 현재 대체 상태 유지
    /// </summary>
    [ContextMenu( "Test Keep" )]
    void TestKeep ()
    {
        if ( Application.isPlaying == true )
            TryKeep( InvestigationId );
    }


}
