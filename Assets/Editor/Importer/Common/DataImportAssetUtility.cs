using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 한 데이터 에셋의 생성 / 갱신 예정 값
/// </summary>
public class DataImportAssetChange
{
    public string Id;		//원본 식별자
    public string Path;		//기존 또는 신규 에셋 경로
    public Type AssetType;		//생성할 SO 종류
    public ScriptableObject Asset;		//재사용하거나 생성한 에셋
    public Action<SerializedObject> Apply;		//관리 필드 반영
}

/// <summary>
/// 데이터 에셋 변경과 마지막 Map 반영 순서
/// </summary>
public class DataImportPlan
{
    public string OutputFolder;		//신규 에셋 폴더
    public Type MapType;		//Map 종류
    public string MapPath;		//Map 저장 경로
    public ScriptableObject DataMap;		//기존 Map
    public Action<SerializedObject, List<DataImportAssetChange>> ApplyMap;		//목록 반영
    /// <summary>
    /// 입력 순서대로 반영할 데이터 에셋
    /// </summary>
    public List<DataImportAssetChange> Changes = new( );
}

/// <summary>
/// 설정 임포터의 경로 검사 / 에셋 저장 / 목록 반영
/// </summary>
public static class DataImportAssetUtility
{

    #region ----- 적용 준비 -----

    /// <summary>
    /// 원본 ID 중복과 파일명 검사
    /// </summary>
    /// <param name="row">원본 행</param>
    /// <param name="ids">앞선 원본 ID</param>
    /// <returns>검사한 ID</returns>
    public static string ReadId ( DataImportRow row, HashSet<string> ids )
    {
        string id = row.Text( "id" );
        if ( ids.Add( id ) == false )
            throw row.Error( "id", $"중복된 식별자: {id}" );
        //폴더 검사와 같은 이름 규칙을 사용
        FoodImportValidator.ValidateOutputFolder( $"Assets/{id}" );
        return id;
    }

    /// <summary>
    /// 기존 Map 또는 지정 폴더의 Map을 준비
    /// </summary>
    /// <param name="folder">신규 에셋 폴더</param>
    /// <param name="map">기존 Map</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <returns>Map 생성 / 갱신 계획</returns>
    public static DataImportPlan PrepareMap<T> ( string folder, T map, string mapName ) where T : ScriptableObject
    {
        folder = FoodImportValidator.ValidateOutputFolder( folder );
        string path = GetMapPath( folder, map, mapName );
        if ( map == null )
            map = AssetDatabase.LoadAssetAtPath<T>( path );
        return new DataImportPlan
        {
            OutputFolder = folder,
            MapType = typeof( T ),
            MapPath = path,
            DataMap = map
        };
    }

    /// <summary>
    /// 기존 Map 경로 또는 신규 Map의 유효한 저장 경로 조회
    /// </summary>
    /// <param name="folder">신규 Map 폴더</param>
    /// <param name="map">지정한 기존 Map</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <returns>Assets 내부 Map 경로</returns>
    public static string GetMapPath ( string folder, ScriptableObject map, string mapName )
    {
        folder = FoodImportValidator.ValidateOutputFolder( folder );
        if ( map != null )
        {
            string existingPath = AssetDatabase.GetAssetPath( map );
            if ( string.IsNullOrEmpty( existingPath ) == true )
                throw new InvalidOperationException( "저장된 Map 에셋을 지정해 주세요." );
            return existingPath;
        }

        string name = mapName?.Trim( );
        if ( string.IsNullOrWhiteSpace( name ) == true ||
             name.Contains( "/" ) == true || name.Contains( "\\" ) == true )
            throw new ArgumentException( "신규 Map 이름에는 폴더 경로 없이 파일명을 입력해 주세요." );
        if ( name.EndsWith( ".asset", StringComparison.OrdinalIgnoreCase ) == true )
            name = name.Substring( 0, name.Length - 6 );
        if ( string.IsNullOrWhiteSpace( name ) == true )
            throw new ArgumentException( "신규 Map 파일명을 입력해 주세요." );
        FoodImportValidator.ValidateOutputFolder( $"Assets/{name}" );
        return $"{folder}/{name}.asset";
    }

