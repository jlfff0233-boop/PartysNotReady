using UnityEngine;

using System.Collections.Generic;

/// <summary>
/// 조리 제어 - 재료 배치, 내용물 표시와 대상 수명 연결
/// </summary>
public class CookController : MonoBehaviour, IItemDisposable
{
    [Header( "----- 레시피 안내 -----" )]
    [SerializeField] Transform _guidePoint;		//레시피 안내 위치
    [SerializeField] TargetHighlight _highlight;		//그릇 외곽선 강조

    [Header ( "----- 재료 배치 -----" )]
    [SerializeField] PlacementTarget _ingredientTarget;		//재료 배치 대상
    [SerializeField] Transform _ingredientRoot;		//투입 재료 부모
    [SerializeField] LayerMask _obstacleMask = ~0;    //재료 배치 방해물

    [Header ( "----- 완성물 위치 -----" )]
    [SerializeField] Transform _contentRoot;        //내용물 표시 부모

    [Header ( "----- 제작 사운드 -----" )]
    [SerializeField] ProductionSound _productionSound;		//제작 사운드
    [SerializeField] SoundPlayer _productionSoundPlayer;		//제작 전용 사운드 재생

    [Header ( "----- 제작 연출 -----" )]
    [SerializeField] ProductionEffect _productionEffect;		//최종 완성 연출

    const float IngredientContactTolerance = 0.001f;     //접촉 판정의 미세 오차 허용 거리

    bool _ingredientRestackRequested;      //등록 해제 후 재료 재정렬 요청 여부
    bool _isInitialized;		//참조 초기화 여부
    string _lastDebugEvent = string.Empty;		//마지막 제작 로그

    ItemCarry _item;		//그릇 아이템
    ProductionSession _session;		//제작 세션
    GameObject _contentInstance;        //내용물 인스턴스

    #region ----- 프로퍼티 -----

    /// <summary>
    /// 레시피 안내 위치
    /// </summary>
    public Transform GuidePoint => _guidePoint;

    /// <summary>
    /// 그릇 외곽선 강조
    /// </summary>
    public TargetHighlight Highlight => _highlight;

    /// <summary>
    /// 제작 세션
    /// </summary>
    public ProductionSession Session =>
        _session ??= new ProductionSession ( this );

    /// <summary>
    /// 연결된 제작 중재자
    /// </summary>
    public ProductionManager Owner { get; private set; }

    /// <summary>
    /// 월드 참조 초기화 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 그릇 아이템
    /// </summary>
    public ItemCarry Item => _item;

    /// <summary>
    /// 재료 배치 대상
    /// </summary>
    public PlacementTarget IngredientTarget => _ingredientTarget;

    /// <summary>
    /// 재료 부모
    /// </summary>
    public Transform IngredientRoot => _ingredientRoot;

    /// <summary>
    /// 내용물 존재 여부
    /// </summary>
    public bool HasContent => _contentInstance != null;

    /// <summary>
    /// 대상 보유 여부
    /// </summary>
    public bool IsBowlHeld => _item != null && _item.IsHold;

    /// <summary>
    /// 폐기 대상 존재 여부
    /// </summary>
    public bool HasDiscardable => _isInitialized && Session.HasDiscardable;

    /// <summary>
    /// 완성품 폐기 가능 여부
    /// </summary>
    public bool CanDispose => Session.CanDispose;

    /// <summary>
    /// 마지막 제작 진단
    /// </summary>
    public string LastDebugEvent => _lastDebugEvent;

    #endregion


    /// <summary>
    /// 그릇 참조 초기화
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( )
    {
        if ( _isInitialized == true )
            return false;

        _item = GetComponent<ItemCarry> ( );

        //비활성 그릇은 Awake 전에도 등록되므로 뷰만 먼저 준비
        if ( _item != null && _item.TryGetComponent ( out ItemWorldController view ) == true )
            view.Init ( );

        _isInitialized = _item != null && _item.IsReady;

        //그릇 준비에 성공한 경우 제작 세션과 사운드 연결
        if ( _isInitialized == true && _productionSound != null )
            _productionSound.Init ( Session , _productionSoundPlayer );

        return _isInitialized;
    }


