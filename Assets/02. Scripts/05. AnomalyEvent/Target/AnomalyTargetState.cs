using UnityEngine;

/// <summary>
/// 변칙 대상 상태 - 정상 위치, 회전과 배치 정보 보관
/// </summary>
public class AnomalyTargetState
{
    Vector3 _position;      //위치
    Quaternion _rotation;       //회전
    bool _active;       //저장 당시 오브젝트 자체의 활성 상태(activeSelf)
    Transform _parent;      //부모 오브젝트
    PlacementTarget _placementTarget;       //배치 대상


    /// <summary>
    /// 월드 위치
    /// </summary>
    public Vector3 Position => _position;
    /// <summary>
    /// 월드 회전
    /// </summary>
    public Quaternion Rotation => _rotation;
    /// <summary>
    /// 저장 당시 오브젝트 자체의 활성 상태
    /// </summary>
    public bool Active => _active;
    /// <summary>
    /// 부모 오브젝트
    /// </summary>
    public Transform Parent => _parent;
    /// <summary>
    /// 배치 대상
    /// </summary>
    public PlacementTarget PlacementTarget => _placementTarget;


    /// <summary>
    /// 정상 위치와 회전 및 배치 상태 생성
    /// </summary>
    /// <param name="position">월드 위치</param>
    /// <param name="rotation">월드 회전</param>
    /// <param name="active">저장 당시 activeSelf</param>
    /// <param name="parent">부모</param>
    /// <param name="placementTarget">배치 대상</param>
    public AnomalyTargetState (
        Vector3 position , Quaternion rotation , bool active ,
        Transform parent , PlacementTarget placementTarget )
    {
        _position = position;
        _rotation = rotation;
        _active = active;
        _parent = parent;
        _placementTarget = placementTarget;
    }
}
