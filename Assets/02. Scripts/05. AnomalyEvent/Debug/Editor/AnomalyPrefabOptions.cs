using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 원본 공통 설정 - 참조 연결과 기능별 일괄 적용값 선택
/// </summary>
public class AnomalyPrefabOptions
{
    public bool BuildVisual;		//전체 외형 자식 구성
    public bool BuildRestoreArea;		//빈자리 조사 영역 구성
    public int RestoreLayer;		//새 조사 영역의 조준 레이어
    public float RestoreMargin = 0.02f;		//루트 기준 조사 영역 여유
    public bool ConnectReferences;		//명확한 참조 연결
    public bool ReplaceReferences;		//기존 참조도 후보로 교체
    public bool ChangePresenceMask;		//존재 장애물 마스크 변경
    public int PresenceMask;		//존재 장애물 레이어
    public bool ChangeAppearanceMask;		//외형 장애물 마스크 변경
    public int AppearanceMask;		//외형 장애물 레이어
    public bool ChangeFloorMask;		//개폐 바닥 마스크 변경
    public int FloorMask;		//개폐 바닥 레이어
    public bool ChangePresence;		//초기 존재 여부 변경
    public bool InitiallyPresent = true;		//처음부터 정상 물건 표시
    public bool ChangeRestoreTime;		//존재 복구 시간 변경
    public float RefillDuration = 3f;		//복구 대기 시간
    public float CheckInterval = 0.25f;		//공간 재검사 간격
    public bool ChangeAxis;		//회전 축 변경
    public Vector3 Axis = Vector3.forward;		//초기 상태 기준 회전 축
    public bool ChangeItemData;		//아이템 데이터 변경
    public ItemData ItemData;		//공통 아이템 데이터
    public bool ChangeMaterialMap;		//색 대체 목록 변경
    public MaterialMap MaterialMap;		//공통 색 대체 목록
    public bool ChangeAppearanceMap;		//외형 대체 목록 변경
    public AppearanceMap AppearanceMap;		//공통 외형 대체 목록

    /// <summary>
    /// 선택한 설정 유무
    /// </summary>
    public bool HasSelection => BuildVisual || BuildRestoreArea || ConnectReferences || ChangePresenceMask || ChangeAppearanceMask ||
        ChangeFloorMask || ChangePresence || ChangeRestoreTime || ChangeAxis ||
        ChangeItemData || ChangeMaterialMap || ChangeAppearanceMap;

    /// <summary>
    /// 창 활성화 후 기본 판정 마스크 구성
    /// </summary>
    public void Init ()
    {
        PresenceMask = LayerMask.GetMask( "InteractableItem", "Player", "Npc", "Enemy", "Wall" );
        AppearanceMask = PresenceMask | LayerMask.GetMask( "Floor", "PlacementTarget" );
        FloorMask = LayerMask.GetMask( "Floor" );
        RestoreLayer = LayerMask.NameToLayer( "InteractableItem" );
    }

    /// <summary>
    /// 적용 후 설정 선택 해제
    /// </summary>
    public void ClearSelection ()
    {
        ConnectReferences = ReplaceReferences = ChangePresenceMask = ChangeAppearanceMask = false;
        ChangeFloorMask = ChangePresence = ChangeRestoreTime = ChangeAxis = false;
        ChangeItemData = ChangeMaterialMap = ChangeAppearanceMap = false;
        BuildVisual = BuildRestoreArea = false;
    }

    /// <summary>
    /// 실제 레이어 번호와 화면의 연속 선택 번호 변환
    /// </summary>
    /// <param name="label">화면 이름</param>
    /// <param name="mask">실제 레이어 비트</param>
    /// <returns>사용자가 선택한 실제 레이어 비트</returns>
    static int DrawMask ( string label, int mask )
    {
        string[] names = InternalEditorUtility.layers;
        int selected = 0;
        for ( int i = 0; i < names.Length; i++ )
        {
            if ( ( mask & ( 1 << LayerMask.NameToLayer( names[i] ) ) ) != 0 )
                selected |= 1 << i;
        }
        int next = EditorGUILayout.MaskField( label, selected, names );
        if ( next == selected )
            return mask;
        int result = 0;
        for ( int i = 0; i < names.Length; i++ )
        {
            if ( ( next & ( 1 << i ) ) != 0 )
                result |= 1 << LayerMask.NameToLayer( names[i] );
        }
        return result;
    }