    /// <summary>
    /// 제작 시간 전달
    /// </summary>
    private void Update ( )
    {
        _session?.Tick ( Time.deltaTime );
    }

    private void LateUpdate ( )
    {
        if ( _ingredientRestackRequested == false )
            return;

        //소비나 제작 중에는 재료 위치를 변경하지 않음
        if ( Session.CanReceiveIngredients == false )
        {
            _ingredientRestackRequested = false;
            return;
        }

        //들고 있는 그릇은 내려놓은 후 정렬
        if ( IsBowlHeld == true )
            return;

        if ( TryRestackIngredients ( out string reason ) == false )
            Debug.LogWarning ( $"[재료 재정렬 실패] {reason}" , this );
    }

    /// <summary>
    /// 파괴된 그릇 등록 해제
    /// </summary>
    private void OnDestroy ( )
    {
        if ( Owner != null )
            Owner.Unregister ( this );
    }


    #region ----- 제작 연결 -----

    /// <summary>
    /// 제작 중재자 연결
    /// </summary>
    /// <param name="owner">제작 중재자</param>
    /// <returns>연결 여부</returns>
    public bool Bind ( ProductionManager owner )
    {
        if ( owner == null || Owner != null ||
              Session.IsConsuming == true )
            return false;

        //재등록 시 기존 월드 참조와 세션 재사용
        if ( _isInitialized == false && Init ( ) == false )
            return false;

        if ( _item == null || _item.IsReady == false )
            return false;

        if ( _ingredientTarget == null || _ingredientRoot == null || _contentRoot == null )
            return false;

        Owner = owner;

        //연결된 그릇의 실제 완성 결과만 연출
        Session.OnCompleted += HandleCompleted;

        return true;
    }

    /// <summary>
    /// 제작 사운드와 재생 기록 정리
    /// </summary>
    public void StopSound ( )
    {
        if ( _productionSound != null )
            _productionSound.Stop ( );
    }

    /// <summary>
    /// 제작 파티클 정리
    /// </summary>
    public void StopEffect ( )
    {
        //파티클 미연결 그릇은 기존 제작 기능 유지
        if ( _productionEffect != null )
            _productionEffect.StopEffect ( );
    }

    /// <summary>
    /// 제작 연결과 진행 정리
    /// </summary>
    /// <param name="owner">연결한 제작 중재자</param>
    public void Unbind ( ProductionManager owner )
    {
        if ( ReferenceEquals ( Owner , owner ) == false )
            return;

        Owner = null;

        Session.OnCompleted -= HandleCompleted;

        //소비 중이면 실제 성공분 반영 후 정리
        Session.Disable ( );

        //등록 해제 시 제작 재생과 기록 정리
        StopSound ( );
        StopEffect ( );
    }

    /// <summary>
    /// 풀 반환 전 제작 상태와 관리자 연결 정리
    /// </summary>
    /// <param name="reason">정리 실패 사유</param>
    /// <returns>반환 준비 성공 여부</returns>
    public bool TryPreparePoolReturn ( out string reason )
    {
        reason = string.Empty;

        //등록 전에 실패한 객체는 제작 세션이 없을 수 있음
        if ( _session != null &&
             _session.TryPreparePoolReturn( out reason ) == false )
            return false;

        //미소비 재료가 반환 그릇의 자식으로 남아 재대여되지 않도록 분리
        if ( _ingredientRoot != null )
        {
            ProductIngredient [ ] ingredients =
                _ingredientRoot.GetComponentsInChildren<ProductIngredient>( true );
            foreach ( ProductIngredient ingredient in ingredients )
            {
                if ( ingredient.gameObject == gameObject ||
                     ingredient.GetComponentInParent<CookController>( true ) != this )
                    continue;

                ingredient.transform.SetParent( null, true );
            }
        }

        if ( Owner != null )
        {
            ProductionManager owner = Owner;		//현재 제작 관리자
            if ( owner.Unregister( this ) == false )
            {
                reason = "반환할 그릇의 제작 등록 해제에 실패했습니다.";
                return false;
            }
        }

        StopSound( );
        StopEffect( );
        return true;
    }

