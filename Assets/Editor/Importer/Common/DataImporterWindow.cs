using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 설정 임포터의 파일 선택 / 검사 / 적용 / 결과 표시 화면
/// </summary>
public abstract class DataImporterWindow : EditorWindow, IDataImporterView, IDataImportPresenceProvider
{
    DataImportPresenceView _presence = new( );		//입력별 자산 존재 표시
    string _sourcePath = string.Empty;		//원본 파일
    string _sourceFeedback;		//파일 선택 안내
    DataImportReport _report;		//검사와 적용 결과
    DataImportPlan _plan;		//검사한 적용 계획
    Vector2 _reportScroll;		//결과 스크롤
    bool _showBindings;		//직접 연결 목록 표시
    string _mapName;		//신규 Map 파일명
    DataImportState _importState;		//검사 / 적용 상태
    ScriptableObject _resultMap;		//성공한 적용 결과 Map

    protected bool ReferenceMapLocked;		//선행 항목의 참조 Map 사용 여부
    protected string OutputFolder;		//신규 에셋 폴더
    protected ScriptableObject DataMap;		//갱신할 Map
    /// <summary>
    /// 원본 ID와 직접 연결한 기존 SO
    /// </summary>
    protected List<DataImportReference> Bindings = new( );

    /// <summary>
    /// 마지막 적용에서 저장을 시도한 ID별 에셋
    /// </summary>
    protected List<DataImportReference> AppliedAssets = new( );

    /// <summary>
    /// JSON 원본의 데이터 종류
    /// </summary>
    protected abstract string DataType { get; }

    /// <summary>
    /// 창의 기능 이름
    /// </summary>
    protected abstract string DisplayName { get; }

    /// <summary>
    /// 신규 에셋의 기본 생성 폴더
    /// </summary>
    protected abstract string DefaultOutputFolder { get; }

    /// <summary>
    /// 대상 Map의 SO 종류
    /// </summary>
    protected abstract Type MapType { get; }

    /// <summary>
    /// 대상 Map 사용 여부
    /// </summary>
    protected bool UsesMap => MapType != null;

    /// <summary>
    /// 현재 검사한 생성 / 갱신 대상
    /// </summary>
    protected IReadOnlyList<DataImportAssetChange> PreparedAssets =>
        _plan == null ? Array.Empty<DataImportAssetChange>( ) : _plan.Changes;

    /// <summary>
    /// 신규 Map의 기본 파일명
    /// </summary>
    protected abstract string DefaultMapName { get; }

    /// <summary>
    /// 지정한 신규 Map 파일명
    /// </summary>
    protected string MapName => _mapName ?? DefaultMapName;

    /// <summary>
    /// 목록에 표시할 임포터 이름
    /// </summary>
    public string ImporterName => DisplayName;

    /// <summary>
    /// 현재 원본 파일
    /// </summary>
    public string SourcePath => _sourcePath;

    /// <summary>
    /// 연결 입력이 실제로 갱신할 기존 자산
    /// </summary>
    protected virtual ScriptableObject PresenceTarget => null;

