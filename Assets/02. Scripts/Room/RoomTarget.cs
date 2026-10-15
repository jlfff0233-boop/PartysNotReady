using UnityEngine;

/// <summary>
/// 방 대상 - 등록, 위치 제공
/// </summary>
[DisallowMultipleComponent]
public class RoomTarget : MonoBehaviour, IRoomTarget
{
    /// <summary>
    /// 방 소속을 판단할 위치
    /// </summary>
    public Transform TargetTransform => transform;

    /// <summary>
    /// 방 대상 조회 참여 여부
    /// </summary>
    public bool IsRoomTargetActive => isActiveAndEnabled;


    private void OnEnable ()
    {
        RoomRegister.RegisterTarget( this );
    }

    private void OnDisable ()
    {
        RoomRegister.UnregisterTarget( this );
    }
}