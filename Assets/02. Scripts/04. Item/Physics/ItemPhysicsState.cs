using UnityEngine;

/// <summary>
/// 아이템 물리 상태 - 고정, 중력과 운동 상태 보관
/// </summary>
public struct ItemPhysicsState
{
    Rigidbody _body;        //상태를 저장한 리지드바디
    bool _isKinematic;        //고정 여부
    bool _useGravity;        //중력 사용 여부
    Vector3 _velocity;        //월드 이동 속도
    Vector3 _angularVelocity;        //월드 회전 속도
    bool _isSleeping;        //수면 여부

    /// <summary>
    /// 상태를 저장한 리지드바디
    /// </summary>
    public Rigidbody Body => _body;

    /// <summary>
    /// 고정 여부
    /// </summary>
    public bool IsKinematic => _isKinematic;

    /// <summary>
    /// 중력 사용 여부
    /// </summary>
    public bool UseGravity => _useGravity;

    /// <summary>
    /// 월드 이동 속도
    /// </summary>
    public Vector3 Velocity => _velocity;

    /// <summary>
    /// 월드 회전 속도
    /// </summary>
    public Vector3 AngularVelocity => _angularVelocity;

    /// <summary>
    /// 수면 여부
    /// </summary>
    public bool IsSleeping => _isSleeping;

    /// <summary>
    /// 복구할 물리 상태 저장
    /// </summary>
    /// <param name="body">유효성을 확인한 리지드바디</param>
    public ItemPhysicsState ( Rigidbody body )
    {
        _body = body;
        _isKinematic = body.isKinematic;
        _useGravity = body.useGravity;
        _velocity = body.linearVelocity;
        _angularVelocity = body.angularVelocity;
        _isSleeping = body.IsSleeping( );
    }
}
