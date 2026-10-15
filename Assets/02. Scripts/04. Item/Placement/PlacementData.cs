using UnityEngine;

/// <summary>
/// 아이템 배치 타입
/// </summary>
public enum PlacementType
{
    Fixed,      //지정 위치 고정
    Free        //표면 위 자유 배치
}

/// <summary>
/// 고정 배치 위치 선정 타입
/// </summary>
public enum FixedPlacementType
{
    Linear,     //직선 배치
    Random      //무작위 위치 배치
}

/// <summary>
/// 아이템 배치 정보 - 적용할 위치, 회전과 고정 설정
/// </summary>
public struct PlacementData
{
    Vector3 _position;      //배치 위치
    Quaternion _rotation;       //배치 회전
    Vector3 _surfaceNormal;     //아이템 회전과 독립적인 표면 방향
    Transform _parent;      //배치 후 부모
    PlacementType _type;        //아이템 배치 타입
    bool _isFixed;      //배치 후 고정 여부
    PlacementTarget _target;     //실제 배치 대상
    bool _positionIsPivot;      //바닥 보정이 끝난 루트 위치 여부

    /// <summary>
    /// 월드 배치 위치
    /// </summary>
    public Vector3 Position => _position;
    /// <summary>
    /// 월드 배치 회전
    /// </summary>
    public Quaternion Rotation => _rotation;
    /// <summary>
    /// 월드 표면 방향
    /// </summary>
    public Vector3 SurfaceNormal => _surfaceNormal;
    /// <summary>
    /// 배치 후 부모
    /// </summary>
    public Transform Parent => _parent;
    /// <summary>
    /// 아이템 배치 타입
    /// </summary>
    public PlacementType PlacementType => _type;
    /// <summary>
    /// 배치 후 고정 여부
    /// </summary>
    public bool IsFixed => _isFixed;
    /// <summary>
    /// 실제 배치 대상
    /// </summary>
    public PlacementTarget Target => _target;

    /// <summary>
    /// 위치가 바닥 보정을 마친 루트 좌표인지 여부
    /// </summary>
    public bool PositionIsPivot => _positionIsPivot;


    /// <summary>
    /// 아이템 배치 정보 생성
    /// </summary>
    /// <param name="position">표면 위치 또는 보정이 끝난 루트 월드 위치</param>
    /// <param name="rotation">월드 배치 회전</param>
    /// <param name="parent">배치 후 부모</param>
    /// <param name="type">배치 타입</param>
    /// <param name="isFixed">배치 후 고정 여부</param>
    /// <param name="target">실제 배치 대상</param>
    /// <param name="surfaceNormal">월드 표면 방향, 생략 시 배치 회전의 위쪽 방향 사용</param>
    /// <param name="positionIsPivot">일반 내려놓기에서도 위치를 추가 보정하지 않을지 여부</param>
    public PlacementData (
        Vector3 position , Quaternion rotation ,
        Transform parent , PlacementType type , bool isFixed ,
        PlacementTarget target = null , Vector3 surfaceNormal = default ,
        bool positionIsPivot = false )
    {
        _position = position;
        _rotation = rotation;
        //표면 방향이 없으면 배치 회전의 위쪽 방향을 사용
        //직접 지정한 방향은 크기에 영향을 받지 않도록 정규화
        _surfaceNormal = surfaceNormal.sqrMagnitude > 0f
            ? surfaceNormal.normalized : rotation * Vector3.up;
        _parent = parent;
        _type = type;
        _isFixed = isFixed;
        _target = target;
        _positionIsPivot = positionIsPivot;
    }

}