    #endregion


    /// <summary>
    /// 재료 부모 연결
    /// </summary>
    /// <param name="ingredient">재료</param>
    public void Attach ( ProductIngredient ingredient )
    {
        ingredient.transform.SetParent ( _ingredientRoot , true );
    }

    /// <summary>
    /// 재료 배치 허용 설정
    /// </summary>
    /// <param name="enabled">배치 허용 여부</param>
    public void SetIngredientPlacement ( bool enabled )
    {
        if ( _ingredientTarget != null )
            _ingredientTarget.enabled = enabled;
    }


    #region ----- 재료 배치 계산 -----


    /// <summary>
    /// 기존 위치 선정 방식으로 재료의 최종 접촉 위치 계산
    /// </summary>
    /// <param name="item">배치할 아이템</param>
    /// <param name="hit">조준한 충돌 정보</param>
    /// <param name="placement">계산한 배치 정보</param>
    /// <returns>배치 위치 확보 여부</returns>
    public bool TryGetIngredientPlacement (
        ItemCarry item , RaycastHit hit ,
        out PlacementData placement )
    {
        placement = default;

        Collider surface = _ingredientTarget.SurfaceCollider;

        Vector3 point = hit.collider == surface
            ? hit.point
            : surface.bounds.center;

        if ( PlacementChecker.TryGetSurfaceHit (
            surface , point , out RaycastHit surfaceHit ) == false )
            return false;

        List<Collider> supports = CollectIngredientSupports ( item );

        //기존 빈자리 선정은 유지하되 높이는 반드시 접촉 계산으로 확정
        if ( _ingredientTarget.TryGetPlacement (
            surfaceHit , item.PlacementBoundsSize ,
            item.PlacementRotationOffset ,
            out PlacementData candidate ) == true )
        {
            if ( TryGetIngredientContactPose ( item ,
                candidate.Position , candidate.Rotation ,
                candidate.SurfaceNormal , supports ,
                out Pose pose , out _ ) == true )
            {
                placement = CreateIngredientPlacement (
                    pose , candidate.SurfaceNormal );

                return true;
            }
        }

        //기존 빈자리를 사용할 수 없으면 중앙에서 다시 접촉 계산
        if ( PlacementChecker.TryGetSurfaceHit (
            surface , surface.bounds.center , out surfaceHit ) == false )
            return false;

        Vector3 normal = surfaceHit.normal.normalized;
        Quaternion rotation =
            PlacementCalculator.GetPlacementRotation ( surface , normal ) *
            item.PlacementRotationOffset;

        if ( TryGetIngredientContactPose (
            item , surfaceHit.point , rotation , normal , supports ,
            out Pose centerPose , out _ ) == false )
            return false;

        placement = CreateIngredientPlacement ( centerPose , normal );

        return true;
    }

    /// <summary>
    /// 접촉 계산을 마친 재료의 고정 배치 정보 생성
    /// </summary>
    /// <param name="pose">최종 루트 위치와 회전</param>
    /// <param name="normal">배치 표면 방향</param>
    /// <returns>재료 배치 정보</returns>
    PlacementData CreateIngredientPlacement ( Pose pose , Vector3 normal )
    {
        return new PlacementData (
            pose.position , pose.rotation , _ingredientRoot ,
            PlacementType.Fixed , true , _ingredientTarget , normal ,
            positionIsPivot: true );
    }

