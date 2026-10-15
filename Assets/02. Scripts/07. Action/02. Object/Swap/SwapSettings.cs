using System;
using UnityEngine;

/// <summary>
/// 교체 검사 설정 - 지지 표면 거리와 배치 공간 허용 범위
/// </summary>
[Serializable]
public class SwapSettings
{
    [Header( "----- 표면 검사 -----" )]
    [SerializeField][Min( 0f )] float _supportDistance = 0.1f;        //현재 지지 표면 탐색 추가 거리
    [SerializeField][Min( 0f )] float _surfaceGap = 0.002f;        //목적지 표면과 둘 간격
    [SerializeField][Min( 0f )] float _surfaceTolerance = 0.001f;        //표면 높이 허용 오차

    [Header( "----- 공간 검사 -----" )]
    [SerializeField] LayerMask _obstacleMask = ~0;        //주변 장애물 검사 레이어
    [SerializeField][Min( 0f )] float _contactTolerance = 0.001f;        //공간 검사 형상을 줄일 거리

    /// <summary>
    /// 현재 지지 표면 탐색 추가 거리
    /// </summary>
    public float SupportDistance => _supportDistance;

    /// <summary>
    /// 목적지 표면과 둘 간격
    /// </summary>
    public float SurfaceGap => _surfaceGap;

    /// <summary>
    /// 표면 높이 허용 오차
    /// </summary>
    public float SurfaceTolerance => _surfaceTolerance;

    /// <summary>
    /// 주변 장애물 검사 레이어
    /// </summary>
    public LayerMask ObstacleMask => _obstacleMask;

    /// <summary>
    /// 공간 검사 형상을 줄일 거리
    /// </summary>
    public float ContactTolerance => _contactTolerance;
}
