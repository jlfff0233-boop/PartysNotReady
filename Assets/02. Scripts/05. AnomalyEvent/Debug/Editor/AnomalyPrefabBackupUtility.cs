using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 원본 복구 관리 - 적용 전 패키지 보관과 순차 가져오기
/// </summary>
public static class AnomalyPrefabBackupUtility
{
    /// <summary>
    /// 순서대로 복구할 원본 기록
    /// </summary>
    static readonly Queue<AnomalyPrefabBackupEntry> _queue = new( );
    /// <summary>
    /// 최근 복구 결과
    /// </summary>
    static readonly List<string> _results = new( );
    static AnomalyPrefabBackupBatch _batch;		//현재 복구 묶음
    static AnomalyPrefabBackupEntry _current;		//가져오는 원본
    static bool _isRestoring;		//복구 수명 관리
    static bool _discardSceneChanges;		//실패한 이전 작업의 씬 변경 정리
    static readonly List<string> _reopenScenes = new( );		//복구 후 다시 열 씬
    static string _activeScenePath;		//복구 전 활성 씬 경로
    static Scene _temporaryScene;		//모든 씬을 닫는 동안 유지할 임시 씬

    /// <summary>
    /// 에셋으로 임포트하지 않는 영구 복구 폴더
    /// </summary>
    public static string Folder => Path.Combine(
        Directory.GetParent( Application.dataPath ).FullName, "UserSettings", "AnomalyPrefabBackups" );
    /// <summary>
    /// 복구 진행 여부
    /// </summary>
    public static bool IsRestoring => _isRestoring;
    /// <summary>
    /// 최근 복구 결과
    /// </summary>
    public static IReadOnlyList<string> Results => _results;

    /// <summary>
    /// 원본 내용을 수정하지 않고 비교값 계산
    /// </summary>
    /// <param name="path">읽을 파일 경로</param>
    /// <returns>내용 확인값</returns>
    public static string GetHash ( string path )
    {
        using SHA256 hash = SHA256.Create( );
        using FileStream stream = File.OpenRead( path );
        return Convert.ToBase64String( hash.ComputeHash( stream ) );
    }

    /// <summary>
    /// 적용 한 회차의 복구 묶음 생성
    /// </summary>
    /// <returns>아직 원본 기록이 없는 새 묶음</returns>
    public static AnomalyPrefabBackupBatch CreateBatch ()
    {
        return new AnomalyPrefabBackupBatch
        {
            Id = DateTime.Now.ToString( "yyyyMMdd_HHmmss_fff" ) + "_" + Guid.NewGuid( ).ToString( "N" ),
            Time = DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss" )
        };
    }

    /// <summary>
    /// 원본 복구 기록 저장, 에셋 파일은 직접 쓰지 않음
    /// </summary>
    /// <param name="batch">저장할 복구 묶음</param>
    static void SaveRecord ( AnomalyPrefabBackupBatch batch )
    {
        string directory = Path.Combine( Folder, batch.Id );
        Directory.CreateDirectory( directory );
        string record = Path.Combine( directory, "record.json" );
        string temporary = Path.Combine( directory, "record.tmp" );
        File.WriteAllText( temporary,
            JsonUtility.ToJson( batch, true ), new UTF8Encoding( true ) );
        //기록 작성 도중 중단되더라도 직전 복구 기록을 보존
        if ( File.Exists( record ) == true )
            File.Replace( temporary, record, null );
        else
            File.Move( temporary, record );
    }

