using UnityEngine;

/// <summary>
/// 배치 표면 후보 - 충돌체 선택과 기존 배치 설정 및 처리 결과 보관
/// </summary>
public class PlacementSurfaceEntry
{
    GameObject _owner;     //후보 충돌체가 붙은 객체
    Collider _surface;     //선택한 대표 표면
    string _path;     //씬과 계층 경로
    string _assetPath;     //원본 적용 경로, 씬과 편집 화면에서는 빈 문자열
    bool _selected;     //일괄 적용 선택
    string _result = string.Empty;     //미리보기와 적용 결과

    /// <summary>
    /// 후보 충돌체 소유 객체
    /// </summary>
    public GameObject Owner => _owner;
    /// <summary>
    /// 씬과 계층 경로
    /// </summary>
    public string Path => _path;
    /// <summary>
    /// 직접 저장할 프리팹 원본 경로
    /// </summary>
    public string AssetPath => _assetPath;
    /// <summary>
    /// 선택한 대표 표면 충돌체
    /// </summary>
    public Collider Surface { get => _surface; set => _surface = value; }
    /// <summary>
    /// 일괄 적용 선택 여부
    /// </summary>
    public bool Selected { get => _selected; set => _selected = value; }
    /// <summary>
    /// 대상별 미리보기와 적용 결과
    /// </summary>
    public string Result { get => _result; set => _result = value; }

    /// <summary>
    /// 후보 구성 - 객체와 계층 경로 및 유일한 표면 연결
    /// </summary>
    /// <param name="owner">후보 충돌체 소유 객체</param>
    /// <param name="surface">명확한 대표 충돌체, 선택 필요 시 없음</param>
    /// <param name="path">씬과 계층 경로</param>
    /// <param name="assetPath">원본 적용 경로, 씬과 편집 화면에서는 빈 문자열</param>
    public PlacementSurfaceEntry ( GameObject owner, Collider surface, string path, string assetPath = "" )
    {
        _owner = owner;
        _surface = surface;
        _path = path;
        _assetPath = assetPath;
    }
}
