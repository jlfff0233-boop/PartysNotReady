using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 원본 일괄 이전 - 외형 구성과 외부 설정 이전, 작업 묶음 저장 및 복구
/// </summary>
public static class AnomalyPrefabMigrationUtility
{
    /// <summary>
    /// 원본 변경에 영향을 받는 편집 에셋 경로 수집
    /// </summary>
    /// <param name="sources">구성할 원본 대응</param>
    /// <returns>수정 원본과 의존 에셋 경로</returns>
    static List<string> FindOwners ( List<AnomalyPrefabMigrationSource> sources )
    {
        HashSet<string> selected = new( sources.ConvertAll( source => source.Path ) );
        List<string> owners = new( );
        foreach ( string path in AssetDatabase.GetAllAssetPaths( ) )
        {
            if ( path.StartsWith( "Assets/", StringComparison.Ordinal ) == false ||
                path.StartsWith( "Assets/Editor/AnomalyBackups/", StringComparison.Ordinal ) ||
                selected.Contains( path ) || AssetDatabase.IsValidFolder( path ) )
                continue;
            string[] dependencies = AssetDatabase.GetDependencies( path, true );
            if ( Array.Exists( dependencies, dependency => selected.Contains( dependency ) ) )
                owners.Add( path );
        }
        owners.Sort( StringComparer.OrdinalIgnoreCase );
        return owners;
    }

    /// <summary>
    /// 의존 에셋을 현재 에셋보다 먼저 저장 순서에 추가
    /// </summary>
    /// <param name="asset">추가할 에셋</param>
    /// <param name="assets">이번 작업 에셋</param>
    /// <param name="visited">이미 확인한 경로</param>
    /// <param name="ordered">저장 순서</param>
    static void Visit ( AnomalyPrefabMigrationAsset asset, List<AnomalyPrefabMigrationAsset> assets,
        HashSet<string> visited, List<AnomalyPrefabMigrationAsset> ordered )
    {
        if ( visited.Add( asset.Path ) == false )
            return;
        foreach ( string dependency in AssetDatabase.GetDependencies( asset.Path, false ) )
        {
            AnomalyPrefabMigrationAsset owner = assets.Find( value => value.Path == dependency );
            if ( owner != null && owner != asset )
                Visit( owner, assets, visited, ordered );
        }
        ordered.Add( asset );
    }

    /// <summary>
    /// 부모와 중첩 원본을 먼저 저장할 편집 에셋 순서 구성
    /// </summary>
    /// <param name="assets">정렬할 에셋</param>
    /// <returns>의존 원본부터 시작하는 저장 순서</returns>
    static List<AnomalyPrefabMigrationAsset> GetSaveOrder ( List<AnomalyPrefabMigrationAsset> assets )
    {
        List<AnomalyPrefabMigrationAsset> ordered = new( );
        HashSet<string> visited = new( );
        foreach ( AnomalyPrefabMigrationAsset asset in assets )
            Visit( asset, assets, visited, ordered );
        return ordered;
    }

    /// <summary>
    /// 현재 설정을 유지하며 변형 프리팹의 부모 연결 해제
    /// </summary>
    /// <param name="asset">구성할 편집 에셋</param>
    /// <param name="sources">이번 작업 원본 대응</param>
    /// <param name="results">전환 결과</param>
    static void DetachVariant ( AnomalyPrefabMigrationAsset asset, List<AnomalyPrefabMigrationSource> sources, List<string> results )
    {
        if ( asset.Root == null || PrefabUtility.GetPrefabAssetType( AssetDatabase.LoadAssetAtPath<GameObject>( asset.Path ) ) != PrefabAssetType.Variant )
            return;
        if ( asset.Source == null )
        {
            asset.Source = new AnomalyPrefabMigrationSource( asset.Path, asset.Root );
            sources.Add( asset.Source );
        }
        PrefabUtility.UnpackPrefabInstance( asset.Root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction );
        results.Add( "독립 일반 프리팹으로 전환: " + asset.Path );
    }