    /// <summary>
    /// 저장 직전 원본만 의존 에셋 없이 내보내고 복구 기록 생성
    /// </summary>
    /// <param name="batch">이번 적용의 복구 묶음</param>
    /// <param name="path">변경할 원본 경로</param>
    /// <param name="results">보관 결과</param>
    /// <returns>보관한 기록, 내보내기 실패 시 없음</returns>
    public static AnomalyPrefabBackupEntry Prepare (
        AnomalyPrefabBackupBatch batch, string path, List<string> results )
    {
        string directory = Path.Combine( Folder, batch.Id );
        Directory.CreateDirectory( directory );
        string guid = AssetDatabase.AssetPathToGUID( path );
        string package = batch.Id + "_" + guid + ".unitypackage";
        string packagePath = Path.Combine( directory, package );
        string before = GetHash( path );
        //원본 프리팹만 보관하여 복구 시 데이터와 스크립트까지 덮어쓰지 않음
        AssetDatabase.ExportPackage( path, packagePath, ExportPackageOptions.Default );
        if ( File.Exists( packagePath ) == false || new FileInfo( packagePath ).Length == 0 )
        {
            results.Add( "제외: 복구 패키지를 보관하지 못했습니다. 원본은 저장하지 않습니다." );
            return null;
        }
        if ( GetHash( path ) != before )
        {
            results.Add( "제외: 보관 중 원본 내용이 변경됐습니다. 다시 미리보기하세요." );
            return null;
        }
        AnomalyPrefabBackupEntry entry = new( )
        {
            Guid = guid, Path = path, Package = package,
            BeforeHash = before, PackageHash = GetHash( packagePath )
        };
        batch.Entries.Add( entry );
        //저장 시도 전에 기록을 남겨 도중 중단 시에도 패키지 위치 보존
        SaveRecord( batch );
        results.Add( "복구 자료 보관 완료" );
        return entry;
    }

    /// <summary>
    /// 에디터 API로 백업 프리팹 폴더 생성
    /// </summary>
    /// <param name="folder">에셋 폴더 경로</param>
    static void EnsureAssetFolder ( string folder )
    {
        if ( AssetDatabase.IsValidFolder( folder ) == true )
            return;
        string parent = Path.GetDirectoryName( folder ).Replace( '\\', '/' );
        EnsureAssetFolder( parent );
        AssetDatabase.CreateFolder( parent, Path.GetFileName( folder ) );
    }

    /// <summary>
    /// 원본과 연관 에셋 전체를 하나의 복구 패키지에 보관
    /// </summary>
    /// <param name="batch">이번 작업 복구 묶음</param>
    /// <param name="paths">변경할 원본과 연관 에셋 경로</param>
    /// <param name="results">보관 결과</param>
    /// <returns>전체 복구 자료 보관 성공 여부</returns>
    public static bool PrepareGroup ( AnomalyPrefabBackupBatch batch, List<string> paths, List<string> results )
    {
        string directory = Path.Combine( Folder, batch.Id );
        Directory.CreateDirectory( directory );
        string package = batch.Id + "_group.unitypackage";
        string packagePath = Path.Combine( directory, package );
        batch.LinkedAssets = true;
        foreach ( string path in paths )
        {
            batch.Entries.Add( new AnomalyPrefabBackupEntry
            {
                Guid = AssetDatabase.AssetPathToGUID( path ), Path = path,
                Package = package, BeforeHash = GetHash( path )
            } );
        }
        AssetDatabase.ExportPackage( paths.ToArray( ), packagePath, ExportPackageOptions.Default );
        if ( File.Exists( packagePath ) == false || new FileInfo( packagePath ).Length == 0 )
        {
            results.Add( "제외: 작업 묶음의 복구 패키지를 보관하지 못했습니다." );
            return false;
        }
        string packageHash = GetHash( packagePath );
        string cloneFolder = "Assets/Editor/AnomalyBackups/" + batch.Id;
        foreach ( AnomalyPrefabBackupEntry entry in batch.Entries )
        {
            if ( GetHash( entry.Path ) != entry.BeforeHash )
            {
                results.Add( "제외: 복구 자료 보관 도중 에셋이 변경됐습니다. " + entry.Path );
                return false;
            }
            entry.PackageHash = packageHash;
            if ( entry.Path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) == false )
                continue;
            EnsureAssetFolder( cloneFolder );
            entry.ClonePath = AssetDatabase.GenerateUniqueAssetPath( cloneFolder + "/" + Path.GetFileName( entry.Path ) );
            if ( AssetDatabase.CopyAsset( entry.Path, entry.ClonePath ) == false )
                throw new InvalidOperationException( "원본 복제 실패: " + entry.Path );
            //변형의 백업은 이후 부모 변경을 상속하지 않는 독립 복제본으로 보관
            GameObject clone = PrefabUtility.LoadPrefabContents( entry.ClonePath );
            try
            {
                if ( PrefabUtility.GetPrefabAssetType( AssetDatabase.LoadAssetAtPath<GameObject>( entry.ClonePath ) ) == PrefabAssetType.Variant )
                {
                    PrefabUtility.UnpackPrefabInstance( clone, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction );
                    PrefabUtility.SaveAsPrefabAsset( clone, entry.ClonePath, out bool success );
                    if ( success == false )
                        throw new InvalidOperationException( "변형 원본의 복제본 저장 실패: " + entry.Path );
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents( clone );
            }
            results.Add( "수정 전 원본 복제: " + entry.ClonePath );
        }
        SaveRecord( batch );
        results.Add( $"작업 묶음 복구 자료 보관 완료: 에셋 {paths.Count}개" );
        return true;
    }

