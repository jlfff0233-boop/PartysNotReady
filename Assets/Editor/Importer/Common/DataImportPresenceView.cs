using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 통합 창에 자산 존재 요약을 제공
/// </summary>
public interface IDataImportPresenceProvider
{
    /// <summary>
    /// 현재 입력의 자산 존재 요약
    /// </summary>
    string PresenceSummary { get; }
}

/// <summary>
/// 입력 ID와 실제 프로젝트 자산의 존재 여부를 캐시하여 표시
/// </summary>
public class DataImportPresenceView
{
    /// <summary>
    /// 한 입력 ID에 대응하는 기존 자산과 조회 안내
    /// </summary>
    class Entry
    {
        public string Id;        //입력 ID
        public string Note;        //조회 불가 사유
        /// <summary>
        /// 대응하는 기존 자산 후보
        /// </summary>
        public List<ScriptableObject> Assets = new( );
    }

    static int _projectVersion;        //프로젝트 자산 변경 횟수
    string _key;        //최근 입력 및 대상 상태
    string _error;        //입력 읽기 오류
    bool _expanded = true;        //존재 목록 펼침
    Vector2 _scroll;        //목록 스크롤
    /// <summary>
    /// 입력 순서의 존재 상태
    /// </summary>
    List<Entry> _entries = new( );

    /// <summary>
    /// 존재와 없음 및 중복을 구분한 요약
    /// </summary>
    public string Summary
    {
        get
        {
            if ( _error != null )
                return "자산 존재: 확인 불가";
            if ( _key == null )
                return "자산 존재: 원본 미지정";
            return $"존재 {_entries.Count( entry => entry.Note == null && entry.Assets.Count == 1 )} / " +
                $"없음 {_entries.Count( entry => entry.Note == null && entry.Assets.Count == 0 )} / " +
                $"중복 {_entries.Count( entry => entry.Note == null && entry.Assets.Count > 1 )} / " +
                $"확인 필요 {_entries.Count( entry => entry.Note != null )}";
        }
    }


    /// <summary>
    /// 자산 변경 이후에만 조회 캐시를 갱신하도록 구독
    /// </summary>
    static DataImportPresenceView ()
    {
        EditorApplication.projectChanged += () => _projectVersion++;
    }


    /// <summary>
    /// 다음 표시에서 존재 상태 재조회
    /// </summary>
    public void Invalidate ()
    {
        _key = null;
    }


    /// <summary>
    /// 후보 중 입력 ID와 일치하는 자산만 추출
    /// </summary>
    /// <param name="assets">조회 후보</param>
    /// <param name="id">입력 ID</param>
    /// <param name="dataType">데이터 종류</param>
    /// <returns>중복을 제거한 일치 후보</returns>
    static List<ScriptableObject> Find ( IEnumerable<ScriptableObject> assets, string id, string dataType )
    {
        return assets.Where( asset => asset != null && Matches( asset, id, dataType ) == true ).Distinct( ).ToList( );
    }