    /// <summary>
    /// 그릇 표면과 현재 등록된 재료의 지지 충돌체 수집
    /// </summary>
    /// <param name="placingItem">자기 자신을 제외할 배치 재료</param>
    /// <returns>접촉 대상으로 사용할 충돌체 목록</returns>
    List<Collider> CollectIngredientSupports ( ItemCarry placingItem )
    {
        List<Collider> supports = new ( );
        supports.Add ( _ingredientTarget.SurfaceCollider );

        ProductIngredient [ ] ingredients =
            _ingredientRoot.GetComponentsInChildren<ProductIngredient> ( false );

        foreach ( ProductIngredient ingredient in ingredients )
        {
            if ( ingredient.Product != this || ingredient.Carry == placingItem )
                continue;

            Collider collider = ingredient.Carry
                .GetComponent<ItemWorldController> ( ).PlacementCollider;

            if ( collider.enabled == false || collider.isTrigger == true )
                continue;

            supports.Add ( collider );
        }

        return supports;
    }


    /// <summary>
    /// 지정 대상이 재료 배치 대상인지 확인
    /// </summary>
    /// <param name="target">확인할 배치 대상</param>
    /// <returns>재료 배치 대상 여부</returns>
    public bool IsIngredientTarget ( PlacementTarget target )
    {
        return target != null && target == _ingredientTarget;
    }

    /// <summary>
    /// 충돌체가 그릇 자신의 아이템에 속하는지 확인
    /// </summary>
    /// <param name="collider">조준한 충돌체</param>
    /// <returns>그릇 몸체 충돌체 여부</returns>
    public bool IsBowlCollider ( Collider collider )
    {
        ItemCarry item = collider.GetComponentInParent<ItemCarry> ( );

        return _item != null && item == _item;
    }

    /// <summary>
    /// 재료 형상을 내려 실제 지지 충돌체와 처음 접촉하는 위치 계산
    /// </summary>
    /// <param name="item">배치할 재료</param>
    /// <param name="point">그릇 표면 기준 위치</param>
    /// <param name="rotation">배치 회전</param>
    /// <param name="normal">배치 표면 방향</param>
    /// <param name="supports">이번 계산에서 허용할 지지 충돌체</param>
    /// <param name="pose">계산한 최종 루트 위치와 회전</param>
    /// <param name="reason">접촉 또는 공간 검사 실패 사유</param>
    /// <returns>접촉 위치 확보 여부</returns>
    bool TryGetIngredientContactPose (
        ItemCarry item , Vector3 point , Quaternion rotation ,
        Vector3 normal , List<Collider> supports ,
        out Pose pose , out string reason )
    {
        pose = default;
        reason = string.Empty;

        Transform root = item.transform;
        Collider shape = item.GetComponent<ItemWorldController> ( ).PlacementCollider;
        Quaternion difference =
            ColliderCalculator.GetRotationDifference ( root , rotation );

        //목적지의 아래 방향을 현재 형상 방향으로 환산하여 피벗과 바닥 사이 거리 계산
        Vector3 sourceNormal = Quaternion.Inverse ( difference ) * normal;

        if ( ColliderCalculator.TryGetMinimum (
            shape , root.position , sourceNormal ,
            out float minimum , out reason ) == false )
            return false;

        Physics.SyncTransforms ( );

        float top = 0f;

        foreach ( Collider support in supports )
        {
            Bounds bounds = support.bounds;
            float height =
                Vector3.Dot ( bounds.center - point , normal ) +
                PlacementCalculator.GetBoundsExtent ( bounds , normal );

            top = Mathf.Max ( top , height );
        }

        //모든 지지체보다 높은 곳에서 시작하여 초기 겹침 없는 Cast 수행
        float distance = top + item.PlacementBoundsSize.magnitude;
        Pose start = new Pose (
            point + normal * ( distance - minimum ) , rotation );

        if ( TryCastIngredient (
            shape , root , start , -normal ,
            distance + IngredientContactTolerance ,
            out RaycastHit [ ] hits , out reason ) == false )
            return false;

        float nearest = float.PositiveInfinity;

        foreach ( RaycastHit hit in hits )
        {
            //외부 물체는 지지면으로 사용하지 않고 최종 공간 검사에서 판단
            if ( supports.Contains ( hit.collider ) == false )
                continue;

            nearest = Mathf.Min ( nearest , hit.distance );
        }

        if ( nearest == float.PositiveInfinity )
        {
            reason = "재료 아래에서 지지면을 찾지 못했습니다.";
            return false;
        }

        Pose contact = new Pose ( start.position - normal * nearest , rotation );

        //재료 관리 루트와 자기 자신을 제외한 주변 공간 검사
        //지지체와의 단순 접촉은 기존 형상별 허용 오차로 처리
        if ( PlacementSpaceChecker.IsClear (
            root , new Collider [ ] { shape } , contact ,
            _ingredientRoot , root , _obstacleMask ,
            IngredientContactTolerance , out reason ) == false )
            return false;

        pose = contact;
        return true;
    }