    /// <summary>
    /// 통합 창에 제공할 현재 자산 존재 요약
    /// </summary>
    public string PresenceSummary
    {
        get
        {
            _presence.Refresh( _sourcePath, DataType, Bindings, DataMap, PresenceTarget, _plan?.Changes );
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
    public virtual Type ReferenceMapType => null;

    /// <summary>
    /// 검사 / 적용 상태 변경 알림
    /// </summary>
    public event Action<IDataImporterView> OnStateChanged;


    private void OnGUI ()
    {
        DrawGUI( );
    }

    /// <summary>
    /// 개별 창과 통합 창의 임포터 화면 표시
    /// </summary>
    public void DrawGUI ()
    {
        if ( OutputFolder == null )
            OutputFolder = DefaultOutputFolder;
        EditorGUILayout.LabelField( DisplayName, EditorStyles.boldLabel );
        EditorGUILayout.HelpBox( UsesMap == true ?
            "JSON / CSV 검사 후 에셋과 지정 Map을 생성 / 갱신합니다. Map 목록은 입력 전체로 교체합니다." :
            "JSON / CSV로 데이터 에셋을 생성 / 갱신합니다. 기존 에셋의 참조와 이름은 유지합니다.", MessageType.Info );
        EditorGUI.BeginChangeCheck( );
        bool changed = ImportSourceFileField.Draw( ref _sourcePath, $"{DisplayName} JSON / CSV 선택",
            $"{DataType}Importer.LastSourceDirectory", ref _sourceFeedback );
        OutputFolder = EditorGUILayout.TextField( "출력 폴더", OutputFolder );
        if ( UsesMap == true )
        {
            DataMap = ( ScriptableObject )EditorGUILayout.ObjectField( "대상 Map", DataMap, MapType, false );
            using ( new EditorGUI.DisabledScope( DataMap != null ) )
                _mapName = EditorGUILayout.TextField( "신규 Map 이름", MapName );
            if ( TryGetTargetMapPath( out string mapPath ) == true )
                EditorGUILayout.LabelField( "Map 저장 경로", mapPath );
        }
        DrawOptions( );
        DrawBindings( );
        if ( EditorGUI.EndChangeCheck( ) == true || changed == true )
        {
            InvalidatePreview( );
        }
        _presence.Refresh( _sourcePath, DataType, Bindings, DataMap, PresenceTarget, _plan?.Changes );
        _presence.Draw( );
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
        DrawReport( );
    }


    #region ----- 화면 -----

    /// <summary>
    /// 대상별 추가 입력 표시
    /// </summary>
    protected virtual void DrawOptions ()
    {
    }

    /// <summary>
    /// 이름과 ID가 다른 기존 에셋의 직접 연결 표시
    /// </summary>
    void DrawBindings ()
    {
        bool previousChanged = GUI.changed;       //표시 접기만으로 검사 상태를 해제하지 않음
        _showBindings = EditorGUILayout.Foldout( _showBindings, "기존 에셋 / 참조 직접 연결", true );
        GUI.changed = previousChanged;
        if ( _showBindings == false )
            return;
        EditorGUILayout.HelpBox( "원본 ID를 기존 데이터나 참조 SO에 연결합니다. " +
            "경로 / guid:GUID 참조도 지원합니다. 잘못된 타입이나 중복 연결은 검사에서 표시합니다.", MessageType.Info );
        for ( int i = 0; i < Bindings.Count; i++ )
        {
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                Bindings [ i ].Id = EditorGUILayout.TextField( Bindings [ i ].Id );
                Bindings [ i ].Asset = EditorGUILayout.ObjectField( Bindings [ i ].Asset, typeof( ScriptableObject ), false );
                if ( GUILayout.Button( "삭제", GUILayout.Width( 44f ) ) == true )
                {
                    Bindings.RemoveAt( i );
                    GUI.changed = true;
                    break;
                }
            }
        }
        if ( GUILayout.Button( "연결 추가" ) == true )
        {
            Bindings.Add( new DataImportReference( ) );
            GUI.changed = true;
        }
    }

    /// <summary>
    /// 검사 결과와 실제 적용 내역 표시
    /// </summary>
    void DrawReport ()
    {
        if ( _report == null )
            return;
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( $"검사 결과 - 오류 {_report.Errors.Count}개 / 경고 {_report.Warnings.Count}개",
            EditorStyles.boldLabel );
        _reportScroll = EditorGUILayout.BeginScrollView( _reportScroll );
        foreach ( string error in _report.Errors )
            EditorGUILayout.HelpBox( error, MessageType.Error );
        foreach ( string warning in _report.Warnings )
            EditorGUILayout.HelpBox( warning, MessageType.Warning );
        foreach ( string detail in _report.Details )
            EditorGUILayout.HelpBox( detail, MessageType.Info );
        if ( _importState == DataImportState.Applied )
            EditorGUILayout.HelpBox( UsesMap == true ? "현재 입력의 에셋과 Map 저장을 완료했습니다." : "현재 입력의 데이터 에셋 저장을 완료했습니다.", MessageType.Info );
        else if ( _report.CanImport == true )
            EditorGUILayout.HelpBox( "검사 통과. 아직 적용하지 않았습니다.", MessageType.Info );
        EditorGUILayout.EndScrollView( );
    }

    #endregion


    #region ----- 검사 / 적용 -----

    /// <summary>
    /// 원본 값 / 참조 / 출력 경로와 Map 교체 내역 검사
    /// </summary>
    void Preview ()
    {
        _report = new DataImportReport( );
        _plan = null;
        _resultMap = null;
        AppliedAssets.Clear( );
        try
        {
            if ( File.Exists( _sourcePath ) == false )
                throw new FileNotFoundException( "원본 JSON 또는 CSV 파일을 선택해 주세요." );
            List<DataImportRow> rows = DataImportSourceParser.Parse( _sourcePath, DataType );
            if ( rows.Count == 0 )
                throw new FormatException( "임포트할 데이터가 없습니다." );
            CheckBindings( );
            _plan = Prepare( rows, _report );
        }
        catch ( Exception exception )
        {
            _report.Errors.Add( exception.Message );
        }
        SetState( _report.CanImport == true ? DataImportState.Ready : DataImportState.Invalid );
    }

    /// <summary>
    /// 원본 재검사 후 에셋과 Map 반영
    /// </summary>
    void ApplyImport ()
    {
        if ( EditorApplication.isPlayingOrWillChangePlaymode == true )
            return;
        Preview( );
        if ( _report.CanImport == false )
            return;
        if ( EditorUtility.DisplayDialog( $"{DisplayName} 적용 확인",
            string.Join( "\n", _report.Details ) + "\n" + string.Join( "\n", _report.Warnings ) +
            "\n변경한 에셋만 저장합니다. 같은 에셋의 기존 미저장 변경도 포함되며 전체 Undo / 자동 복원은 지원하지 않습니다.",
            "적용", "취소" ) == false )
            return;
        DataImportResult result = DataImportAssetUtility.Import( _plan );
        AppliedAssets.Clear( );
        foreach ( DataImportAssetChange change in _plan.Changes )
        {
            if ( change.Asset != null )
                AppliedAssets.Add( new DataImportReference { Id = change.Id, Asset = change.Asset } );
        }
        _report.Details.AddRange( result.Changes );
        if ( result.DataMap != null )
            DataMap = result.DataMap;
        if ( result.Error != null )
        {
            _report.Errors.Add( result.Error );
            SetState( DataImportState.Failed );
            Debug.LogError( result.Error + "\n" + string.Join( "\n", result.Changes ) );
            return;
        }
        _resultMap = UsesMap == true ? DataMap : null;
        SetState( DataImportState.Applied );
        UnityEngine.Object selected = UsesMap == true ? DataMap : AppliedAssets [ 0 ].Asset;
        Selection.activeObject = selected;
        EditorGUIUtility.PingObject( selected );
        EditorUtility.DisplayDialog( $"{DisplayName} 완료",
            $"생성: {result.CreatedCount}\n갱신: {result.UpdatedCount}\n{( UsesMap == true ? "Map 저장 완료" : "데이터 에셋 저장 완료" )}", "확인" );
    }

    /// <summary>
    /// 이전 검사와 적용 완료 표시 해제
    /// </summary>
    public void InvalidatePreview ()
    {
        _report = null;
        _plan = null;
        _resultMap = null;
        AppliedAssets.Clear( );
        SetState( DataImportState.Unchecked );
    }

    /// <summary>
    /// 참조 Map을 선행 항목의 결과로 연결
    /// </summary>
    /// <param name="map">선행 결과 Map 또는 연결 해제 null</param>
    /// <param name="locked">선행 연결 중 수동 변경 차단 여부</param>
    public virtual void SetReferenceMap ( ScriptableObject map, bool locked = false )
    {
        if ( map != null )
            throw new ArgumentException( "이 임포터는 참조 Map 연결을 사용하지 않습니다." );
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
        if ( UsesMap == false )
            return false;
        try
        {
            path = DataImportAssetUtility.GetMapPath( OutputFolder ?? DefaultOutputFolder, DataMap, MapName );
            return true;
        }
        catch ( Exception exception ) when ( exception is ArgumentException || exception is InvalidOperationException )
        {
            //잘못된 설정은 검사 버튼에서 상세 오류를 표시
            return false;
        }
    }

    /// <summary>
    /// 대상 데이터의 값과 기존 참조를 검사하여 적용 계획 준비
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="report">검사 결과</param>
    /// <returns>적용 계획</returns>
    protected abstract DataImportPlan Prepare ( List<DataImportRow> rows, DataImportReport report );

    /// <summary>
    /// 직접 연결 목록의 빈 값과 중복 검사
    /// </summary>
    void CheckBindings ()
    {
        HashSet<string> ids = new( StringComparer.Ordinal );
        foreach ( DataImportReference binding in Bindings )
        {
            binding.Id = binding.Id?.Trim( );
            if ( string.IsNullOrWhiteSpace( binding.Id ) == true || binding.Asset == null || ids.Add( binding.Id ) == false )
                throw new FormatException( "직접 연결에는 고유한 원본 ID와 기존 에셋이 필요합니다." );
        }
    }

    #endregion
}
