using UnityEngine;

/// <summary>
/// 아이템 배치 상태 - 위치와 회전, 부모와 지지 표면 보관
/// </summary>
public struct ItemPlacementState
{
    Transform _root;        //상태를 저장한 아이템 루트
    Vector3 _position;        //월드 위치
    Quaternion _rotation;        //월드 회전
    Vector3 _localScale;        //부모 기준 배율
    Transform _parent;        //부모
    bool _hadParent;        //저장 당시 부모 존재 여부
    Collider _surface;        //기록된 지지 표면
    bool _hadSurface;        //저장 당시 지지 표면 존재 여부
    Vector3 _localNormal;        //지지 표면의 로컬 좌표 기준 법선

    /// <summary>
    /// 상태를 저장한 아이템 루트
    /// </summary>
    public Transform Root => _root;

    /// <summary>
    /// 월드 위치
    /// </summary>
    public Vector3 Position => _position;

    /// <summary>
    /// 월드 회전
    /// </summary>
    public Quaternion Rotation => _rotation;

    /// <summary>
    /// 부모 기준 배율
    /// </summary>
    public Vector3 LocalScale => _localScale;

    /// <summary>
    /// 부모
    /// </summary>
    public Transform Parent => _parent;

    /// <summary>
    /// 저장 당시 부모 존재 여부
    /// </summary>
    public bool HadParent => _hadParent;

    /// <summary>
    /// 기록된 지지 표면
    /// </summary>
    public Collider Surface => _surface;

    /// <summary>
    /// 저장 당시 지지 표면 존재 여부
    /// </summary>
    public bool HadSurface => _hadSurface;

    /// <summary>
    /// 지지 표면의 로컬 좌표 기준 법선
    /// </summary>
    public Vector3 LocalNormal => _localNormal;

    /// <summary>
    /// 현재 위치, 회전과 배치 표면 기록 저장
    /// </summary>
    /// <param name="root">유효성을 확인한 아이템 루트</param>
    /// <param name="surface">배치 모듈에 기록된 지지 표면</param>
    /// <param name="localNormal">표면의 로컬 좌표 기준 법선</param>
    public ItemPlacementState (
        Transform root, Collider surface, Vector3 localNormal )
    {
        _root = root;
        _position = root.position;
        _rotation = root.rotation;
        _localScale = root.localScale;
        _parent = root.parent;
        //처음부터 부모가 없었던 상태와 저장 후 부모가 삭제된 상태를 구분
        _hadParent = _parent != null;
        _surface = surface;
        _hadSurface = surface != null;
        _localNormal = localNormal;
    }
}
