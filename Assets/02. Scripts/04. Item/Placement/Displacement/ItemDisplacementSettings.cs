using System;
using UnityEngine;

/// <summary>
/// 주변 이동 목적지 탐색 설정
/// </summary>
[Serializable]
public class ItemDisplacementSettings
{
    [Header("----- 이동 -----")]
    [SerializeField] [Min( 0.01f )] float _minDist = 1f;        //최소 이동 거리
    [SerializeField] [Min( 0.01f )] float _maxDist = 3f;        //최대 이동 거리
    [SerializeField] [Min( 1 )] int _tryCount = 20;        //시도 횟수

    [SerializeField] LayerMask _floorMask;        //바닥 감지용 레이어마스크
    [SerializeField] [Min( 0.01f )] float _rayHeight = 1f;        //레이 높이
    [SerializeField] [Min( 0.01f )] float _searchDepth = 5f;        //탐색 깊이

    /// <summary>
    /// 최소 이동 거리
    /// </summary>
    public float MinDist => _minDist;

    /// <summary>
    /// 최대 이동 거리
    /// </summary>
    public float MaxDist => _maxDist;

    /// <summary>
    /// 시도 횟수
    /// </summary>
    public int TryCount => _tryCount;

    /// <summary>
    /// 바닥 감지용 레이어마스크
    /// </summary>
    public LayerMask FloorMask => _floorMask;

    /// <summary>
    /// 레이 높이
    /// </summary>
    public float RayHeight => _rayHeight;

    /// <summary>
    /// 탐색 깊이
    /// </summary>
    public float SearchDepth => _searchDepth;

    /// <summary>
    /// 탐색 값이 유효한지 여부
    /// </summary>
    public bool IsValid => CheckValid( );


    /// <summary>
    /// 유한한 양수인지 확인
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>유한한 양수 여부</returns>
    static bool IsPositive ( float value )
    {
        return float.IsNaN( value ) == false &&
               float.IsInfinity( value ) == false &&
               value > 0f;
    }

    /// <summary>
    /// 이동 거리와 바닥 탐색 설정의 유효성 확인
    /// </summary>
    /// <returns>이동 설정 유효 여부</returns>
    bool CheckValid ()
    {
        return IsPositive( MinDist ) && IsPositive( MaxDist ) &&
               MaxDist >= MinDist && TryCount > 0 && FloorMask.value != 0 &&
               IsPositive( RayHeight ) && IsPositive( SearchDepth ) && IsPositive( RayHeight + SearchDepth );
    }


}

