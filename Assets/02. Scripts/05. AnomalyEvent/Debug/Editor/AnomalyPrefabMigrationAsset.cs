using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 외부 설정 보관 - 편집 에셋의 참조와 배치 재정의 수집 및 이전
/// </summary>
public class AnomalyPrefabMigrationAsset : IDisposable
{
    /// <summary>
    /// 변경 전 객체 연결 정보
    /// </summary>
    class Link
    {
        public Object Value;
        public AnomalyPrefabObjectAddress Address;
        public AnomalyPrefabObjectAddress Instance;
        public AnomalyPrefabMigrationSource Source;
        public string Key;
        public string AssetPath;
        public AnomalyPrefabObjectAddress Relative;
        public Type AddedType;
        public int AddedIndex;
    }
    /// <summary>
    /// 이전할 직렬화 객체 참조
    /// </summary>
    class Reference
    {
        public Link Owner;
        public string Property;
        public Link Value;
    }
    /// <summary>
    /// 배치별 속성 재정의
    /// </summary>
    class Modification
    {
        public Link Target;
        public string Property;
        public string Value;
        public Link Object;
    }
    /// <summary>
    /// 배치별 재정의와 제거 컴포넌트
    /// </summary>
    class Instance
    {
        public AnomalyPrefabObjectAddress Address;
        public readonly List<Modification> Modifications = new( );
        public readonly List<Link> Removed = new( );
    }

    readonly List<Reference> _references = new( );		//새 객체로 연결할 참조
    readonly List<Instance> _instances = new( );		//원본 배치별 재정의
    readonly List<AnomalyPrefabMigrationSource> _sources;		//이번 작업의 원본 대응
    Scene _scene;		//편집 중인 씬
    bool _closeScene;		//작업 종료 시 추가로 연 씬 닫기
    Object[] _assets;		//일반 에셋의 직렬화 객체
    public string Path { get; }		//저장할 에셋 경로
    public GameObject Root { get; }		//프리팹 편집 루트
    public AnomalyPrefabMigrationSource Source { get; set; }		//수정 원본의 식별자 대응
    public bool IsScene => Path.EndsWith( ".unity", StringComparison.OrdinalIgnoreCase );

    /// <summary>
    /// 현재 편집 영역의 계층 루트 조회
    /// </summary>
    /// <returns>프리팹 또는 씬의 루트 목록</returns>
    GameObject[] GetRoots () => Root != null ? new[] { Root } : _scene.IsValid( ) ? _scene.GetRootGameObjects( ) : Array.Empty<GameObject>( );

