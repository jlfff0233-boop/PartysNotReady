using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 사운드 설정 창 - 데이터 값 편집, 대상 조회와 일괄 연결
/// </summary>
public class SoundSetupWindow : EditorWindow
{
    [SerializeField] List<SoundSetupEntry> _entries = new( );		//에셋별 데이터와 설정값
    [SerializeField] SoundData _jump;		//공용 점프음
    [SerializeField] SoundData _pickUp;		//공용 집기음
    [SerializeField] SoundData _dogSatisfied;		//공용 개 만족음
    [SerializeField] SoundData _crowTake;		//공용 음식 확보음
    [SerializeField] AudioMixerGroup _output;		//효과음 출력 그룹
    [SerializeField] int _scope;		//현재 씬, 선택 하위, 열린 프리팹
    [SerializeField] bool _overwrite;		//기존 데이터와 UI 재생기 참조 교체
    [SerializeField] bool _configureExisting;		//기존 음원 소스 설정 변경
    [SerializeField] bool _applyValues = true;		//연결 시 데이터 설정값도 적용

    /// <summary>
    /// 조회 범위에 포함된 사운드 연결 담당 컴포넌트
    /// </summary>
    List<Component> _targets = new( );

    Vector2 _scroll;		//창 스크롤
    bool _showData = true;		//데이터 설정표 펼침
    bool _showTargets = true;		//변경 대상 목록 펼침
    string _status = string.Empty;		//최근 적용 결과

    void OnEnable ()
    {
        minSize = new Vector2( 780f, 500f );
        if ( _entries.Count == 0 )
        {
            CreateEntries( );
            LoadData( );
        }
        RefreshTargets( );
    }

