using System.Collections.Generic;
using System.IO;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 프리팹 기능 구성 - 원본별 기능 부착과 저장, 임시 편집 객체 정리
/// </summary>
public static class AnomalyPrefabSetupUtility
{

    /// <summary>
    /// 원본 루트와 선택한 자식 범위의 레이어 구성
    /// </summary>
    /// <param name="root">구성할 원본 루트</param>
    /// <param name="layer">변경할 레이어 번호</param>
    /// <param name="includeChildren">비활성 자식까지 포함할지 여부</param>
    /// <param name="apply">실제 적용 여부</param>
    /// <param name="results">변경 범위를 기록할 목록</param>
    static void SetLayer (
        GameObject root, int layer, bool includeChildren, bool apply, List<string> results )
    {
        Transform[] targets = includeChildren == true
            ? root.GetComponentsInChildren<Transform>( true )
            : new[] { root.transform };
        int changed = 0;
        List<Transform> restoreAreas = new( );
        foreach ( PresenceDisplayController presence in root.GetComponentsInChildren<PresenceDisplayController>( true ) )
        {
            using SerializedObject serialized = new( presence );
            Collider restore = serialized.FindProperty( "_restoreCheckCollider" ).objectReferenceValue as Collider;
            if ( restore != null )
                restoreAreas.Add( restore.transform );
        }
        foreach ( Transform target in targets )
        {
            //빈자리 조사 영역의 별도 조준 레이어는 외형 레이어로 덮어쓰지 않음
            if ( restoreAreas.Exists( area => target == area || target.IsChildOf( area ) ) == true )
                continue;
            if ( target.gameObject.layer == layer )
                continue;
            changed++;
            if ( apply == true )
                target.gameObject.layer = layer;
        }
        results.Add( $"레이어 {LayerMask.LayerToName( layer )}: 변경 대상 {changed}개 오브젝트 / 연결된 조사 영역은 유지" );
    }

    /// <summary>
    /// 원본 루트의 기능 구성 미리보기 또는 적용과 저장
    /// </summary>
    /// <param name="path">구성할 원본 에셋 경로</param>
    /// <param name="kinds">부착할 기능 묶음</param>
    /// <param name="apply">실제 적용과 저장 여부</param>
    /// <param name="results">원본별 처리 결과</param>
    /// <param name="layer">변경할 레이어 번호, 변경하지 않으면 -1</param>
    /// <param name="includeChildren">레이어 변경에 비활성 자식까지 포함할지 여부</param>
    /// <param name="options">참조와 기능별 공통 설정</param>
    /// <param name="batch">저장 전 복구 묶음, 미리보기는 없음</param>
    /// <param name="references">원본별 직접 지정 정보</param>
    /// <param name="contents">일괄 이전에서 유지할 임시 원본</param>
    /// <param name="source">외부 연결 이전에 사용할 원본 객체 대응</param>
    /// <returns>구성 가능 또는 원본 저장 성공 여부</returns>
    public static bool Process (
        string path, AnomalySetupKind[] kinds, bool apply, List<string> results,
        int layer, bool includeChildren, AnomalyPrefabOptions options, AnomalyPrefabBackupBatch batch,
        AnomalyPrefabReferenceEntry references = null, GameObject contents = null, AnomalyPrefabMigrationSource source = null )
    {
        results.Add( "원본: " + path );
        if ( CheckEditable( path, results ) == false )
            return false;

        GameObject root = contents != null ? contents : PrefabUtility.LoadPrefabContents( path );
        int start = results.Count;
        try
        {
            if ( kinds.Length == 0 && root.GetComponentInChildren<AnomalyTarget>( true ) == null )
            {
                results.Add( "제외: 기존 설정만 변경할 원본에는 변칙 대상이 있어야 합니다." );
                return false;
            }
            //기존 부착과 참조 연결을 재사용하고 임시 객체의 실행 취소 기록은 남기지 않음
            foreach ( AnomalySetupKind kind in kinds )
            {
                //미리보기도 임시 객체에는 구성하여 새 기능의 참조와 설정까지 확인
                if ( AnomalyComponentSetupUtility.Process( root, kind, true, results, false, false ) == false )
                {
                    results.Add( "제외: 이 원본의 변경은 저장하지 않습니다." );
                    return false;
                }
            }
            AnomalyTarget[] targets = root.GetComponentsInChildren<AnomalyTarget>( true );
            foreach ( AnomalyTarget target in targets )
            {
                if ( AnomalyPrefabSettingsUtility.CheckTarget( target.gameObject, results ) == false )
                    return false;
                if ( AnomalyPrefabSettingsUtility.CheckTargetOptions( target.gameObject, options, results ) == false )
                    return false;
            }
            if ( AnomalyPrefabSelectedReferenceUtility.ApplySelected( path, root, references, results ) == false )
                return false;
            if ( AnomalyPrefabStructureUtility.Process( path, root, options, results, source ) == false )
                return false;
            if ( kinds.Length > 0 && options.ConnectReferences == false )
                AnomalyPrefabReferenceUtility.Process( root, false, results, references );
            foreach ( AnomalyTarget target in targets )
                AnomalyPrefabSettingsUtility.Process( target.gameObject, options, results,
                    target.gameObject == root ? references : null );
            if ( AnomalyPrefabSelectedReferenceUtility.CheckSelected( root, references, results ) == false )
                return false;
            foreach ( AnomalyTarget target in targets )
                AnomalyPrefabSelectedReferenceUtility.ReportPending( target.gameObject, results );
            if ( layer >= 0 )
                SetLayer( root, layer, includeChildren, true, results );
            if ( apply == false )
                return true;

            AnomalyPrefabBackupEntry entry = AnomalyPrefabBackupUtility.Prepare( batch, path, results );
            if ( entry == null )
                return false;
            try
            {
                PrefabUtility.SaveAsPrefabAsset( root, path, out bool success );
                results.Add( success == true ? "적용 및 원본 저장 완료" : "원본 저장 실패 — 복구 기록에서 실제 상태 확인" );
                return success;
            }
            finally
            {
                //저장 실패나 예외에서도 실제 디스크 상태를 복구 기록에 남김
                AnomalyPrefabBackupUtility.FinishSave( batch, entry );
            }
        }
        finally
        {
            //실패하거나 예외가 발생해도 임시 편집 객체는 항상 정리
            if ( contents == null )
                PrefabUtility.UnloadPrefabContents( root );
            if ( apply == false && contents == null )
            {
                for ( int i = start; i < results.Count; i++ )
                    results[i] = "미리보기: " + results[i];
            }
        }
    }

