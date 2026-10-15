using System.Collections.Generic;

/// <summary>
/// 절도 대상 데이터 - 활성 상태, 보유 방식과 접근 정보
/// </summary>
public struct StealTargetData
{
    bool _isActive;     //대상 활성 여부
    HoldType _holdType;     //대상 보유 방식
    float _dist;        //적 위치와 음식 중심 사이의 직선 거리
    bool _canApproach;      //현재 접근 단계의 비행 경로가 비어 있는지 여부

    /// <summary>
    /// 대상 활성 여부
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 대상 보유 방식
    /// </summary>
    public HoldType HoldType => _holdType;

    /// <summary>
    /// 적 위치와 음식 중심 사이의 직선 거리
    /// </summary>
    public float Dist => _dist;

    /// <summary>
    /// 현재 접근 단계의 비행 경로가 비어 있는지 여부
    /// </summary>
    public bool CanApproach => _canApproach;

    /// <summary>
    /// 절도 대상 정보 초기화
    /// </summary>
    /// <param name="isActive">대상 활성 여부</param>
    /// <param name="holdType">대상 보유 방식</param>
    /// <param name="dist">적 위치와 음식 중심 사이의 직선 거리</param>
    /// <param name="canApproach">현재 접근 단계의 비행 경로가 비어 있는지 여부</param>
    public StealTargetData (
        bool isActive, HoldType holdType,
        float dist, bool canApproach )
    {
        _isActive = isActive;
        _holdType = holdType;
        _dist = dist;
        _canApproach = canApproach;
    }
}
