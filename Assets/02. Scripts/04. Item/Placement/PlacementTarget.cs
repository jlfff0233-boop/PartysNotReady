using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 아이템 배치 대상 - 고정/자유 배치 정보 생성
/// </summary>
public class PlacementTarget : MonoBehaviour
{
    [Header( "----- 배치 설정 -----" )]
    [SerializeField] PlacementType _placementType;      //배치 타입
    [SerializeField] FixedPlacementType _fixedPlacementType;        //고정 배치 타입
    [SerializeField] FoodPlacementType _foodPlacementType;    //음식 배치 대상 타입

    [SerializeField] Collider _surfaceCollider;      //배치 표면 콜라이더
    [SerializeField] LayerMask _itemLayerMask;      //충돌 검사 아이템 레이어 마스크
    [SerializeField] float _space;      //직선 배치 간격
    [SerializeField] int _randomTryCount;       //무작위 위치 탐색 횟수

    [Header( "----- 준비 평가 -----" )]
    [SerializeField] PreparationLocationType _preparationLocation;		//준비 표면 종류

    /// <summary>
    /// 준비 평가용 표면 종류
    /// </summary>
    public PreparationLocationType PreparationLocation => _preparationLocation;

    /// <summary>
    /// 음식 배치 대상 타입
    /// </summary>
    public FoodPlacementType FoodPlacementType => _foodPlacementType;

    /// <summary>
    /// 실제 배치 표면 콜라이더
    /// </summary>
    public Collider SurfaceCollider => _surfaceCollider;

    /// <summary>
    /// 아이템 배치 알림(배치된 아이템)
    /// </summary>
    public event Action<ItemCarry> OnItemPlaced;


    private void Awake ()
    {
        //직접 지정하지 않았다면 자식까지 탐색
        if ( _surfaceCollider == null )
            _surfaceCollider = GetComponentInChildren<Collider>( );
    }


    #region ----- 배치 -----

    /// <summary>
    /// 강제 배치 정보 생성
    /// </summary>
    /// <param name="pose">위치 보정과 공간 검사를 마친 루트 위치와 회전</param>
    /// <param name="surfaceNormal">검사한 목적지 표면의 월드 법선</param>
    /// <param name="placement">생성한 강제 배치 정보</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>배치 정보 생성 성공 여부</returns>
    public bool TryCreateForcedPlacement (
        Pose pose, Vector3 surfaceNormal,
        out PlacementData placement, out string reason )
    {
        placement = default;
        reason = string.Empty;

        if ( isActiveAndEnabled == false ||
             _surfaceCollider == null ||
             _surfaceCollider.enabled == false ||
             _surfaceCollider.gameObject.activeInHierarchy == false ||
             _surfaceCollider.isTrigger )
        {
            reason = "활성 배치 대상과 일반 표면 충돌체가 필요합니다.";
            return false;
        }

        if ( ColliderCalculator.CheckFinite( pose.position ) == false ||
             ColliderCalculator.CheckRotation( pose.rotation ) == false ||
             ColliderCalculator.CheckFinite( surfaceNormal ) == false ||
             ColliderCalculator.CheckFinite(
                 surfaceNormal.sqrMagnitude ) == false ||
             surfaceNormal.sqrMagnitude < 0.000001f )
        {
            reason = "배치 위치, 회전과 표면 방향을 확인해 주세요.";
            return false;
        }

        //기존 배치 규칙처럼 지정 배치는 고정
        //자유 배치 대상도 벽이면 물리적으로 고정
        bool isWall =
            _surfaceCollider.gameObject.layer == LayerMask.NameToLayer( "Wall" );
        bool isFixed = _placementType == PlacementType.Fixed || isWall;

        //호출부에서 지지 표면과 공간 검사를 마친 위치를 그대로 사용
        //부모에서 분리하여 검사 당시 월드 크기를 유지
        placement = new PlacementData(
            pose.position, pose.rotation.normalized, null,
            _placementType, isFixed, this, surfaceNormal );

        return true;
    }

    /// <summary>
    /// 배치 유형에 맞는 위치 조회
    /// </summary>
    /// <param name="hit">레이 충돌 정보</param>
    /// <param name="placementBoundsSize">아이템 축 기준 크기</param>
    /// <param name="rotationOffset">아이템 배치 방향 보정</param>
    /// <param name="placement">배치 정보</param>
    /// <returns>배치 정보 조회 성공 여부</returns>
    public bool TryGetPlacement (
        RaycastHit hit, Vector3 placementBoundsSize, Quaternion rotationOffset,
        out PlacementData placement )
    {
        //배치 표면이 없으면 배치 불가
        if ( _surfaceCollider == null )
        {
            placement = default;
            return false;
        }

        //지정 배치 유형이라면
        if ( _placementType == PlacementType.Fixed )
            return TryGetFixedPlacement( placementBoundsSize, rotationOffset, out placement );

        //자유 배치 유형일 때
        return TryGetFreePlacement( hit, placementBoundsSize, rotationOffset, out placement );
    }

