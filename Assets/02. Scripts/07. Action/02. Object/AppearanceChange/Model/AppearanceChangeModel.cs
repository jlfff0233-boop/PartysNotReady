using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 외형 변경 모델 - 후보 선택과 복원용 설정 관리
/// </summary>
public class AppearanceChangeModel
{
    readonly List<AppearanceData> _candidates = new( );     //남은 후보
    readonly HashSet<string> _candidateIds =
        new( StringComparer.Ordinal );     //후보 ID 중복 검사

    AppearanceData _original;     //변경 직전 외형 설정
    bool _isChanged;     //변경 후 복원 또는 유지 확정을 기다리는 상태

    /// <summary>
    /// 복원 정보가 남아 있는 변경 여부
    /// </summary>
    public bool IsChanged => _isChanged;

    /// <summary>
    /// 변경 직전 외형 설정
    /// </summary>
    public AppearanceData Original => _original;


    #region ----- 후보 선택 -----

    /// <summary>
    /// 현재와 중복되지 않는 외형 후보 수집
    /// </summary>
    /// <param name="appearances">설정된 외형 후보 목록</param>
    /// <param name="current">현재 외형 설정</param>
    /// <param name="sources">설정별로 조회한 외형 원본 식별 정보</param>
    /// <param name="isHeld">현재 보유 여부</param>
    /// <returns>사용할 후보 존재 여부</returns>
    public bool TryPrepareCandidates (
        IReadOnlyList<AppearanceData> appearances,
        AppearanceData current,
        IReadOnlyDictionary<AppearanceData, GameObject> sources, bool isHeld )
    {
        //이전 시도의 후보가 다음 요청에 남지 않도록 먼저 정리
        _candidates.Clear( );
        _candidateIds.Clear( );

        if ( _isChanged == true || isHeld == true ||
             appearances == null || sources == null || current == null ||
             string.IsNullOrWhiteSpace( current.Id ) == true )
            return false;

        foreach ( AppearanceData candidate in appearances )
        {
            if ( CheckCandidate( candidate, current, sources ) == false )
                continue;

            //ID가 같으면 같은 후보로 취급
            if ( _candidateIds.Add( candidate.Id ) == false )
                continue;

            _candidates.Add( candidate );
        }

        return _candidates.Count > 0;
    }

    /// <summary>
    /// 남은 외형 후보 중 하나를 무작위 선택
    /// </summary>
    /// <param name="selected">선택한 외형, 후보가 없으면 null</param>
    /// <returns>후보 선택 성공 여부</returns>
    public bool TryTakeCandidate ( out AppearanceData selected )
    {
        selected = null;

        if ( _isChanged == true )
            return false;

        while ( _candidates.Count > 0 )
        {
            int index = Random.Range( 0, _candidates.Count );
            AppearanceData candidate = _candidates [ index ];
            //배치에 실패한 외형을 같은 요청에서 다시 뽑지 않도록 먼저 제외
            _candidates.RemoveAt( index );

            //준비 이후 설정 객체가 제거된 경우 다음 후보 확인
            if ( candidate == null )
                continue;

            selected = candidate;
            return true;
        }

        return false;
    }

    #endregion


    /// <summary>
    /// 변경 직전 외형 설정 저장
    /// </summary>
    /// <param name="original">변경 직전 외형 설정</param>
    public void BeginChange ( AppearanceData original )
    {
        _original = original;
        _isChanged = true;

        //성공 이후 후보 정리
        _candidates.Clear( );
        _candidateIds.Clear( );
    }

    /// <summary>
    /// 변경 상태와 복원용 설정 초기화
    /// </summary>
    public void EndChange ()
    {
        _original = null;
        _isChanged = false;

        _candidates.Clear( );
        _candidateIds.Clear( );
    }


    #region ----- 설정/상태 검사 -----

