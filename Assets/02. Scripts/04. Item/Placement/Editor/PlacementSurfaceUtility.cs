using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 배치 표면 구성 - 씬 후보 수집과 표면 자동 연결 및 일괄 설정
/// </summary>
public static class PlacementSurfaceUtility
{
    /// <summary>
    /// 후보 수집 - 씬과 프리팹 원본 및 편집 화면에서 충돌체 소유 객체 조회
    /// </summary>
    /// <param name="roots">하이어라키에서 선택한 탐색 루트</param>
    /// <returns>중복 없는 배치 표면 후보</returns>
    public static List<PlacementSurfaceEntry> Collect ( GameObject[] roots )
    {
        List<PlacementSurfaceEntry> entries = new( );
        HashSet<GameObject> owners = new( );
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage( );
        foreach ( GameObject root in roots )
        {
            bool asset = EditorUtility.IsPersistent( root );
            bool editing = stage != null && root.scene == stage.scene;
            if ( asset ? PrefabUtility.IsPartOfPrefabAsset( root ) == false :
                root.scene.IsValid( ) == false || EditorSceneManager.IsPreviewScene( root.scene ) && editing == false )
                continue;
            string assetPath = asset ? AssetDatabase.GetAssetPath( root ) : string.Empty;
            foreach ( Collider collider in root.GetComponentsInChildren<Collider>( true ) )
            {
                GameObject owner = collider.gameObject;
                if ( owners.Add( owner ) == false )
                    continue;
                Collider[] colliders = owner.GetComponents<Collider>( );
                PlacementTarget target = owner.GetComponent<PlacementTarget>( );
                Collider surface = target != null ? target.SurfaceCollider : null;
                if ( surface == null && colliders.Length == 1 )
                    surface = colliders[0];
                string path = owner.name;
                for ( Transform parent = owner.transform.parent; parent != null; parent = parent.parent )
                    path = parent.name + "/" + path;
                string scope = asset ? assetPath : editing ? stage.assetPath + " (편집 중)" : owner.scene.name;
                entries.Add( new PlacementSurfaceEntry( owner, surface, scope + "/" + path, assetPath ) );
            }
        }
        entries.Sort( ( left, right ) => string.CompareOrdinal( left.Path, right.Path ) );
        return entries;
    }

    /// <summary>
    /// 열린 씬 조회 - 로드된 일반 씬의 모든 루트에서 배치 표면 후보 수집
    /// </summary>
    /// <returns>열린 씬의 중복 없는 배치 표면 후보</returns>
    public static List<PlacementSurfaceEntry> CollectOpenScenes ()
    {
        List<GameObject> roots = new( );
        for ( int i = 0; i < SceneManager.sceneCount; i++ )
        {
            Scene scene = SceneManager.GetSceneAt( i );
            if ( scene.isLoaded && EditorSceneManager.IsPreviewScene( scene ) == false )
                roots.AddRange( scene.GetRootGameObjects( ) );
        }
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage( );
        if ( stage != null )
            roots.Add( stage.prefabContentsRoot );
        return Collect( roots.ToArray( ) );
    }

