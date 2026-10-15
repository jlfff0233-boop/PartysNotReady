using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 프레임 예산 안에서 수집과 시스템 검사를 진행
/// </summary>
public class PlayReadinessChecker
{
    IEnumerator _work;        //검사 순서
    public PlayReadinessContext Context;        //현재 검사 결과
    public bool IsRunning;        //검사 진행 여부
    public string Progress = string.Empty;        //진행 또는 실패 설명

    /// <summary>
    /// 검사 작업과 연결된 결과 생성
    /// </summary>
    /// <param name="target">선택 범위 또는 플레이 씬</param>
    /// <param name="selectionOnly">선택 범위만 검사할지 여부</param>
    /// <param name="folder">추가 자산 폴더</param>
    /// <param name="radius">NavMesh 표본 반경</param>
    /// <param name="agentType">NavMesh 에이전트 종류</param>
    public void Begin ( Object target, bool selectionOnly, string folder, float radius, int agentType )
    {
        Cancel( );
        Context = new PlayReadinessContext { SampleRadius = radius, AgentType = agentType };
        Progress = "검사 대상과 연결 자산 수집 중";
        _work = Scan( target, selectionOnly, folder );
        IsRunning = true;
    }

    /// <summary>
    /// 진행 중 열거자 해제
    /// </summary>
    public void Cancel ()
    {
        ( _work as IDisposable )?.Dispose( );
        _work = null;
        IsRunning = false;
    }

    /// <summary>
    /// 편집기 프레임당 약 8밀리초까지 검사
    /// </summary>
    public void CheckNext ()
    {
        if ( IsRunning == false )
            return;
        double limit = EditorApplication.timeSinceStartup + 0.008;
        try
        {
            while ( EditorApplication.timeSinceStartup < limit )
            {
                if ( _work.MoveNext( ) == false )
                {
                    Progress = "정적 점검 완료 · 실제 플레이 확인은 별도입니다.";
                    Cancel( );
                    break;
                }
            }
        }
        catch ( Exception exception )
        {
            //일부 결과를 전체 성공으로 표시하지 않고 검사 실패를 남깁니다.
            Context.Report( null, "씬", "scan-failed", "검사 실행", PlayReadinessStatus.Blocked,
                exception.ToString( ) );
            Progress = "점검 중단: 상세 오류를 확인해 주세요.";
            Cancel( );
        }
    }

    /// <summary>
    /// 대상 수집과 시스템별 검사 순서
    /// </summary>
    /// <param name="target">지정 대상</param>
    /// <param name="selectionOnly">선택 범위 한정 여부</param>
    /// <param name="folder">추가 폴더</param>
    /// <returns>분할 검사 순서</returns>
    IEnumerator Scan ( Object target, bool selectionOnly, string folder )
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage( );
        if ( selectionOnly == true )
        {
            Context.Add( target, false );
            if ( target is Component selected )
                Context.Add( selected.gameObject, false );
        }
        else if ( stage != null )
            Context.Add( stage.prefabContentsRoot, false );
        else
        {
            Scene scene = target is Component component && component.gameObject.scene.IsValid( ) == true
                ? component.gameObject.scene : target is GameObject gameObject && gameObject.scene.IsValid( ) == true
                    ? gameObject.scene : SceneManager.GetActiveScene( );
            Context.HasScene = scene.IsValid( ) == true && scene.isLoaded == true;
            Context.Scene = scene;
            if ( Context.HasScene == true )
            {
                foreach ( GameObject root in scene.GetRootGameObjects( ) )
                {
                    Context.Add( root, true );
                    yield return null;
                }
            }
        }
        //씬의 연결 자산을 먼저 수집하여 추가 폴더와 구분합니다.
        for ( int i = 0; i < Context.Objects.Count; i++ )
        {
            Context.CollectReferences( Context.Objects [ i ] );
            yield return null;
        }
        if ( string.IsNullOrEmpty( folder ) == false )
        {
            foreach ( string guid in AssetDatabase.FindAssets( "t:ScriptableObject t:Prefab", new[] { folder } ) )
            {
                string path = AssetDatabase.GUIDToAssetPath( guid );
                Context.Add( AssetDatabase.LoadAssetAtPath<Object>( path ), false );
                yield return null;
            }
        }
        for ( int i = 0; i < Context.Objects.Count; i++ )
        {
            Object owner = Context.Objects [ i ];
            if ( owner == null )
                continue;
            Progress = $"{i + 1} / {Context.Objects.Count} · {owner.name}";
            Context.CollectReferences( owner );
            PlaySceneSetupChecker.Check( Context, owner );
            PlayDataSetupChecker.Check( Context, owner );
            PlayContentSetupChecker.Check( Context, owner );
            PlayPresentationSetupChecker.Check( Context, owner );
            PlayMapSetupChecker.Check( Context, owner );
            yield return null;
        }
        PlayDataSetupChecker.CheckRelations( Context );
        PlayContentSetupChecker.CheckRelations( Context );
        PlayPresentationSetupChecker.CheckRelations( Context );
        if ( Context.HasScene == true && Context.Objects.Exists( value => value is PlayScene scene && Context.CanCheckScene( scene ) == true ) == false )
            Context.Report( target, "씬", "no-play-scene", "PlayScene", PlayReadinessStatus.Blocked,
                "현재 검사 씬에는 PlayScene이 없습니다. 플레이 씬을 열거나 기준 PlayScene을 지정해 주세요." );
        Context.Report( target, "통합", "play-confirmation", "실제 플레이", PlayReadinessStatus.Pending,
            "제작→미션 반영→배치→준비 평가→결과 보상, AI 이동, UI 표시와 음향은 플레이 디버그에서 직접 확인해 주세요. 정적 통과로 실행 확인을 대체하지 않습니다." );
    }
}