    /// <summary>
    /// 저장 성공과 실패 모두 실제 남은 원본 상태 기록
    /// </summary>
    /// <param name="batch">이번 적용의 복구 묶음</param>
    /// <param name="entry">저장을 시도한 원본</param>
    public static void FinishSave ( AnomalyPrefabBackupBatch batch, AnomalyPrefabBackupEntry entry )
    {
        if ( File.Exists( entry.Path ) == true )
            entry.AfterHash = GetHash( entry.Path );
        SaveRecord( batch );
    }

    /// <summary>
    /// 남아 있는 복구 기록을 최신 순서로 조회
    /// </summary>
    /// <returns>유니티를 다시 실행해도 유지되는 복구 묶음</returns>
    public static List<AnomalyPrefabBackupBatch> LoadBatches ()
    {
        List<AnomalyPrefabBackupBatch> batches = new( );
        if ( Directory.Exists( Folder ) == false )
            return batches;
        string[] records = Directory.GetFiles( Folder, "record.json", SearchOption.AllDirectories );
        Array.Sort( records, StringComparer.OrdinalIgnoreCase );
        Array.Reverse( records );
        foreach ( string record in records )
            batches.Add( JsonUtility.FromJson<AnomalyPrefabBackupBatch>( File.ReadAllText( record ) ) );
        return batches;
    }


    /// <summary>
    /// 가져오기 수명 종료와 이벤트 구독 해제
    /// </summary>
    static void EndRestore ()
    {
        AssetDatabase.importPackageCompleted -= HandleCompleted;
        AssetDatabase.importPackageFailed -= HandleFailed;
        AssetDatabase.importPackageCancelled -= HandleCancelled;
        AssemblyReloadEvents.beforeAssemblyReload -= EndRestore;
        EditorApplication.delayCall -= ImportNext;
        _queue.Clear( );
        _current = null;
        _isRestoring = false;
        _discardSceneChanges = false;
        foreach ( string path in _reopenScenes )
            EditorSceneManager.OpenScene( path, OpenSceneMode.Additive );
        _reopenScenes.Clear( );
        Scene active = SceneManager.GetSceneByPath( _activeScenePath ?? "" );
        if ( active.IsValid( ) == true && active.isLoaded == true )
            SceneManager.SetActiveScene( active );
        if ( _temporaryScene.IsValid( ) == true && _temporaryScene.isLoaded == true && SceneManager.sceneCount > 1 )
            EditorSceneManager.CloseScene( _temporaryScene, true );
    }