    /// <summary>
    /// 비활성 충돌체의 설정으로 목적지 형상 이동 검사
    /// </summary>
    /// <param name="shape">재료의 주 충돌체</param>
    /// <param name="root">현재 재료 루트</param>
    /// <param name="pose">검사를 시작할 루트 위치와 회전</param>
    /// <param name="direction">이동 검사 방향</param>
    /// <param name="distance">이동 검사 거리</param>
    /// <param name="hits">검출된 충돌 정보</param>
    /// <param name="reason">지원 형상 조회 실패 사유</param>
    /// <returns>이동 검사 실행 여부</returns>
    bool TryCastIngredient (
        Collider shape , Transform root , Pose pose ,
        Vector3 direction , float distance ,
        out RaycastHit [ ] hits , out string reason )
    {
        hits = null;
        reason = string.Empty;

        Transform target = shape.transform;
        Vector3 scale = ColliderCalculator.GetScale ( target );
        Quaternion difference =
            ColliderCalculator.GetRotationDifference ( root , pose.rotation );

        switch ( shape )
        {
            case BoxCollider box:
            {
                Vector3 center = ColliderCalculator.GetPosition (
                    root , pose , target.TransformPoint ( box.center ) );
                Vector3 halfExtents =
                    ColliderCalculator.GetBoxHalfExtents ( box.size , scale );

                hits = Physics.BoxCastAll (
                    center , halfExtents , direction , difference * target.rotation ,
                    distance , ~0 , QueryTriggerInteraction.Ignore );
                return true;
            }

            case SphereCollider sphere:
            {
                Vector3 center = ColliderCalculator.GetPosition (
                    root , pose , target.TransformPoint ( sphere.center ) );
                float radius = ColliderCalculator.GetSphereRadius ( sphere , scale );

                hits = Physics.SphereCastAll (
                    center , radius , direction , distance , ~0 ,
                    QueryTriggerInteraction.Ignore );
                return true;
            }

            case CapsuleCollider capsule:
            {
                if ( ColliderCalculator.TryGetCapsule (
                    capsule , scale , out Vector3 sourceCenter ,
                    out float radius , out _ , out Vector3 axis ,
                    out float halfSegment , out reason ) == false )
                    return false;

                Vector3 center =
                    ColliderCalculator.GetPosition ( root , pose , sourceCenter );
                Vector3 offset = difference * axis * halfSegment;

                hits = Physics.CapsuleCastAll (
                    center + offset , center - offset , radius , direction ,
                    distance , ~0 , QueryTriggerInteraction.Ignore );
                return true;
            }

            default:
                reason = "재료 배치는 Box/Sphere/CapsuleCollider를 지원합니다.";
                return false;
        }
    }

