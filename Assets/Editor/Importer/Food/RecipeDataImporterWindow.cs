using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JSON / CSV 레시피 데이터 임포터 창
/// </summary>
public class RecipeDataImporterWindow : EditorWindow, IDataImporterView, IDataImportPresenceProvider
{
    DataImportPresenceView _presence = new( );		//레시피 ID별 자산 존재 표시
    const string DefaultFoodMapPath = "Assets/03. Configs/Item/Food/Food_Data_Map.asset";
    const string DefaultOutputFolder = "Assets/03. Configs/Item/Food/Recipe";
    const string LastSourceDirectoryPreference = "RecipeDataImporter.LastSourceDirectory";

    string _sourcePath = string.Empty;
    string _sourceFeedback;
    string _outputFolder = DefaultOutputFolder;
    FoodDataMap _foodDataMap;
    List<ParsedRecipeImport> _parsedRecipes;
    FoodImportReport _report;
    Vector2 _reportScroll;
    string _mapName = "Recipe_Data_Map";		//신규 Map 파일명
    DataImportState _importState;		//검사 / 적용 상태
    ScriptableObject _resultMap;		//성공한 적용 결과 Map
    bool _referenceMapLocked;		//선행 음식 Map 사용 여부
    RecipeDataMap _recipeDataMap;		//대상 레시피 Map

    /// <summary>
    /// 목록에 표시할 임포터 이름
    /// </summary>
    public string ImporterName => "레시피 데이터 임포터";

    /// <summary>
    /// 현재 원본 파일
    /// </summary>
    public string SourcePath => _sourcePath;

    /// <summary>
    /// 통합 창에 제공할 레시피 자산 존재 요약
    /// </summary>
    public string PresenceSummary
    {
        get
        {
            _presence.Refresh( _sourcePath, "RecipeData", scope: _recipeDataMap );
            return _presence.Summary;
        }
    }

    /// <summary>
    /// 검사 / 적용 상태
    /// </summary>
    public DataImportState ImportState => _importState;

    /// <summary>
    /// 성공한 적용 결과 Map
    /// </summary>
    public ScriptableObject ResultMap => _resultMap;

    /// <summary>
    /// 선행 항목에서 연결할 참조 Map 종류
    /// </summary>
    public Type ReferenceMapType => typeof( FoodDataMap );

    /// <summary>
    /// 검사 / 적용 상태 변경 알림
    /// </summary>
    public event Action<IDataImporterView> OnStateChanged;


    private void OnEnable ()
    {
        if ( _foodDataMap == null )
            _foodDataMap = AssetDatabase.LoadAssetAtPath<FoodDataMap>( DefaultFoodMapPath );
        if ( _recipeDataMap == null )
            _recipeDataMap = AssetDatabase.LoadAssetAtPath<RecipeDataMap>( $"{DefaultOutputFolder}/Recipe_Data_Map.asset" );
    }


    private void OnGUI ()
    {
        DrawGUI( );
    }