    /// <summary>
    /// 변칙 대상 원본을 참조를 유지하며 지정 폴더로 이동
    /// </summary>
    /// <param name="path">이동할 원본 경로</param>
    /// <param name="folder">기존 이동 대상 폴더 경로</param>
    /// <param name="results">이동 결과를 기록할 목록</param>
    /// <returns>원본 이동 성공 여부</returns>
    public static bool Move ( string path, string folder, List<string> results )
    {
        results.Add( "이동 원본: " + path );
        if ( CheckEditable( path, results ) == false )
            return false;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>( path );
        if ( asset.GetComponentInChildren<AnomalyTarget>( true ) == null )
        {
            results.Add( "제외: 변칙 대상이 없는 원본입니다." );
            return false;
        }
        string destination = folder + "/" + Path.GetFileName( path );
        if ( path == destination )
        {
            results.Add( "제외: 이미 해당 폴더에 있습니다." );
            return false;
        }
        //동일 이름 원본을 덮어쓰지 않고 Unity의 이동 검사 결과 사용
        string error = AssetDatabase.ValidateMoveAsset( path, destination );
        if ( string.IsNullOrEmpty( error ) == false )
        {
            results.Add( "이동 제외: " + error );
            return false;
        }
        error = AssetDatabase.MoveAsset( path, destination );
        results.Add( string.IsNullOrEmpty( error ) == true
            ? "이동 완료: " + destination : "이동 실패: " + error );
        return string.IsNullOrEmpty( error );
    }

    /// <summary>
    /// 원본의 편집 가능 여부와 미저장 상태 확인
    /// </summary>
    /// <param name="path">검사할 원본 경로</param>
    /// <param name="results">제외 사유를 기록할 목록</param>
    /// <returns>편집 가능 여부</returns>
    public static bool CheckEditable ( string path, List<string> results )
    {
        if ( path.EndsWith( ".unity", StringComparison.OrdinalIgnoreCase ) )
        {
            Scene scene = SceneManager.GetSceneByPath( path );
            if ( scene.IsValid( ) == true && scene.isDirty == true )
            {
                results.Add( "제외: 씬에 미저장 변경이 있습니다. 먼저 저장하세요. " + path );
                return false;
            }
            return true;
        }
        if ( path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) == false )
        {
            foreach ( Object value in AssetDatabase.LoadAllAssetsAtPath( path ) )
            {
                if ( EditorUtility.IsDirty( value ) == true )
                {
                    results.Add( "제외: 에셋에 미저장 변경이 있습니다. 먼저 저장하세요. " + path );
                    return false;
                }
            }
            return true;
        }
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>( path );
        if ( asset == null || PrefabUtility.GetPrefabAssetType( asset ) == PrefabAssetType.Model )
        {
            results.Add( "제외: 편집 가능한 일반 프리팹이 아닙니다." );
            return false;
        }
        bool dirty = EditorUtility.IsDirty( asset );
        foreach ( Component component in asset.GetComponentsInChildren<Component>( true ) )
        {
            if ( component != null )
                dirty |= EditorUtility.IsDirty( component ) || EditorUtility.IsDirty( component.gameObject );
        }
        if ( dirty == true )
        {
            results.Add( "제외: 원본에 미저장 설정이 있습니다. 먼저 저장한 뒤 다시 진행하세요." );
            return false;
        }
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage( );
        if ( stage != null && stage.assetPath == path )
        {
            results.Add( "제외: 현재 프리팹 편집 모드에서 열린 원본입니다. 편집 모드를 닫은 뒤 다시 진행하세요." );
            return false;
        }
        return true;
    }

}