    /// <summary>
    /// 등록 해제 처리가 끝난 뒤 남은 재료 재정렬 요청
    /// </summary>
    public void RequestIngredientRestack ( )
    {
        _ingredientRestackRequested = true;
    }

    /// <summary>
    /// 재료 전체 정리에 따른 재정렬 요청 취소
    /// </summary>
    public void CancelIngredientRestack ( )
    {
        _ingredientRestackRequested = false;
    }

    /// <summary>
    /// 현재 높이 순서로 남은 재료를 지지면에 다시 배치
    /// </summary>
    /// <param name="reason">재정렬 실패 사유</param>
    /// <returns>전체 재료 재정렬 성공 여부</returns>
    public bool TryRestackIngredients ( out string reason )
    {
        _ingredientRestackRequested = false;
        reason = string.Empty;

        Collider surface = _ingredientTarget.SurfaceCollider;

        if ( PlacementChecker.TryGetSurfaceHit (
            surface , surface.bounds.center , out RaycastHit surfaceHit ) == false )
        {
            reason = "그릇의 재료 배치 표면을 찾지 못했습니다.";
            return false;
        }

        Vector3 normal = surfaceHit.normal.normalized;
        List<ProductIngredient> ingredients = new ( );

        foreach ( ProductIngredient ingredient in
            _ingredientRoot.GetComponentsInChildren<ProductIngredient> ( false ) )
        {
            if ( ingredient.Product == this )
                ingredients.Add ( ingredient );
        }

        Physics.SyncTransforms ( );

        //아래쪽 재료부터 확정하고 그 결과를 위쪽 재료의 지지면으로 사용
        ingredients.Sort ( ( left , right ) =>
        {
            float leftHeight = GetIngredientBottom ( left , normal );
            float rightHeight = GetIngredientBottom ( right , normal );
            int order = leftHeight.CompareTo ( rightHeight );

            return order != 0 ? order : left.InstanceId.CompareTo ( right.InstanceId );
        } );

        List<Collider> supports = new ( );
        supports.Add ( surface );

        List<ItemCarry> movedItems = new ( );
        bool completed = true;

        foreach ( ProductIngredient ingredient in ingredients )
        {
            ItemCarry item = ingredient.Carry;

            //표면에 대한 수평 위치와 기존 회전을 유지하고 높이만 다시 계산
            Vector3 point = item.transform.position;
            point -= normal * Vector3.Dot ( point - surfaceHit.point , normal );

            if ( TryGetIngredientContactPose (
                item , point , item.transform.rotation , normal , supports ,
                out Pose pose , out reason ) == false )
            {
                completed = false;
                break;
            }

            ItemWorldController view = item.GetComponent<ItemWorldController> ( );

            if ( view.TryApplyPlacement (
                CreateIngredientPlacement ( pose , normal ) , out reason ) == false )
            {
                completed = false;
                break;
            }

            movedItems.Add ( item );
            supports.Add ( view.PlacementCollider );
        }

        //위치 적용 후 상태만 알림. OnPlaced를 다시 보내 재료 등록을 반복하지 않음
        //도중 실패하더라도 이미 이동한 재료의 완료 알림은 전달
        foreach ( ItemCarry item in movedItems )
            item.CompleteForcedPlacement ( );

        return completed;
    }

    /// <summary>
    /// 재정렬 순서를 정할 현재 재료의 바닥 높이 계산
    /// </summary>
    /// <param name="ingredient">등록된 재료</param>
    /// <param name="normal">그릇 표면 방향</param>
    /// <returns>표면 방향으로 투영한 바닥 높이</returns>
    float GetIngredientBottom ( ProductIngredient ingredient , Vector3 normal )
    {
        Collider collider = ingredient.Carry
            .GetComponent<ItemWorldController> ( ).PlacementCollider;
        Bounds bounds = collider.bounds;

        //Bounds는 정렬 순서에만 사용하며 최종 접촉 위치 계산에는 사용하지 않음
        return Vector3.Dot ( bounds.center , normal ) -
            PlacementCalculator.GetBoundsExtent ( bounds , normal );
    }

