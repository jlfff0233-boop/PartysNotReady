using UnityEngine;

/// <summary>
/// 배치 표면 설정 - 신규 구성 값과 기존 설정 수정 항목 보관
/// </summary>
public class PlacementSurfaceOptions
{
    bool _changeLayer;     //표면 레이어 변경
    int _layer;     //변경할 표면 레이어
    PlacementType _type = PlacementType.Free;     //신규 배치 방식
    FixedPlacementType _fixedType;     //신규 고정 배치 방식
    float _space = 0.05f;     //고정 배치 간격
    int _tries = 20;     //무작위 배치 탐색 횟수
    LayerMask _itemMask;     //배치 공간 검사 레이어
    FoodPlacementType _food;     //음식 배치 용도
    PreparationLocationType _preparation;     //준비 평가 용도
    bool _editPlacement;     //기존 배치 방식 수정
    bool _editMask;     //기존 공간 검사 레이어 수정
    bool _editFood;     //기존 음식 배치 용도 수정
    bool _editPreparation;     //기존 준비 평가 용도 수정

    /// <summary>
    /// 표면 레이어 변경 여부
    /// </summary>
    public bool ChangeLayer { get => _changeLayer; set => _changeLayer = value; }
    /// <summary>
    /// 적용할 표면 레이어
    /// </summary>
    public int Layer { get => _layer; set => _layer = value; }
    /// <summary>
    /// 아이템 배치 방식
    /// </summary>
    public PlacementType Type { get => _type; set => _type = value; }
    /// <summary>
    /// 고정 배치 위치 선정 방식
    /// </summary>
    public FixedPlacementType FixedType { get => _fixedType; set => _fixedType = value; }
    /// <summary>
    /// 고정 배치 간격
    /// </summary>
    public float Space { get => _space; set => _space = value; }
    /// <summary>
    /// 무작위 위치 탐색 횟수
    /// </summary>
    public int Tries { get => _tries; set => _tries = value; }
    /// <summary>
    /// 배치 공간 검사 레이어
    /// </summary>
    public LayerMask ItemMask { get => _itemMask; set => _itemMask = value; }
    /// <summary>
    /// 음식 배치 허용 용도
    /// </summary>
    public FoodPlacementType Food { get => _food; set => _food = value; }
    /// <summary>
    /// 준비 목표 평가용 표면 구분
    /// </summary>
    public PreparationLocationType Preparation { get => _preparation; set => _preparation = value; }
    /// <summary>
    /// 기존 배치 방식 수정 여부
    /// </summary>
    public bool EditPlacement { get => _editPlacement; set => _editPlacement = value; }
    /// <summary>
    /// 기존 공간 검사 레이어 수정 여부
    /// </summary>
    public bool EditMask { get => _editMask; set => _editMask = value; }
    /// <summary>
    /// 기존 음식 배치 용도 수정 여부
    /// </summary>
    public bool EditFood { get => _editFood; set => _editFood = value; }
    /// <summary>
    /// 기존 준비 평가 용도 수정 여부
    /// </summary>
    public bool EditPreparation { get => _editPreparation; set => _editPreparation = value; }
}