    /// <summary>
    /// 편집 객체의 기존 원본 연결 정보 보관
    /// </summary>
    /// <param name="value">참조 또는 소유 객체</param>
    /// <returns>저장 이후에도 조회 가능한 연결 정보</returns>
    Link CaptureLink ( Object value )
    {
        Link link = new( ) { Value = value };
        if ( value == null )
            return link;
        if ( EditorUtility.IsPersistent( value ) == true )
        {
            string key = AnomalyPrefabMigrationSource.GetKey( value );
            link.Source = _sources.Find( source => source.Path == AssetDatabase.GetAssetPath( value ) && source.Contains( key ) );
            link.Key = key;
            if ( key != null )
                link.AssetPath = AssetDatabase.GUIDToAssetPath( key.Substring( 0, 32 ) );
            return link;
        }
        GameObject owner = value is Component component ? component.gameObject : value as GameObject;
        if ( owner == null )
            return link;
        GameObject[] roots = GetRoots( );
        //다른 씬의 참조를 이 에셋의 로컬 주소로 기록하지 않음
        if ( Array.Exists( roots, root => owner == root || owner.transform.IsChildOf( root.transform ) ) == false )
            return link;
        Transform current = owner.transform;
        while ( current != null )
        {
            if ( PrefabUtility.IsAnyPrefabInstanceRoot( current.gameObject ) == true )
            {
                foreach ( AnomalyPrefabMigrationSource source in _sources )
                {
                    if ( PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot( current.gameObject ) != source.Path )
                        continue;
                    Object original = PrefabUtility.GetCorrespondingObjectFromSourceAtPath( value, source.Path );
                    string key = AnomalyPrefabMigrationSource.GetKey( original );
                    if ( source.Contains( key ) == false )
                    {
                        //배치에만 추가한 객체와 컴포넌트는 대응하는 부모를 기준으로 유지
                        Transform anchor = owner.transform;
                        while ( anchor != null )
                        {
                            key = AnomalyPrefabMigrationSource.GetKey( PrefabUtility.GetCorrespondingObjectFromSourceAtPath( anchor.gameObject, source.Path ) );
                            if ( source.Contains( key ) == true )
                            {
                                link.Relative = AnomalyPrefabObjectAddress.Capture( new[] { anchor.gameObject }, owner );
                                if ( value is Component added )
                                {
                                    link.AddedType = added.GetType( );
                                    foreach ( Component sibling in owner.GetComponents( link.AddedType ) )
                                    {
                                        if ( sibling == added )
                                            break;
                                        if ( PrefabUtility.GetCorrespondingObjectFromSourceAtPath( sibling, source.Path ) == null )
                                            link.AddedIndex++;
                                    }
                                }
                                break;
                            }
                            if ( anchor == current )
                                break;
                            anchor = anchor.parent;
                        }
                        if ( source.Contains( key ) == false )
                            continue;
                    }
                    link.Source = source;
                    link.Key = key;
                    link.Instance = AnomalyPrefabObjectAddress.Capture( roots, current.gameObject );
                    return link;
                }
            }
            current = current.parent;
        }
        link.Address = AnomalyPrefabObjectAddress.Capture( roots, value );
        return link;
    }