    #endregion


    /// <summary>
    /// 최종 완성 결과에 따른 파티클 재생
    /// </summary>
    /// <param name="result">확정된 제작 결과</param>
    void HandleCompleted ( ProductionResult result )
    {
        //비활성화 중 마무리된 소비에서는 연출하지 않음
        if ( isActiveAndEnabled == false || Owner == null )
            return;

        if ( _productionEffect != null )
            _productionEffect.PlaySuccess ( );
    }


    #region ----- 내용물 -----

    /// <summary>
    /// 내용물 폐기
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>폐기 여부</returns>
    public bool TryDispose ( out string reason )
    {
        return Session.TryDispose ( out reason );
    }

    /// <summary>
    /// 제작 결과 데이터 적용
    /// </summary>
    /// <param name="data">완성 데이터</param>
    public void ApplyResultItem ( ItemData data )
    {
        if ( _item != null )
            _item.SetItemData ( data );
    }

    /// <summary>
    /// 내용물 생성
    /// </summary>
    /// <param name="prefab">완성품 프리팹</param>
    /// <returns>내용물 표시 준비 여부</returns>
    public bool TryShowContent ( GameObject prefab )
    {
        if ( _contentInstance != null ||
             prefab == null ||
             _contentRoot == null )
            return false;

        //그릇의 물리와 배치는 기존 구성 유지
        if ( prefab.GetComponentInChildren<Rigidbody> ( true ) != null ||
             prefab.GetComponentInChildren<Collider> ( true ) != null )
        {
            Debug.LogWarning (
                "[제작 내용물 표시 실패] " +
                "내용물 프리팹에는 Rigidbody와 Collider를 넣지 마세요." , this );
            return false;
        }

        _contentInstance = Instantiate ( prefab , _contentRoot );

        if ( this == null || _contentInstance == null || _contentRoot == null )
            return false;

        //트랜스폼 초기화
        _contentInstance.transform.localPosition = Vector3.zero;
        _contentInstance.transform.localRotation = Quaternion.identity;

        return true;
    }

    /// <summary>
    /// 내용물 제거
    /// </summary>
    public void ClearContent ( )
    {
        StopEffect ( );

        if ( _contentInstance == null )
            return;

        //지연 삭제 전 숨겨 같은 프레임의 재대여에서 이전 내용물 표시 방지
        _contentInstance.SetActive( false );
        Destroy ( _contentInstance );
        _contentInstance = null;
    }

    #endregion

    #region ----- 디버그 -----

    /// <summary>
    /// 제작 로그 기록
    /// </summary>
    /// <param name="message">상태 변경 내용</param>
    public void RecordProductionDebug ( string message )
    {
        _lastDebugEvent = message;
    }

#if UNITY_EDITOR

    /// <summary>
    /// 제작 완료 디버그 실행
    /// </summary>
    [ContextMenu ( "제작 디버그/완성 처리" )]
    void DebugComplete ( )
    {
        if ( Application.isPlaying == true )
            Session.Minigame.DebugComplete ( );
    }

    /// <summary>
    /// 제작 실패 디버그 실행
    /// </summary>
    [ContextMenu ( "제작 디버그/실패 처리" )]
    void DebugFail ( )
    {
        if ( Application.isPlaying == true )
            Session.Minigame.DebugFail ( );
    }
#endif

    #endregion

    /// <summary>
    /// 제작 대상 비활성화 정리
    /// </summary>
    private void OnDisable ( )
    {
        //임시 비활성화는 등록과 완성 결과 유지
        _session?.Disable ( );
        StopEffect ( );
    }

}