    /// <summary>
    /// 개별 창과 통합 창의 임포터 화면 표시
    /// </summary>
    public void DrawGUI ()
    {
        EditorGUILayout.LabelField( "레시피 데이터 임포터", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox(
            "JSON 또는 CSV의 FoodId를 FoodDataMap에서 찾아 RecipeData 참조로 저장합니다. " +
            "입력에서 사라진 기존 RecipeData 에셋은 삭제하지 않습니다.", MessageType.Info );

        EditorGUI.BeginChangeCheck( );
        DrawSourceField( );
        _outputFolder = EditorGUILayout.TextField( "출력 폴더", _outputFolder );
        using ( new EditorGUI.DisabledScope( _referenceMapLocked == true ) )
            _foodDataMap = ( FoodDataMap )EditorGUILayout.ObjectField(
                "참조 FoodDataMap", _foodDataMap, typeof( FoodDataMap ), false );
        _recipeDataMap = ( RecipeDataMap )EditorGUILayout.ObjectField(
            "대상 RecipeDataMap", _recipeDataMap, typeof( RecipeDataMap ), false );
        using ( new EditorGUI.DisabledScope( _recipeDataMap != null ) )
            _mapName = EditorGUILayout.TextField( "신규 Map 이름", _mapName );
        if ( TryGetTargetMapPath( out string mapPath ) == true )
            EditorGUILayout.LabelField( "Map 저장 경로", mapPath );
        if ( EditorGUI.EndChangeCheck( ) == true )
            InvalidatePreview( );

        EditorGUILayout.Space( );
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            if ( GUILayout.Button( "검사 / 미리보기", GUILayout.Height( 28f ) ) == true )
                Preview( );

            using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode == true ||
                _report == null || _report.CanImport == false ) )
            {
                if ( GUILayout.Button( "임포트 적용", GUILayout.Height( 28f ) ) == true )
                    ApplyImport( );
            }
        }

        _presence.Refresh( _sourcePath, "RecipeData", scope: _recipeDataMap );
        _presence.Draw( );
        DrawReport( );
    }


    /// <summary>
    /// 레시피 데이터 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/Food/Recipe" )]
    public static void Open ()
    {
        RecipeDataImporterWindow window =
            GetWindow<RecipeDataImporterWindow>( "Recipe Data Importer" );
        window.minSize = new Vector2( 560f, 360f );
    }

    /// <summary>
    /// 원본 파일 선택과 이전 검사 해제
    /// </summary>
    void DrawSourceField ()
    {
        if ( ImportSourceFileField.Draw(
                 ref _sourcePath, "레시피 JSON 또는 CSV 선택",
                 LastSourceDirectoryPreference, ref _sourceFeedback ) )
        {
            InvalidatePreview( );
        }
    }

    /// <summary>
    /// 원본과 적용 예정 경로 검사
    /// </summary>
    void Preview ()
    {
        _report = new FoodImportReport( );
        _parsedRecipes = null;
        _resultMap = null;
        try
        {
            if ( File.Exists( _sourcePath ) == false )
            {
                _report.AddError( "원본 JSON 또는 CSV 파일을 선택해 주세요." );
                return;
            }
            if ( string.IsNullOrWhiteSpace( _outputFolder ) ||
                 ( _outputFolder != "Assets" &&
                   _outputFolder.StartsWith( "Assets/", StringComparison.Ordinal ) == false ) )
            {
                _report.AddError( "출력 폴더는 Assets 내부 경로여야 합니다." );
                return;
            }

            List<RecipeImportRow> rows = FoodSourceParser.ParseRecipes( _sourcePath );
            _parsedRecipes = FoodImportValidator.ValidateRecipes(
                rows, _foodDataMap, _report );
            if ( rows.Count == 0 )
                _report.AddError( "임포트할 레시피 데이터가 없습니다." );
            if ( _report.CanImport )
                RecipeDataAssetImporter.Preflight( _parsedRecipes, _outputFolder, _recipeDataMap, _report, _mapName );
        }
        catch ( Exception exception )
        {
            _report.AddError( exception.Message );
        }
        finally
        {
            SetState( _report.CanImport == true ? DataImportState.Ready : DataImportState.Invalid );
        }
    }

    /// <summary>
    /// 검증 후 임포트 적용
    /// </summary>
    void ApplyImport ()
    {
        if ( EditorApplication.isPlayingOrWillChangePlaymode )
        {
            Debug.LogWarning( "플레이 중이거나 전환 중에는 임포트할 수 없습니다." );
            return;
        }
        Preview( );
        if ( _report.CanImport == false || _parsedRecipes == null )
            return;
        if ( EditorUtility.DisplayDialog( "레시피 임포트 확인",
            "변경한 에셋만 저장합니다. 같은 에셋의 기존 미저장 변경도 포함되며 전체 Undo는 지원하지 않습니다.",
            "적용", "취소" ) == false )
            return;

        try
        {
            RecipeDataImportResult result = RecipeDataAssetImporter.Import(
                _parsedRecipes, _outputFolder.TrimEnd( '/', '\\' ), _recipeDataMap, _mapName );
            foreach ( string change in result.Changes )
                _report.AddDetail( change );
            if ( result.Error != null )
            {
                _report.AddError( result.Error );
                SetState( DataImportState.Failed );
                Debug.LogError( result.Error + "\n" + string.Join( "\n", result.Changes ) );
                return;
            }
            _recipeDataMap = result.DataMap;
            _resultMap = _recipeDataMap;
            SetState( DataImportState.Applied );
            Selection.activeObject = _recipeDataMap;
            EditorGUIUtility.PingObject( _recipeDataMap );
            EditorUtility.DisplayDialog( "레시피 데이터 임포트 완료",
                $"생성: {result.CreatedCount}\n갱신: {result.UpdatedCount}\n" +
                "RecipeDataMap 저장 완료. 신규 레시피의 미니게임 / 결과 Prefab 연결은 별도로 확인해 주세요.", "확인" );
        }
        catch ( Exception exception )
        {
            _report.AddError( exception.Message );
            SetState( DataImportState.Failed );
            Debug.LogException( exception );
        }
    }

    /// <summary>
    /// 이전 검사와 적용 완료 표시 해제
    /// </summary>
    public void InvalidatePreview ()
    {
        _report = null;
        _parsedRecipes = null;
        _resultMap = null;
        SetState( DataImportState.Unchecked );
    }

    /// <summary>
    /// 선행 항목의 참조 Map 연결
    /// </summary>
    /// <param name="map">참조 Map 또는 연결 해제 null</param>
    /// <param name="locked">선행 연결 중 수동 변경 차단 여부</param>
    public void SetReferenceMap ( ScriptableObject map, bool locked = false )
    {
        if ( map != null && map is not FoodDataMap )
            throw new ArgumentException( "FoodDataMap을 연결해 주세요." );
        _referenceMapLocked = locked;
        if ( _foodDataMap == map )
            return;
        _foodDataMap = map as FoodDataMap;
        InvalidatePreview( );
    }

    /// <summary>
    /// 검사 / 적용 상태와 목록 갱신 알림 반영
    /// </summary>
    /// <param name="state">새 상태</param>
    void SetState ( DataImportState state )
    {
        _importState = state;
        OnStateChanged?.Invoke( this );
    }

    /// <summary>
    /// 현재 대상 Map의 저장 경로 조회
    /// </summary>
    /// <param name="path">유효한 Map 경로</param>
    /// <returns>경로 설정 유효 여부</returns>
    public bool TryGetTargetMapPath ( out string path )
    {
        path = string.Empty;
        try
        {
            path = DataImportAssetUtility.GetMapPath( _outputFolder, _recipeDataMap, _mapName );
            return true;
        }
        catch ( Exception exception ) when ( exception is ArgumentException || exception is InvalidOperationException )
        {
            //잘못된 설정은 검사 버튼에서 상세 오류를 표시
            return false;
        }
    }

    /// <summary>
    /// 검사 결과와 적용 내역 표시
    /// </summary>
    void DrawReport ()
    {
        if ( _report == null )
            return;

        EditorGUILayout.Space( );
        int count = _parsedRecipes != null ? _parsedRecipes.Count : 0;
        EditorGUILayout.LabelField(
            $"검사 결과 - 레시피 {count}개 / 오류 {_report.Errors.Count}개 / 경고 {_report.Warnings.Count}개",
            EditorStyles.boldLabel );

        _reportScroll = EditorGUILayout.BeginScrollView( _reportScroll );
        for ( int i = 0; i < _report.Errors.Count; i++ )
            EditorGUILayout.HelpBox( _report.Errors [ i ], MessageType.Error );
        for ( int i = 0; i < _report.Warnings.Count; i++ )
            EditorGUILayout.HelpBox( _report.Warnings [ i ], MessageType.Warning );
        for ( int i = 0; i < _report.Details.Count; i++ )
            EditorGUILayout.HelpBox( _report.Details [ i ], MessageType.Info );
        if ( _importState == DataImportState.Applied )
            EditorGUILayout.HelpBox( "현재 입력의 에셋과 Map 저장을 완료했습니다.", MessageType.Info );
        else if ( _report.CanImport == true )
            EditorGUILayout.HelpBox( "검사 통과. 아직 적용하지 않았습니다.", MessageType.Info );
        EditorGUILayout.EndScrollView( );
    }
}
