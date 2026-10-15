using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 원본 객체 대응 - 기존 식별자와 구성 후 객체 연결
/// </summary>
public class AnomalyPrefabMigrationSource
{
    /// <summary>
    /// 중첩 원본의 구성 후 계층 대응
    /// </summary>
    class Nested
    {
        public AnomalyPrefabMigrationSource Source;
        public string Key;
        public AnomalyPrefabObjectAddress Instance;
    }
    readonly Dictionary<string, Object> _objects = new( );		//기존 식별자와 편집 객체
    readonly Dictionary<string, AnomalyPrefabObjectAddress> _addresses = new( );		//저장 후 객체 위치
    readonly Dictionary<string, Nested> _nested = new( );		//다른 원본 구성에 따라 변하는 위치
    public string Path { get; }		//수정할 원본 경로
    public GameObject Root { get; }		//임시 편집 원본

    /// <summary>
    /// 에셋 객체 식별자 조회
    /// </summary>
    /// <param name="value">원본 객체</param>
    /// <returns>에셋과 내부 객체의 식별자, 에셋 객체가 아니면 없음</returns>
    public static string GetKey ( Object value )
    {
        return value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier( value, out string guid, out long id )
            ? guid + ":" + id : null;
    }

    /// <summary>
    /// 원본 객체와 같은 위치의 편집 객체 연결
    /// </summary>
    /// <param name="assets">원본 계층</param>
    /// <param name="contents">편집 계층</param>
    /// <param name="value">원본 객체</param>
    void Add ( GameObject[] assets, GameObject[] contents, Object value )
    {
        string key = GetKey( value );
        if ( key != null && key.StartsWith( AssetDatabase.AssetPathToGUID( Path ) + ":", StringComparison.Ordinal ) )
            _objects[key] = AnomalyPrefabObjectAddress.Capture( assets, value ).Resolve( contents );
    }

    /// <summary>
    /// 원본의 게임 오브젝트와 컴포넌트 식별자 보관
    /// </summary>
    /// <param name="path">원본 경로</param>
    /// <param name="root">구성 전 임시 원본</param>
    public AnomalyPrefabMigrationSource ( string path, GameObject root )
    {
        Path = path;
        Root = root;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>( path );
        GameObject[] assets = { asset };
        GameObject[] contents = { root };
        foreach ( Transform transform in asset.GetComponentsInChildren<Transform>( true ) )
        {
            Add( assets, contents, transform.gameObject );
            foreach ( Component component in transform.GetComponents<Component>( ) )
            {
                if ( component != null )
                    Add( assets, contents, component );
            }
        }
    }


    /// <summary>
    /// 삭제할 컴포넌트 대신 새 컴포넌트를 대응에 등록
    /// </summary>
    /// <param name="original">삭제할 편집 컴포넌트</param>
    /// <param name="replacement">새 외형의 컴포넌트</param>
    public void Replace ( Object original, Object replacement )
    {
        foreach ( string key in new List<string>( _objects.Keys ) )
        {
            if ( _objects[key] == original )
                _objects[key] = replacement;
        }
    }

    /// <summary>
    /// 구성이 끝난 객체의 저장 후 조회 위치 보관
    /// </summary>
    /// <param name="sources">이번 작업의 모든 원본 대응</param>
    public void Seal ( List<AnomalyPrefabMigrationSource> sources )
    {
        foreach ( KeyValuePair<string, Object> pair in _objects )
        {
            if ( pair.Value == null )
                throw new InvalidOperationException( "원본 객체 대응이 유실됐습니다: " + Path + " / " + pair.Key );
            GameObject owner = pair.Value is Component component ? component.gameObject : (GameObject)pair.Value;
            GameObject instance = PrefabUtility.GetNearestPrefabInstanceRoot( owner );
            if ( instance != null )
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot( instance );
                AnomalyPrefabMigrationSource parent = sources.Find( source => source != this && source.Path == path );
                string key = parent != null ? GetKey( PrefabUtility.GetCorrespondingObjectFromSourceAtPath( pair.Value, path ) ) : null;
                if ( parent != null && parent.Contains( key ) == true )
                {
                    _nested.Add( pair.Key, new Nested
                    {
                        Source = parent, Key = key,
                        Instance = AnomalyPrefabObjectAddress.Capture( new[] { Root }, instance )
                    } );
                    continue;
                }
            }
            _addresses.Add( pair.Key, AnomalyPrefabObjectAddress.Capture( new[] { Root }, pair.Value ) );
        }
    }

    /// <summary>
    /// 중첩 원본의 변경을 반영한 객체 위치 조회
    /// </summary>
    /// <param name="key">기존 객체 식별자</param>
    /// <returns>구성 후 계층에서의 객체 위치</returns>
    AnomalyPrefabObjectAddress GetAddress ( string key )
    {
        if ( _nested.TryGetValue( key, out Nested nested ) )
            return nested.Instance.Append( nested.Source.GetAddress( nested.Key ) );
        return _addresses[key];
    }

    /// <summary>
    /// 기존 식별자에 대응하는 저장된 원본 객체 조회
    /// </summary>
    /// <param name="key">기존 객체 식별자</param>
    /// <returns>구성 후 원본 객체</returns>
    public Object Resolve ( string key )
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>( Path );
        return GetAddress( key ).Resolve( new[] { asset } );
    }

    /// <summary>
    /// 저장 후 원본의 객체 대응이 모두 유지되는지 확인
    /// </summary>
    public void CheckSaved ()
    {
        foreach ( string key in _objects.Keys )
        {
            if ( Resolve( key ) == null )
                throw new InvalidOperationException( "저장 후 원본 객체를 찾지 못했습니다: " + Path + " / " + key );
        }
    }

    /// <summary>
    /// 기존 객체가 이번 원본에 속하는지 조회
    /// </summary>
    /// <param name="key">기존 객체 식별자</param>
    /// <returns>원본 객체인지 여부</returns>
    public bool Contains ( string key ) => key != null && _objects.ContainsKey( key );

}