    /// <summary>
    /// 참조와 기능별 공통 설정 입력 표시
    /// </summary>
    public void Draw ()
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "회전 / 흔들림 / 추가 / 제거 변칙 - 외형 구성", EditorStyles.boldLabel );
        BuildVisual = EditorGUILayout.ToggleLeft( "전체 외형 자식 구성", BuildVisual );
        BuildRestoreArea = EditorGUILayout.ToggleLeft( "추가 / 제거 변칙 - 빈자리 조사 영역 생성", BuildRestoreArea );
        using ( new EditorGUI.DisabledScope( BuildRestoreArea == false ) )
        {
            RestoreLayer = EditorGUILayout.LayerField( "새 조사 영역 레이어", RestoreLayer );
            RestoreMargin = Mathf.Max( 0f, EditorGUILayout.FloatField( "크기 여유 (루트 단위)", RestoreMargin ) );
        }
        if ( BuildVisual == true || BuildRestoreArea == true )
            EditorGUILayout.HelpBox( "구조 구성은 단일 물건 원본에 적용합니다. 회전·흔들림·존재 표시 참조도 구성한 외형에 연결합니다. 기존 유효 조사 영역은 유지하며 새 영역의 레이어는 플레이어 상호작용 탐지 마스크에 포함해야 합니다.", MessageType.Info );
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "기존 변칙 기능 설정 (선택한 항목만 적용)", EditorStyles.boldLabel );
        ConnectReferences = EditorGUILayout.ToggleLeft( "명확한 참조 자동 연결", ConnectReferences );
        using ( new EditorGUI.DisabledScope( ConnectReferences == false ) )
            ReplaceReferences = EditorGUILayout.ToggleLeft( "기존 참조도 다시 지정", ReplaceReferences );
        ChangePresenceMask = EditorGUILayout.ToggleLeft( "추가 / 제거 변칙 - 장애물 레이어 설정", ChangePresenceMask );
        using ( new EditorGUI.DisabledScope( ChangePresenceMask == false ) )
            PresenceMask = DrawMask( "존재 장애물", PresenceMask );
        ChangeAppearanceMask = EditorGUILayout.ToggleLeft( "외형 대체 변칙 - 장애물 레이어 설정", ChangeAppearanceMask );
        using ( new EditorGUI.DisabledScope( ChangeAppearanceMask == false ) )
            AppearanceMask = DrawMask( "외형 대체 장애물", AppearanceMask );
        ChangeFloorMask = EditorGUILayout.ToggleLeft( "개폐 변칙 - 바닥 레이어 설정", ChangeFloorMask );
        using ( new EditorGUI.DisabledScope( ChangeFloorMask == false ) )
            FloorMask = DrawMask( "바닥", FloorMask );
        ChangePresence = EditorGUILayout.ToggleLeft( "추가 / 제거 변칙 - 초기 존재 상태 설정", ChangePresence );
        using ( new EditorGUI.DisabledScope( ChangePresence == false ) )
            InitiallyPresent = EditorGUILayout.Toggle( "처음부터 물건 있음", InitiallyPresent );
        ChangeRestoreTime = EditorGUILayout.ToggleLeft( "추가 / 제거 변칙 - 복구 시간 설정", ChangeRestoreTime );
        using ( new EditorGUI.DisabledScope( ChangeRestoreTime == false ) )
        {
            RefillDuration = Mathf.Max( 0f, EditorGUILayout.FloatField( "복구 대기 시간", RefillDuration ) );
            CheckInterval = Mathf.Max( 0.05f, EditorGUILayout.FloatField( "공간 검사 간격", CheckInterval ) );
        }
        ChangeAxis = EditorGUILayout.ToggleLeft( "회전 변칙 - 회전 축 설정", ChangeAxis );
        using ( new EditorGUI.DisabledScope( ChangeAxis == false ) )
            Axis = EditorGUILayout.Vector3Field( "로컬 회전 축", Axis );
        ChangeItemData = EditorGUILayout.ToggleLeft( "낙하 / 이동 / 교체 변칙 공통 - 아이템 데이터 설정", ChangeItemData );
        using ( new EditorGUI.DisabledScope( ChangeItemData == false ) )
            ItemData = (ItemData)EditorGUILayout.ObjectField( "아이템 데이터", ItemData, typeof( ItemData ), false );
        ChangeMaterialMap = EditorGUILayout.ToggleLeft( "색 대체 변칙 - 대체 목록 설정", ChangeMaterialMap );
        using ( new EditorGUI.DisabledScope( ChangeMaterialMap == false ) )
            MaterialMap = (MaterialMap)EditorGUILayout.ObjectField( "색 대체 목록", MaterialMap, typeof( MaterialMap ), false );
        ChangeAppearanceMap = EditorGUILayout.ToggleLeft( "외형 대체 변칙 - 대체 목록 설정", ChangeAppearanceMap );
        using ( new EditorGUI.DisabledScope( ChangeAppearanceMap == false ) )
            AppearanceMap = (AppearanceMap)EditorGUILayout.ObjectField( "외형 대체 목록", AppearanceMap, typeof( AppearanceMap ), false );
    }
}
