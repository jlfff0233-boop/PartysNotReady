using UnityEngine;

/// <summary>
/// 외형 데이터 - ID, 프리팹과 배치 보정값 보관
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Action/AppearanceData" )]
public class AppearanceData : ScriptableObject
{
    [SerializeField] string _id;     //외형 아이디
    [SerializeField] GameObject _prefab;     //AppearanceComponents 하나를 포함한 아이템 또는 외형 프리팹

    [Header( "----- 로컬 보정 -----" )]
    [SerializeField] Vector3 _localPosition;     //외형 부모 기준 위치
    [SerializeField] Vector3 _localEulerAngles;     //외형 부모 기준 회전
    [SerializeField] Vector3 _localScale = Vector3.one;     //외형 부모 기준 크기

    /// <summary>
    /// 외형 아이디
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// 생성할 AppearanceComponents를 포함한 프리팹
    /// </summary>
    public GameObject Prefab => _prefab;

    /// <summary>
    /// 적용할 로컬 위치
    /// </summary>
    public Vector3 LocalPosition => _localPosition;

    /// <summary>
    /// 적용할 로컬 회전
    /// </summary>
    public Quaternion LocalRotation => Quaternion.Euler( _localEulerAngles );

    /// <summary>
    /// 적용할 로컬 크기
    /// </summary>
    public Vector3 LocalScale => _localScale;
}