    /// <summary>
    /// 현재 배치 대상에 들어온 아이템 알림
    /// </summary>
    /// <param name="item">배치된 아이템</param>
    public void NotifyPlaced ( ItemCarry item )
    {
        if ( isActiveAndEnabled == false || item == null )
            return;

        OnItemPlaced?.Invoke( item );
    }

    #endregion

    #region ----- 고정 배치 -----

    /// <summary>
    /// 직선/무작위 배치 위치 조회
    /// </summary>
    /// <param name="placementBoundsSize">아이템 축 기준 크기</param>
    /// <param name="rotationOffset">아이템 배치 방향 보정</param>
    /// <param name="placement">배치 정보</param>
    /// <returns>배치 정보 조회 성공 여부</returns>
    bool TryGetFixedPlacement (
        Vector3 placementBoundsSize,
        Quaternion rotationOffset,
        out PlacementData placement )
    {
        //고정 배치 위치 선정 타입에 따라 위치 탐색
        if ( _fixedPlacementType == FixedPlacementType.Linear )
            return TryGetLinearPlacement(
                placementBoundsSize, rotationOffset, out placement );

        return TryGetRandomPlacement(
            placementBoundsSize, rotationOffset, out placement );
    }

    /// <summary>
    /// 후보 위치의 고정 배치 정보 생성
    /// </summary>
    /// <param name="position">배치 후보 월드 위치</param>
    /// <param name="placementBoundsSize">아이템 축 기준 크기</param>
    /// <param name="rotationOffset">아이템 배치 방향 보정</param>
    /// <param name="placement">배치 정보</param>
    /// <returns>배치 데이터 생성 성공 여부</returns>
    bool TryCreateFixedPlacement (
        Vector3 position,
        Vector3 placementBoundsSize,
        Quaternion rotationOffset,
        out PlacementData placement )
    {
        //후보 위치에 실제 표면이 없으면 배치 불가
        if ( PlacementChecker.TryGetSurfaceHit(
            _surfaceCollider, position, out RaycastHit surfaceHit ) == false )
        {
            placement = default;
            return false;
        }

        //실제 표면 방향에 아이템의 배치 방향 보정 적용
        Quaternion rotation = PlacementCalculator.GetPlacementRotation(
            _surfaceCollider, surfaceHit.normal ) * rotationOffset;

        //다른 아이템과 겹치면 배치 불가
        if ( PlacementSpaceChecker.IsPlacementAvailable (
            surfaceHit.point , rotation , placementBoundsSize ,
            surfaceHit.normal , _itemLayerMask , _surfaceCollider ) == false )
        {
            placement = default;
            return false;
        }

        //배치 데이터 생성
        //지정 위치 배치는 항상 고정
        placement = new PlacementData(
            surfaceHit.point, rotation, null,
            PlacementType.Fixed, true, this, surfaceHit.normal );
        return true;
    }

    /// <summary>
    /// 직선 배열에서 빈 배치 위치 조회
    /// </summary>
    /// <param name="placementBoundsSize">아이템 축 기준 크기</param>
    /// <param name="rotationOffset">아이템 배치 방향 보정</param>
    /// <param name="placement">배치 정보</param>
    /// <returns>조회 성공 여부</returns>
    bool TryGetLinearPlacement (
        Vector3 placementBoundsSize,
        Quaternion rotationOffset,
        out PlacementData placement )
    {
        Bounds surfaceBounds = _surfaceCollider.bounds;
        Transform surface = _surfaceCollider.transform;
        Quaternion rotation = PlacementCalculator
            .GetPlacementRotation( _surfaceCollider, surface.up ) * rotationOffset;

        //배치 대상과 아이템의 좌우 방향 반경 계산
        float surfaceHalfWidth = PlacementCalculator
            .GetBoundsExtent( surfaceBounds, surface.right );
        float itemHalfWidth = PlacementCalculator
            .GetItemExtent( placementBoundsSize, rotation, surface.right );

        //배치 대상과 아이템의 앞뒤 방향 반경 계산
        float surfaceHalfDepth = PlacementCalculator
            .GetBoundsExtent( surfaceBounds, surface.forward );
        float itemHalfDepth = PlacementCalculator
            .GetItemExtent( placementBoundsSize, rotation, surface.forward );

        //직선 배치가 가능한 좌우 탐색 범위 계산
        float minOffset = -surfaceHalfWidth + itemHalfWidth;
        float maxOffset = surfaceHalfWidth - itemHalfWidth;

        //아이템이 배치 대상보다 크면 배치 불가
        if ( minOffset > maxOffset || itemHalfDepth > surfaceHalfDepth )
        {
            placement = default;
            return false;
        }

        //아이템 너비와 배치 간격을 반영한 탐색 단위 계산
        float step = itemHalfWidth * 2f + _space;

        //비정상 간격과 진행되지 않는 실수 덧셈은 배치 실패로 처리
        if ( float.IsNaN( step ) || float.IsInfinity( step ) || step <= 0f ||
             float.IsNaN( minOffset ) || float.IsInfinity( minOffset ) ||
             float.IsNaN( maxOffset ) || float.IsInfinity( maxOffset ) ||
             float.IsNaN( itemHalfDepth ) || float.IsInfinity( itemHalfDepth ) ||
             float.IsNaN( surfaceHalfDepth ) || float.IsInfinity( surfaceHalfDepth ) )
        {
            placement = default;
            return false;
        }

        for ( float offset = minOffset; offset <= maxOffset; )
        {
            //왼쪽 끝부터 오른쪽으로 탐색해 처음 비어 있는 위치 선택
            Vector3 position =
                surfaceBounds.center + surface.right * offset;

            if ( TryCreateFixedPlacement(
                position, placementBoundsSize, rotationOffset, out placement ) == true )
                return true;

            float nextOffset = offset + step;
            if ( nextOffset <= offset || float.IsInfinity( nextOffset ) )
                break;
            offset = nextOffset;
        }

        placement = default;
        return false;
    }