    /// <summary>
    /// 적용 대상 조회 - 기존 배치 대상과 대표 표면 일치 여부 확인
    /// </summary>
    /// <param name="entry">검사할 후보</param>
    /// <param name="target">재사용할 기존 배치 대상, 신규 추가 시 없음</param>
    /// <param name="reason">검사 결과</param>
    /// <returns>안전하게 구성할 수 있는지 여부</returns>
    static bool TryGetTarget ( PlacementSurfaceEntry entry, out PlacementTarget target, out string reason )
    {
        target = null;
        reason = string.Empty;
        if ( entry.Owner == null )
            reason = "제외: 대상 객체가 삭제되었습니다.";
        else if ( entry.Surface == null )
            reason = "확인 필요: 대표 표면 충돌체를 선택하세요.";
        else if ( entry.Surface.gameObject != entry.Owner )
            reason = "제외: 후보 객체에 붙은 충돌체를 선택하세요.";
        else if ( entry.Surface.isTrigger )
            reason = "제외: 트리거는 배치 표면으로 사용하지 않습니다.";
        else if ( entry.Surface.enabled == false )
            reason = "제외: 비활성 충돌체는 활성화한 뒤 설정하세요.";
        if ( reason.Length > 0 )
            return false;

        if ( entry.AssetPath.Length > 0 )
        {
            if ( PrefabUtility.GetPrefabAssetType( entry.Owner ) == PrefabAssetType.Model )
            {
                reason = "제외: 모델 원본은 수정할 수 없습니다. 일반 프리팹을 사용하세요.";
                return false;
            }
        }

        PlacementTarget[] local = entry.Owner.GetComponents<PlacementTarget>( );
        if ( local.Length > 1 )
        {
            reason = "제외: 같은 객체에 배치 대상이 여러 개입니다.";
            return false;
        }
        if ( local.Length == 1 )
        {
            target = local[0];
            if ( target.SurfaceCollider != null && target.SurfaceCollider != entry.Surface )
            {
                reason = "제외: 기존 대표 표면과 다릅니다. 기존 참조를 먼저 확인하세요.";
                return false;
            }
        }
        else
        {
            //가장 가까운 부모의 대상이 실제로 이 표면을 사용하면 중복 추가하지 않음
            Transform parent = entry.Owner.transform.parent;
            PlacementTarget inherited = parent != null ? parent.GetComponentInParent<PlacementTarget>( true ) : null;
            if ( inherited != null && inherited.SurfaceCollider == entry.Surface )
                target = inherited;
        }
        if ( target != null && target.enabled == false )
        {
            reason = "제외: 비활성 배치 대상은 활성화한 뒤 설정하세요.";
            return false;
        }
        reason = target == null ? "추가: 배치 대상과 대표 표면 연결" :
            target.SurfaceCollider == null ? "연결: 기존 배치 대상의 대표 표면" : "유지: 기존 배치 대상과 대표 표면";
        return true;
    }

    /// <summary>
    /// 미리보기 - 씬을 수정하지 않고 선택 대상의 구성 가능 여부 표시
    /// </summary>
    /// <param name="entries">현재 후보 목록</param>
    /// <param name="options">적용할 설정</param>
    /// <returns>적용 가능한 선택 후보 수</returns>
    public static int Preview ( List<PlacementSurfaceEntry> entries, PlacementSurfaceOptions options )
    {
        int count = 0;
        Dictionary<string, string> assetErrors = new( );
        foreach ( PlacementSurfaceEntry entry in entries )
        {
            if ( entry.Selected == false )
                continue;
            if ( entry.AssetPath.Length > 0 )
            {
                if ( assetErrors.TryGetValue( entry.AssetPath, out string error ) == false )
                {
                    List<string> errors = new( );
                    AnomalyPrefabSetupUtility.CheckEditable( entry.AssetPath, errors );
                    error = string.Join( " / ", errors );
                    assetErrors.Add( entry.AssetPath, error );
                }
                if ( error.Length > 0 )
                {
                    entry.Result = error;
                    continue;
                }
            }
            if ( TryGetTarget( entry, out PlacementTarget target, out string reason ) )
            {
                count++;
                bool edit = target != null && ( options.EditPlacement || options.EditMask || options.EditFood || options.EditPreparation );
                if ( edit )
                    reason += " / 선택한 기존 설정 수정";
                if ( options.ChangeLayer )
                    reason += " / 레이어: " + LayerMask.LayerToName( options.Layer );
                if ( target == null || options.EditPlacement )
                    reason += options.Type == PlacementType.Free ? " / 배치: 자유" : " / 배치: 고정";
                if ( target == null || options.EditMask )
                    reason += " / 공간 검사 마스크: " + options.ItemMask.value;
                if ( target == null || options.EditFood )
                    reason += " / 음식 용도: " + options.Food;
                if ( target == null || options.EditPreparation )
                    reason += " / 준비 용도: " + options.Preparation;
            }
            entry.Result = reason;
        }
        return count;
    }