    void OnGUI ()
    {
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            _scroll = EditorGUILayout.BeginScrollView( _scroll );
            if ( GUILayout.Button( "비어 있는 데이터 자동 조회" ) == true )
                LoadData( );
            DrawShared( );
            DrawTargets( );
            EditorGUILayout.BeginHorizontal( );
            if ( GUILayout.Button( "데이터 값만 적용" ) == true )
                Apply( false );
            using ( new EditorGUI.DisabledScope( _targets.Count == 0 ) )
            {
                if ( GUILayout.Button( "대상 구성 / 연결 적용" ) == true )
                    Apply( true );
            }
            EditorGUILayout.EndHorizontal( );
            if ( string.IsNullOrEmpty( _status ) == false )
                EditorGUILayout.HelpBox( _status, MessageType.Info );
            DrawData( );
            EditorGUILayout.EndScrollView( );
        }
    }


    /// <summary>
    /// 사운드 설정 창 열기
    /// </summary>
    [MenuItem( "Tools/Sound/사운드 설정" )]
    static void Open ()
    {
        GetWindow<SoundSetupWindow>( "사운드 설정" );
    }


    void OnSelectionChange ()
    {
        RefreshTargets( );
        Repaint( );
    }

    void OnHierarchyChange ()
    {
        //구성 적용과 Undo 이후 삭제된 컴포넌트가 목록에 남지 않게 갱신
        RefreshTargets( );
        Repaint( );
    }

    #region ----- 데이터 -----

    /// <summary>
    /// 현재 에셋 25개의 초기 설정표 생성
    /// </summary>
    void CreateEntries ()
    {
        _entries.Add( new( "0_FootStep", new( 0.25f, 0.35f ), new( 0.95f, 1.05f ) ) );
        _entries.Add( new( "1_Resist", new( 0.30f, 0.30f ), Vector2.one ) );
        _entries.Add( new( "2_Land", new( 0.40f, 0.50f ), new( 0.95f, 1.05f ) ) );
        _entries.Add( new( "3_Dog_Bark", new( 0.50f, 0.60f ), new( 0.95f, 1.05f ) ) );
        _entries.Add( new( "4_Bear_Eat", new( 0.25f, 0.25f ), Vector2.one ) );
        _entries.Add( new( "5_Bear_Good", new( 0.50f, 0.60f ), new( 0.95f, 1.05f ) ) );
        _entries.Add( new( "6_Bear_Mad", new( 0.65f, 0.75f ), new( 0.95f, 1.05f ) ) );
        _entries.Add( new( "7_Attack", new( 0.55f, 0.65f ), new( 0.95f, 1.05f ) ) );
        _entries.Add( new( "8_Capture", new( 0.55f, 0.65f ), new( 0.95f, 1.05f ) ) );
        _entries.Add( new( "9_Collect", new( 0.25f, 0.25f ), Vector2.one ) );
        _entries.Add( new( "10_Crow_Fly", new( 0.30f, 0.30f ), Vector2.one ) );
        _entries.Add( new( "11_Place", new( 0.35f, 0.45f ), new( 0.95f, 1.05f ) ) );
        _entries.Add( new( "12_Throw", new( 0.40f, 0.50f ), new( 0.95f, 1.05f ) ) );
        _entries.Add( new( "13_Produce_Start", new( 0.40f, 0.40f ), Vector2.one ) );
        _entries.Add( new( "14_Produce_Cooking", new( 0.20f, 0.20f ), Vector2.one ) );
        _entries.Add( new( "15_Produce_MinigameSuccess", new( 0.45f, 0.45f ), Vector2.one ) );
        _entries.Add( new( "16_Produce_Fail", new( 0.50f, 0.50f ), Vector2.one ) );
        _entries.Add( new( "17_Produce_Complete", new( 0.60f, 0.60f ), Vector2.one ) );
        _entries.Add( new( "18_Button", new( 0.25f, 0.25f ), Vector2.one ) );
        _entries.Add( new( "19_Shop_Purchase", new( 0.50f, 0.50f ), Vector2.one ) );
        _entries.Add( new( "20_Shop_Fail", new( 0.45f, 0.45f ), Vector2.one ) );
        _entries.Add( new( "21_Mission_Complete", new( 0.65f, 0.65f ), Vector2.one ) );
        _entries.Add( new( "22_FinalRush", new( 0.65f, 0.65f ), Vector2.one ) );
        _entries.Add( new( "23_Game_Complete", new( 0.70f, 0.70f ), Vector2.one ) );
        _entries.Add( new( "24_Game_Over", new( 0.60f, 0.60f ), Vector2.one ) );
    }

    /// <summary>
    /// 현재 폴더에서 비어 있는 데이터 참조 조회
    /// </summary>
    void LoadData ()
    {
        foreach ( SoundSetupEntry entry in _entries )
        {
            if ( entry.Sound == null )
                entry.Sound = AssetDatabase.LoadAssetAtPath<SoundData>(
                    $"Assets/03. Configs/Sounds/{entry.Name}.asset" );
        }
    }

    /// <summary>
    /// 적용할 음량과 음높이 범위 정리
    /// </summary>
    /// <param name="value">입력 범위</param>
    /// <param name="min">허용 최솟값</param>
    /// <param name="max">허용 최댓값</param>
    /// <returns>정리한 범위</returns>
    Vector2 ClampRange ( Vector2 value, float min, float max )
    {
        value.x = Mathf.Clamp( value.x, min, max );
        value.y = Mathf.Clamp( value.y, value.x, max );
        return value;
    }

    /// <summary>
    /// 클립 참조를 유지하고 표의 데이터 설정값 적용
    /// </summary>
    void ApplyValues ()
    {
        foreach ( SoundSetupEntry entry in _entries )
        {
            if ( entry.Sound == null )
                continue;
            using SerializedObject settings = new( entry.Sound );		//음원 이외 재생 설정
            settings.FindProperty( "_volumeRange" ).vector2Value = ClampRange( entry.Volume, 0f, 1f );
            settings.FindProperty( "_pitchRange" ).vector2Value = ClampRange( entry.Pitch, 0.01f, 3f );
            settings.FindProperty( "_cooldown" ).floatValue = 0f;
            settings.ApplyModifiedProperties( );
        }
    }

    /// <summary>
    /// 기본 데이터와 공용 행동 데이터를 연결 순서로 구성
    /// </summary>
    /// <returns>기본 25개와 공용 4개 데이터</returns>
    SoundData [ ] GetSounds ()
    {
        SoundData [ ] sounds = new SoundData [ 29 ];		//행동별 참조 배열
        for ( int i = 0; i < _entries.Count; i++ )
            sounds [ i ] = _entries [ i ].Sound;
        sounds [ SoundSetupUtility.Jump ] = _jump;
        sounds [ SoundSetupUtility.PickUp ] = _pickUp;
        sounds [ SoundSetupUtility.DogSatisfied ] = _dogSatisfied;
        sounds [ SoundSetupUtility.CrowTake ] = _crowTake;
        return sounds;
    }

    #endregion

    #region ----- 대상 / 적용 -----

    /// <summary>
    /// 선택 범위에 포함된 씬 객체 조회
    /// </summary>
    /// <returns>조회할 최상위 객체</returns>
    GameObject [ ] GetRoots ()
    {
        if ( _scope == 2 )
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage( );
            return stage != null ? new [ ] { stage.prefabContentsRoot } : new GameObject [ 0 ];
        }
        if ( _scope == 1 )
        {
            List<GameObject> roots = new( );		//프로젝트 에셋을 제외한 선택 객체
            foreach ( GameObject target in Selection.gameObjects )
            {
                if ( EditorUtility.IsPersistent( target ) == false && target.scene.IsValid( ) == true )
                    roots.Add( target );
            }
            return roots.ToArray( );
        }
        return SceneManager.GetActiveScene( ).GetRootGameObjects( );
    }

    /// <summary>
    /// 변경 대상 목록 새로 조회
    /// </summary>
    void RefreshTargets ()
    {
        _targets = SoundSetupUtility.CollectTargets( GetRoots( ) );
    }


    /// <summary>
    /// 데이터 값 또는 대상 연결을 한 Undo 그룹으로 적용
    /// </summary>
    /// <param name="connect">대상 구성과 참조 연결 여부</param>
    void Apply ( bool connect )
    {
        RefreshTargets( );
        if ( ( connect == false || _applyValues == true ) && CheckValues( ) == false )
            return;
        Undo.IncrementCurrentGroup( );
        int group = Undo.GetCurrentGroup( );		//이번 일괄 적용의 되돌리기 그룹
        Undo.SetCurrentGroupName( "사운드 일괄 설정" );
        try
        {
            if ( connect == false || _applyValues == true )
                ApplyValues( );
            if ( connect == true )
                SoundSetupUtility.Apply( _targets, GetSounds( ), _output, _overwrite, _configureExisting );
            _status = connect == true
                ? $"담당 컴포넌트 {_targets.Count}개 적용. 씬 / 프리팹과 데이터 저장 상태를 확인해 주세요."
                : "데이터 설정값 적용. 클립 참조는 유지했습니다.";
        }
        finally
        {
            //오류를 숨기지 않고 부분 변경도 한 번에 되돌릴 수 있게 묶음
            Undo.CollapseUndoOperations( group );
        }
    }


    /// <summary>
    /// 같은 에셋에 충돌하는 설정값을 지정했는지 확인
    /// </summary>
    /// <returns>적용 가능한 설정 여부</returns>
    bool CheckValues ()
    {
        Dictionary<SoundData, SoundSetupEntry> values = new( );		//에셋별 최초 설정 항목
        foreach ( SoundSetupEntry entry in _entries )
        {
            if ( entry.Sound == null )
                continue;
            if ( values.TryGetValue( entry.Sound, out SoundSetupEntry previous ) == true &&
                 ( previous.Volume != entry.Volume || previous.Pitch != entry.Pitch ) )
            {
                _status = $"{entry.Sound.name}: 같은 데이터에 다른 값이 지정되어 있습니다. 표의 값을 통일해 주세요.";
                return false;
            }
            values [ entry.Sound ] = entry;
        }
        return true;
    }

    #endregion

    #region ----- 화면 -----

    /// <summary>
    /// 데이터 위치에 대응하는 연결 필드 안내
    /// </summary>
    /// <param name="index">설정표의 데이터 위치</param>
    /// <returns>담당 컴포넌트와 연결 필드</returns>
    string GetBindingLabel ( int index )
    {
        return index switch
        {
            0 => "플레이어 / NPC / 지상 적: Walk Sound",
            1 => "플레이어 / NPC: Captured Sound",
            2 => "PlayerSound: Land Sound",
            3 => "개 BothersomeSound: Request Sound",
            4 => "곰 DemandSound: Eat Sound",
            5 => "곰 DemandSound: Satisfied Sound",
            6 => "곰 DemandSound: Mad Sound",
            7 => "곰 DemandSound / 담비 HarmSound: Attack Sound",
            8 => "곰 DemandSound / 여우 KidnapSound: Capture Sound",
            9 => "담비 HarmSound: Collect Sound",
            10 => "까마귀 StealSound: Flight Sound",
            11 => "PlayerSound: Place Sound",
            12 => "PlayerSound: Throw Sound",
            13 => "ProductionSound: Start Sound",
            14 => "ProductionSound: Process Sound",
            15 => "ProductionSound: Success Sound",
            16 => "ProductionSound: Failure Sound",
            17 => "ProductionSound: Complete Sound",
            18 => "ButtonSound: Click Sound",
            19 => "ShopSound: Purchase Success Sound",
            20 => "ShopSound: Purchase Failure Sound",
            21 => "GameFlowSound: Mission Completed Sound",
            22 => "GameFlowSound: Final Rush Sound",
            23 => "GameResultSound: Success Sound",
            24 => "GameResultSound: Failure Sound",
            _ => string.Empty
        };
    }

    /// <summary>
    /// 데이터 참조와 적용할 설정값 표시
    /// </summary>
    void DrawData ()
    {
        _showData = EditorGUILayout.Foldout( _showData, "데이터 25개 / 적용할 설정값", true );
        if ( _showData == false )
            return;
        EditorGUILayout.HelpBox( "표의 값은 적용 전 설정안입니다. 재생 간격은 0으로 적용하며 클립은 변경하지 않습니다.", MessageType.Info );
        for ( int i = 0; i < _entries.Count; i++ )
        {
            SoundSetupEntry entry = _entries [ i ];		//현재 설정 행
            EditorGUILayout.LabelField( entry.Name, EditorStyles.boldLabel );
            EditorGUILayout.LabelField( GetBindingLabel( i ), EditorStyles.miniLabel );
            entry.Sound = (SoundData)EditorGUILayout.ObjectField( "데이터", entry.Sound, typeof( SoundData ), false );
            EditorGUILayout.BeginHorizontal( );
            entry.Volume = EditorGUILayout.Vector2Field( "음량", entry.Volume );
            entry.Pitch = EditorGUILayout.Vector2Field( "음높이", entry.Pitch );
            EditorGUILayout.EndHorizontal( );
        }
    }

    /// <summary>
    /// 공용 행동 데이터와 출력 그룹 선택
    /// </summary>
    void DrawShared ()
    {
        EditorGUILayout.LabelField( "공용 데이터 선택", EditorStyles.boldLabel );
        _jump = (SoundData)EditorGUILayout.ObjectField( "플레이어 점프", _jump, typeof( SoundData ), false );
        _pickUp = (SoundData)EditorGUILayout.ObjectField( "아이템 집기", _pickUp, typeof( SoundData ), false );
        _dogSatisfied = (SoundData)EditorGUILayout.ObjectField( "개 만족", _dogSatisfied, typeof( SoundData ), false );
        _crowTake = (SoundData)EditorGUILayout.ObjectField( "까마귀 음식 확보", _crowTake, typeof( SoundData ), false );
        _output = (AudioMixerGroup)EditorGUILayout.ObjectField( "효과음 출력 그룹", _output, typeof( AudioMixerGroup ), false );
        EditorGUILayout.HelpBox( "미지정 데이터 필드는 적용하지 않습니다. 공용 4개에는 이미 만든 데이터를 선택하세요. 출력 그룹 미지정 시 기존 출력을 유지합니다.", MessageType.Info );
    }

    /// <summary>
    /// 변경 범위와 담당 컴포넌트 목록 표시
    /// </summary>
    void DrawTargets ()
    {
        EditorGUI.BeginChangeCheck( );
        _scope = EditorGUILayout.Popup( "대상 범위", _scope, new [ ] { "현재 씬", "선택 객체 하위", "열린 프리팹" } );
        bool scopeChanged = EditorGUI.EndChangeCheck( );		//범위 변경 여부
        bool refresh = GUILayout.Button( "대상 새로 조회" );		//대상 재조회 요청
        if ( scopeChanged == true || refresh == true )
            RefreshTargets( );
        _showTargets = EditorGUILayout.Foldout( _showTargets, $"변경 대상 {_targets.Count}개", true );
        if ( _showTargets == true )
        {
            foreach ( Component target in _targets )
            {
                if ( target == null )
                    continue;
                EditorGUILayout.ObjectField( target.GetType( ).Name, target, typeof( Component ), true );
            }
        }
        _overwrite = EditorGUILayout.Toggle( "기존 데이터 / UI 재생기 교체", _overwrite );
        _configureExisting = EditorGUILayout.Toggle( "기존 AudioSource 설정도 적용", _configureExisting );
        _applyValues = EditorGUILayout.Toggle( "연결 시 데이터 값도 적용", _applyValues );
        EditorGUILayout.HelpBox( "캐릭터 / 아이템 / 제작은 전용 재생기를 구성합니다. UI는 씬 루트의 공용 재생기를 사용합니다. 열린 프리팹의 UI 공용 재생기는 씬에서 다시 적용해 연결하세요. 씬 / 프리팹은 자동 저장하지 않습니다.", MessageType.Info );
    }


    #endregion
}