    /// <summary>
    /// 다음 원본을 재확인하고 하나씩 가져오기
    /// </summary>
    static void ImportNext ()
    {
        if ( _isRestoring == false )
            return;
        bool requested = false;
        try
        {
            while ( _queue.Count > 0 )
            {
                _current = _queue.Dequeue( );
                if ( CheckRestore( _batch, _current, _results ) == false )
                    continue;
                AssetDatabase.ImportPackage( Path.Combine( Folder, _batch.Id, _current.Package ), false );
                requested = true;
                return;
            }
            _results.Add( "선택한 원본 복구 처리 종료" );
        }
        finally
        {
            //검사나 가져오기 요청이 예외로 끝나면 진행 상태와 구독 해제
            if ( requested == false )
                EndRestore( );
        }
    }


    /// <summary>
    /// 가져온 원본 내용 확인과 다음 복구 예약
    /// </summary>
    /// <param name="package">가져온 패키지 이름</param>
    static void HandleCompleted ( string package )
    {
        if ( IsCurrentPackage( package ) == false )
            return;
        bool recorded = false;
        try
        {
            List<AnomalyPrefabBackupEntry> entries = _batch.LinkedAssets == true ? _batch.Entries : new( ) { _current };
            bool verified = entries.TrueForAll( entry => File.Exists( entry.Path ) == true && GetHash( entry.Path ) == entry.BeforeHash );
            if ( verified == false )
            {
                _results.Add( "복구 내용 확인 실패: " + _current.Path + " — 다음 복구를 중단합니다." );
                return;
            }
            foreach ( AnomalyPrefabBackupEntry entry in entries )
                entry.Restored = true;
            SaveRecord( _batch );
            recorded = true;
        }
        finally
        {
            //기록 실패가 복구 진행 상태를 영구적으로 남기지 않게 정리
            _current = null;
            if ( recorded == false || _queue.Count == 0 )
                EndRestore( );
        }
        _results.Add( "복구 완료: " + package );
        if ( _isRestoring == true )
            EditorApplication.delayCall += ImportNext;
    }

    /// <summary>
    /// 가져오기 실패를 표시하고 후속 복구 중단
    /// </summary>
    /// <param name="package">실패한 패키지 이름</param>
    /// <param name="error">유니티 실패 사유</param>
    static void HandleFailed ( string package, string error )
    {
        if ( IsCurrentPackage( package ) == false )
            return;
        _results.Add( "복구 실패: " + error );
        EndRestore( );
    }

    /// <summary>
    /// 가져오기 취소를 표시하고 후속 복구 중단
    /// </summary>
    /// <param name="package">취소한 패키지 이름</param>
    static void HandleCancelled ( string package )
    {
        if ( IsCurrentPackage( package ) == false )
            return;
        _results.Add( "복구 취소: " + package );
        EndRestore( );
    }

    /// <summary>
    /// 선택한 복구 기록의 순차 가져오기 시작
    /// </summary>
    /// <param name="batch">복구할 작업 묶음</param>
    /// <param name="entries">사용자가 선택한 원본 기록</param>
    /// <param name="discardSceneChanges">실패한 작업에서 만든 씬 변경을 폐기할지 여부</param>
    public static void Restore ( AnomalyPrefabBackupBatch batch, List<AnomalyPrefabBackupEntry> entries, bool discardSceneChanges = false )
    {
        if ( _isRestoring == true || EditorApplication.isPlayingOrWillChangePlaymode == true )
            return;
        _batch = batch;
        _results.Clear( );
        _discardSceneChanges = discardSceneChanges;
        if ( batch.LinkedAssets == true )
        {
            AnomalyPrefabBackupEntry changed = null;
            //묶음 전체를 검사한 뒤 한 패키지로 복구하여 부분 복구 방지
            foreach ( AnomalyPrefabBackupEntry entry in batch.Entries )
            {
                if ( File.Exists( entry.Path ) == true && GetHash( entry.Path ) == entry.BeforeHash )
                    continue;
                if ( CheckRestore( batch, entry, _results ) == false )
                {
                    _discardSceneChanges = false;
                    return;
                }
                changed = entry;
            }
            if ( changed == null )
            {
                _results.Add( "작업 묶음이 이미 적용 전 상태입니다." );
                _discardSceneChanges = false;
                return;
            }
            _queue.Enqueue( changed );
            _activeScenePath = SceneManager.GetActiveScene( ).path;
            foreach ( AnomalyPrefabBackupEntry entry in batch.Entries )
            {
                Scene scene = SceneManager.GetSceneByPath( entry.Path );
                if ( scene.IsValid( ) == false || scene.isLoaded == false )
                    continue;
                if ( scene.isDirty == true && discardSceneChanges == false )
                {
                    _results.Add( "복구 중단: 씬에 미저장 변경이 있습니다. " + entry.Path );
                    _queue.Clear( );
                    EndRestore( );
                    return;
                }
                if ( SceneManager.sceneCount == 1 )
                    _temporaryScene = EditorSceneManager.NewScene( NewSceneSetup.EmptyScene, NewSceneMode.Additive );
                _reopenScenes.Add( entry.Path );
                EditorSceneManager.CloseScene( scene, true );
            }
        }
        else
        {
            foreach ( AnomalyPrefabBackupEntry entry in entries )
                _queue.Enqueue( entry );
        }
        _isRestoring = true;
        AssetDatabase.importPackageCompleted += HandleCompleted;
        AssetDatabase.importPackageFailed += HandleFailed;
        AssetDatabase.importPackageCancelled += HandleCancelled;
        AssemblyReloadEvents.beforeAssemblyReload += EndRestore;
        EditorApplication.delayCall += ImportNext;
    }

