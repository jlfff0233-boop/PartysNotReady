using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 조사 범위 계산 - 충돌체 형태를 관리 루트 기준 상자 범위로 변환
/// </summary>
public static class AnomalyPrefabBoundsUtility
{
    /// <summary>
    /// 충돌체의 로컬 형태 범위 조회
    /// </summary>
    /// <param name="collider">범위를 조회할 충돌체</param>
    /// <param name="bounds">충돌체 로컬 범위</param>
    /// <returns>지원하는 충돌체 형태인지 여부</returns>
    static bool GetLocalBounds ( Collider collider, out Bounds bounds )
    {
        switch ( collider )
        {
            case BoxCollider box:
                bounds = new Bounds( box.center, box.size );
                return true;
            case SphereCollider sphere:
                bounds = new Bounds( sphere.center, Vector3.one * sphere.radius * 2f );
                return true;
            case CapsuleCollider capsule:
                Vector3 size = Vector3.one * capsule.radius * 2f;
                size[capsule.direction] = Mathf.Max( capsule.height, size[capsule.direction] );
                bounds = new Bounds( capsule.center, size );
                return true;
            case MeshCollider mesh when mesh.sharedMesh != null:
                bounds = mesh.sharedMesh.bounds;
                return true;
            default:
                bounds = default;
                return false;
        }
    }

    /// <summary>
    /// 비균일 크기를 사용하는 구와 캡슐의 실제 물리 크기 범위 계산
    /// </summary>
    /// <param name="collider">구 또는 캡슐 충돌체</param>
    /// <returns>월드 기준 보수적인 상자 범위</returns>
    static Bounds GetRoundBounds ( Collider collider )
    {
        Vector3 scale = collider.transform.lossyScale;
        scale = new Vector3( Mathf.Abs( scale.x ), Mathf.Abs( scale.y ), Mathf.Abs( scale.z ) );
        if ( collider is SphereCollider sphere )
        {
            float radius = sphere.radius * Mathf.Max( scale.x, Mathf.Max( scale.y, scale.z ) );
            return new Bounds( sphere.transform.TransformPoint( sphere.center ), Vector3.one * radius * 2f );
        }
        CapsuleCollider capsule = (CapsuleCollider)collider;
        int axis = capsule.direction;
        float width = Mathf.Max( scale[( axis + 1 ) % 3], scale[( axis + 2 ) % 3] );
        float worldRadius = capsule.radius * width;
        float halfSegment = Mathf.Max( capsule.height * scale[axis] * 0.5f - worldRadius, 0f );
        Vector3 direction = Vector3.zero;
        direction[axis] = 1f;
        direction = capsule.transform.TransformDirection( direction );
        Vector3 extents = Vector3.one * worldRadius +
            new Vector3( Mathf.Abs( direction.x ), Mathf.Abs( direction.y ), Mathf.Abs( direction.z ) ) * halfSegment;
        return new Bounds( capsule.transform.TransformPoint( capsule.center ), extents * 2f );
    }

    /// <summary>
    /// 비활성 충돌체도 포함하여 관리 루트 기준 조사 범위 계산
    /// </summary>
    /// <param name="root">조사 영역을 둘 관리 루트</param>
    /// <param name="visual">전체 외형 객체</param>
    /// <param name="margin">루트 기준 각 면의 여유</param>
    /// <param name="bounds">루트 기준 조사 범위</param>
    /// <param name="results">범위 계산 실패 사유</param>
    /// <returns>유효한 조사 범위를 계산했는지 여부</returns>
    public static bool Calculate ( Transform root, GameObject visual, float margin, out Bounds bounds, List<string> results )
    {
        bounds = default;
        bool found = false;
        foreach ( Collider collider in visual.GetComponentsInChildren<Collider>( true ) )
        {
            if ( collider.isTrigger == true )
                continue;
            if ( GetLocalBounds( collider, out Bounds local ) == false )
            {
                results.Add( "제외: 조사 범위를 계산할 수 없는 충돌체입니다. " + collider.name );
                return false;
            }
            bool worldSpace = collider is SphereCollider || collider is CapsuleCollider;
            if ( worldSpace == true )
                local = GetRoundBounds( collider );
            for ( int i = 0; i < 8; i++ )
            {
                Vector3 corner = local.center + Vector3.Scale( local.extents,
                    new Vector3( ( i & 1 ) == 0 ? -1f : 1f, ( i & 2 ) == 0 ? -1f : 1f, ( i & 4 ) == 0 ? -1f : 1f ) );
                Vector3 point = root.InverseTransformPoint( worldSpace == true ? corner : collider.transform.TransformPoint( corner ) );
                if ( found == false )
                    bounds = new Bounds( point, Vector3.zero );
                else
                    bounds.Encapsulate( point );
                found = true;
            }
        }
        bounds.Expand( margin * 2f );
        Vector3 size = bounds.size;
        if ( found == false || size.x <= 0f || size.y <= 0f || size.z <= 0f ||
            float.IsNaN( size.sqrMagnitude ) || float.IsInfinity( size.sqrMagnitude ) ||
            float.IsNaN( bounds.center.sqrMagnitude ) || float.IsInfinity( bounds.center.sqrMagnitude ) )
        {
            results.Add( "제외: 조사 영역에 유효한 입체 범위가 필요합니다." );
            return false;
        }
        return true;
    }
}
