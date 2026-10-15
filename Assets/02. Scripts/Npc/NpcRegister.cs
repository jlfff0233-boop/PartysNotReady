using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// NPC 등록 - 시작 NPC와 포획 후보 관리
/// </summary>
public static class NpcRegister
{
    /// <summary>
    /// 등록 NPC 집합
    /// </summary>
    static HashSet<Npc> _targets = new( );

    /// <summary>
    /// 시작 NPC 집합
    /// </summary>
    static HashSet<Npc> _initialNpcs = new( );

    static Scene _initialScene;		//시작 NPC를 확정한 씬

    /// <summary>
    /// 시작 NPC 수
    /// </summary>
    public static int InitialCount => _initialNpcs.Count;

    /// <summary>
    /// 시작 NPC의 잔존 수
    /// </summary>
    public static int InitialRemainingCount => CountRemaining( _initialNpcs );

    /// <summary>
    /// 게임에 남은 NPC 수
    /// </summary>
    public static int RemainingCount => CountRemaining( _targets );

    /// <summary>
    /// NPC 등록 이벤트(NPC)
    /// </summary>
    public static event Action<Npc> OnRegistered;

    /// <summary>
    /// NPC 등록 해제 이벤트(NPC)
    /// </summary>
    public static event Action<Npc> OnUnregistered;


    /// <summary>
    /// 플레이 시작 시 이전 등록 정보 초기화
    /// </summary>
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration )]
    static void Reset ()
    {
        _targets.Clear( );
        _initialNpcs.Clear( );
        _initialScene = default;
        OnRegistered = null;
        OnUnregistered = null;
    }

    /// <summary>
    /// 시작 NPC 확정
    /// </summary>
    /// <param name="scene">플레이 씬</param>
    /// <returns>확정 여부</returns>
    public static bool TrySetInitialNpcs ( Scene scene )
    {
        if ( scene.IsValid( ) == false || scene.isLoaded == false ||
             ( _initialScene.IsValid( ) && _initialScene.isLoaded ) )
            return false;

        _initialNpcs.Clear( );

        //Awake 전인 비활성 손님도 같은 씬의 시작 NPC에 포함
        Npc [ ] found = Object.FindObjectsByType<Npc>(
            FindObjectsInactive.Include, FindObjectsSortMode.None );

        foreach ( Npc npc in found )
        {
            if ( npc.gameObject.scene == scene && npc.IsRemaining == true )
                _initialNpcs.Add( npc );
        }

        if ( _initialNpcs.Count == 0 )
            return false;

        //이후 등록과 비활성화는 시작 집합을 바꾸지 않음
        _initialScene = scene;
        return true;
    }


    /// <summary>
    /// 시작 NPC 조회
    /// </summary>
    /// <param name="results">조회 결과 목록</param>
    public static void GetInitial ( List<Npc> results )
    {
        results.Clear( );
        foreach ( Npc npc in _initialNpcs )
        {
            if ( npc != null )
                results.Add( npc );
        }
    }

    /// <summary>
    /// NPC 등록
    /// </summary>
    /// <param name="target">등록할 NPC</param>
    public static void Register ( Npc target )
    {
        if ( target != null && _targets.Add( target ) == true )
            OnRegistered?.Invoke( target );
    }

    /// <summary>
    /// NPC 등록 해제
    /// </summary>
    /// <param name="target">해제할 NPC</param>
    public static void Unregister ( Npc target )
    {
        if ( _targets.Remove( target ) == true )
            OnUnregistered?.Invoke( target );
    }

    /// <summary>
    /// 등록된 NPC 조회
    /// </summary>
    /// <param name="results">조회 결과 목록</param>
    public static void GetAll ( List<Npc> results )
    {
        results.Clear( );

        foreach ( Npc target in _targets )
        {
            if ( target != null )
                results.Add( target );
        }
    }

    /// <summary>
    /// 포획 가능 NPC 조회
    /// </summary>
    /// <param name="results">조회 결과를 받을 목록</param>
    public static void GetCaptureCandidates ( List<Npc> results )
    {
        //이전 조회 결과를 비우고 현재 포획 가능한 대상만 수집
        results.Clear( );

        foreach ( Npc target in _targets )
        {
            if ( target == null || target.CanCapture == false )
                continue;

            results.Add( target );
        }
    }

    /// <summary>
    /// 잔존 NPC 수 집계
    /// </summary>
    /// <param name="targets">집계할 NPC 집합</param>
    /// <returns>이탈하지 않은 NPC 수</returns>
    static int CountRemaining ( HashSet<Npc> targets )
    {
        //포획 중이거나 비활성 상태여도 게임에서 이탈하지 않았다면 집계
        int count = 0;

        foreach ( Npc target in targets )
        {
            if ( target != null && target.IsRemaining == true )
                count++;
        }

        return count;
    }

    /// <summary>
    /// 시작 NPC 여부
    /// </summary>
    /// <param name="npc">확인할 NPC</param>
    /// <returns>시작 집합 포함 여부</returns>
    public static bool IsInitial ( Npc npc )
    {
        return npc != null && _initialNpcs.Contains( npc );
    }

}
