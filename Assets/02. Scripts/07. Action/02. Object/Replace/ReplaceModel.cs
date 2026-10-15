using System.Collections.Generic;

/// <summary>
/// 대체 모델 - 유형 추첨, 진행 상태와 조사 ID 관리
/// </summary>
public class ReplaceModel
{
    /// <summary>
    /// 이번 요청에서 아직 시도하지 않은 유형과 가중치 목록
    /// </summary>
    List<ReplaceTypeWeight> _candidates = new( );
    bool _isChanged;        //변경 여부
    int _investigationId;       //조사 ID

    /// <summary>
    /// 진행 중인 대체 여부
    /// </summary>
    public bool IsChanged => _isChanged;

    /// <summary>
    /// 현재 대체의 조사 ID
    /// </summary>
    public int InvestigationId => _investigationId;


    /// <summary>
    /// 남은 유형 후보 비우기
    /// </summary>
    public void ClearCandidates ()
    {
        _candidates.Clear( );
    }

    /// <summary>
    /// 추첨할 유형과 가중치 추가
    /// </summary>
    /// <param name="candidate">유형과 가중치</param>
    public void AddCandidate ( ReplaceTypeWeight candidate )
    {
        if ( CheckSupportedType( candidate.Type ) == false ||
             float.IsNaN( candidate.Weight ) || float.IsInfinity( candidate.Weight ) ||
             candidate.Weight <= 0f )
            return;

        _candidates.Add( candidate );
    }

    /// <summary>
    /// 가중치로 다음 시도 유형 선택
    /// </summary>
    /// <param name="sample">0부터 1 사이의 무작위 값</param>
    /// <param name="type">선택한 유형</param>
    /// <returns>유형 선택 성공 여부</returns>
    public bool TryTakeCandidate ( double sample, out ReplaceType type )
    {
        type = default;
        if ( _candidates.Count == 0 || double.IsNaN( sample ) ||
             sample < 0d || sample > 1d )
            return false;

        //가중치 합이 float 범위를 넘을 수 있으므로 double로 누적
        double total = 0d;
        foreach ( ReplaceTypeWeight candidate in _candidates )
            total += candidate.Weight;

        double remaining = sample * total;
        //누적 가중치 구간에서 선택. sample이 정확히 1이면 마지막 후보를 사용
        type = _candidates [ _candidates.Count - 1 ].Type;
        foreach ( ReplaceTypeWeight candidate in _candidates )
        {
            remaining -= candidate.Weight;
            if ( remaining < 0d )
            {
                type = candidate.Type;
                break;
            }
        }

        //중복 등록한 유형은 가중치를 합산하되 요청당 한 번만 시도
        for ( int i = _candidates.Count - 1; i >= 0; i-- )
        {
            if ( _candidates [ i ].Type == type )
                _candidates.RemoveAt( i );
        }

        return true;
    }


    /// <summary>
    /// 변경 상태를 기록하고 새 조사 ID 발급
    /// </summary>
    public void BeginChange ()
    {
        _isChanged = true;

        unchecked
        {
            _investigationId++;
        }
    }


    /// <summary>
    /// 같은 조사 ID의 대체 종료
    /// </summary>
    /// <param name="expectedId">종료할 조사 ID</param>
    /// <returns>종료 성공 여부</returns>
    public bool TryEnd ( int expectedId )
    {
        if ( Matches( expectedId ) == false )
            return false;

        _isChanged = false;
        return true;
    }


    /// <summary>
    /// 진행 중인 대체의 조사 ID와 일치하는지 확인
    /// </summary>
    /// <param name="expectedId">요청한 조사 ID</param>
    public bool Matches ( int expectedId )
    {
        return _isChanged == true &&
               _investigationId == expectedId;
    }

    /// <summary>
    /// 현재 대체를 조사할 수 있는지 확인
    /// </summary>
    /// <param name="expectedId">조사 시작 당시 ID</param>
    /// <param name="featureChanged">일반 기능의 변경 유지 여부</param>
    /// <param name="isHeld">현재 보유 여부</param>
    public bool CanInvestigate (
        int expectedId, bool featureChanged, bool isHeld )
    {
        return Matches( expectedId ) == true &&
               featureChanged == true &&
               isHeld == false;
    }

    /// <summary>
    /// 구현된 대체 유형인지 확인
    /// </summary>
    /// <param name="type">확인할 대체 유형</param>
    public static bool CheckSupportedType ( ReplaceType type )
    {
        return type == ReplaceType.Color ||
               type == ReplaceType.Appearance;
    }

    /// <summary>
    /// 새 대체를 시작할 수 있는지 확인
    /// </summary>
    /// <param name="type">요청할 대체 유형</param>
    /// <param name="featureChanged">일반 기능의 변경 여부</param>
    /// <param name="isHeld">현재 보유 여부</param>
    public bool CanChange (
        ReplaceType type, bool featureChanged, bool isHeld )
    {
        return CheckSupportedType( type ) == true &&
               _isChanged == false &&
               featureChanged == false &&
               isHeld == false;
    }

}