    /// <summary>
    /// 설정 기록 - 신규 대상 기본값 또는 선택한 기존 설정만 반영
    /// </summary>
    /// <param name="target">수정할 배치 대상</param>
    /// <param name="surface">연결할 대표 표면</param>
    /// <param name="options">신규 기본값과 수정 선택</param>
    /// <param name="isNew">이번에 추가한 대상 여부</param>
    /// <param name="useUndo">실행 취소 기록 여부, 원본 임시 편집은 백업 사용</param>
    static void Configure ( PlacementTarget target, Collider surface, PlacementSurfaceOptions options, bool isNew, bool useUndo )
    {
        using SerializedObject serialized = new( target );
        if ( target.SurfaceCollider == null )
            serialized.FindProperty( "_surfaceCollider" ).objectReferenceValue = surface;
        if ( isNew || options.EditPlacement )
        {
            serialized.FindProperty( "_placementType" ).intValue = (int)options.Type;
            serialized.FindProperty( "_fixedPlacementType" ).intValue = (int)options.FixedType;
            serialized.FindProperty( "_space" ).floatValue = options.Space;
            serialized.FindProperty( "_randomTryCount" ).intValue = options.Tries;
        }
        if ( isNew || options.EditMask )
            serialized.FindProperty( "_itemLayerMask" ).intValue = options.ItemMask.value;
        if ( isNew || options.EditFood )
            serialized.FindProperty( "_foodPlacementType" ).intValue = (int)options.Food;
        if ( isNew || options.EditPreparation )
            serialized.FindProperty( "_preparationLocation" ).intValue = (int)options.Preparation;
        if ( useUndo )
            serialized.ApplyModifiedProperties( );
        else
            serialized.ApplyModifiedPropertiesWithoutUndo( );
    }

    /// <summary>
    /// 변경 기록 - 씬 인스턴스의 프리팹 재정의와 저장 필요 상태 표시
    /// </summary>
    /// <param name="value">변경한 씬 객체 또는 컴포넌트</param>
    static void Record ( Object value )
    {
        if ( PrefabUtility.IsPartOfPrefabInstance( value ) )
            PrefabUtility.RecordPrefabInstancePropertyModifications( value );
        EditorUtility.SetDirty( value );
    }

    /// <summary>
    /// 표면 적용 - 대표 충돌체 연결과 선택한 설정 및 레이어 반영
    /// </summary>
    /// <param name="entry">설정할 표면 후보</param>
    /// <param name="options">신규 기본값과 수정 선택</param>
    /// <param name="useUndo">씬과 편집 화면의 실행 취소 기록 여부</param>
    /// <returns>설정 변경 여부</returns>
    static bool ApplyEntry ( PlacementSurfaceEntry entry, PlacementSurfaceOptions options, bool useUndo )
    {
        if ( TryGetTarget( entry, out PlacementTarget target, out string reason ) == false )
        {
            entry.Result = reason;
            return false;
        }
        bool isNew = target == null;
        bool configure = isNew || target.SurfaceCollider == null || options.EditPlacement ||
            options.EditMask || options.EditFood || options.EditPreparation;
        bool layer = options.ChangeLayer && entry.Owner.layer != options.Layer;
        if ( configure )
        {
            if ( isNew )
                target = useUndo ? Undo.AddComponent<PlacementTarget>( entry.Owner ) : entry.Owner.AddComponent<PlacementTarget>( );
            else if ( useUndo )
                Undo.RecordObject( target, "배치 표면 설정" );
            Configure( target, entry.Surface, options, isNew, useUndo );
            Record( target );
        }
        if ( layer )
        {
            if ( useUndo )
                Undo.RecordObject( entry.Owner, "배치 표면 레이어 설정" );
            entry.Owner.layer = options.Layer;
            Record( entry.Owner );
        }
        entry.Result = configure || layer ? "완료: " + reason : "유지: 변경할 설정이 없습니다.";
        return configure || layer;
    }

    /// <summary>
    /// 원본 객체 연결 - 원본 계층의 자식 순서로 임시 편집 객체 조회
    /// </summary>
    /// <param name="root">임시로 연 프리팹 루트</param>
    /// <param name="original">원본의 후보 객체</param>
    /// <returns>계층이 대응하는 임시 객체</returns>
    static GameObject ResolveOwner ( GameObject root, GameObject original )
    {
        Stack<int> indices = new( );
        for ( Transform current = original.transform; current.parent != null; current = current.parent )
            indices.Push( current.GetSiblingIndex( ) );
        Transform resolved = root.transform;
        while ( indices.Count > 0 )
            resolved = resolved.GetChild( indices.Pop( ) );
        return resolved.gameObject;
    }

