using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외형 구성 검사 - 외형 계층과 Renderer / Collider 설정 확인
/// </summary>
public static class AppearanceChecker
{

    #region ----- 구성 검사 -----

    /// <summary>
    /// 외형 계층과 등록 컴포넌트 검사
    /// </summary>
    /// <param name="view">검사할 프리팹 또는 씬 외형</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>사용 가능한 외형 구성 여부</returns>
    public static bool Check (
        AppearanceComponents view, out string reason )
    {
        reason = string.Empty;

        if ( view == null )
            return Fail( "외형 컴포넌트가 없습니다.", out reason );

        if ( CheckRoot( view, out reason ) == false )
            return false;

        if ( CheckReferences(
            view.transform, view.Renderers, "Renderer", out reason ) == false )
            return false;

        if ( CheckReferences(
            view.transform, view.Colliders, "Collider", out reason ) == false )
            return false;

        if ( CheckColliders( view, out reason ) == false )
            return false;

        return true;
    }

    /// <summary>
    /// 프리팹에서 사용할 외형 하나 조회
    /// </summary>
    /// <param name="prefab">아이템 또는 외형 프리팹</param>
    /// <param name="appearance">실제로 복제할 외형 루트</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>사용할 외형 조회 성공 여부</returns>
    public static bool TryGetPrefabAppearance (
        GameObject prefab, out AppearanceComponents appearance, out string reason )
    {
        appearance = null;
        reason = string.Empty;

        if ( prefab == null )
            return Fail( "AppearanceData의 Prefab이 지정되지 않았습니다.", out reason );

        AppearanceComponents [ ] views =
            prefab.GetComponentsInChildren<AppearanceComponents>( true );

        if ( views.Length == 0 )
            return Fail(
                $"{prefab.name}: 프리팹 내부에 AppearanceComponents가 없습니다.",
                out reason );

        if ( views.Length > 1 )
            return Fail(
                $"{prefab.name}: AppearanceComponents가 {views.Length}개입니다." +
                $" 생성할 외형을 하나로 구성해 주세요.",
                out reason );

        AppearanceComponents selected = views [ 0 ];

        //전체 아이템이 아니라 선택한 외형 아래의 구성만 검사
        //Renderer와 Collider가 외형 밖을 참조하는 경우도 기존 검사에서 거절
        if ( Check( selected, out string detail ) == false )
            return Fail(
                $"{prefab.name} / {selected.name}: {detail}", out reason );

        appearance = selected;
        return true;
    }

    #endregion

    #region ----- 배치 충돌체 조회 -----

    /// <summary>
    /// 외형 공간 검사에 포함할 충돌체인지 확인
    /// </summary>
    /// <param name="collider">확인할 충돌체</param>
    /// <param name="appearanceRoot">비활성 여부를 무시할 외형 루트</param>
    /// <returns>검사에 포함할 충돌체 여부</returns>
    public static bool CheckParticipating (
        Collider collider, Transform appearanceRoot )
    {
        if ( collider == null || appearanceRoot == null ||
             collider.enabled == false || collider.isTrigger == true )
            return false;

        //외형 루트와 부모의 비활성 상태는 허용하고 내부 자식의 활성 상태만 확인
        Transform current = collider.transform;
        while ( current != appearanceRoot )
        {
            if ( current == null || current.gameObject.activeSelf == false )
                return false;

            current = current.parent;
        }

        return true;
    }

    /// <summary>
    /// 외형에서 배치 검사에 사용할 충돌체 수집
    /// </summary>
    /// <param name="view">참조 구성을 검사한 외형</param>
    /// <param name="colliders">검사에 참여할 일반 충돌체 목록</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>검사할 충돌체 존재 여부</returns>
    public static bool TryGetPlacementColliders (
        AppearanceComponents view, out List<Collider> colliders, out string reason )
    {
        colliders = new List<Collider>( );
        reason = string.Empty;
        if ( view == null || view.Colliders == null )
            return Fail( "외형 또는 충돌체 목록이 없습니다.", out reason );

        foreach ( Collider collider in view.Colliders )
        {
            if ( collider == null || collider.transform.IsChildOf( view.transform ) == false )
                return Fail( "외형 내부의 충돌체 참조가 필요합니다.", out reason );

            //루트 비활성은 준비 상태로 허용하고 자식 비활성 및 트리거는 제외
            if ( CheckParticipating( collider, view.transform ) )
                colliders.Add( collider );
        }

        if ( colliders.Count == 0 )
            return Fail( "배치를 검사할 일반 충돌체가 없습니다.", out reason );
        return true;
    }

    #endregion

    #region ----- 참조 검사 -----

    /// <summary>
    /// 외형 내부의 관리 기능과 중첩 외형 검사
    /// </summary>
    /// <param name="view">검사할 외형</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>관리 루트와 분리된 외형 여부</returns>
    static bool CheckRoot (
        AppearanceComponents view, out string reason )
    {
        reason = string.Empty;

        //부모의 Rigidbody는 유지하고 외형 내부에 추가된 것만 차단
        if ( view.GetComponentInChildren<Rigidbody>( true ) != null )
            return Fail(
                "외형 내부에는 Rigidbody를 추가할 수 없습니다.", out reason );

        if ( view.GetComponentInChildren<ItemCarry>( true ) != null ||
             view.GetComponentInChildren<AnomalyTarget>( true ) != null )
        {
            return Fail(
                "ItemCarry와 AnomalyTarget은 외형 밖의 관리 루트에 두어야 합니다.",
                out reason );
        }

        AppearanceComponents [ ] views =
            view.GetComponentsInChildren<AppearanceComponents>( true );

        if ( views.Length != 1 )
            return Fail(
                "하나의 외형 안에 AppearanceComponents가 중첩되어 있습니다.", out reason );

        return true;
    }