    /// <summary>
    /// 재임포트된 일반 에셋과 프리팹 내부 객체의 기존 식별자 조회
    /// </summary>
    /// <param name="link">변경하지 않는 에셋의 연결 정보</param>
    /// <returns>현재 에셋 객체</returns>
    static Object ResolvePersistent ( Link link )
    {
        foreach ( Object asset in AssetDatabase.LoadAllAssetsAtPath( link.AssetPath ) )
        {
            if ( AnomalyPrefabMigrationSource.GetKey( asset ) == link.Key )
                return asset;
            if ( asset is GameObject root )
            {
                foreach ( Transform child in root.GetComponentsInChildren<Transform>( true ) )
                {
                    if ( AnomalyPrefabMigrationSource.GetKey( child.gameObject ) == link.Key )
                        return child.gameObject;
                    foreach ( Component component in child.GetComponents<Component>( ) )
                    {
                        if ( component != null && AnomalyPrefabMigrationSource.GetKey( component ) == link.Key )
                            return component;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 보관한 연결을 현재 원본 또는 배치 객체로 조회
    /// </summary>
    /// <param name="link">보관한 객체 연결</param>
    /// <returns>현재 연결 대상</returns>
    Object Resolve ( Link link )
    {
        if ( link.Source == null )
        {
            if ( link.Address != null )
                return link.Address.Resolve( GetRoots( ) );
            if ( link.AssetPath != null )
                return ResolvePersistent( link );
            return link.Value;
        }
        Object source = link.Source.Resolve( link.Key );
        if ( link.Instance == null || source == null )
            return source;
        GameObject instance = link.Instance.Resolve( GetRoots( ) ) as GameObject;
        if ( instance == null )
            return null;
        foreach ( Transform child in instance.GetComponentsInChildren<Transform>( true ) )
        {
            if ( source is GameObject && PrefabUtility.GetCorrespondingObjectFromSourceAtPath( child.gameObject, link.Source.Path ) == source )
            {
                if ( link.Relative == null )
                    return child.gameObject;
                GameObject owner = link.Relative.Resolve( new[] { child.gameObject } ) as GameObject;
                if ( owner == null || link.AddedType == null )
                    return owner;
                int index = 0;
                foreach ( Component added in owner.GetComponents( link.AddedType ) )
                {
                    if ( PrefabUtility.GetCorrespondingObjectFromSourceAtPath( added, link.Source.Path ) == null && index++ == link.AddedIndex )
                        return added;
                }
                return null;
            }
            if ( source is Component )
            {
                foreach ( Component component in child.GetComponents<Component>( ) )
                {
                    if ( component != null && PrefabUtility.GetCorrespondingObjectFromSourceAtPath( component, link.Source.Path ) == source )
                        return component;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 에셋을 에디터 API로 열어 저장 전 상태 유지
    /// </summary>
    /// <param name="path">편집할 에셋 경로</param>
    /// <param name="sources">이번 작업의 원본 대응</param>
    public AnomalyPrefabMigrationAsset ( string path, List<AnomalyPrefabMigrationSource> sources )
    {
        Path = path;
        _sources = sources;
        if ( path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) )
            Root = PrefabUtility.LoadPrefabContents( path );
        else if ( IsScene == true )
        {
            _scene = SceneManager.GetSceneByPath( path );
            if ( _scene.IsValid( ) == false || _scene.isLoaded == false )
            {
                _scene = EditorSceneManager.OpenScene( path, OpenSceneMode.Additive );
                _closeScene = true;
            }
        }
        else
            _assets = AssetDatabase.LoadAllAssetsAtPath( path );
    }

    /// <summary>
    /// 편집 영역의 직렬화 객체 수집
    /// </summary>
    /// <returns>계층 컴포넌트 또는 일반 에셋 객체</returns>
    List<Object> GetObjects ()
    {
        List<Object> objects = new( );
        foreach ( GameObject root in GetRoots( ) )
        {
            foreach ( Transform child in root.GetComponentsInChildren<Transform>( true ) )
            {
                foreach ( Component component in child.GetComponents<Component>( ) )
                {
                    //계층과 컴포넌트 소유 관계는 프리팹 갱신에 맡기고 직접 참조만 이전
                    if ( component != null && component is Transform == false )
                        objects.Add( component );
                }
            }
        }
        if ( _assets != null )
            objects.AddRange( _assets );
        return objects;
    }

    /// <summary>
    /// 원본 변경 전 직접 참조와 배치별 재정의 보관
    /// </summary>
    /// <param name="results">미리보기 결과</param>
    public void Capture ( List<string> results )
    {
        foreach ( Object owner in GetObjects( ) )
        {
            using SerializedObject serialized = new( owner );
            SerializedProperty property = serialized.GetIterator( );
            while ( property.Next( true ) )
            {
                if ( property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null ||
                    property.name == "m_GameObject" || property.name == "m_CorrespondingSourceObject" ||
                    property.name == "m_PrefabInstance" || property.name == "m_PrefabAsset" )
                    continue;
                Link value = CaptureLink( property.objectReferenceValue );
                if ( value.Source != null )
                    _references.Add( new Reference { Owner = CaptureLink( owner ), Property = property.propertyPath, Value = value } );
            }
        }
        HashSet<GameObject> captured = new( );
        foreach ( GameObject root in GetRoots( ) )
        {
            foreach ( Transform child in root.GetComponentsInChildren<Transform>( true ) )
            {
                GameObject instance = PrefabUtility.GetOutermostPrefabInstanceRoot( child.gameObject );
                if ( instance == null || captured.Add( instance ) == false )
                    continue;
                Instance record = new( ) { Address = AnomalyPrefabObjectAddress.Capture( GetRoots( ), instance ) };
                bool affected = false;
                foreach ( PropertyModification modification in PrefabUtility.GetPropertyModifications( instance ) ?? Array.Empty<PropertyModification>( ) )
                {
                    Link target = CaptureLink( modification.target );
                    Link value = CaptureLink( modification.objectReference );
                    affected |= target.Source != null || value.Source != null;
                    record.Modifications.Add( new Modification
                    {
                        Target = target, Property = modification.propertyPath, Value = modification.value, Object = value
                    } );
                }
                foreach ( RemovedComponent removed in PrefabUtility.GetRemovedComponents( instance ) )
                {
                    Link original = CaptureLink( removed.assetComponent );
                    if ( original.Source == null )
                        continue;
                    //새 원본의 대응 컴포넌트에도 배치별 제거 상태 적용
                    original.Instance = AnomalyPrefabObjectAddress.Capture( GetRoots( ),
                        PrefabUtility.GetNearestPrefabInstanceRoot( removed.containingInstanceGameObject ) );
                    record.Removed.Add( original );
                    affected = true;
                }
                if ( affected == true )
                    _instances.Add( record );
            }
        }
        results.Add( $"참조 수집: {Path} / 배치 재정의 {_instances.Count}개 / 직접 참조 {_references.Count}개" );
    }

    /// <summary>
    /// 저장된 원본 대응으로 배치 재정의와 직접 참조 이전
    /// </summary>
    /// <param name="results">이전 결과</param>
    public void Transfer ( List<string> results )
    {
        foreach ( Instance record in _instances )
        {
            GameObject instance = record.Address.Resolve( GetRoots( ) ) as GameObject;
            if ( instance == null )
                throw new InvalidOperationException( "배치 원본을 찾지 못했습니다: " + Path );
            List<PropertyModification> modifications = new( );
            foreach ( Modification saved in record.Modifications )
            {
                Object target = Resolve( saved.Target );
                if ( target == null && saved.Target.Source != null )
                    throw new InvalidOperationException( "재정의 대상이 유실됐습니다: " + Path + " / " + saved.Property );
                Object value = Resolve( saved.Object );
                if ( value == null && saved.Object.Source != null )
                    throw new InvalidOperationException( "재정의 참조를 찾지 못했습니다: " + Path + " / " + saved.Property );
                modifications.Add( new PropertyModification
                {
                    target = target, propertyPath = saved.Property, value = saved.Value, objectReference = value
                } );
            }
            PrefabUtility.SetPropertyModifications( instance, modifications.ToArray( ) );
            foreach ( Link removed in record.Removed )
            {
                Component component = Resolve( removed ) as Component;
                if ( component != null )
                    Object.DestroyImmediate( component );
            }
        }
        foreach ( Reference reference in _references )
        {
            Object owner = Resolve( reference.Owner );
            Object value = Resolve( reference.Value );
            if ( owner == null || value == null )
                throw new InvalidOperationException( "직접 참조를 이전하지 못했습니다: " + Path + " / " + reference.Property );
            using SerializedObject serialized = new( owner );
            SerializedProperty property = serialized.FindProperty( reference.Property );
            if ( property == null )
                throw new InvalidOperationException( "참조 필드가 변경됐습니다: " + Path + " / " + reference.Property );
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo( );
            if ( PrefabUtility.IsPartOfPrefabInstance( owner ) == true )
                PrefabUtility.RecordPrefabInstancePropertyModifications( owner );
        }
        results.Add( "외부 설정 이전 완료: " + Path );
    }

    /// <summary>
    /// 편집 내용을 에디터 API로 저장
    /// </summary>
    public void Save ()
    {
        bool success = true;
        if ( Root != null )
            PrefabUtility.SaveAsPrefabAsset( Root, Path, out success );
        else if ( IsScene == true )
            success = EditorSceneManager.SaveScene( _scene );
        else
        {
            _assets = AssetDatabase.LoadAllAssetsAtPath( Path );
            foreach ( Object asset in _assets )
            {
                EditorUtility.SetDirty( asset );
                AssetDatabase.SaveAssetIfDirty( asset );
            }
        }
        if ( success == false )
            throw new InvalidOperationException( "에셋 저장 실패: " + Path );
    }

    /// <summary>
    /// 실패한 작업에서 일반 에셋에 남은 미저장 변경 정리
    /// </summary>
    public void DiscardAssetChanges ()
    {
        if ( _assets != null )
            AssetDatabase.ImportAsset( Path, ImportAssetOptions.ForceUpdate );
    }

    /// <summary>
    /// 임시 프리팹과 작업 중 추가로 연 씬 정리
    /// </summary>
    public void Dispose ()
    {
        if ( Root != null )
            PrefabUtility.UnloadPrefabContents( Root );
        if ( _closeScene == true )
            EditorSceneManager.CloseScene( _scene, true );
    }
}
