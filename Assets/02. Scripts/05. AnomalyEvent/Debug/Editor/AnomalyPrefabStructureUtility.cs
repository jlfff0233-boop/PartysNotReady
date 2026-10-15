using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 변칙 외형 구성 - 전체 외형 자식과 빈자리 조사 영역 구성
/// </summary>
public static class AnomalyPrefabStructureUtility
{
    /// <summary>
    /// 지정 필드의 기존 객체 참조 조회
    /// </summary>
    /// <param name="owner">참조 소유 컴포넌트</param>
    /// <param name="field">직렬화 필드 이름</param>
    /// <returns>기존 참조 또는 없음</returns>
    static Object Read ( Component owner, string field )
    {
        if ( owner == null )
            return null;
        using SerializedObject serialized = new( owner );
        return serialized.FindProperty( field ).objectReferenceValue;
    }

    /// <summary>
    /// 구성한 구조의 객체 참조 연결
    /// </summary>
    /// <param name="owner">참조 소유 컴포넌트</param>
    /// <param name="field">직렬화 필드 이름</param>
    /// <param name="value">구성한 객체 참조</param>
    static void Bind ( Component owner, string field, Object value )
    {
        if ( owner == null )
            return;
        using SerializedObject serialized = new( owner );
        serialized.FindProperty( field ).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo( );
    }

    /// <summary>
    /// 생성 이름 수정 - 도구가 만든 한글 이름과 중복 번호만 영문으로 변경
    /// </summary>
    /// <param name="target">이름을 수정할 객체</param>
    /// <param name="before">이전 생성 이름</param>
    /// <param name="after">새 생성 이름</param>
    /// <returns>이름 변경 여부</returns>
    static bool RenameGenerated ( Transform target, string before, string after )
    {
        if ( target == null )
            return false;
        string suffix = target.name.Substring( target.name.StartsWith( before ) ? before.Length : 0 );
        if ( target.name != before &&
            ( target.name.StartsWith( before + " (" ) == false || suffix.EndsWith( ")" ) == false ||
            int.TryParse( suffix.Substring( 2, suffix.Length - 3 ), out _ ) == false ) )
            return false;
        target.name = GameObjectUtility.GetUniqueNameForSibling( target.parent, after + suffix );
        return true;
    }

    /// <summary>
    /// 기존 생성 이름 일괄 수정 - 변칙 원본의 외형, 몸체와 조사 영역 이름 변경 및 저장
    /// </summary>
    [MenuItem( "Tools/Utilities/Migration/Rename Generated Objects" )]
    static void RenameExisting ()
    {
        const string folder = "Assets/04. Prefabs/AnomalyTarget";
        List<string> results = new( );
        AnomalyPrefabBackupBatch batch = AnomalyPrefabBackupUtility.CreateBatch( );
        int count = 0;
        foreach ( string guid in AssetDatabase.FindAssets( "t:Prefab", new[] { folder } ) )
        {
            string path = AssetDatabase.GUIDToAssetPath( guid );
            if ( AnomalyPrefabSetupUtility.CheckEditable( path, results ) == false )
            {
                results.Add( "건너뜀: " + path );
                continue;
            }
            GameObject root = PrefabUtility.LoadPrefabContents( path );
            try
            {
                if ( root.GetComponent<AnomalyTarget>( ) == null )
                    continue;
                Transform visual = AnomalyPrefabReferenceUtility.FindVisual( root );
                Collider restore = Read( root.GetComponent<PresenceDisplayController>( ), "_restoreCheckCollider" ) as Collider;
                bool changed = RenameGenerated( visual, "외형", "Visual" );
                if ( visual != null )
                {
                    foreach ( Transform child in visual )
                        changed |= RenameGenerated( child, "몸체", "Body" );
                }
                changed |= RenameGenerated( restore != null ? restore.transform : null, "빈자리 조사 영역", "RestoreArea" );
                if ( changed == false )
                    continue;
                AnomalyPrefabBackupEntry entry = AnomalyPrefabBackupUtility.Prepare( batch, path, results );
                if ( entry == null )
                    continue;
                PrefabUtility.SaveAsPrefabAsset( root, path, out bool saved );
                AnomalyPrefabBackupUtility.FinishSave( batch, entry );
                if ( saved == false )
                    throw new InvalidOperationException( "이름 변경 저장 실패: " + path );
                count++;
                results.Add( "이름 수정 완료: " + path );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents( root );
            }
        }
        Debug.Log( $"기존 생성 이름 수정: {count}개 프리팹\n" + string.Join( "\n", results ) );
        EditorUtility.DisplayDialog( "이름 일괄 수정", $"{count}개 프리팹의 생성 이름을 수정했습니다.\n상세 결과는 콘솔에서 확인할 수 있습니다.", "확인" );
    }


