using UnityEngine;

/// <summary>
/// 포획 팔로워 - 보유 지점의 위치와 회전 적용
/// </summary>
public class CaptureFollower
{
    Transform _target;		//자신 트랜스폼
    Transform _carryPoint;		//포획자의 보유 지점

    /// <summary>
    /// 따라가는 중 여부
    /// </summary>
    public bool IsFollowing =>
        ReferenceEquals( _carryPoint, null ) == false;

    /// <summary>
    /// 따라가기 가능 여부
    /// </summary>
    public bool CanFollow =>
        _target != null && _carryPoint != null &&
        _carryPoint.gameObject.activeInHierarchy;

    /// <summary>
    /// 자신 트랜스폼 연결
    /// </summary>
    /// <param name="target">자신 트랜스폼</param>
    public CaptureFollower ( Transform target )
    {
        _target = target;
    }

    /// <summary>
    /// 보유 지점 연결
    /// </summary>
    /// <param name="carryPoint">보유 지점</param>
    /// <returns>시작 여부</returns>
    public bool TryBegin ( Transform carryPoint )
    {
        if ( IsFollowing == true || _target == null || carryPoint == null ||
             carryPoint.gameObject.activeInHierarchy == false )
            return false;

        _carryPoint = carryPoint;
        return true;
    }

    /// <summary>
    /// 보유 지점 따라가기
    /// </summary>
    /// <returns>따라가기 여부</returns>
    public bool TryFollow ()
    {
        //소실된 지점의 종료와 복구는 호출자가 처리
        if ( CanFollow == false )
            return false;

        _target.SetPositionAndRotation( _carryPoint.position, _carryPoint.rotation );
        return true;
    }

    /// <summary>
    /// 따라가기 종료
    /// </summary>
    public void End ()
    {
        _carryPoint = null;
    }
}