    /// <summary>
    /// 컴포넌트 목록의 누락, 중복과 소속 검사
    /// </summary>
    /// <typeparam name="T">검사할 컴포넌트 형식</typeparam>
    /// <param name="root">외형 루트</param>
    /// <param name="references">직접 등록한 참조 목록</param>
    /// <param name="label">실패 사유에 표시할 이름</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>모든 참조가 올바르게 등록됐는지 여부</returns>
    static bool CheckReferences<T> (
        Transform root, IReadOnlyList<T> references,
        string label, out string reason ) where T : Component
    {
        reason = string.Empty;

        if ( references == null || references.Count == 0 )
            return Fail( $"{label} 목록이 비어 있습니다.", out reason );

        HashSet<T> registered = new( );

        for ( int i = 0; i < references.Count; i++ )
        {
            T reference = references [ i ];

            if ( reference == null )
                return Fail(
                    $"{label} 목록의 {i}번 참조가 비어 있습니다.", out reason );

            if ( reference.transform != root &&
                 reference.transform.IsChildOf( root ) == false )
            {
                return Fail(
                    $"{reference.name}: 외형 밖의 {label}를 참조하고 있습니다.",
                    out reason );
            }

            if ( registered.Add( reference ) == false )
                return Fail(
                    $"{reference.name}: {label}가 중복 등록되어 있습니다.",
                    out reason );
        }

        //비활성 자식까지 비교해 목록에서 빠진 Renderer나 Collider가 없는지 확인
        T [ ] actual = root.GetComponentsInChildren<T>( true );

        foreach ( T component in actual )
        {
            if ( registered.Contains( component ) == false )
                return Fail(
                    $"{component.name}: {label} 목록에서 누락됐습니다.",
                    out reason );
        }

        return true;
    }

    #endregion

    #region ----- 충돌체 검사 -----

    /// <summary>
    /// 대표 Collider와 외형 충돌체 설정 검사
    /// </summary>
    /// <param name="view">검사할 외형</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>사용 가능한 충돌체 구성 여부</returns>
    static bool CheckColliders (
        AppearanceComponents view, out string reason )
    {
        reason = string.Empty;
        Collider main = view.MainCollider;

        if ( main == null )
            return Fail( "대표 Collider가 지정되지 않았습니다.", out reason );

        bool containsMain = false;

        foreach ( Collider collider in view.Colliders )
        {
            if ( collider == main )
                containsMain = true;

            if ( CheckCollider( collider, out reason ) == false )
                return false;
        }

        if ( containsMain == false )
            return Fail(
                "대표 Collider가 외형 Collider 목록에 없습니다.", out reason );

        if ( main.isTrigger == true || main.enabled == false )
            return Fail(
                "대표 Collider는 활성화된 일반 충돌체여야 합니다.", out reason );

        //씬 외형 루트 자체는 준비나 보관을 위해 비활성화할 수 있음
        Transform current = main.transform;
        while ( current != view.transform )
        {
            if ( current.gameObject.activeSelf == false )
                return Fail(
                    "대표 Collider까지의 자식 경로가 비활성화되어 있습니다.",
                    out reason );

            current = current.parent;
        }

        if ( main is BoxCollider == false &&
             main is SphereCollider == false &&
             main is CapsuleCollider == false )
        {
            return Fail(
                "대표 Collider는 Box, Sphere 또는 Capsule을 사용해 주세요.",
                out reason );
        }

        return true;
    }

    /// <summary>
    /// 공간 검사에 사용할 수 있는 Collider인지 확인
    /// </summary>
    /// <param name="collider">검사할 충돌체</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>지원하는 충돌체 여부</returns>
    static bool CheckCollider (
        Collider collider, out string reason )
    {
        reason = string.Empty;

        if ( collider is BoxCollider ||
             collider is SphereCollider ||
             collider is CapsuleCollider )
            return true;

        if ( collider is MeshCollider mesh )
        {
            if ( mesh.sharedMesh == null )
                return Fail(
                    $"{mesh.name}: Mesh가 지정되지 않았습니다.", out reason );

            if ( mesh.convex == false )
                return Fail(
                    $"{mesh.name}: 외형 MeshCollider는 Convex가 필요합니다.",
                    out reason );

            return true;
        }

        return Fail(
            $"{collider.name}: 지원하지 않는 Collider 종류입니다.", out reason );
    }

    /// <summary>
    /// 검사 실패 사유 반환
    /// </summary>
    /// <param name="message">실패 설명</param>
    /// <param name="reason">반환할 실패 사유</param>
    /// <returns>검사 실패</returns>
    static bool Fail ( string message, out string reason )
    {
        reason = message;
        return false;
    }

    #endregion

}