    /// <summary>
    /// 루트 컴포넌트의 소유 정보는 유지하며 설정 복사
    /// </summary>
    /// <param name="source">기존 루트 컴포넌트</param>
    /// <param name="body">복사할 외형 자식</param>
    /// <returns>새 자식의 복사된 컴포넌트</returns>
    static Component Copy ( Component source, GameObject body )
    {
        Component copied = body.AddComponent( source.GetType( ) );
        using SerializedObject original = new( source );
        using SerializedObject destination = new( copied );
        SerializedProperty property = original.GetIterator( );
        bool enter = true;
        while ( property.Next( enter ) )
        {
            enter = false;
            if ( property.name == "m_GameObject" || property.name == "m_CorrespondingSourceObject" ||
                property.name == "m_PrefabInstance" || property.name == "m_PrefabAsset" )
                continue;
            destination.CopyFromSerializedProperty( property );
        }
        destination.ApplyModifiedPropertiesWithoutUndo( );
        return copied;
    }

    /// <summary>
    /// 임시 원본 내부의 교체 컴포넌트 참조 연결
    /// </summary>
    /// <param name="root">내부 참조를 수정할 임시 원본</param>
    /// <param name="replacements">기존 컴포넌트와 새 컴포넌트 대응</param>
    static void Remap ( GameObject root, Dictionary<Object, Object> replacements )
    {
        foreach ( Component owner in root.GetComponentsInChildren<Component>( true ) )
        {
            if ( owner == null || replacements.ContainsKey( owner ) )
                continue;
            using SerializedObject serialized = new( owner );
            SerializedProperty property = serialized.GetIterator( );
            while ( property.Next( true ) )
            {
                if ( property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue != null && replacements.TryGetValue( property.objectReferenceValue, out Object value ) )
                    property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo( );
        }
    }

    /// <summary>
    /// 전체 외형과 물리 충돌체를 별도 자식으로 구성
    /// </summary>
    /// <param name="path">원본 경로</param>
    /// <param name="root">구성할 임시 관리 루트</param>
    /// <param name="results">구성 결과와 제외 사유</param>
    /// <param name="visual">구성하거나 유지한 전체 외형</param>
    /// <param name="source">외부 연결 이전에 사용할 원본 객체 대응</param>
    /// <returns>전체 외형 구성 성공 여부</returns>
    static bool BuildVisual ( string path, GameObject root, List<string> results, out Transform visual, AnomalyPrefabMigrationSource source )
    {
        visual = AnomalyPrefabReferenceUtility.FindVisual( root );
        Collider restore = Read( root.GetComponent<PresenceDisplayController>( ), "_restoreCheckCollider" ) as Collider;
        List<Component> moved = new( );
        foreach ( Component component in root.GetComponents<Component>( ) )
        {
            if ( component == null || component is Renderer renderer && renderer is MeshRenderer == false ||
                component is Collider physical && physical.isTrigger == false && physical is BoxCollider == false &&
                physical is SphereCollider == false && physical is CapsuleCollider == false && physical is MeshCollider == false )
            {
                results.Add( "제외: 루트에 자동 이동할 수 없는 컴포넌트가 있습니다." );
                return false;
            }
            if ( component is MeshFilter || component is MeshRenderer || component is Collider collider && collider.isTrigger == false )
                moved.Add( component );
        }
        List<Transform> branches = new( );
        foreach ( MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>( true ) )
        {
            if ( renderer.transform == root.transform )
                continue;
            Transform branch = renderer.transform;
            while ( branch.parent != root.transform )
                branch = branch.parent;
            if ( branches.Contains( branch ) == false )
                branches.Add( branch );
        }
        foreach ( Collider collider in root.GetComponentsInChildren<Collider>( true ) )
        {
            if ( collider.transform == root.transform || collider == restore || collider.isTrigger == true )
                continue;
            Transform branch = collider.transform;
            while ( branch.parent != root.transform )
                branch = branch.parent;
            if ( branches.Contains( branch ) == false )
                branches.Add( branch );
        }
        if ( visual != null && moved.Count == 0 && branches.Count == 1 && branches[0] == visual )
        {
            results.Add( "유지: 기존 전체 외형 자식 " + visual.name );
            return true;
        }
        if ( root.GetComponentInChildren<MeshRenderer>( true ) == null )
        {
            results.Add( "제외: 구성할 메시 외형이 없습니다." );
            return false;
        }
        if ( source == null && PrefabUtility.GetPrefabAssetType( AssetDatabase.LoadAssetAtPath<GameObject>( path ) ) == PrefabAssetType.Variant )
        {
            results.Add( "제외: 변형 프리팹의 계층은 원본에서 먼저 구성해야 합니다." );
            return false;
        }
        foreach ( Transform branch in branches )
        {
            foreach ( Component component in branch.GetComponentsInChildren<Component>( true ) )
            {
                if ( component == null || source == null && PrefabUtility.IsPartOfPrefabInstance( component ) ||
                    ( component is Transform == false && component is MeshFilter == false &&
                        component is MeshRenderer == false && component is Collider == false ) ||
                    component is Collider trigger && trigger.isTrigger == true )
                {
                    results.Add( "제외: 외형 가지에 중첩 프리팹이나 별도 동작이 있습니다. " + branch.name );
                    return false;
                }
            }
        }
        if ( restore != null && ( restore.transform == root.transform ||
            branches.Exists( branch => restore.transform == branch || restore.transform.IsChildOf( branch ) ) ) )
        {
            results.Add( "제외: 기존 조사 영역이 이동할 외형 안에 있습니다. 먼저 외형과 분리하세요." );
            return false;
        }
        if ( source == null && AnomalyPrefabReferenceChecker.Check( path, moved, results ) == false )
            return false;
        //이전 작업에서는 정적 외형의 중첩 연결을 풀고 현재 설정을 유지
        if ( source != null )
        {
            foreach ( Transform branch in branches )
            {
                if ( PrefabUtility.IsAnyPrefabInstanceRoot( branch.gameObject ) == true )
                    PrefabUtility.UnpackPrefabInstance( branch.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction );
            }
        }
        //새 부모는 루트 기준 기본 변환을 사용하여 원래 외형의 위치와 크기를 유지
        GameObject group = new( GameObjectUtility.GetUniqueNameForSibling( root.transform, "Visual" ) );
        group.layer = root.layer;
        group.transform.SetParent( root.transform, false );
        visual = group.transform;
        foreach ( Transform branch in branches )
            branch.SetParent( visual, false );
        if ( moved.Count > 0 )
        {
            GameObject body = new( "Body" );
            body.layer = root.layer;
            body.tag = root.tag;
            GameObjectUtility.SetStaticEditorFlags( body, GameObjectUtility.GetStaticEditorFlags( root ) );
            body.transform.SetParent( visual, false );
            Dictionary<Object, Object> replacements = new( );
            foreach ( Component component in moved )
                replacements.Add( component, Copy( component, body ) );
            Remap( root, replacements );
            foreach ( KeyValuePair<Object, Object> pair in replacements )
                source?.Replace( pair.Key, pair.Value );
            foreach ( Component component in moved )
                Object.DestroyImmediate( component );
        }
        results.Add( "구성: 전체 외형 자식과 내부 참조 연결" );
        return true;
    }

    /// <summary>
    /// 명시된 대표 충돌체를 우선하고 없으면 유일한 물리 충돌체 조회
    /// </summary>
    /// <param name="root">참조를 조회할 관리 루트</param>
    /// <param name="visual">대표 충돌체가 속할 외형</param>
    /// <returns>명확한 대표 충돌체 또는 없음</returns>
    static Collider FindMain ( GameObject root, Transform visual )
    {
        Collider main = Read( root.GetComponent<PresenceDisplayController>( ), "_collider" ) as Collider;
        if ( main == null )
            main = Read( root.GetComponent<ItemWorldController>( ), "_collider" ) as Collider;
        if ( main != null )
            return main.isTrigger == false && ( main.transform == visual || main.transform.IsChildOf( visual ) ) ? main : null;
        foreach ( Collider collider in visual.GetComponentsInChildren<Collider>( true ) )
        {
            if ( collider.isTrigger == true )
                continue;
            if ( main != null )
                return null;
            main = collider;
        }
        return main;
    }

    /// <summary>
    /// 기존 조사 영역 유지 또는 외형 범위의 새 조사 영역 생성
    /// </summary>
    /// <param name="root">조사 영역을 둘 관리 루트</param>
    /// <param name="visual">전체 외형</param>
    /// <param name="main">존재 기능의 대표 충돌체</param>
    /// <param name="options">새 조사 영역의 설정</param>
    /// <param name="results">유지와 생성 결과</param>
    /// <returns>조사 영역 구성 성공 여부</returns>
    static bool BuildRestoreArea ( GameObject root, Transform visual, Collider main, AnomalyPrefabOptions options, List<string> results )
    {
        PresenceDisplayController presence = root.GetComponent<PresenceDisplayController>( );
        if ( presence == null )
        {
            results.Add( "건너뜀: 존재 기능이 없어 조사 영역을 생성하지 않습니다." );
            return true;
        }
        Collider restore = Read( presence, "_restoreCheckCollider" ) as Collider;
        if ( restore != null )
        {
            if ( PresenceSettingsChecker.CheckRestoreConfig( root.transform, visual.gameObject, main, restore, out string reason ) == false )
            {
                results.Add( "제외: 기존 조사 영역을 먼저 수정해야 합니다. " + reason );
                return false;
            }
            results.Add( "유지: 기존 조사 영역의 크기와 레이어" );
            return true;
        }
        //연결이 빠진 기존 영역도 명확한 한 개이면 재사용
        foreach ( Collider candidate in root.GetComponentsInChildren<Collider>( true ) )
        {
            if ( PresenceSettingsChecker.CheckRestoreConfig( root.transform, visual.gameObject, main, candidate, out _ ) == false )
                continue;
            if ( restore != null )
            {
                results.Add( "제외: 기존 조사 영역 후보가 여러 개입니다. 먼저 지정하세요." );
                return false;
            }
            restore = candidate;
        }
        if ( restore != null )
        {
            Bind( presence, "_restoreCheckCollider", restore );
            results.Add( "연결: 기존 조사 영역의 크기와 레이어 유지" );
            return true;
        }
        if ( AnomalyPrefabBoundsUtility.Calculate( root.transform, visual.gameObject, options.RestoreMargin, out Bounds bounds, results ) == false )
            return false;
        GameObject area = new( GameObjectUtility.GetUniqueNameForSibling( root.transform, "RestoreArea" ) );
        area.layer = options.RestoreLayer;
        area.transform.SetParent( root.transform, false );
        BoxCollider box = area.AddComponent<BoxCollider>( );
        box.center = bounds.center;
        box.size = bounds.size;
        box.enabled = false;
        box.isTrigger = false;
        Bind( presence, "_restoreCheckCollider", box );
        results.Add( $"생성: 빈자리 조사 영역 / 레이어 {LayerMask.LayerToName( area.layer )} / 중심 {box.center} / 크기 {box.size}" );
        return true;
    }

    /// <summary>
    /// 임시 원본에 선택한 외형 구성과 조사 영역 적용
    /// </summary>
    /// <param name="path">원본 경로</param>
    /// <param name="root">임시 관리 루트</param>
    /// <param name="options">선택한 구성 설정</param>
    /// <param name="results">처리 결과 목록</param>
    /// <param name="source">외부 연결 이전에 사용할 원본 객체 대응</param>
    /// <returns>선택한 구조를 구성하고 연결했는지 여부</returns>
    public static bool Process ( string path, GameObject root, AnomalyPrefabOptions options, List<string> results,
        AnomalyPrefabMigrationSource source = null )
    {
        if ( options.BuildVisual == false && options.BuildRestoreArea == false )
            return true;
        bool presence = root.GetComponentInChildren<PresenceDisplayController>( true ) != null;
        bool buildVisual = options.BuildVisual == true && ( presence == true ||
            root.GetComponentInChildren<RotateView>( true ) != null || root.GetComponentInChildren<ShakeView>( true ) != null );
        bool buildRestore = options.BuildRestoreArea == true && presence == true;
        if ( options.BuildVisual == true && buildVisual == false )
            results.Add( "건너뜀: 전체 외형 자식 구성 / 회전, 흔들림, 존재 기능 없음" );
        if ( options.BuildRestoreArea == true && buildRestore == false )
            results.Add( "건너뜀: 빈자리 조사 영역 생성 / 존재 기능 없음" );
        if ( buildVisual == false && buildRestore == false )
            return true;
        if ( buildRestore == true &&
            ( options.RestoreLayer < 0 || options.RestoreLayer > 31 ||
                string.IsNullOrEmpty( LayerMask.LayerToName( options.RestoreLayer ) ) ||
                options.RestoreMargin < 0f || float.IsNaN( options.RestoreMargin ) || float.IsInfinity( options.RestoreMargin ) ) )
        {
            results.Add( "제외: 존재 기능의 조사 영역에는 등록된 레이어와 유한한 크기 여유가 필요합니다." );
            return false;
        }
        if ( CheckStructure( root, results ) == false )
            return false;
        Transform visual = AnomalyPrefabReferenceUtility.FindVisual( root );
        if ( buildVisual == true && BuildVisual( path, root, results, out visual, source ) == false )
            return false;
        if ( visual == null )
        {
            results.Add( "제외: 조사 영역 구성 전에 전체 외형 자식이 필요합니다." );
            return false;
        }
        Collider main = FindMain( root, visual );
        if ( root.GetComponent<PresenceDisplayController>( ) != null && main == null )
        {
            results.Add( "제외: 존재 기능의 대표 충돌체가 명확하지 않습니다. 먼저 지정하세요." );
            return false;
        }
        Bind( root.GetComponent<RotateView>( ), "_rotatingTarget", visual );
        Bind( root.GetComponent<ShakeView>( ), "_shakingTarget", visual );
        Bind( root.GetComponent<PresenceDisplayController>( ), "_target", visual.gameObject );
        Bind( root.GetComponent<PresenceDisplayController>( ), "_collider", main );
        if ( buildRestore == true && BuildRestoreArea( root, visual, main, options, results ) == false )
            return false;
        if ( Read( root.GetComponent<ItemWorldController>( ), "_collider" ) == null && main != null )
            Bind( root.GetComponent<ItemWorldController>( ), "_collider", main );
        return true;
    }

    /// <summary>
    /// 자동 구성 범위의 단일 물건과 특수 구조 확인
    /// </summary>
    /// <param name="root">구성할 관리 루트</param>
    /// <param name="results">제외 사유 목록</param>
    /// <returns>지원하는 단일 물건 구조인지 여부</returns>
    static bool CheckStructure ( GameObject root, List<string> results )
    {
        AnomalyTarget[] targets = root.GetComponentsInChildren<AnomalyTarget>( true );
        if ( targets.Length != 1 || targets[0].gameObject != root )
        {
            results.Add( "제외: 외형과 조사 영역 구성은 루트에 변칙 대상이 하나 있는 물건만 지원합니다." );
            return false;
        }
        if ( root.GetComponentInChildren<AppearanceChangeController>( true ) != null ||
            root.GetComponentInChildren<Animator>( true ) != null || root.GetComponentInChildren<Animation>( true ) != null ||
            root.GetComponentInChildren<SkinnedMeshRenderer>( true ) != null )
        {
            results.Add( "제외: 외형 교체나 애니메이션 전용 구조는 직접 구성해야 합니다." );
            return false;
        }
        return true;
    }

}
