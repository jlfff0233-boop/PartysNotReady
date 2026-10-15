using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 통합 임포터 창 - 작업 항목과 개별 화면, 선행 Map과 진행 상태 관리
/// </summary>
public class DataImporterHubWindow : EditorWindow
{
    /// <summary>
    /// 등록할 임포터 종류
    /// </summary>
    enum ImporterKind
    {
        Food,           //음식
        Recipe,         //레시피
        EventSettings,  //사건 설정
        Spawn,          //발생 설정
        Mission,        //미션
        CookGoal,       //제작 목표
        FoodPlacement,  //음식 배치 목표
        PreparationCategory,    //준비 카테고리
        Preparation,    //준비 평가 목표
        Item,           //일반 아이템
        ItemPlacement,   //일반 아이템 배치 미션
        Pressure,       //발생 압박
        Mood,           //분위기
        Score,          //결과 평가
        FoodMap,        //음식맵
        Difficulty,     //난이도 연결
        Bear            //곰 취향 연결
    }

    /// <summary>
    /// 작업 목록의 표시 분류
    /// </summary>
    enum ImporterCategory
    {
        All,        //전체
        Food,       //음식과 레시피
        Anomaly,    //변칙 사건과 발생
        Mission,    //미션과 준비 목표
        Item,       //일반 아이템
        Balance,    //분위기와 평가, 난이도
        Enemy       //적 취향
    }

    /// <summary>
    /// 독립 입력과 선행 연결을 가진 임포트 작업 항목
    /// </summary>
    class ImportEntry
    {
        public ImporterKind Kind;           //임포터 종류
        public string Name;                 //작업 이름
        public bool Excluded;               //이번 작업 제외 여부
        public EditorWindow Window;         //통합 창이 소유한 임포터
        public IDataImporterView View;      //개별 화면과 상태
        public ImportEntry Source;          //참조 Map을 제공할 선행 항목
    }

    /// <summary>
    /// 현재 창에서 진행할 임포트 항목
    /// </summary>
    List<ImportEntry> _entries = new( );
    string [ ] _kindNames = { "음식", "레시피", "사건 설정", "발생 설정", "미션", "제작 목표", "음식 배치 목표", "준비 카테고리", "준비 평가 목표", "일반 아이템", "일반 아이템 배치 미션", "발생 압박", "분위기", "결과 평가", "음식맵", "난이도 연결", "곰 취향 연결" };      //항목 종류 이름
    string [ ] _dataTypes = { "FoodData", "RecipeData", "AnomalyEventSettings", "AnomalySpawnData", "MissionData", "CookGoalData", "FoodPlacementData", "PreparationCategoryData", "PreparationGoalData", "ItemData", "ItemPlacementMissionData", "AnomalyPressureData", "MoodData", "ResultScoreData", "FoodDataMap", "GameDifficultyDataMap", "EnemyDemandLinks" };      //종류별 입력 데이터 형식
    string [ ] _categoryNames = { "전체", "음식", "변칙", "미션 / 준비", "일반 아이템", "밸런스 / 난이도", "적" };      //분류 표시 이름
    ImporterCategory _category;             //목록 표시 분류
    int _selected;                          //선택한 항목
    int _addKind;                           //추가할 종류
    Vector2 _listScroll;                    //목록 스크롤
    Vector2 _detailScroll;                  //상세 화면 스크롤
    bool _isHandlingState;                  //연결 변경 알림 처리 중 여부

    private void OnEnable ()
    {
        //현재 창의 작업만 관리하며 도메인 재로드 후 이전 완료 상태를 사용하지 않음
        if ( _entries.Count == 0 )
        {
            for ( int i = 0; i < _kindNames.Length; i++ )
                AddImporter( ( ImporterKind )i );
            _selected = 0;
            SelectVisibleImporter( );
        }
    }

    private void OnDisable ()
    {
        ReleaseImporters( );
    }

