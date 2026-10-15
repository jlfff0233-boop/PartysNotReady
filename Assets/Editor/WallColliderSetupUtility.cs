using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 선택한 맵의 벽 메시 콜라이더 점검과 일회성 추가
/// </summary>
public static class WallColliderSetupUtility
{
    /// <summary>
    /// 선택한 계층에서 중복 없이 벽 메시 수집
    /// </summary>
    static HashSet<MeshFilter> GetWalls ()
    {
        int wallLayer = LayerMask.NameToLayer( "Wall" );
        HashSet<MeshFilter> walls = new( );

        foreach ( GameObject root in Selection.gameObjects )
        {
            //프로젝트 에셋 대신 씬 또는 프리팹 편집 모드의 선택만 처리
            if ( EditorUtility.IsPersistent( root ) || root.scene.IsValid( ) == false )
                continue;

            foreach ( MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>( true ) )
            {
                if ( mesh.gameObject.layer == wallLayer && mesh.sharedMesh != null &&
                     mesh.TryGetComponent<MeshRenderer>( out _ ) )
                    walls.Add( mesh );
            }
        }

        return walls;
    }

    /// <summary>
    /// 기존 콜라이더와 움직이는 리지드바디를 제외한 추가 가능 여부
    /// </summary>
    static bool CanAdd ( MeshFilter mesh )
    {
        //기존 콜라이더의 형상과 설정은 유지
        if ( mesh.TryGetComponent<Collider>( out _ ) )
            return false;

        //비볼록 메시 콜라이더는 동적 리지드바디에 추가하지 않음
        Rigidbody body = mesh.GetComponentInParent<Rigidbody>( true );
        return body == null || body.isKinematic;
    }

    /// <summary>
    /// 선택한 벽 메시와 콜라이더 추가 예정 수 표시
    /// </summary>
    [MenuItem( "Tools/MapChecks/Walls/Inspect Selection" )]
    static void InspectSelection ()
    {
        HashSet<MeshFilter> walls = GetWalls( );
        int available = 0;
        foreach ( MeshFilter mesh in walls )
        {
            if ( CanAdd( mesh ) )
                available++;
        }

        Debug.Log( $"[벽 콜라이더 점검] 벽 메시: {walls.Count} / 추가 가능: {available} / 제외: {walls.Count - available}" );
    }

    /// <summary>
    /// 벽 형상을 유지하는 메시 콜라이더를 한 번의 Undo로 추가
    /// </summary>
    [MenuItem( "Tools/MapChecks/Walls/Add Missing Colliders" )]
    static void AddMissingColliders ()
    {
        //플레이 중 변경은 저장되지 않으므로 편집 상태에서만 실행
        if ( EditorApplication.isPlayingOrWillChangePlaymode )
        {
            Debug.LogWarning( "[벽 콜라이더 추가] 플레이를 종료한 뒤 실행해 주세요." );
            return;
        }

        HashSet<MeshFilter> walls = GetWalls( );
        Undo.IncrementCurrentGroup( );
        int group = Undo.GetCurrentGroup( );
        Undo.SetCurrentGroupName( "벽 메시 콜라이더 일괄 추가" );
        int added = 0;

        foreach ( MeshFilter mesh in walls )
        {
            if ( CanAdd( mesh ) == false )
                continue;

            MeshCollider collider = Undo.AddComponent<MeshCollider>( mesh.gameObject );
            Undo.RecordObject( collider, "벽 메시 연결" );
            collider.sharedMesh = mesh.sharedMesh;
            collider.convex = false;
            collider.isTrigger = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications( collider );
            EditorSceneManager.MarkSceneDirty( mesh.gameObject.scene );
            added++;
        }

        Undo.CollapseUndoOperations( group );
        Debug.Log( $"[벽 콜라이더 추가] 추가: {added} / 제외: {walls.Count - added} / 저장은 직접 진행해 주세요." );
    }
}