    /// <summary>
    /// 원본과 대상 변경 시 존재 여부만 조회하며 임포트 검사는 실행하지 않음
    /// </summary>
    /// <param name="path">CSV 또는 JSON 원본</param>
    /// <param name="dataType">입력 종류</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="scope">대상 목록맵</param>
    /// <param name="target">연결 입력의 기존 대상</param>
    /// <param name="changes">검사에서 확정한 적용 대상</param>
    public void Refresh ( string path, string dataType, IReadOnlyList<DataImportReference> bindings = null,
        ScriptableObject scope = null, ScriptableObject target = null, IReadOnlyList<DataImportAssetChange> changes = null )
    {
        if ( string.IsNullOrWhiteSpace( path ) == true )
        {
            _key = null;
            _error = null;
            _entries.Clear( );
            return;
        }
        try
        {
            FileInfo source = new( path );
            StringBuilder key = new( $"{path}|{dataType}|{source.LastWriteTimeUtc.Ticks}|{source.Length}|{_projectVersion}|{scope?.GetInstanceID( )}|{target?.GetInstanceID( )}" );
            if ( bindings != null )
                foreach ( DataImportReference binding in bindings )
                    key.Append( $"|{binding.Id}:{binding.Asset?.GetInstanceID( )}" );
            if ( changes != null )
                foreach ( DataImportAssetChange change in changes )
                    key.Append( $"|{change.Id}:{change.Asset?.GetInstanceID( )}" );
            if ( _key == key.ToString( ) )
                return;
            _key = key.ToString( );
            _error = null;
            _entries.Clear( );
            Type type = typeof( FoodData ).Assembly.GetType( dataType == "EnemyDemandLinks" ? "EnemyDemandData" : dataType );
            if ( type == null || typeof( ScriptableObject ).IsAssignableFrom( type ) == false )
                throw new FormatException( "지원하지 않는 자산 종류입니다: " + dataType );
            List<string> ids = dataType switch
            {
                "FoodData" => FoodSourceParser.ParseFoods( path ).Select( row => row.foodId ).ToList( ),
                "RecipeData" => FoodSourceParser.ParseRecipes( path ).Select( row => row.recipeId ).ToList( ),
                _ => DataImportSourceParser.Parse( path, dataType ).Select( row => row.Text( "id" ) ).ToList( )
            };
            List<ScriptableObject> assets = AssetDatabase.FindAssets( "t:" + type.Name )
                .Select( guid => AssetDatabase.LoadAssetAtPath<ScriptableObject>( AssetDatabase.GUIDToAssetPath( guid ) ) )
                .Where( asset => asset != null && (dataType == "ItemData" ? asset.GetType( ) == type : type.IsInstanceOfType( asset )) ).ToList( );
            List<ScriptableObject> scoped = new( );
            if ( scope != null )
            {
                using SerializedObject serialized = new( scope );
                SerializedProperty property = serialized.GetIterator( );
                while ( property.Next( true ) == true )
                    if ( property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is ScriptableObject asset && type.IsInstanceOfType( asset ) == true )
                        scoped.Add( asset );
            }
            foreach ( string rawId in ids )
            {
                string id = rawId?.Trim( );
                Entry entry = new( ) { Id = id };
                _entries.Add( entry );
                if ( string.IsNullOrWhiteSpace( id ) == true )
                {
                    entry.Note = "ID 누락";
                    continue;
                }
                if ( dataType == "GameDifficultyDataMap" )
                {
                    if ( target == null )
                        entry.Note = "갱신할 난이도맵 미지정";
                    else
                        entry.Assets.Add( target );
                    continue;
                }
                if ( dataType == "EnemyDemandLinks" && target != null )
                    entry.Assets.Add( target );
                else if ( bindings != null && bindings.Any( binding => binding.Id == id ) == true )
                {
                    foreach ( DataImportReference binding in bindings.Where( binding => binding.Id == id ) )
                    {
                        if ( binding.Asset is ScriptableObject asset && type.IsInstanceOfType( asset ) == true )
                            entry.Assets.Add( asset );
                        else
                            entry.Note = "직접 연결의 타입 또는 참조 확인 필요";
                    }
                }
                else
                {
                    List<ScriptableObject> selected = changes == null ? new( ) : changes.Where( change => change.Id == id && change.Asset != null ).Select( change => change.Asset ).Distinct( ).ToList( );
                    entry.Assets = selected.Count > 0 ? selected : Find( scoped, id, dataType );
                    if ( entry.Assets.Count == 0 )
                        entry.Assets = Find( assets, id, dataType );
                    if ( entry.Assets.Count == 0 && dataType == "FoodData" )
                        entry.Assets = Find( assets, FoodDataAssetImporter.GetLegacyId( id ), dataType );
                }
            }
        }
        catch ( Exception exception ) when ( exception is IOException || exception is UnauthorizedAccessException ||
            exception is FormatException || exception is ArgumentException || exception is InvalidOperationException )
        {
            _error = exception.Message;
            _entries.Clear( );
        }
    }

    /// <summary>
    /// 존재 요약과 자산 선택 버튼을 읽기 전용으로 표시
    /// </summary>
    public void Draw ()
    {
        bool previousChanged = GUI.changed;
        _expanded = EditorGUILayout.Foldout( _expanded, "자산 존재 · " + Summary, true );
        if ( GUILayout.Button( "존재 여부 새로고침", GUILayout.Width( 170f ) ) == true )
            Invalidate( );
        if ( _expanded == true )
        {
            EditorGUILayout.LabelField( "존재 여부이며 입력 값 적용 여부나 날짜를 뜻하지 않습니다.", EditorStyles.miniLabel );
            if ( _error != null )
                EditorGUILayout.HelpBox( _error, MessageType.Warning );
            _scroll = EditorGUILayout.BeginScrollView( _scroll, GUILayout.MaxHeight( 180f ) );
            foreach ( Entry entry in _entries )
            {
                using ( new EditorGUILayout.HorizontalScope( ) )
                {
                    EditorGUILayout.LabelField( entry.Id ?? "(ID 없음)" );
                    EditorGUILayout.LabelField( entry.Note ?? (entry.Assets.Count == 0 ? "없음" : entry.Assets.Count == 1 ? "자산 존재" : "중복"), GUILayout.Width( 150f ) );
                }
                foreach ( ScriptableObject asset in entry.Assets )
                    if ( GUILayout.Button( new GUIContent( asset.name + " · 선택", AssetDatabase.GetAssetPath( asset ) ) ) == true )
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject( asset );
                    }
            }
            EditorGUILayout.EndScrollView( );
        }
        //표시 접기와 자산 선택은 임포트 설정 변경으로 취급하지 않습니다.
        GUI.changed = previousChanged;
    }

    /// <summary>
    /// 자산 이름 또는 데이터 식별자가 입력 ID와 같은지 확인
    /// </summary>
    /// <param name="asset">기존 자산</param>
    /// <param name="id">입력 ID</param>
    /// <param name="dataType">데이터 종류</param>
    /// <returns>식별자 일치 여부</returns>
    static bool Matches ( ScriptableObject asset, string id, string dataType )
    {
        if ( asset.name == id )
            return true;
        string field = dataType switch
        {
            "FoodData" => "_foodId",
            "RecipeData" => "_recipeId",
            "MissionData" => "_id",
            "PreparationGoalData" => "_goalId",
            _ => null
        };
        if ( field == null )
            return false;
        using SerializedObject serialized = new( asset );
        SerializedProperty property = serialized.FindProperty( field );
        return property != null && property.propertyType == SerializedPropertyType.String && property.stringValue == id;
    }

}