    private void OnGUI ()
    {
        EditorGUILayout.HelpBox( "항목별로 검사 후 적용합니다. 기존 데이터의 관리 필드는 갱신하고 Map 목록은 입력 전체로 교체합니다. " +
            "이번에 사용하지 않는 항목은 제외해 주세요. 작업 목록은 창 종료 / 스크립트 재로드 시 초기화됩니다.", MessageType.Info );
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            _addKind = EditorGUILayout.Popup( _addKind, _kindNames, GUILayout.Width( 160f ) );
            if ( GUILayout.Button( "항목 추가", GUILayout.Width( 90f ) ) == true )
                AddImporter( ( ImporterKind )_addKind );
            using ( new EditorGUI.DisabledScope( _selected < 0 ) )
            {
                if ( GUILayout.Button( "선택 항목 삭제", GUILayout.Width( 110f ) ) == true )
                    RemoveImporter( _selected );
            }
        }
        DrawProgress( );
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            DrawImporterList( );
            DrawSelectedImporter( );
        }
    }


    /// <summary>
    /// 통합 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/통합" )]
    public static void Open ()
    {
        DataImporterHubWindow window = GetWindow<DataImporterHubWindow>( "통합 임포터" );
        window.minSize = new Vector2( 960f, 560f );
    }


    #region ----- 항목 수명 -----

    /// <summary>
    /// 종류에 맞는 독립 임포터 항목 생성
    /// </summary>
    /// <param name="kind">추가할 임포터 종류</param>
    void AddImporter ( ImporterKind kind )
    {
        Type type = kind switch
        {
            ImporterKind.Food => typeof( FoodDataImporterWindow ),
            ImporterKind.Recipe => typeof( RecipeDataImporterWindow ),
            ImporterKind.EventSettings => typeof( EventSettingsImporterWindow ),
            ImporterKind.Spawn => typeof( AnomalySpawnImporterWindow ),
            ImporterKind.Mission => typeof( MissionImporterWindow ),
            ImporterKind.CookGoal => typeof( CookGoalImporterWindow ),
            ImporterKind.FoodPlacement => typeof( FoodPlacementImporterWindow ),
            ImporterKind.Preparation => typeof( PreparationGoalImporterWindow ),
            ImporterKind.PreparationCategory => typeof( PreparationCategoryImporterWindow ),
            ImporterKind.Item => typeof( ItemDataImporterWindow ),
            ImporterKind.ItemPlacement => typeof( ItemPlacementImporterWindow ),
            ImporterKind.Pressure or ImporterKind.Mood or ImporterKind.Score or
                ImporterKind.FoodMap or ImporterKind.Difficulty or ImporterKind.Bear => typeof( BalanceDataImporterWindow ),
            _ => throw new ArgumentOutOfRangeException( nameof( kind ) )
        };
        EditorWindow window = ( EditorWindow )CreateInstance( type );
        if ( window is BalanceDataImporterWindow balance )
            balance.Configure( kind switch
            {
                ImporterKind.Pressure => BalanceImportKind.Pressure,
                ImporterKind.Mood => BalanceImportKind.Mood,
                ImporterKind.Score => BalanceImportKind.Score,
                ImporterKind.FoodMap => BalanceImportKind.FoodMap,
                ImporterKind.Difficulty => BalanceImportKind.Difficulty,
                ImporterKind.Bear => BalanceImportKind.Bear,
                _ => throw new ArgumentOutOfRangeException( nameof( kind ) )
            } );
        //개별 창을 띄우지 않고 동일한 입력 화면만 사용
        IDataImporterView view = ( IDataImporterView )window;
        ImportEntry entry = new( )
        {
            Kind = kind,
            Name = _kindNames [ ( int )kind ],
            Window = window,
            View = view
        };
        view.OnStateChanged += HandleStateChanged;
        _entries.Add( entry );
        _selected = _entries.Count - 1;
        //추가한 항목이 필터에 가려지지 않도록 해당 분류 표시
        if ( _category != ImporterCategory.All && GetCategory( kind ) != _category )
            _category = GetCategory( kind );
        _listScroll = Vector2.zero;
        _detailScroll = Vector2.zero;
    }

    /// <summary>
    /// 항목의 선행 연결과 임포터 인스턴스 정리
    /// </summary>
    /// <param name="index">삭제할 항목 번호</param>
    void RemoveImporter ( int index )
    {
        ImportEntry entry = _entries [ index ];
        foreach ( ImportEntry dependent in _entries )
        {
            if ( dependent.Source != entry )
                continue;
            dependent.Source = null;
            ConnectSource( dependent );
            dependent.View.InvalidatePreview( );
        }
        entry.View.OnStateChanged -= HandleStateChanged;
        _entries.RemoveAt( index );
        DestroyImmediate( entry.Window );
        _selected = Mathf.Clamp( _selected, 0, Mathf.Max( 0, _entries.Count - 1 ) );
        SelectVisibleImporter( );
    }

    /// <summary>
    /// 창이 소유한 모든 임포터 정리
    /// </summary>
    void ReleaseImporters ()
    {
        foreach ( ImportEntry entry in _entries )
        {
            entry.View.OnStateChanged -= HandleStateChanged;
            if ( entry.Window != null )
                DestroyImmediate( entry.Window );
        }
        _entries.Clear( );
    }

    #endregion


    #region ----- 화면 -----

    /// <summary>
    /// 임포터 종류의 표시 분류 조회
    /// </summary>
    /// <param name="kind">임포터 종류</param>
    /// <returns>목록 분류</returns>
    ImporterCategory GetCategory ( ImporterKind kind )
    {
        return kind switch
        {
            ImporterKind.Food or ImporterKind.Recipe or ImporterKind.FoodMap => ImporterCategory.Food,
            ImporterKind.EventSettings or ImporterKind.Spawn or ImporterKind.Pressure => ImporterCategory.Anomaly,
            ImporterKind.Mission or ImporterKind.CookGoal or ImporterKind.FoodPlacement or
                ImporterKind.PreparationCategory or ImporterKind.Preparation or ImporterKind.ItemPlacement => ImporterCategory.Mission,
            ImporterKind.Item => ImporterCategory.Item,
            ImporterKind.Mood or ImporterKind.Score or ImporterKind.Difficulty => ImporterCategory.Balance,
            ImporterKind.Bear => ImporterCategory.Enemy,
            _ => throw new ArgumentOutOfRangeException( nameof( kind ) )
        };
    }


    /// <summary>
    /// 현재 선택을 유지하거나 표시 가능한 첫 항목 선택
    /// </summary>
    void SelectVisibleImporter ()
    {
        if ( _selected >= 0 && _selected < _entries.Count && IsVisible( _entries [ _selected ] ) == true )
            return;
        _selected = _entries.FindIndex( entry => IsVisible( entry ) );
        _detailScroll = Vector2.zero;
    }

    /// <summary>
    /// 항목 이름과 원본, 진행 상태 표시
    /// </summary>
    void DrawImporterList ()
    {
        using ( new EditorGUILayout.VerticalScope( GUILayout.Width( 280f ) ) )
        {
            ImporterCategory category = ( ImporterCategory )EditorGUILayout.Popup( "분류", ( int )_category, _categoryNames );
            if ( category != _category )
            {
                _category = category;
                _listScroll = Vector2.zero;
                SelectVisibleImporter( );
            }
            int visibleCount = 0;        //선택 분류의 항목 수
            foreach ( ImportEntry entry in _entries )
            {
                if ( IsVisible( entry ) == true )
                    visibleCount++;
            }
            EditorGUILayout.LabelField( $"표시 {visibleCount} / 전체 {_entries.Count}", EditorStyles.miniLabel );
            _listScroll = EditorGUILayout.BeginScrollView( _listScroll );
            for ( int i = 0; i < _entries.Count; i++ )
            {
                ImportEntry entry = _entries [ i ];
                if ( IsVisible( entry ) == false )
                    continue;
                string status = entry.Excluded == true ? "제외" : GetStateName( entry.View.ImportState );
                if ( GUILayout.Toggle( _selected == i, $"{i + 1}. {entry.Name} / {status}", "Button" ) == true )
                    _selected = i;
                string file = string.IsNullOrWhiteSpace( entry.View.SourcePath ) == true ?
                    "원본 미지정" : Path.GetFileName( entry.View.SourcePath );
                EditorGUILayout.LabelField( _dataTypes [ ( int )entry.Kind ], EditorStyles.miniLabel );
                EditorGUILayout.LabelField( file, EditorStyles.miniLabel );
                if ( entry.View is IDataImportPresenceProvider presence )
                    EditorGUILayout.LabelField( presence.PresenceSummary, EditorStyles.miniLabel );
                EditorGUILayout.Space( 4f );
            }
            if ( visibleCount == 0 )
                EditorGUILayout.HelpBox( "이 분류에 등록된 항목이 없습니다.", MessageType.Info );
            EditorGUILayout.EndScrollView( );
        }
    }

    /// <summary>
    /// 선택한 기존 임포터 화면과 선행 Map 연결 표시
    /// </summary>
    void DrawSelectedImporter ()
    {
        if ( _selected < 0 )
        {
            EditorGUILayout.HelpBox( "표시할 항목이 없습니다. 분류를 변경하거나 항목을 추가해 주세요.", MessageType.Info );
            return;
        }
        ImportEntry entry = _entries [ _selected ];
        using ( new EditorGUILayout.VerticalScope( ) )
        {
            _detailScroll = EditorGUILayout.BeginScrollView( _detailScroll );
            EditorGUILayout.LabelField( "데이터 종류", _dataTypes [ ( int )entry.Kind ] );
            entry.Name = EditorGUILayout.TextField( "작업 이름", entry.Name );
            bool excluded = EditorGUILayout.Toggle( "이번 작업 제외", entry.Excluded );
            if ( excluded != entry.Excluded )
            {
                entry.Excluded = excluded;
                entry.View.InvalidatePreview( );
            }
            DrawReferenceSource( entry );
            DrawMapConflict( entry );
            DrawAssetConflict( entry );
            bool waiting = entry.Source != null &&
                ( entry.Source.Excluded == true || entry.Source.View.ImportState != DataImportState.Applied );
            if ( waiting == true )
                EditorGUILayout.HelpBox( "선행 항목의 적용을 먼저 완료해 주세요.", MessageType.Warning );
            using ( new EditorGUI.DisabledScope( entry.Excluded == true || waiting == true ) )
                entry.View.DrawGUI( );
            EditorGUILayout.EndScrollView( );
        }
    }

    /// <summary>
    /// 참조 Map을 제공할 선행 작업 선택
    /// </summary>
    /// <param name="entry">참조를 연결할 항목</param>
    void DrawReferenceSource ( ImportEntry entry )
    {
        Type referenceType = entry.View.ReferenceMapType;
        IDataImporterAssetReference assetReference = entry.View as IDataImporterAssetReference;
        if ( referenceType == null && assetReference == null )
            return;
        List<ImportEntry> sources = new( );       //참조 종류가 맞는 선행 항목
        List<string> names = new( ) { assetReference == null ? "기존 Map 직접 지정" : "기존 카테고리 검색 / 직접 연결" };       //연결 선택 이름
        int selected = 0;
        foreach ( ImportEntry candidate in _entries )
        {
            bool matches = referenceType == typeof( FoodDataMap ) && candidate.Kind == ImporterKind.Food ||
                referenceType == typeof( MissionDataMap ) && candidate.Kind == ImporterKind.Mission ||
                ( assetReference != null && candidate.View is IDataImporterAssetSource assetSource &&
                  assetSource.ResultAssetType == assetReference.ReferenceAssetType );
            if ( matches == false || candidate == entry )
                continue;
            sources.Add( candidate );
            names.Add( $"{_entries.IndexOf( candidate ) + 1}. {candidate.Name}" );
            if ( candidate == entry.Source )
                selected = sources.Count;
        }
        int next = EditorGUILayout.Popup( assetReference == null ? "참조 Map 연결" : "참조 카테고리 연결", selected, names.ToArray( ) );
        if ( next == selected )
            return;
        entry.Source = next == 0 ? null : sources [ next - 1 ];
        ConnectSource( entry );
        entry.View.InvalidatePreview( );
    }


    /// <summary>
    /// 검사 후 같은 카테고리를 갱신하는 항목 경고
    /// </summary>
    /// <param name="entry">현재 항목</param>
    void DrawAssetConflict ( ImportEntry entry )
    {
        if ( entry.Excluded == true )
            return;
        foreach ( ImportEntry other in _entries )
        {
            if ( other != entry && other.Excluded == false && HasAssetConflict( entry, other ) == true )
            {
                EditorGUILayout.HelpBox( $"'{other.Name}'도 같은 데이터 SO를 갱신합니다. 마지막 적용 값이 사용됩니다.", MessageType.Warning );
                return;
            }
        }
    }

    /// <summary>
    /// 다른 항목과 대상 Map 경로가 같은지 표시
    /// </summary>
    /// <param name="entry">검사할 항목</param>
    void DrawMapConflict ( ImportEntry entry )
    {
        if ( entry.Excluded == true || entry.View.TryGetTargetMapPath( out string path ) == false )
            return;
        foreach ( ImportEntry other in _entries )
        {
            if ( other == entry || other.Excluded == true ||
                 other.View.TryGetTargetMapPath( out string otherPath ) == false )
                continue;
            if ( string.Equals( path, otherPath, StringComparison.OrdinalIgnoreCase ) == true )
            {
                EditorGUILayout.HelpBox( $"'{other.Name}'도 같은 Map을 교체합니다. 단계별로 신규 Map 이름 또는 대상 Map을 구분해 주세요.", MessageType.Warning );
                return;
            }
        }
    }

    /// <summary>
    /// 실제 적용 완료와 남은 항목 수 표시
    /// </summary>
    void DrawProgress ()
    {
        int targets = 0;         //작업 대상 수
        int applied = 0;         //실제 적용 완료 수
        int errors = 0;          //검사 / 적용 실패 수
        foreach ( ImportEntry entry in _entries )
        {
            if ( entry.Excluded == true )
                continue;
            targets++;
            if ( entry.View.ImportState == DataImportState.Applied )
                applied++;
            if ( entry.View.ImportState == DataImportState.Invalid || entry.View.ImportState == DataImportState.Failed )
                errors++;
        }
        EditorGUILayout.LabelField( $"전체 작업: 적용 완료 {applied} / 대상 {targets} / 미완료 {targets - applied} / 오류 {errors} / 제외 {_entries.Count - targets}",
            EditorStyles.boldLabel );
    }

    /// <summary>
    /// 상태의 표시 이름 조회
    /// </summary>
    /// <param name="state">검사 / 적용 상태</param>
    /// <returns>표시할 이름</returns>
    string GetStateName ( DataImportState state )
    {
        return state switch
        {
            DataImportState.Ready => "검사 통과",
            DataImportState.Invalid => "검사 오류",
            DataImportState.Applied => "적용 완료",
            DataImportState.Failed => "적용 실패",
            _ => "미검사"
        };
    }


    /// <summary>
    /// 선택 분류에 표시되는 항목인지 확인
    /// </summary>
    /// <param name="entry">작업 항목</param>
    /// <returns>목록 표시 여부</returns>
    bool IsVisible ( ImportEntry entry )
    {
        return _category == ImporterCategory.All || GetCategory( entry.Kind ) == _category;
    }

    /// <summary>
    /// 검사한 두 카테고리 항목의 저장 경로 중복 확인
    /// </summary>
    /// <param name="first">첫 번째 항목</param>
    /// <param name="second">두 번째 항목</param>
    /// <returns>같은 SO 또는 생성 경로 사용 여부</returns>
    bool HasAssetConflict ( ImportEntry first, ImportEntry second )
    {
        if ( first.View is not IDataImporterAssetSource firstSource ||
             second.View is not IDataImporterAssetSource secondSource )
            return false;
        foreach ( DataImportAssetChange firstAsset in firstSource.TargetAssets )
        {
            foreach ( DataImportAssetChange secondAsset in secondSource.TargetAssets )
            {
                if ( string.Equals( firstAsset.Path, secondAsset.Path, StringComparison.OrdinalIgnoreCase ) == true )
                    return true;
            }
        }
        return false;
    }

    #endregion


    #region ----- 연결 갱신 -----

    /// <summary>
    /// 선행 Map 또는 ID별 카테고리 결과를 연결
    /// </summary>
    /// <param name="entry">참조를 받는 항목</param>
    void ConnectSource ( ImportEntry entry )
    {
        if ( entry.View is IDataImporterAssetReference reference )
        {
            IReadOnlyList<DataImportReference> assets = null;
            if ( entry.Source != null && entry.Source.Excluded == false &&
                 entry.Source.View.ImportState == DataImportState.Applied &&
                 entry.Source.View is IDataImporterAssetSource source )
                assets = source.ResultAssets;
            reference.SetReferenceAssets( assets, entry.Source != null );
        }
        else
        {
            entry.View.SetReferenceMap( GetSourceMap( entry.Source ), entry.Source != null );
        }
    }

    /// <summary>
    /// 선행 결과 연결과 관련 항목의 이전 검사 해제
    /// </summary>
    /// <param name="view">상태가 변경된 임포터</param>
    void HandleStateChanged ( IDataImporterView view )
    {
        if ( _isHandlingState == true )
            return;
        ImportEntry source = _entries.Find( entry => entry.View == view );
        if ( source == null )
            return;
        bool assetsWritten = view.ImportState == DataImportState.Applied || view.ImportState == DataImportState.Failed;
        bool hasPath = view.TryGetTargetMapPath( out string sourcePath );
        _isHandlingState = true;
        try
        {
            foreach ( ImportEntry entry in _entries )
            {
                if ( entry == source )
                    continue;
                if ( entry.Source == source )
                {
                    ConnectSource( entry );
                    entry.View.InvalidatePreview( );
                }
                else if ( DependsOn( entry.Kind, source.Kind ) == true ||
                    ( assetsWritten == true && hasPath == true &&
                      entry.View.TryGetTargetMapPath( out string targetPath ) == true &&
                      string.Equals( sourcePath, targetPath, StringComparison.OrdinalIgnoreCase ) == true ) ||
                    ( assetsWritten == true && HasAssetConflict( source, entry ) == true ) )
                {
                    entry.View.InvalidatePreview( );
                }
            }
            //같은 Map / 카테고리 SO를 쓰는 항목의 완료 해제도 연결된 참조에 전파
            foreach ( ImportEntry entry in _entries )
            {
                if ( entry.Source != null )
                    ConnectSource( entry );
            }
        }
        finally
        {
            _isHandlingState = false;
        }
        Repaint( );
    }

    /// <summary>
    /// 성공한 선행 작업의 Map 조회
    /// </summary>
    /// <param name="entry">선행 항목 또는 직접 연결 null</param>
    /// <returns>현재 유효한 결과 Map</returns>
    ScriptableObject GetSourceMap ( ImportEntry entry )
    {
        if ( entry == null || entry.Excluded == true || entry.View.ImportState != DataImportState.Applied )
            return null;
        return entry.View.ResultMap;
    }

    /// <summary>
    /// 선행 데이터 변경의 검사 영향 확인
    /// </summary>
    /// <param name="target">검사를 해제할 종류</param>
    /// <param name="source">변경된 종류</param>
    /// <returns>검사 의존 여부</returns>
    bool DependsOn ( ImporterKind target, ImporterKind source )
    {
        if ( target == ImporterKind.Difficulty )
            return source == ImporterKind.Pressure || source == ImporterKind.Mood ||
                source == ImporterKind.Score || source == ImporterKind.Spawn;
        if ( target == ImporterKind.Bear )
            return source == ImporterKind.FoodMap || source == ImporterKind.Food;
        if ( target == ImporterKind.FoodMap )
            return source == ImporterKind.Food;
        if ( source == ImporterKind.PreparationCategory )
            return target == ImporterKind.Preparation;
        if ( source == ImporterKind.Item )
            return target == ImporterKind.ItemPlacement || target == ImporterKind.Preparation;
        if ( source == ImporterKind.Food )
            return target == ImporterKind.Recipe || target == ImporterKind.CookGoal ||
                target == ImporterKind.FoodPlacement || target == ImporterKind.Preparation;
        if ( source == ImporterKind.Mission )
            return target == ImporterKind.CookGoal || target == ImporterKind.FoodPlacement ||
                target == ImporterKind.ItemPlacement;
        if ( source == ImporterKind.Recipe )
            return target == ImporterKind.CookGoal || target == ImporterKind.FoodPlacement;
        return false;
    }

    #endregion
}