    /// <summary>
    /// 저장 전 원본과 외부 에셋을 함께 구성하고 이전할 설정 보관
    /// </summary>
    /// <param name="paths">선택한 원본</param>
    /// <param name="kinds">부착할 기능</param>
    /// <param name="layer">변경할 레이어</param>
    /// <param name="includeChildren">자식 레이어 변경 여부</param>
    /// <param name="options">공통 설정</param>
    /// <param name="references">원본별 명시 참조</param>
    /// <param name="assets">수명이 유지되는 편집 에셋</param>
    /// <param name="available">구성이 가능한 원본</param>
    /// <param name="excluded">원본별 제외 사유</param>
    /// <param name="results">처리 결과</param>
    static void Prepare ( List<string> paths, AnomalySetupKind[] kinds, int layer, bool includeChildren,
        AnomalyPrefabOptions options, AnomalyPrefabReferencePanel references, List<AnomalyPrefabMigrationAsset> assets,
        List<string> available, Dictionary<string, string> excluded, List<string> results )
    {
        List<AnomalyPrefabMigrationSource> sources = new( );
        foreach ( string path in paths )
        {
            int start = results.Count;
            if ( AnomalyPrefabSetupUtility.CheckEditable( path, results ) == false )
            {
                excluded[path] = string.Join( "\n", results.GetRange( start, results.Count - start ) );
                continue;
            }
            AnomalyPrefabMigrationAsset asset = new( path, sources );
            assets.Add( asset );
            asset.Source = new AnomalyPrefabMigrationSource( path, asset.Root );
            sources.Add( asset.Source );
            DetachVariant( asset, sources, results );
            if ( AnomalyPrefabSetupUtility.Process( path, kinds, false, results, layer, includeChildren, options, null,
                references.Get( path ), asset.Root, asset.Source ) == false )
            {
                excluded[path] = string.Join( "\n", results.GetRange( start, results.Count - start ).FindAll( value => value.Contains( "제외:" ) ) );
                sources.Remove( asset.Source );
                assets.Remove( asset );
                asset.Dispose( );
                continue;
            }
            available.Add( path );
        }
        if ( available.Count == 0 )
            return;
        foreach ( string path in FindOwners( sources ) )
        {
            if ( AnomalyPrefabSetupUtility.CheckEditable( path, results ) == false )
                throw new InvalidOperationException( "연관 에셋을 먼저 저장하거나 편집 모드를 닫으세요: " + path );
            AnomalyPrefabMigrationAsset asset = new( path, sources );
            assets.Add( asset );
            if ( asset.Root != null )
            {
                asset.Source = new AnomalyPrefabMigrationSource( path, asset.Root );
                sources.Add( asset.Source );
                //맵 변형의 상속은 유지하고, 단일 변칙 물건의 변형만 독립 구성
                if ( asset.Root.GetComponent<AnomalyTarget>( ) != null &&
                    asset.Root.GetComponentsInChildren<AnomalyTarget>( true ).Length == 1 &&
                    PrefabUtility.GetPrefabAssetType( AssetDatabase.LoadAssetAtPath<GameObject>( path ) ) == PrefabAssetType.Variant )
                {
                    DetachVariant( asset, sources, results );
                    if ( AnomalyPrefabSetupUtility.Process( path, kinds, false, results, layer, includeChildren, options, null,
                        null, asset.Root, asset.Source ) == false )
                        throw new InvalidOperationException( "연관 변형 원본을 구성하지 못했습니다: " + path );
                }
            }
        }
        foreach ( AnomalyPrefabMigrationSource source in sources )
            source.Seal( sources );
        foreach ( AnomalyPrefabMigrationAsset asset in assets )
            asset.Capture( results );
    }