    /// <summary>
    /// 기존 참조를 유지하는 데이터 변경 준비
    /// </summary>
    /// <param name="plan">Map과 출력 폴더</param>
    /// <param name="id">원본 식별자</param>
    /// <param name="existing">Map에서 찾은 기존 에셋</param>
    /// <param name="apply">관리 필드 반영 함수</param>
    /// <returns>에셋 변경 계획</returns>
    public static DataImportAssetChange Change<T> ( DataImportPlan plan, string id,
        T existing, Action<SerializedObject> apply ) where T : ScriptableObject
    {
        string path = existing != null ? AssetDatabase.GetAssetPath( existing ) : $"{plan.OutputFolder}/{id}.asset";
        if ( existing == null )
            existing = AssetDatabase.LoadAssetAtPath<T>( path );
        return new DataImportAssetChange { Id = id, Path = path, AssetType = typeof( T ), Asset = existing, Apply = apply };
    }

    /// <summary>
    /// 생성 / 갱신 경로와 Map 제외 항목 검사
    /// </summary>
    /// <param name="plan">반영 예정 데이터</param>
    /// <param name="mapField">Map의 목록 필드</param>
    /// <param name="entryField">구조체 항목의 SO 참조 필드, 일반 배열이면 null</param>
    /// <param name="report">검사 결과</param>
    public static void Preflight ( DataImportPlan plan, string mapField, string entryField, DataImportReport report )
    {
        HashSet<string> paths = new( StringComparer.OrdinalIgnoreCase );
        HashSet<ScriptableObject> targets = new( );
        foreach ( DataImportAssetChange change in plan.Changes )
        {
            FoodImportValidator.ValidateAssetPath( change.Path, change.Asset, paths );
            if ( change.Asset != null && targets.Add( change.Asset ) == false )
                throw new InvalidOperationException( $"같은 에셋에 둘 이상의 입력이 대응합니다: {change.Path}" );
            report.Details.Add( $"{( change.Asset == null ? "생성" : "갱신 / 참조 보존" )}: {change.Id} → {change.Path}" );
        }
        if ( plan.MapType == null )
            return;
        FoodImportValidator.ValidateAssetPath( plan.MapPath, plan.DataMap, paths );
        report.Details.Add( $"Map 전체 교체: {plan.MapPath} / {plan.Changes.Count}개. 제외된 에셋은 삭제하지 않습니다." );
        if ( plan.DataMap == null )
            return;
        SerializedProperty entries = new SerializedObject( plan.DataMap ).FindProperty( mapField );
        for ( int i = 0; i < entries.arraySize; i++ )
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex( i );
            UnityEngine.Object asset = ( entryField == null ? entry : entry.FindPropertyRelative( entryField ) ).objectReferenceValue;
            if ( asset == null || targets.Contains( asset as ScriptableObject ) == false )
                report.Details.Add( $"Map 제외: {( asset == null ? $"빈 항목 {i}" : AssetDatabase.GetAssetPath( asset ) )}" );
        }
    }

    #endregion


    #region ----- 에셋 적용 -----

    /// <summary>
    /// 에셋 값과 Map을 순서대로 반영하고 변경한 에셋만 저장
    /// </summary>
    /// <param name="plan">재검사한 적용 계획</param>
    /// <returns>실제 반영 / 저장 결과</returns>
    public static DataImportResult Import ( DataImportPlan plan )
    {
        DataImportResult result = new( );
        string step = "적용 준비";
        try
        {
            if ( EditorApplication.isPlayingOrWillChangePlaymode == true )
                throw new InvalidOperationException( "플레이 중이거나 전환 중에는 임포트할 수 없습니다." );
            EnsureFolder( plan.OutputFolder );
            foreach ( DataImportAssetChange change in plan.Changes )
            {
                bool created = change.Asset == null;
                step = $"생성: {change.Id}";
                if ( created == true )
                {
                    change.Asset = CreateAsset( change.AssetType, change.Path );
                    result.CreatedCount++;
                    result.Changes.Add( step );
                }
                step = $"값 반영: {change.Id}";
                SerializedObject serialized = new( change.Asset );
                change.Apply( serialized );
                serialized.ApplyModifiedPropertiesWithoutUndo( );
                EditorUtility.SetDirty( change.Asset );
                if ( created == false )
                    result.UpdatedCount++;
                result.Changes.Add( $"{step}: {change.Path}" );
                step = $"저장: {change.Path}";
                Save( change.Asset );
                result.Changes.Add( step );
            }
            if ( plan.MapType == null )
                return result;
            step = "Map 생성";
            if ( plan.DataMap == null )
            {
                plan.DataMap = CreateAsset( plan.MapType, plan.MapPath );
                result.Changes.Add( $"Map 생성: {plan.MapPath}" );
            }
            result.DataMap = plan.DataMap;
            step = "Map 반영";
            SerializedObject map = new( plan.DataMap );
            plan.ApplyMap( map, plan.Changes );
            map.ApplyModifiedPropertiesWithoutUndo( );
            EditorUtility.SetDirty( plan.DataMap );
            result.MapApplied = true;
            result.Changes.Add( $"Map 반영: {plan.MapPath}" );
            step = "Map 저장";
            Save( plan.DataMap );
            result.Changes.Add( "Map 저장 완료" );
        }
        catch ( Exception exception )
        {
            result.Error = $"{step} 실패: {exception.Message}\nMap 반영: {result.MapApplied}. 앞선 변경은 자동 복원되지 않습니다.";
        }
        return result;
    }

    /// <summary>
    /// 일반 SO 배열 Map의 목록 반영
    /// </summary>
    /// <param name="map">대상 Map</param>
    /// <param name="field">목록 필드</param>
    /// <param name="changes">적용한 에셋 목록</param>
    public static void ApplyList ( SerializedObject map, string field, List<DataImportAssetChange> changes )
    {
        SerializedProperty values = map.FindProperty( field );
        values.arraySize = changes.Count;
        for ( int i = 0; i < changes.Count; i++ )
            values.GetArrayElementAtIndex( i ).objectReferenceValue = changes [ i ].Asset;
    }

    /// <summary>
    /// enum 배열을 직렬화 값에 반영
    /// </summary>
    /// <param name="serialized">대상 에셋</param>
    /// <param name="field">목록 필드</param>
    /// <param name="values">enum 값 목록</param>
    public static void ApplyKinds<T> ( SerializedObject serialized, string field, T [ ] values ) where T : struct, Enum
    {
        SerializedProperty property = serialized.FindProperty( field );
        property.arraySize = values.Length;
        for ( int i = 0; i < values.Length; i++ )
            property.GetArrayElementAtIndex( i ).intValue = Convert.ToInt32( values [ i ] );
    }

    #endregion


    #region ----- 참조 조회 -----

    /// <summary>
    /// Map 목록의 기존 SO 참조 조회
    /// </summary>
    /// <param name="map">조회할 Map</param>
    /// <param name="field">목록 필드</param>
    /// <returns>등록된 SO 목록</returns>
    public static List<T> GetEntries<T> ( ScriptableObject map, string field ) where T : ScriptableObject
    {
        List<T> entries = new( );
        if ( map == null )
            return entries;
        SerializedProperty values = new SerializedObject( map ).FindProperty( field );
        for ( int i = 0; i < values.arraySize; i++ )
            entries.Add( values.GetArrayElementAtIndex( i ).objectReferenceValue as T );
        return entries;
    }

    /// <summary>
    /// 명시 연결 / 경로 / GUID 또는 고유 식별자로 참조 조회
    /// </summary>
    /// <param name="id">원본 참조</param>
    /// <param name="bindings">사용자가 직접 연결한 참조</param>
    /// <param name="candidates">조회 범위</param>
    /// <param name="getId">후보의 식별자 조회</param>
    /// <returns>유일하게 대응한 에셋</returns>
    public static T Resolve<T> ( string id, List<DataImportReference> bindings,
        IEnumerable<T> candidates, Func<T, string> getId ) where T : UnityEngine.Object
    {
        T found = null;
        foreach ( DataImportReference binding in bindings )
        {
            if ( binding.Id != id )
                continue;
            T asset = binding.Asset as T;
            if ( asset == null )
                throw new InvalidOperationException( $"'{id}' 직접 연결은 {typeof( T ).Name} 에셋이어야 합니다." );
            if ( found != null )
                throw new InvalidOperationException( $"'{id}' 직접 연결이 중복됐습니다." );
            found = asset;
        }
        if ( found != null )
            return found;
        if ( id.StartsWith( "Assets/", StringComparison.Ordinal ) == true || id.StartsWith( "guid:", StringComparison.Ordinal ) == true )
        {
            string path = id.StartsWith( "guid:", StringComparison.Ordinal ) == true ? AssetDatabase.GUIDToAssetPath( id.Substring( 5 ) ) : id;
            found = AssetDatabase.LoadAssetAtPath<T>( path );
        }
        else
        {
            foreach ( T asset in candidates )
            {
                if ( asset == null || getId( asset ) != id )
                    continue;
                if ( found != null && found != asset )
                    throw new InvalidOperationException( $"'{id}' 참조가 둘 이상입니다. 경로 / GUID 또는 직접 연결을 지정해 주세요." );
                found = asset;
            }
        }
        if ( found == null )
            throw new InvalidOperationException( $"{typeof( T ).Name} 참조를 찾을 수 없습니다: '{id}'." );
        return found;
    }

    /// <summary>
    /// 지정 종류의 프로젝트 에셋 조회
    /// </summary>
    /// <returns>조회한 에셋 목록</returns>
    public static List<T> FindAssets<T> () where T : UnityEngine.Object
    {
        List<T> assets = new( );
        foreach ( string guid in AssetDatabase.FindAssets( $"t:{typeof( T ).Name}" ) )
            assets.Add( AssetDatabase.LoadAssetAtPath<T>( AssetDatabase.GUIDToAssetPath( guid ) ) );
        return assets;
    }

    #endregion


    #region ----- 생성 / 저장 -----

    /// <summary>
    /// 빈 출력 경로에 SO 생성
    /// </summary>
    /// <param name="type">SO 종류</param>
    /// <param name="path">에셋 경로</param>
    /// <returns>생성한 에셋</returns>
    static ScriptableObject CreateAsset ( Type type, string path )
    {
        if ( AssetDatabase.LoadMainAssetAtPath( path ) != null || File.Exists( path ) == true )
            throw new IOException( $"생성 경로에 다른 파일이 있습니다: {path}" );
        ScriptableObject asset = ScriptableObject.CreateInstance( type );
        asset.name = Path.GetFileNameWithoutExtension( path );
        AssetDatabase.CreateAsset( asset, path );
        if ( AssetDatabase.GetAssetPath( asset ) != path )
            throw new IOException( $"에셋 생성에 실패했습니다: {path}" );
        return asset;
    }

    /// <summary>
    /// 변경한 에셋을 저장하고 저장 여부 확인
    /// </summary>
    /// <param name="asset">저장할 에셋</param>
    static void Save ( ScriptableObject asset )
    {
        AssetDatabase.SaveAssetIfDirty( asset );
        if ( EditorUtility.IsDirty( asset ) == true )
            throw new IOException( $"변경 내용이 저장되지 않았습니다: {AssetDatabase.GetAssetPath( asset )}" );
    }

    /// <summary>
    /// 출력 경로의 없는 폴더 생성
    /// </summary>
    /// <param name="folder">검사한 폴더 경로</param>
    static void EnsureFolder ( string folder )
    {
        if ( AssetDatabase.IsValidFolder( folder ) == true )
            return;
        string parent = Path.GetDirectoryName( folder ).Replace( '\\', '/' );
        EnsureFolder( parent );
        if ( string.IsNullOrEmpty( AssetDatabase.CreateFolder( parent, Path.GetFileName( folder ) ) ) == true )
            throw new IOException( $"폴더 생성에 실패했습니다: {folder}" );
    }

    #endregion

}