    /// <summary>
    /// 이전 외형으로 복원할 수 있는지 확인
    /// </summary>
    /// <param name="isHeld">현재 보유 여부</param>
    /// <returns>복원을 시도할 수 있는지 여부</returns>
    public bool CanRestore ( bool isHeld )
    {
        return _isChanged == true &&
               _original != null &&
               isHeld == false;
    }

    /// <summary>
    /// 후보의 설정과 외형 중복 확인
    /// </summary>
    /// <param name="candidate">확인할 후보</param>
    /// <param name="current">현재 외형 설정</param>
    /// <param name="sources">설정별 외형 원본 식별 정보</param>
    /// <returns>후보 등록 가능 여부</returns>
    bool CheckCandidate (
        AppearanceData candidate, AppearanceData current,
        IReadOnlyDictionary<AppearanceData, GameObject> sources )
    {
        if ( candidate == null || candidate == current ||
             candidate.Prefab == null ||
             string.IsNullOrWhiteSpace( candidate.Id ) == true )
            return false;

        if ( string.Equals(
            candidate.Id, current.Id, StringComparison.Ordinal ) == true )
            return false;

        if ( CheckTransform( candidate ) == false )
            return false;

        //ID가 달라도 프리팹과 보정값이 같으면 중복 외형으로 제외
        if ( IsSameAppearance( candidate, current, sources ) == true )
            return false;

        foreach ( AppearanceData registered in _candidates )
        {
            if ( IsSameAppearance( candidate, registered, sources ) == true )
                return false;
        }

        return true;
    }

    /// <summary>
    /// 같은 외형과 배치 보정값을 사용하는지 확인
    /// </summary>
    /// <param name="left">비교할 첫 번째 설정</param>
    /// <param name="right">비교할 두 번째 설정</param>
    /// <param name="sources">설정별 외형 원본 식별 정보</param>
    /// <returns>동일한 외형과 보정값 여부</returns>
    bool IsSameAppearance (
        AppearanceData left, AppearanceData right,
        IReadOnlyDictionary<AppearanceData, GameObject> sources )
    {
        if ( left == null || right == null ||
             left.Prefab == null || right.Prefab == null )
            return false;

        if ( left.LocalPosition != right.LocalPosition ||
             left.LocalRotation != right.LocalRotation ||
             left.LocalScale != right.LocalScale )
            return false;

        if ( left.Prefab == right.Prefab )
            return true;

        //모델은 전달받은 원본 식별 정보만 비교하며 프리팹 내부를 조회하지 않음
        return sources.TryGetValue( left, out GameObject leftSource ) &&
               sources.TryGetValue( right, out GameObject rightSource ) &&
               leftSource != null && leftSource == rightSource;
    }

    /// <summary>
    /// 외형의 위치, 회전과 크기 설정 확인
    /// </summary>
    /// <param name="data">검사할 외형 설정</param>
    /// <returns>적용 가능한 보정값 여부</returns>
    bool CheckTransform ( AppearanceData data )
    {
        Vector3 position = data.LocalPosition;
        Quaternion rotation = data.LocalRotation;
        Vector3 scale = data.LocalScale;

        return CheckFinite( position.x ) &&
               CheckFinite( position.y ) &&
               CheckFinite( position.z ) &&
               CheckFinite( rotation.x ) &&
               CheckFinite( rotation.y ) &&
               CheckFinite( rotation.z ) &&
               CheckFinite( rotation.w ) &&
               CheckFinite( scale.x ) &&
               CheckFinite( scale.y ) &&
               CheckFinite( scale.z ) &&
               scale.x > 0f && scale.y > 0f && scale.z > 0f;
    }

    /// <summary>
    /// NaN이나 무한대가 아닌 값인지 확인
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>유한한 값 여부</returns>
    bool CheckFinite ( float value )
    {
        return float.IsNaN( value ) == false &&
               float.IsInfinity( value ) == false;
    }

    #endregion

}