    /// <summary>
    /// 변경 예정 에셋의 미리보기 또는 백업과 일괄 저장
    /// </summary>
    /// <param name="paths">선택한 원본</param>
    /// <param name="kinds">부착할 기능</param>
    /// <param name="layer">변경할 레이어</param>
    /// <param name="includeChildren">자식 레이어 변경 여부</param>
    /// <param name="options">공통 설정</param>
    /// <param name="references">원본별 명시 참조</param>
    /// <param name="apply">실제 저장 여부</param>
    /// <param name="hashes">미리보기한 원본과 연관 에셋의 내용 확인값</param>
    /// <param name="available">구성이 가능한 원본</param>
    /// <param name="excluded">원본별 제외 사유</param>
    /// <param name="results">처리 결과</param>
    /// <returns>전체 이전 성공 또는 미리보기 완료 여부</returns>
    public static bool Process ( List<string> paths, AnomalySetupKind[] kinds, int layer, bool includeChildren,
        AnomalyPrefabOptions options, AnomalyPrefabReferencePanel references, bool apply, Dictionary<string, string> hashes,
        List<string> available, Dictionary<string, string> excluded, List<string> results )
    {
        List<AnomalyPrefabMigrationAsset> assets = new( );
        AnomalyPrefabBackupBatch batch = null;
        Scene active = SceneManager.GetActiveScene( );
        bool saved = false;
        bool success = false;
        try
        {
            if ( apply == true )
            {
                foreach ( KeyValuePair<string, string> pair in hashes )
                {
                    if ( File.Exists( pair.Key ) == false || AnomalyPrefabBackupUtility.GetHash( pair.Key ) != pair.Value )
                        throw new InvalidOperationException( "미리보기 이후 변경된 에셋입니다. 다시 미리보기하세요: " + pair.Key );
                }
            }
            Prepare( paths, kinds, layer, includeChildren, options, references, assets, available, excluded, results );
            if ( apply == false )
            {
                hashes.Clear( );
                foreach ( AnomalyPrefabMigrationAsset asset in assets )
                    hashes.Add( asset.Path, AnomalyPrefabBackupUtility.GetHash( asset.Path ) );
                success = true;
                return true;
            }
            foreach ( AnomalyPrefabMigrationAsset asset in assets )
            {
                if ( hashes.ContainsKey( asset.Path ) == false )
                    throw new InvalidOperationException( "새로운 연관 에셋이 발견됐습니다. 다시 미리보기하세요: " + asset.Path );
            }
            if ( available.Count == 0 || excluded.Count > 0 )
                throw new InvalidOperationException( "원본 구성이 미리보기와 달라졌습니다. 다시 미리보기하세요." );
            batch = AnomalyPrefabBackupUtility.CreateBatch( );
            if ( AnomalyPrefabBackupUtility.PrepareGroup( batch, assets.ConvertAll( asset => asset.Path ), results ) == false )
                return false;
            foreach ( AnomalyPrefabMigrationAsset asset in GetSaveOrder( assets ) )
            {
                //이전 원본들이 먼저 저장된 뒤 현재 에셋의 연결을 복원
                if ( asset.Source != null )
                {
                    saved = true;
                    asset.Save( );
                    asset.Source.CheckSaved( );
                }
                asset.Transfer( results );
                saved = true;
                asset.Save( );
                AnomalyPrefabBackupUtility.FinishSave( batch, batch.Entries.Find( entry => entry.Path == asset.Path ) );
                results.Add( "원본 및 연관 에셋 저장 완료: " + asset.Path );
            }
            success = true;
            return true;
        }
        catch ( Exception exception )
        {
            //여러 에셋의 저장 경계에서만 실패를 수집하고 작업 묶음 복구
            results.Add( "일괄 이전 실패: " + exception.Message );
            Debug.LogException( exception );
            if ( apply == false )
            {
                foreach ( string path in available )
                    excluded[path] = exception.Message;
                available.Clear( );
                hashes.Clear( );
            }
            return false;
        }
        finally
        {
            foreach ( AnomalyPrefabMigrationAsset asset in assets )
            {
                if ( saved == true && success == false )
                    asset.DiscardAssetChanges( );
                asset.Dispose( );
            }
            if ( active.IsValid( ) == true && active.isLoaded == true )
                SceneManager.SetActiveScene( active );
            if ( saved == true && batch != null && success == false )
            {
                foreach ( AnomalyPrefabBackupEntry entry in batch.Entries )
                    AnomalyPrefabBackupUtility.FinishSave( batch, entry );
                AnomalyPrefabBackupUtility.Restore( batch, batch.Entries, true );
                results.Add( "원본과 연관 에셋의 적용 전 상태 복구를 요청했습니다. 복구 결과를 확인하세요." );
            }
        }
    }
}
