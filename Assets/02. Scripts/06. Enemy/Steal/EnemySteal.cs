using UnityEngine;

using System.Collections.Generic;

/// <summary>
/// 아이템 절도 적 - 비행, 음식 탐색과 절도
/// </summary>
[RequireComponent( typeof( FlightMover ) )]
public partial class EnemySteal : Enemy, IAnomalySpawnTarget
{
    [Header( "----- 설정 데이터 -----" )]
    [SerializeField] EnemyStealData _data;		//비행과 절도 설정

    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] FlightMover _mover;		//비행 이동
    [SerializeField] HideActivationController _activation;		//모델과 몸체 충돌 표시 제어
    [SerializeField] AnimatorHandler _animatorHandler;		//동작 애니메이션

    [Header( "----- 사운드 -----" )]
    [SerializeField] StealSound _enemySound;		//적 사운드
    [SerializeField] SoundPlayer _soundPlayer;		//적 전용 사운드 재생

    [Header( "----- 보유 -----" )]
    [SerializeField] Transform _holdPoint;		//음식 보유 지점

    Transform [ ] _enterPoints;		//침입 경유점
    Transform _spawnPoint;		//사건 시작 시 등장 지점
    Transform [ ] _escapePoints;		//탈출 경유점

    float _searchElapsed;		//다음 탐색까지 경과 시간
    bool _isEnding;		//보유 해제/종료 중 재진입 차단
    bool _isDescending;		//확보 위치로 하강 중

    Collider [ ] _bodyColliders;		//보유 음식과 충돌 제외할 몸체
    ItemCarry _target;		//접근 대상
    ItemWorldController _targetView;		//대상 위치 조회용 뷰
    ItemCarry _heldItem;		//확보한 음식
    IngredientSource _targetSource;		//접근할 음식 공급원
    IngredientStockManager _stock;		//씬에 등록된 공급원 조회
    ItemHolder _foodHolder;		//확보한 음식의 실제 보유 관리
    uint _activityVersion;		//탐색 활동 회차

    StealModel _model;		//절도 모델

    /// <summary>
    /// 음식 후보 목록
    /// </summary>
    List<ItemStealTargetData> _candidates = new( );

    /// <summary>
    /// 이번 플레이에 등록된 공급원 목록
    /// </summary>
    List<IngredientSource> _sources = new( );

    /// <summary>
    /// 탐색한 공급원 집합
    /// </summary>
    HashSet<IngredientSource> _checkedSources = new( );

    /// <summary>
    /// 음식 후보 판정 정보
    /// </summary>
    List<StealTargetData> _targetData = new( );

    /// <summary>
    /// 탐색한 아이템 집합
    /// </summary>
    HashSet<ItemCarry> _checkedItems = new( );

    /// <summary>
    /// 자동 발생 대상 종류
    /// </summary>
    public override EnemySpawnType SpawnType => EnemySpawnType.Steal;

    /// <summary>
    /// 초기 상태
    /// </summary>
    protected override EnemyStateType InitialState => EnemyStateType.Enter;

    /// <summary>
    /// 초기 행동 상태 자동 진입 여부
    /// </summary>
    protected override bool AutoStart => false;

    #region ----- 초기화 -----

    /// <summary>
    /// 비행, 절도와 행동 상태 초기화
    /// </summary>
    protected override void Init ()
    {
        base.Init( );

        _mover.Init( _animatorHandler );

        //비행 이동 결과를 사운드 담당에 연결
        if ( _enemySound != null )
            _enemySound.InitFlight( _mover, _soundPlayer );

        _bodyColliders = GetComponentsInChildren<Collider>( true );

        //음식 탐색과 확보, 재탈취 설정
        _model = new StealModel(
            _data.SearchDist, _data.TakeDist,
            _data.SearchTime, _data.RetryChance );

        //침입 경로 이동
        _states [ ( int ) EnemyStateType.Enter ] =
            new StealRouteState(
                this, _enterPoints, EnemyStateType.Enter,
                _data.MoveRetryInterval, _data.MoveFailureTime );

        _states [ ( int ) EnemyStateType.Normal ] = new StealSearchState( this );

        //탈출 경로 이동
        _states [ ( int ) EnemyStateType.Escape ] =
            new StealRouteState(
                this, _escapePoints, EnemyStateType.Escape,
                _data.MoveRetryInterval, _data.MoveFailureTime );
    }

    /// <summary>
    /// 씬 배치 참조 연결과 적 초기화
    /// </summary>
    /// <param name="settings">배치 설정</param>
    /// <param name="stock">공급원 재고 중재자</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( StealSceneSettings settings, IngredientStockManager stock )
    {
        //제어 루트를 끄는 잘못된 숨김 설정은 초기화 전에 차단
        if ( _activation == null )
        {
            Debug.LogError(
                $"[적 초기화 실패] {name}: 표시 제어를 연결해 주세요.", this );
            return false;
        }

        if ( _activation.CheckSettings( transform, out string reason ) == false )
        {
            Debug.LogError(
                $"[적 초기화 실패] {name}: {reason}", this );
            return false;
        }

        if ( settings.EnterPoints == null || settings.EnterPoints.Length == 0 ||
            settings.EscapePoints == null || settings.EscapePoints.Length == 0 )
        {
            Debug.LogError( "[적 초기화 실패] " +
                $"{name}: 침입과 탈출 경유점을 연결해 주세요.", this );
            return false;
        }

        _enterPoints = settings.EnterPoints;
        _escapePoints = settings.EscapePoints;
        _spawnPoint = settings.SpawnPoint;
        _stock = stock;
        return base.Init( settings.StartRoom );
    }

    /// <summary>
    /// 활동 상태에 따른 비행 사운드 갱신 요청
    /// </summary>
    void LateUpdate ()
    {
        if ( _enemySound != null )
            _enemySound.UpdateFlight(
                CanRun == true && _currentState != null );
    }


    /// <summary>
    /// 사운드를 초기화하고 등장 승인 전 표시 숨김
    /// </summary>
    protected override void Activate ()
    {
        _activityVersion++;
        if ( _enemySound != null )
            _enemySound.ResetSounds( );

        //승인 전에는 제어 루트를 유지한 채 모델과 몸체 충돌만 숨김
        if ( _currentState == null )
            _activation.Hide( );
    }

    /// <summary>
    /// 이동과 보유 음식을 정리하고 표시 숨김
    /// </summary>
    protected override void Deactivate ()
    {
        _activityVersion++;
        if ( _enemySound != null )
            _enemySound.ResetSounds( );

        ClearTarget( );
        Stop( );

        if ( _isEnding == true )
        {
            _activation.Hide( );
            return;
        }

        _isEnding = true;

        try
        {
            if ( TryReleaseFood( ) == false )
                Debug.LogWarning( $"[절도 종료 실패] " +
                    $"{name} 보유 음식을 해제하지 못했습니다.", this );
        }
        finally
        {
            //보유 정리 중 발생하는 알림을 막은 상태에서 표시 숨김
            _activation.Hide( );
            _isEnding = false;
        }
    }

    /// <summary>
    /// 새 등장 탐색 준비
    /// </summary>
    void PrepareAppearance ()
    {
        _activityVersion++;
        //재탈취 기회는 탐색 재시도가 아닌 새 등장 때만 복구
        Stop( );
        ClearTarget( );
        _model.Reset( );
        _searchElapsed = 0f;
        _candidates.Clear( );
        _targetData.Clear( );
        _checkedItems.Clear( );
        _checkedSources.Clear( );
        _sources.Clear( );
    }

    #endregion


    /// <summary>
    /// 보유 음식 변경에 따른 재탈취/탈출 전환
    /// </summary>
    /// <param name="previousType">이전 보유 방식</param>
    /// <param name="currentType">현재 보유 방식</param>
    void HandleHoldChanged (
        HoldType previousType, HoldType currentType )
    {
        if ( _isEnding == true )
            return;

        ClearHeldItem( );

        //플레이어가 되찾았을 때만 재탈취 기회 사용
        if ( previousType == HoldType.World &&
             currentType == HoldType.View &&
             _model.TryRetry( Random.Range( 0, 10000 ) / 10000f ) == true )
        {
            ChangeState( EnemyStateType.Normal );
            return;
        }

        ChangeState( EnemyStateType.Escape );
    }

    /// <summary>
    /// 보유 음식 비활성화 후 탈출 전환
    /// </summary>
    void HandleFoodUnavailable ()
    {
        ClearHeldItem( );
        if ( _isEnding == false && CanRun == true )
            ChangeState( EnemyStateType.Escape );
    }

    #region ----- 이동 -----

    /// <summary>
    /// 비행 이동
    /// </summary>
    /// <param name="position">목적지</param>
    /// <returns>이동 적용 여부</returns>
    public bool MoveTo ( Vector3 position )
    {
        Vector3 previousPosition = _mover.Position;		//이동 전 위치
        Vector3 nextPosition = Vector3.MoveTowards(
            previousPosition, position, _data.MoveSpeed * Time.deltaTime );

        if ( _mover.UpdateMove( nextPosition ) == false )
            return false;

        Vector3 direction = _mover.MoveDir;
        direction.y = 0f;

        if ( direction.sqrMagnitude > 0.0001f )
        {
            _mover.Rotate(
                Quaternion.LookRotation( direction ), _data.RotateSpeed );
        }

        return true;
    }

    /// <summary>
    /// 이동 정지
    /// </summary>
    public void Stop ()
    {
        _mover.Stop( );
    }

    /// <summary>
    /// 탈출 시작 시 상승 위치 조회
    /// </summary>
    /// <returns>현재 위치 위 상승 지점</returns>
    public Vector3 GetEscapeRisePosition ()
    {
        return _mover.Position + Vector3.up * _data.ApproachHeight;
    }

    /// <summary>
    /// 목적지 도착 여부
    /// </summary>
    /// <param name="position">목적지</param>
    /// <returns>도착 여부</returns>
    public bool HasArrived ( Vector3 position )
    {
        //가까워도 막힌 구간을 통과한 것으로 처리하지 않음
        return _mover.isActiveAndEnabled == true &&
            Vector3.Distance( _mover.Position, position ) <= _data.ArriveDist &&
            _mover.IsPathBlocked( _mover.Position, position ) == false;
    }

    #endregion

    #region ----- 아이템 탐색/확보 -----

    /// <summary>
    /// 음식 확보에 필요한 몸체 위치 계산
    /// </summary>
    /// <param name="foodPosition">음식 중심 위치</param>
    /// <returns>보유 지점을 음식에 맞춘 몸체 위치</returns>
    Vector3 GetTakePosition ( Vector3 foodPosition )
    {
        Vector3 holdOffset = _holdPoint.position - _mover.Position;
        return foodPosition - holdOffset;
    }

    /// <summary>
    /// 음식 접근 경로와 거리 정보 생성
    /// </summary>
    /// <param name="target">개별 음식 또는 공급원</param>
    /// <param name="foodPosition">음식 중심 위치</param>
    /// <param name="isDescending">하강 중인지 여부</param>
    /// <returns>접근 가능 여부와 거리 정보</returns>
    StealTargetData GetTargetData (
        ItemStealTargetData target, Vector3 foodPosition, bool isDescending )
    {
        Vector3 takePosition = GetTakePosition( foodPosition );
        Vector3 approachPosition =
            takePosition + Vector3.up * _data.ApproachHeight;

        //접근 중에는 상단 접근과 하강 구간을 함께 확인
        bool canApproach = isDescending == true
            ? _mover.IsPathBlocked( _mover.Position, takePosition ) == false
            : _mover.IsPathBlocked( _mover.Position, approachPosition ) == false &&
              _mover.IsPathBlocked( approachPosition, takePosition ) == false;

        return new StealTargetData(
            target.IsSource == true
                ? target.Source.CanSteal == true && target.Source.SuppliesFood == true
                : target.Item.isActiveAndEnabled,
            target.IsSource == true ? HoldType.None : target.Item.CurrentType,
            Vector3.Distance( _mover.Position, foodPosition ), canApproach );
    }

    /// <summary>
    /// 훔칠 음식 아이템 찾기
    /// </summary>
    /// <returns>대상 선정 여부</returns>
    bool TryFindTarget ()
    {
        _candidates.Clear( );
        _targetData.Clear( );
        _checkedItems.Clear( );
        _checkedSources.Clear( );
        if ( _stock != null )
            _stock.GetSources( _sources );
        else
            _sources.Clear( );

        Collider [ ] hits = _mover.FindItems( _data.SearchDist, _data.ItemMask );

        for ( int i = 0; i < hits.Length; i++ )
        {
            bool isSourcePart = false;
            foreach ( IngredientSource source in _sources )
            {
                if ( source.Contains( hits [ i ].transform ) == false )
                    continue;

                isSourcePart = true;
                if ( _checkedSources.Add( source ) == false ||
                     source.CanSteal == false || source.SuppliesFood == false )
                    continue;

                ItemStealTargetData sourceTarget = new( source );
                _candidates.Add( sourceTarget );
                _targetData.Add( GetTargetData(
                    sourceTarget, source.StealPoint.position, false ) );
            }

            //공급원 본체와 하위 배치물은 개별 음식 후보에서 제외
            if ( isSourcePart == true )
                continue;

            ItemCarry item = hits [ i ].GetComponentInParent<ItemCarry>( );

            //아이템의 여러 콜라이더가 잡혀도 한 번만 처리
            if ( item == null || _checkedItems.Add( item ) == false )
                continue;

            if ( item.TryGetComponent<Food>( out _ ) == false )
                continue;

            ItemWorldController itemView = item.GetComponent<ItemWorldController>( );
            Vector3 foodPosition = itemView.GetBounds( ).center;

            //모델이 고른 후보 번호로 아이템을 찾도록 순서 유지
            ItemStealTargetData itemTarget = new( item );
            _candidates.Add( itemTarget );
            _targetData.Add( GetTargetData( itemTarget, foodPosition, false ) );
        }

        int selectedIndex = _model.SelectTarget( _targetData );

        if ( selectedIndex < 0 )
            return false;

        ItemStealTargetData selected = _candidates [ selectedIndex ];
        _target = selected.Item;
        _targetSource = selected.Source;
        _targetView = _target != null ? _target.GetComponent<ItemWorldController>( ) : null;
        _isDescending = false;
        return true;
    }

    /// <summary>
    /// 접근 대상 참조 해제
    /// </summary>
    void ClearTarget ()
    {
        //확보한 음식의 보유 관계는 유지
        _target = null;
        _targetSource = null;
        _targetView = null;
        _isDescending = false;
    }

    /// <summary>
    /// 보유 음식 구독과 참조 해제
    /// </summary>
    void ClearHeldItem ()
    {
        ItemHolder holder = _foodHolder;
        _foodHolder = null;
        if ( _heldItem != null )
        {
            _heldItem.OnHoldChanged -= HandleHoldChanged;
            _heldItem.OnUnavailable -= HandleFoodUnavailable;
        }

        _heldItem = null;
        holder?.Dispose( );
    }


    /// <summary>
    /// 음식 확보와 탈출 시작
    /// </summary>
    /// <param name="data">대상 정보</param>
    /// <param name="foodPosition">음식 중심 위치</param>
    /// <returns>확보 성공 여부</returns>
    bool TryTakeTarget ( StealTargetData data, Vector3 foodPosition )
    {
        //탐색은 몸체 기준
        //확보는 보유 지점과 음식 중심 사이 거리로 판정
        float holdDist = Vector3.Distance( _holdPoint.position, foodPosition );
        bool isBlocked = _mover.IsTakeBlocked( _holdPoint.position, foodPosition );

        if ( _model.CanTake( data, holdDist, isBlocked ) == false )
            return false;

        uint version = _activityVersion;
        IEnemyState state = _currentState;
        IngredientSource source = _targetSource;
        ItemCarry item;
        bool taken;
        if ( source != null )
        {
            taken = source.TrySteal(
                () => CanAcquireFood( version, state ),
                candidate => source.StealPoint != null &&
                    source.StealPoint.gameObject.activeInHierarchy == true &&
                    Vector3.Distance( _holdPoint.position, source.StealPoint.position ) <= _data.TakeDist &&
                    _mover.IsTakeBlocked( _holdPoint.position, source.StealPoint.position ) == false &&
                    TryAcquireFood( candidate, version, state ),
                candidate => _foodHolder != null && _foodHolder.IsHolding( candidate ) == true,
                out item );
        }
        else
        {
            item = _target;
            taken = TryAcquireFood( item, version, state );
        }

        if ( taken == false || CanAcquireFood( version, state ) == false ||
             HasHeldFood( ) == false )
        {
            //공급 확정 뒤 요청이 취소됐다면 같은 활동에 남은 음식만 해제
            if ( _activityVersion == version && _heldItem == item &&
                 TryReleaseFood( ) == false )
                EndFailedMove( );
            return false;
        }

#if UNITY_EDITOR
        if ( source != null )
            PlayDebugHistory.Record( "절도 적 공급원 절도", this,
                $"공급원 {source.name} / 획득 1개 / {source.GetStockStatus( )}" );
#endif

        //음식 보유와 활성 상태가 확인된 경우 확보음 재생
        if ( _enemySound != null )
            _enemySound.PlayTake( );

        ClearTarget( );
        ChangeState( EnemyStateType.Escape );
        return true;
    }

    /// <summary>
    /// 같은 탐색 활동의 음식 확보 요청 확인
    /// </summary>
    /// <param name="version">확보를 시작한 활동 회차</param>
    /// <param name="state">확보를 시작한 행동 상태</param>
    /// <returns>확보 요청 유지 여부</returns>
    bool CanAcquireFood ( uint version, IEnemyState state )
    {
        return CanRun == true && _isEnding == false && _activityVersion == version &&
            _currentState == state && state != null && state.StateType == EnemyStateType.Normal &&
            _model.IsSearchExpired == false &&
            _holdPoint != null && _holdPoint.gameObject.activeInHierarchy == true;
    }

    /// <summary>
    /// 음식 집기 완료 후 실제 보유 등록
    /// </summary>
    /// <param name="item">확보할 음식</param>
    /// <param name="version">확보를 시작한 활동 회차</param>
    /// <param name="state">확보를 시작한 행동 상태</param>
    /// <returns>실제 보유 등록 여부</returns>
    bool TryAcquireFood ( ItemCarry item, uint version, IEnemyState state )
    {
        if ( CanAcquireFood( version, state ) == false )
            return false;

        ItemHolder holder = new( HoldType.World );
        bool registered = false;
        try
        {
            if ( holder.TryPickUp( item, _holdPoint, _bodyColliders ) == false ||
                 CanAcquireFood( version, state ) == false ||
                 holder.IsHolding( item ) == false )
                return false;

            _foodHolder = holder;
            _heldItem = item;
            item.OnHoldChanged += HandleHoldChanged;
            item.OnUnavailable += HandleFoodUnavailable;
            registered = true;
            return true;
        }
        finally
        {
            //집기 알림에서 적이 종료돼도 보유 전환이 끝난 뒤 자신의 음식만 해제
            if ( registered == false )
                holder.Dispose( );
        }
    }

    /// <summary>
    /// 현재 개별 음식 또는 공급원의 확보 위치 조회
    /// </summary>
    /// <param name="foodPosition">확보할 위치</param>
    /// <param name="data">현재 접근 정보</param>
    /// <returns>유효한 대상 조회 여부</returns>
    bool TryGetCurrentTarget ( out Vector3 foodPosition, out StealTargetData data )
    {
        foodPosition = default;
        data = default;
        ItemStealTargetData target;
        if ( _targetSource != null )
        {
            if ( _targetSource.CanSteal == false || _targetSource.SuppliesFood == false )
                return false;
            foodPosition = _targetSource.StealPoint.position;
            target = new ItemStealTargetData( _targetSource );
        }
        else
        {
            if ( _target == null || _targetView == null )
                return false;
            foodPosition = _targetView.GetBounds( ).center;
            target = new ItemStealTargetData( _target );
        }

        data = GetTargetData( target, foodPosition, _isDescending );
        return true;
    }

    /// <summary>
    /// 음식 탐색 준비
    /// </summary>
    public void BeginSearch ()
    {
        _activityVersion++;
        ClearTarget( );

        //상태 진입 직후 첫 탐색 실행
        _searchElapsed = _data.SearchInterval;
        Stop( );
    }

    /// <summary>
    /// 음식 탐색/접근과 확보
    /// </summary>
    public void UpdateSearch ()
    {
        //후보 교체로 탐색 제한시간이 늘어나지 않도록 누적
        _model.UpdateSearch( Time.deltaTime );

        if ( _model.IsSearchExpired == true )
        {
            ClearTarget( );
            ChangeState( EnemyStateType.Escape );
            return;
        }

        if ( _target == null && _targetSource == null )
        {
            _searchElapsed += Time.deltaTime;
            if ( _searchElapsed < _data.SearchInterval )
                return;

            _searchElapsed = 0f;
            if ( TryFindTarget( ) == false )
                return;
        }

        if ( TryGetCurrentTarget( out Vector3 foodPosition, out StealTargetData data ) == false )
        {
            ClearTarget( );
            Stop( );
            return;
        }

        //접근 도중 플레이어가 집거나 경로가 막히면 다시 탐색
        if ( _model.CanTarget( data ) == false )
        {
            ClearTarget( );
            Stop( );
            return;
        }

        Vector3 takePosition = GetTakePosition( foodPosition );

        //음식 위쪽에 접근한 뒤 확보 위치로 하강
        if ( _isDescending == false )
        {
            Vector3 approachPosition =
                takePosition + Vector3.up * _data.ApproachHeight;
            if ( HasArrived( approachPosition ) == false )
            {
                if ( MoveTo( approachPosition ) == false )
                {
                    //다시 탐색하되 전체 탐색 제한시간은 유지
                    ClearTarget( );
                    Stop( );
                }
                return;
            }
            _isDescending = true;
        }

        //음식이 확보 범위로 들어오면 추가 이동 없이 집기
        uint version = _activityVersion;
        IEnemyState state = _currentState;
        if ( TryTakeTarget( data, foodPosition ) == true )
            return;

        if ( CanAcquireFood( version, state ) == false )
            return;

        if ( MoveTo( takePosition ) == false )
        {
            ClearTarget( );
            Stop( );
        }
    }


    /// <summary>
    /// 자신의 보유 지점에 연결된 물건 확인
    /// </summary>
    /// <param name="item">검사할 물건</param>
    /// <returns>보유 지점 연결 여부</returns>
    bool IsHeldByThisPoint ( ItemCarry item )
    {
        return item != null && item.CurrentType == HoldType.World &&
               item.transform.parent == _holdPoint;
    }

    /// <summary>
    /// 확보한 음식의 실제 보유 여부
    /// </summary>
    /// <returns>자신의 보유 지점에 있는 활성 음식 여부</returns>
    bool HasHeldFood ()
    {
        return IsHeldByThisPoint( _heldItem ) &&
               _heldItem.gameObject.activeInHierarchy == true &&
               _foodHolder != null && _foodHolder.IsHolding( _heldItem ) == true;
    }

    #endregion

    #region ----- 종료 -----

    /// <summary>
    /// 보유 음식 해제
    /// </summary>
    /// <returns>보유 음식이 없거나 해제에 성공했는지 여부</returns>
    bool TryReleaseFood ()
    {
        ItemCarry item = _heldItem;
        if ( IsHeldByThisPoint( item ) == false )
        {
            ClearHeldItem( );
            return true;
        }

        //해제 실패 시 참조 유지
        item.OnHoldChanged -= HandleHoldChanged;
        item.OnUnavailable -= HandleFoodUnavailable;
        if ( item.TryRelease( ) == false )
        {
            item.OnHoldChanged += HandleHoldChanged;
            item.OnUnavailable += HandleFoodUnavailable;
            return false;
        }

        ClearHeldItem( );
        return true;
    }

    /// <summary>
    /// 이동 실패 종료
    /// </summary>
    public void EndFailedMove ()
    {
        if ( _isEnding == true )
            return;

        _isEnding = true;
        try
        {
            Stop( );
            ClearTarget( );

            //해제 실패 시 적을 숨기지 않고 다음 갱신에서 다시 정리
            //이동 실패는 음식 소비나 절도 성공으로 처리하지 않음
            if ( TryReleaseFood( ) == false )
                return;

            //이번 활동을 정리하고 숨긴 상태로 다음 발생 준비
            ResetAppearance( );
        }
        finally
        {
            _isEnding = false;
        }
    }

    /// <summary>
    /// 출구 도착 후 음식 소비와 퇴장
    /// </summary>
    public void CompleteEscape ()
    {
        if ( _isEnding == true || _currentState == null ||
             _currentState.StateType != EnemyStateType.Escape )
            return;

        Transform exit = _escapePoints [ _escapePoints.Length - 1 ];
        if ( exit == null || HasArrived( exit.position ) == false )
            return;

        _isEnding = true;

        try
        {
            IEnemyState state = _currentState;
            ItemCarry item = _heldItem;
            bool isComplete = _model.IsStealComplete( true, HasHeldFood( ) );

            if ( TryReleaseFood( ) == false )
                return;

            //보유 해제 알림에서 종료된 행동의 소비를 이어가지 않음
            if ( isActiveAndEnabled == false || _currentState != state )
                return;

            //출구까지 보유한 음식만 소비하며 소비 실패는 별도로 기록
            if ( isComplete == true && item != null &&
                 item.isActiveAndEnabled == true && item.IsHold == false )
            {
                if ( item.TryGetComponent( out Food food ) == false ||
                    food.Consume( ) == false )
                    Debug.LogWarning( $"[절도 소비 실패] " +
                        $"{name} 음식이 소비되지 않았습니다.", this );
            }

            //이번 활동을 정리하고 숨긴 상태로 다음 발생 준비
            ResetAppearance( );
        }
        finally
        {
            _isEnding = false;
        }
    }

    /// <summary>
    /// 침입 완료 후 음식 탐색 시작
    /// </summary>
    public void CompleteEnter ()
    {
        _model.BeginSearch( );
        ChangeState( EnemyStateType.Normal );
    }

    #endregion

    #region ----- 발생 -----


    /// <summary>
    /// 시작 조건과 거절 사유 조회
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>시작 가능 여부</returns>
    public bool CheckStart ( Room room, out string reason )
    {
        if ( CheckAppearanceStart( room, out reason ) == false )
            return false;
        if ( _isEnding == true )
            reason = "이전 절도 활동을 정리 중입니다.";
        return reason.Length == 0;
    }

    /// <summary>
    /// 실제 등장 준비 실패 사유를 포함하여 새 등장 시작
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="reason">시작 실패 사유</param>
    /// <returns>시작 성공 여부</returns>
    public bool TryStart ( Room room, out string reason )
    {
        if ( CheckStart( room, out reason ) == false )
            return false;

        if ( _mover.CheckRoute( _enterPoints, out reason ) == false )
        {
            Debug.LogError( $"[절도 시작 실패] " +
                $"{name} 침입 경로: {reason}", this );
            return false;
        }

        if ( _mover.CheckRoute( _escapePoints, out reason ) == false )
        {
            Debug.LogError( $"[절도 시작 실패] " +
                $"{name} 탈출 경로: {reason}", this );
            return false;
        }

        //숨긴 모델 아래의 정상 보유 지점은 허용
        if ( _activation.IsAvailableWhenShown( _holdPoint ) == false ||
             _data.MoveSpeed <= 0f || _data.ArriveDist <= 0f ||
             _data.MoveRetryInterval <= 0f || _data.MoveFailureTime <= 0f )
        {
            reason = "보유 지점과 이동 설정을 확인해 주세요.";
            Debug.LogError( $"[절도 시작 실패] " +
                $"{name} 보유 지점과 이동 설정을 확인해 주세요.", this );
            return false;
        }

        //이전 음식이 남았다면 해제 성공 후에만 새 등장 시작
        _isEnding = true;

        bool released;
        try
        {
            released = TryReleaseFood( );
        }
        finally
        {
            _isEnding = false;
        }

        if ( released == false )
        {
            reason = "이전 보유 음식을 해제하지 못했습니다.";
            return false;
        }
        if ( CheckStart( room, out reason ) == false )
            return false;

        //이전 보유 음식 해제가 끝난 뒤 침입 시작 위치 복원
        if ( _mover.TryResetPosition( _spawnPoint ) == false )
        {
            reason = "등장 지점의 연결과 장애물 겹침을 확인해 주세요.";
            Debug.LogWarning(
                $"[절도 적 등장 실패] {name}: 등장 지점의 연결과 장애물 겹침을 확인해 주세요.",
                this );
            return false;
        }

        PrepareAppearance( );
        //등장 위치와 회차 초기화가 끝난 뒤에만 표시
        _activation.Show( );
        ChangeState( InitialState );

        //표시 복구 중 상태 진입이 실패한 경우 다시 숨김
        if ( _currentState == null )
        {
            reason = "침입 상태에 진입하지 못했습니다.";
            _activation.Hide( );
            return false;
        }

        return true;
    }


    /// <summary>
    /// 새 등장 시작
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>시작 여부</returns>
    public bool TryStart ( Room room )
    {
        return TryStart( room, out _ );
    }

    /// <summary>
    /// 새 등장 가능 여부
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>시작 가능 여부</returns>
    public bool CanStart ( Room room )
    {
        return CheckStart( room, out _ );
    }

    #endregion


}