    /// <summary>
    /// 무작위 후보에서 빈 배치 위치 조회
    /// </summary>
    /// <param name="placementBoundsSize">아이템 축 기준 크기</param>
    /// <param name="rotationOffset">아이템 배치 방향 보정</param>
    /// <param name="placement">배치 정보</param>
    /// <returns>확인 성공 여부</returns>
    bool TryGetRandomPlacement (
        Vector3 placementBoundsSize,
        Quaternion rotationOffset,
        out PlacementData placement )
    {
        Bounds surfaceBounds = _surfaceCollider.bounds;
        Transform surface = _surfaceCollider.transform;
        Quaternion rotation = PlacementCalculator
            .GetPlacementRotation( _surfaceCollider, surface.up ) * rotationOffset;

        //배치 대상과 아이템의 좌우 방향 반경 계산
        float surfaceHalfWidth = PlacementCalculator
            .GetBoundsExtent( surfaceBounds, surface.right );
        float itemHalfWidth = PlacementCalculator
            .GetItemExtent( placementBoundsSize, rotation, surface.right );

        //배치 대상과 아이템의 앞뒤 방향 반경 계산
        float surfaceHalfDepth = PlacementCalculator
            .GetBoundsExtent( surfaceBounds, surface.forward );
        float itemHalfDepth = PlacementCalculator
            .GetItemExtent( placementBoundsSize, rotation, surface.forward );

        //아이템 크기를 반영한 무작위 탐색 범위 계산
        float minX = -surfaceHalfWidth + itemHalfWidth;
        float maxX = surfaceHalfWidth - itemHalfWidth;
        float minZ = -surfaceHalfDepth + itemHalfDepth;
        float maxZ = surfaceHalfDepth - itemHalfDepth;

        //아이템이 배치 대상보다 크면 배치 불가
        if ( minX > maxX || minZ > maxZ )
        {
            placement = default;
            return false;
        }

        //지정 횟수 내에 빈자리를 찾지 못하면 배치 실패로 반환
        for ( int i = 0; i < _randomTryCount; i++ )
        {
            //배치 범위 안에서 무작위 좌우, 앞뒤 위치 계산
            float offsetX = Random.Range( minX, maxX );
            float offsetZ = Random.Range( minZ, maxZ );

            Vector3 position =
                surfaceBounds.center + surface.right * offsetX + surface.forward * offsetZ;

            if ( TryCreateFixedPlacement(
                position, placementBoundsSize, rotationOffset, out placement ) == true )
                return true;
        }

        placement = default;
        return false;
    }

    #endregion

    #region ----- 자유 배치 -----

    /// <summary>
    /// 조준한 표면의 배치 정보 생성
    /// </summary>
    /// <param name="hit">레이 충돌 정보</param>
    /// <param name="placementBoundsSize">아이템 축 기준 크기</param>
    /// <param name="rotationOffset">아이템 배치 방향 보정</param>
    /// <param name="placement">배치 정보</param>
    /// <returns>배치 정보 조회 성공 여부</returns>
    bool TryGetFreePlacement (
        RaycastHit hit,
        Vector3 placementBoundsSize,
        Quaternion rotationOffset,
        out PlacementData placement )
    {
        //충돌 표면을 기준으로 아이템 배치 회전 계산
        Quaternion rotation = PlacementCalculator
            .GetPlacementRotation( _surfaceCollider, hit.normal ) * rotationOffset;

        //다른 아이템과 겹치면 배치 불가
        if ( PlacementSpaceChecker.IsPlacementAvailable (
            hit.point , rotation , placementBoundsSize ,
            hit.normal , _itemLayerMask , _surfaceCollider ) == false )
        {
            placement = default;
            return false;
        }

        //충돌한 표면이 벽이면 자유 배치 후에도 고정
        bool isWall = hit.collider.gameObject.layer == LayerMask.NameToLayer( "Wall" );

        placement = new PlacementData(
            hit.point, rotation, null, PlacementType.Free, isWall, this, hit.normal );
        return true;
    }

    #endregion

}
