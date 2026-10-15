using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이 설정 검사와 씬 대상 조회
/// </summary>
public static class PlaySetupValidator
{
    /// <summary>
    /// 지정 씬의 실제 컴포넌트 수집
    /// </summary>
    /// <typeparam name="T">조회 형식</typeparam>
    /// <param name="scene">조회할 씬</param>
    /// <param name="results">결과 목록</param>
    public static void Collect<T> ( Scene scene, List<T> results ) where T : Component
    {
        results.Clear( );
        foreach ( T value in Object.FindObjectsByType<T>( FindObjectsInactive.Include, FindObjectsSortMode.None ) )
        {
            if ( value.gameObject.scene == scene && EditorSceneManager.IsPreviewScene( scene ) == false )
                results.Add( value );
        }
    }

    /// <summary>
    /// 발생 설정과 씬 연결 검사
    /// </summary>
    /// <param name="pressure">발생 관리자</param>
    /// <param name="enemies">씬의 적</param>
    /// <param name="events">씬의 사건 중재자</param>
    /// <param name="results">검사 결과</param>
    /// <param name="excludeAi">AI 테스트에서 발생과 적 연결 검사 제외 여부</param>
    public static void Check (
        AnomalySpawnSystem pressure, List<Enemy> enemies,
        List<AnomalyEventSystem> events, List<AnomalyDebugDetail> results,
        bool excludeAi = false )
    {
        results.Clear( );
        if ( excludeAi == true )
        {
            results.Add( new( "발생 / 적 연결", "AI 테스트 설정으로 검사 제외" ) );
            foreach ( AnomalyEventSystem presenter in events )
            {
                if ( presenter == null )
                    continue;

                results.Add( new( presenter.name,
                    presenter.CheckDebugSettings( out string eventReason ) == true
                        ? "사건 시간 설정 정상 / 실제 대상 자격은 실행 시 검사" : eventReason ) );
            }
            return;
        }

        if ( pressure == null )
        {
            results.Add( new( "발생 설정", "Pressure 미연결" ) );
            return;
        }
        AnomalySpawnDataMap map = pressure.DebugSpawnMap;		//검사할 설정
        if ( map == null )
        {
            results.Add( new( "발생 설정", "난이도 발생 설정은 플레이 초기화 후 확인할 수 있습니다." ) );
            return;
        }
        results.Add( new( map.name, map.CheckSettings( out string reason ) ? "설정 검사 통과" : reason ) );
        foreach ( Enemy enemy in enemies )
            results.Add( new( enemy.name, $"{enemy.SpawnType} / 시작 방: {EnemyDebugUtility.Name( enemy.StartRoom )} / 활성: {enemy.isActiveAndEnabled}" ) );
        if ( map.Data == null )
            return;
        foreach ( AnomalySpawnData data in map.Data )
        {
            if ( data == null )
                continue;
            int enemyCount = enemies.FindAll( enemy => data.Contains( enemy.SpawnType ) ).Count;
            int eventCount = events.FindAll( presenter => data.Contains( presenter.EventType ) ).Count;
            string mode = data.EnemyType == EnemySpawnType.None ? "독립 사건: 적에게 결과 전달 없음" : $"적 {data.EnemyType}";
            results.Add( new( data.name, $"{mode} / 연결 적 {enemyCount} / 사건 중재자 {eventCount}" ) );
            if ( data.EnemyType != EnemySpawnType.None && enemyCount == 0 )
                results.Add( new( data.name, "해당 적이 씬에 없습니다." ) );
            if ( ( data.HasEventOwner || data.EnemyType == EnemySpawnType.None ) && eventCount == 0 )
                results.Add( new( data.name, "설정된 종류의 사건 중재자가 없습니다." ) );
        }
        foreach ( AnomalyEventSystem presenter in events )
            results.Add( new( presenter.name, presenter.CheckDebugSettings( out reason ) ? "사건 시간 설정 정상 / 실제 대상 자격은 실행 시 검사" : reason ) );
    }
}