    /// <summary>
    /// 원본 적용 - 후보를 임시 편집 객체에 연결하고 백업 후 같은 원본에 저장
    /// </summary>
    /// <param name="path">저장할 원본 경로</param>
    /// <param name="entries">원본에 속한 선택 후보</param>
    /// <param name="options">적용 설정</param>
    /// <param name="backup">적용 전 백업 묶음</param>
    /// <returns>저장한 원본에서 변경한 후보 수</returns>
    static int ApplyAsset ( string path, List<PlacementSurfaceEntry> entries, PlacementSurfaceOptions options, AnomalyPrefabBackupBatch backup )
    {
        List<string> errors = new( );
        if ( AnomalyPrefabSetupUtility.CheckEditable( path, errors ) == false )
        {
            foreach ( PlacementSurfaceEntry entry in entries )
                entry.Result = string.Join( " / ", errors );
            return 0;
        }
        GameObject root = PrefabUtility.LoadPrefabContents( path );
        int count = 0;
        try
        {
            foreach ( PlacementSurfaceEntry entry in entries )
            {
                if ( TryGetTarget( entry, out _, out string reason ) == false )
                {
                    entry.Result = reason;
                    continue;
                }
                GameObject owner = ResolveOwner( root, entry.Owner );
                int index = Array.IndexOf( entry.Owner.GetComponents<Collider>( ), entry.Surface );
                PlacementSurfaceEntry staged = new( owner, owner.GetComponents<Collider>( )[index], entry.Path );
                if ( ApplyEntry( staged, options, false ) )
                    count++;
                entry.Result = staged.Result;
            }
            if ( count == 0 )
                return 0;
            AnomalyPrefabBackupEntry record = AnomalyPrefabBackupUtility.Prepare( backup, path, errors );
            if ( record == null )
            {
                foreach ( PlacementSurfaceEntry entry in entries )
                    entry.Result = "원본 저장 안 함: " + string.Join( " / ", errors );
                return 0;
            }
            PrefabUtility.SaveAsPrefabAsset( root, path, out bool saved );
            AnomalyPrefabBackupUtility.FinishSave( backup, record );
            if ( saved == false )
                throw new InvalidOperationException( "배치 표면 원본 저장 실패: " + path );
            return count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents( root );
        }
    }

    /// <summary>
    /// 일괄 적용 - 씬과 편집 화면은 실행 취소 기록, 원본은 백업 후 저장
    /// </summary>
    /// <param name="entries">적용할 선택 후보</param>
    /// <param name="options">신규 기본값과 수정 선택</param>
    /// <param name="backup">이번 작업의 원본 백업 묶음</param>
    /// <returns>설정을 반영한 후보 수</returns>
    public static int Apply ( List<PlacementSurfaceEntry> entries, PlacementSurfaceOptions options, out AnomalyPrefabBackupBatch backup )
    {
        backup = AnomalyPrefabBackupUtility.CreateBatch( );
        Dictionary<string, List<PlacementSurfaceEntry>> assets = new( );
        Undo.IncrementCurrentGroup( );
        int group = Undo.GetCurrentGroup( );
        Undo.SetCurrentGroupName( "배치 표면 일괄 설정" );
        int count = 0;
        try
        {
            foreach ( PlacementSurfaceEntry entry in entries )
            {
                if ( entry.Selected == false )
                    continue;
                if ( entry.AssetPath.Length > 0 )
                {
                    if ( assets.TryGetValue( entry.AssetPath, out List<PlacementSurfaceEntry> candidates ) == false )
                    {
                        candidates = new List<PlacementSurfaceEntry>( );
                        assets.Add( entry.AssetPath, candidates );
                    }
                    candidates.Add( entry );
                    continue;
                }
                if ( ApplyEntry( entry, options, true ) )
                {
                    EditorSceneManager.MarkSceneDirty( entry.Owner.scene );
                    count++;
                }
            }
        }
        finally
        {
            Undo.CollapseUndoOperations( group );
        }
        foreach ( KeyValuePair<string, List<PlacementSurfaceEntry>> asset in assets )
            count += ApplyAsset( asset.Key, asset.Value, options, backup );
        return count;
    }
}