    /// <summary>
    /// 원본 이동과 적용 후 추가 수정, 복구 자료 손상 확인
    /// </summary>
    /// <param name="batch">확인할 복구 묶음</param>
    /// <param name="entry">확인할 원본 기록</param>
    /// <param name="results">가능 여부와 제외 사유</param>
    /// <returns>현재 원본을 자동 복구해도 되는지 여부</returns>
    public static bool CheckRestore (
        AnomalyPrefabBackupBatch batch, AnomalyPrefabBackupEntry entry, List<string> results )
    {
        results.Add( "복구 원본: " + entry.Path );
        if ( entry.Restored == true )
        {
            results.Add( "제외: 이미 복구했습니다." );
            return false;
        }
        if ( AssetDatabase.GUIDToAssetPath( entry.Guid ) != entry.Path )
        {
            results.Add( "제외: 원본이 이동되거나 삭제됐습니다. 다른 경로에 가져오지 않습니다." );
            return false;
        }
        if ( ( _discardSceneChanges == false || entry.Path.EndsWith( ".unity", StringComparison.OrdinalIgnoreCase ) == false ) &&
            AnomalyPrefabSetupUtility.CheckEditable( entry.Path, results ) == false )
            return false;
        string current = GetHash( entry.Path );
        if ( current == entry.BeforeHash )
        {
            results.Add( "제외: 적용 전과 같은 상태입니다." );
            return false;
        }
        if ( string.IsNullOrEmpty( entry.AfterHash ) == true )
        {
            results.Add( "제외: 저장 종료 기록이 없습니다. 도중 중단된 원본은 자동 복구하지 않습니다." );
            return false;
        }
        if ( current != entry.AfterHash )
        {
            results.Add( "제외: 적용 이후 추가 수정됐습니다. 해당 수정을 덮어쓰지 않습니다." );
            return false;
        }
        string package = Path.Combine( Folder, batch.Id, entry.Package );
        if ( File.Exists( package ) == false || GetHash( package ) != entry.PackageHash )
        {
            results.Add( "제외: 복구 패키지가 없거나 내용이 변경됐습니다." );
            return false;
        }
        results.Add( "복구 가능" );
        return true;
    }

    /// <summary>
    /// 현재 가져오기 요청의 알림인지 확인
    /// </summary>
    /// <param name="package">유니티가 알린 패키지 이름</param>
    /// <returns>현재 복구 알림 여부</returns>
    static bool IsCurrentPackage ( string package )
    {
        return _current != null &&
            Path.GetFileNameWithoutExtension( package ) == Path.GetFileNameWithoutExtension( _current.Package );
    }

}
