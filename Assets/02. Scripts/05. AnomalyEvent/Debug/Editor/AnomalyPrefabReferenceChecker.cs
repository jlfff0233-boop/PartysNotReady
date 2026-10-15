using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 외부 참조 검사 - 이동할 루트 컴포넌트의 에셋 참조와 재정의 확인
/// </summary>
public static class AnomalyPrefabReferenceChecker
{
    static readonly Dictionary<string, HashSet<string>> _references = new( ); //외부 참조별 소유 경로
    static readonly List<string> _binaryPaths = new( ); //텍스트로 확인할 수 없는 에셋
    static bool _isIndexed; //현재 작업의 참조 목록 구성 여부

    /// <summary>
    /// 새 미리보기와 적용 작업의 참조 목록 초기화
    /// </summary>
    public static void Begin ()
    {
        _references.Clear( );
        _binaryPaths.Clear( );
        _isIndexed = false;
    }

    /// <summary>
    /// 참조 식별자별 소유 경로 기록
    /// </summary>
    /// <param name="key">원본 식별자와 컴포넌트 번호</param>
    /// <param name="path">참조를 가진 소유 경로</param>
    static void Add ( string key, string path )
    {
        if ( _references.TryGetValue( key, out HashSet<string> owners ) == false )
        {
            owners = new( );
            _references.Add( key, owners );
        }
        owners.Add( path );
    }

    /// <summary>
    /// 객체의 원본 식별자로 참조 기록
    /// </summary>
    /// <param name="value">참조된 객체</param>
    /// <param name="owner">참조 소유자 표시</param>
    static void AddObject ( Object value, string owner )
    {
        if ( value == null )
            return;
        if ( EditorUtility.IsPersistent( value ) == false )
            value = PrefabUtility.GetCorrespondingObjectFromOriginalSource( value );
        if ( value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier( value, out string guid, out long id ) )
            Add( guid + ":" + id, owner );
    }

    /// <summary>
    /// 직렬화 에셋과 열린 씬의 참조 목록 구성
    /// </summary>
    static void Index ()
    {
        if ( _isIndexed == true )
            return;
        Regex reference = new( @"\{fileID:\s*(-?\d+),\s*guid:\s*([a-fA-F0-9]{32})" );
        HashSet<string> extensions = new( )
        {
            ".prefab", ".unity", ".asset", ".anim", ".controller", ".overrideController", ".playable", ".mat"
        };
        foreach ( string path in AssetDatabase.GetAllAssetPaths( ) )
        {
            if ( extensions.Contains( Path.GetExtension( path ) ) == false || File.Exists( path ) == false )
                continue;
            using StreamReader reader = new( path );
            string first = reader.ReadLine( );
            if ( first == null || first.StartsWith( "%YAML" ) == false )
            {
                _binaryPaths.Add( path );
                continue;
            }
            foreach ( Match match in reference.Matches( reader.ReadToEnd( ) ) )
                Add( match.Groups[2].Value.ToLowerInvariant( ) + ":" + match.Groups[1].Value, path );
        }
        //미저장 씬에서 추가한 직접 참조도 원본 컴포넌트 교체 전에 확인
        foreach ( MonoBehaviour owner in Resources.FindObjectsOfTypeAll<MonoBehaviour>( ) )
        {
            if ( owner == null || owner.gameObject.scene.IsValid( ) == false ||
                owner.gameObject.scene.isLoaded == false || EditorSceneManager.IsPreviewScene( owner.gameObject.scene ) )
                continue;
            using SerializedObject serialized = new( owner );
            SerializedProperty property = serialized.GetIterator( );
            while ( property.Next( true ) )
            {
                if ( property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_GameObject" )
                    AddObject( property.objectReferenceValue, "열린 씬: " + owner.name );
            }
        }
        _isIndexed = true;
    }

    /// <summary>
    /// 교체할 루트 컴포넌트의 외부 참조와 재정의 확인
    /// </summary>
    /// <param name="path">편집할 원본 경로</param>
    /// <param name="moved">교체할 루트 컴포넌트</param>
    /// <param name="results">제외 사유 목록</param>
    /// <returns>외부 참조가 없어 컴포넌트를 교체할 수 있는지 여부</returns>
    public static bool Check ( string path, List<Component> moved, List<string> results )
    {
        if ( moved.Count == 0 )
            return true;
        Index( );
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>( path );
        foreach ( Component source in asset.GetComponents<Component>( ) )
        {
            if ( source == null || moved.Exists( component => component.GetType( ) == source.GetType( ) ) == false )
                continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier( source, out string guid, out long id );
            if ( _references.TryGetValue( guid + ":" + id, out HashSet<string> owners ) == false )
                continue;
            foreach ( string owner in owners )
            {
                if ( owner == path )
                    continue;
                results.Add( $"제외: 이동할 {source.GetType( ).Name}에 외부 참조 또는 재정의가 있습니다. {owner}" );
                return false;
            }
        }
        foreach ( string owner in _binaryPaths )
        {
            if ( owner != path && Array.IndexOf( AssetDatabase.GetDependencies( owner, false ), path ) >= 0 )
            {
                results.Add( "제외: 참조 내용을 확인할 수 없는 에셋이 연결되어 있습니다. " + owner );
                return false;
            }
        }
        return true;
    }
}
