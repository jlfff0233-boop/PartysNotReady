using System;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Random = UnityEngine.Random;

/// <summary>
/// NPC 이동 모듈 - 로컬 및 방 이동, 포획 따라가기와 해제 후 착지 복구
/// </summary>
[RequireComponent ( typeof ( Rigidbody ) )]
public class NpcMover : CharacterMover
{
    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] Collider _collider;		//몸체 콜라이더

    [Header ( "----- 착지 -----" )]
    [SerializeField] LayerMask _groundLayerMask;		//일반 이동으로 복귀할 지면
    [SerializeField, Min ( 0.01f )] float _checkDist = 0.3f;		//착지 후 NavMesh 확인 거리

    NpcData _data;		//일반 이동 설정
    Vector3 _destination;		//로컬 목적지
    float _moveElapsed;		//로컬 이동 경과 시간
    bool _colliderEnabled;		//포획 전 콜라이더 상태
    bool _colliderIsTrigger;		//포획 전 Trigger 설정
    bool _agentEnabled;		//포획 전 에이전트 상태
    bool _isFalling;		//해제 후 낙하 중
    Quaternion _captureRotation;		//해제 시 복구할 포획 전 수평 회전
#if UNITY_EDITOR
    string _debugLandingReason = "낙하 전";		//최근 착지 검사 결과
#endif

    Room _room;		//로컬 이동 시작 방
    Room _destinationRoom;		//방 이동 목적지
    Rigidbody _rigid;		//낙하 물리

    RouteNavigator _navigator;		//방 이동 경로 진행
    CaptureFollower _follower;		//보유 지점 따라가기

    /// <summary>
    /// 낙하 중 여부
    /// </summary>
    public bool IsFalling => _isFalling;

    /// <summary>
    /// 포획 따라가기 시작 가능 여부
    /// </summary>
    public bool CanBeginCapture => CheckCanBeginCapture( );

    /// <summary>
    /// 포획 따라가기 가능 여부
    /// </summary>
    public bool CanFollow => CheckCanFollow( );

    /// <summary>
    /// 방 이동 경로 사용 가능 여부
    /// </summary>
    public bool CanMoveRooms => _navigator != null;

    /// <summary>
    /// 방 이동 상태
    /// </summary>
    public RouteStatusType RoomMoveStatus => _navigator?.Status ?? RouteStatusType.Idle;

    /// <summary>
    /// 따라가기 중단 이벤트
    /// </summary>
    public event Action OnCaptureInterrupted;


    /// <summary>
    /// NPC 이동과 포획 물리 초기화
    /// </summary>
    public override void Init ( )
    {
        base.Init ( );

        _rigid = GetComponent<Rigidbody> ( );
        _follower = new CaptureFollower( transform );

        //평상시는 Agent 또는 제자리 대기
        _rigid.isKinematic = true;
        _rigid.useGravity = false;
    }

    /// <summary>
    /// NPC 이동 설정과 공통 경로 연결
    /// </summary>
    /// <param name="data">일반 이동 설정</param>
    /// <param name="routeGraph">방 이동 경로 그래프, 없으면 로컬 이동만 사용</param>
    public void InitNormalMove ( NpcData data , RouteGraph routeGraph )
    {
        _data = data;
        if ( _data == null )
        {
            Debug.LogWarning (
                $"[NPC] {name} 설정 데이터가 없어 일반 이동을 시작하지 않습니다." , this );
            return;
        }

        if ( routeGraph == null )
        {
            Debug.LogWarning (
                $"[NPC] {name} 파티룸 또는 경로 그래프가 없어 로컬 이동만 실행합니다." , this );
            return;
        }

        RouteBuilder builder = new (
            routeGraph , PathChecker ,
            _data.CorrectionDist , _data.RoomConnectDist );

        _navigator = new RouteNavigator (
            this , builder , _data.MoveSpeed , _data.RotateSpeed ,
            _data.ArriveDist , _data.RoomRetryInterval ,
            _data.RoomMoveTimeLimit , _data.RoomStuckTimeLimit );
    }

    private void LateUpdate ( )
    {
        if ( _follower == null || _follower.IsFollowing == false )
            return;

        //따라갈 수 없으면 포획 취소 요청
        if ( isActiveAndEnabled == false || _follower.TryFollow( ) == false )
            OnCaptureInterrupted?.Invoke( );
    }

    /// <summary>
    /// 이동 표시 정리와 포획 따라가기 중단 알림
    /// </summary>
    protected override void OnDisable ()
    {
        base.OnDisable( );

        if ( _follower != null && _follower.IsFollowing == true )
            OnCaptureInterrupted?.Invoke( );
    }

    /// <summary>
    /// 착지 충돌 확인
    /// </summary>
    /// <param name="collision">충돌 정보</param>
    private void OnCollisionEnter ( Collision collision )
    {
        CheckLanding ( collision );
    }

    /// <summary>
    /// 접촉 중 착지 확인
    /// </summary>
    /// <param name="collision">충돌 정보</param>
    private void OnCollisionStay ( Collision collision )
    {
        CheckLanding ( collision );
    }


    #region ----- 일반 이동 -----


    /// <summary>
    /// 일반 이동 중단과 목적지 정리
    /// </summary>
    public void StopNormalMove ( )
    {
        _navigator?.Cancel ( );
        _room = null;
        _destinationRoom = null;
        _moveElapsed = 0f;

        //비활성화와 포획 중에는 이동 가능한 경우에만 정지
        if ( CanNavigate == true )
            Stop ( );
    }

    /// <summary>
    /// 같은 방 안의 유효 목적지로 이동 시작
    /// </summary>
    /// <param name="room">로컬 이동을 허용할 현재 방</param>
    /// <returns>이동 시작 여부</returns>
    public bool TryBeginLocalMove ( Room room )
    {
        Vector3 origin = Position;

        if ( _data == null || room == null || CanNavigate == false )
            return false;

        for ( int i = 0 ; i < _data.LocalMoveTryCount ; i++ )
        {
            Vector2 offset = Random.insideUnitCircle * _data.LocalMoveRadius;
            Vector3 candidate = origin + new Vector3 ( offset.x , 0f , offset.y );

            if ( room.Contains ( candidate ) == false )
                continue;

            if ( TryGetMovePosition (
                candidate , _data.CorrectionDist , out Vector3 destination ) == false )
                continue;

            //NavMesh 보정으로 방이나 탐색 반경 밖으로 이동한 후보 제외
            if ( room.Contains ( destination ) == false )
                continue;

            float distance = Vector3.Distance ( origin , destination );

            if ( distance <= _data.ArriveDist || distance > _data.LocalMoveRadius )
                continue;

            //부분 경로는 이동 후보에서 제외
            if ( PathChecker.TryGetDistance ( origin , destination , out _ ) == false )
                continue;

            if ( MoveTo (
                destination , _data.MoveSpeed , _data.ArriveDist ) == false )
                continue;

            _room = room;
            _destination = destination;
            _moveElapsed = 0f;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 로컬 이동 진행과 도착/실패 판정
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <returns>이동 결과</returns>
    public RouteStatusType UpdateLocalMove ( float deltaTime )
    {
        if ( _data == null || CanNavigate == false )
            return RouteStatusType.Failed;

        _moveElapsed += deltaTime;

        if ( _room == null ||
             _room.Contains ( Position ) == false ||
             _moveElapsed >= _data.LocalMoveTimeLimit )
        {
            return RouteStatusType.Failed;
        }

        //경로 요청 중에도 위의 전체 제한시간은 진행
        if ( PathPending == true )
            return RouteStatusType.Moving;

        if ( PathStatus != NavMeshPathStatus.PathComplete )
        {
            return RouteStatusType.Failed;
        }

        float distance = Vector3.Distance ( Position , _destination );

        //경로가 끝났더라도 실제 목적지 근처인지 함께 확인
        if ( distance <= _data.ArriveDist &&
             ( HasPath == false || RemainingDistance <= _data.ArriveDist ) )
        {
            return RouteStatusType.Arrived;
        }

        if ( HasPath == false )
        {
            return RouteStatusType.Failed;
        }

        RotateAlongMovement ( _data.RotateSpeed );
        return RouteStatusType.Moving;
    }

    /// <summary>
    /// 목적지 방 내부의 이동 가능한 랜덤 위치 조회
    /// </summary>
    /// <param name="room">목적지 방</param>
    /// <param name="position">이동 가능한 목적지</param>
    /// <returns>목적지 조회 성공 여부</returns>
    bool TryGetRoomDestination ( Room room , out Vector3 position )
    {
        position = default;

        if ( room == null || room.isActiveAndEnabled == false )
        {
            Debug.LogWarning ( $"[NPC 방 목적지 실패] {name} / 목적지 방이 없거나 비활성" , this );
            return false;
        }

        //출발 위치도 동일 에이전트 조건으로 NavMesh에 맞춤
        if ( TryGetMovePosition (
            Position , _data.CorrectionDist , out Vector3 origin ) == false )
        {
            Debug.LogWarning (
                $"[NPC 방 목적지 실패] {name} / 출발 위치 NavMesh 보정 실패 / 위치: {Position} / 보정 거리: {_data.CorrectionDist}" , this );
            return false;
        }

        int floorRejected = 0;
        int navMeshRejected = 0;
        int roomRejected = 0;
        int pathRejected = 0;
        Dictionary<string, int> pathFailures = new( );
        string firstPathFailure = string.Empty;
        for ( int i = 0 ; i < _data.RoomMoveTryCount ; i++ )
        {
            if ( room.TryGetRandomFloorProbe ( out Vector3 candidate , out float probeDistance ) == false )
            {
                Debug.LogWarning ( $"[NPC 방 목적지 실패] {name} / 방: {room} / 방 내부 바닥 탐색 범위 조회 실패" , this );
                return false;
            }

            //다른 층으로 보정되지 않도록 지면을 먼저 탐색
            if ( Physics.Raycast (
                candidate , Vector3.down , out RaycastHit floor ,
                probeDistance , _groundLayerMask , QueryTriggerInteraction.Ignore ) == false )
            {
                floorRejected++;
                continue;
            }

            if ( room.Contains ( floor.point ) == false )
            {
                roomRejected++;
                continue;
            }

            if ( TryGetMovePosition (
                floor.point , _data.CorrectionDist , out Vector3 destination ) == false )
            {
                navMeshRejected++;
                continue;
            }

            //NavMesh 보정으로 다른 방이나 중첩 영역에 들어간 후보 제외
            if ( RoomRegister.TryGetRoom ( destination , out Room actualRoom ) == false ||
                 actualRoom != room )
            {
                roomRejected++;
                continue;
            }

            //고립된 NavMesh나 부분 경로는 후보에서 제외
            if ( PathChecker.TryGetDistance ( origin , destination , out _ ) == false )
            {
                pathRejected++;
                string reason = PathChecker.FailureReason;
                pathFailures.TryGetValue( reason, out int count );
                pathFailures [ reason ] = count + 1;
                if ( firstPathFailure.Length == 0 )
                    firstPathFailure = $"출발 {origin:F3} / 목적지 {destination:F3} / {PathChecker.FailureDetails}";
                continue;
            }

            position = destination;
            return true;
        }

        //일반 빌드에도 실패 원인을 남김
        string pathSummary = string.Empty;
        foreach ( var failure in pathFailures )
            pathSummary += $" / {failure.Key}: {failure.Value}";
        Debug.LogWarning (
            $"[NPC 방 목적지 실패] {name} / 방: {room} / 지면: {floorRejected} / NavMesh: {navMeshRejected} / 방: {roomRejected} / 경로: {pathRejected}" +
            $"{pathSummary}\n대표 실패: {firstPathFailure}" , this );
        return false;
    }

    /// <summary>
    /// 목적지 방으로 경로 이동 요청
    /// </summary>
    /// <param name="destination">NPC가 선택한 목적지 방</param>
    /// <param name="currentRoom">출발 방</param>
    /// <returns>경로 이동 요청 여부</returns>
    public bool TryBeginRoomMove ( Room destination , Room currentRoom )
    {
        if ( CanMoveRooms == false || CanNavigate == false || destination == null )
        {
            Debug.LogWarning (
                $"[NPC 방 이동 요청 실패] {name} / 경로 사용 가능: {CanMoveRooms} / 이동 가능: {CanNavigate} / 목적지 방: {destination}" , this );
            return false;
        }

        if ( TryGetRoomDestination ( destination , out Vector3 position ) == false )
            return false;

        _destinationRoom = destination;


        _navigator.Begin ( position );

        if ( _navigator.Status == RouteStatusType.Failed )
            Debug.LogWarning ( $"[NPC 방 이동 실패] {name} / {_navigator.FailureReason}" , this );

        return true;
    }

    /// <summary>
    /// 방 이동 진행과 결과 반환
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <returns>이동 결과</returns>
    public RouteStatusType UpdateRoomMove ( float deltaTime )
    {
        if ( _navigator == null || CanNavigate == false ||
             _destinationRoom == null || _destinationRoom.isActiveAndEnabled == false )
        {
            return RouteStatusType.Failed;
        }

        RouteStatusType status = _navigator.UpdateMove ( deltaTime );

        if ( status == RouteStatusType.Moving )
            return status;

        if ( status == RouteStatusType.Failed )
            Debug.LogWarning ( $"[NPC 방 이동 실패] {name} / {_navigator.FailureReason}" , this );

        return status;
    }

    #endregion

    #region ----- 포획/해제 -----

    /// <summary>
    /// 포획 따라가기 시작
    /// </summary>
    /// <param name="carryPoint">보유 지점</param>
    /// <returns>따라가기 시작 여부</returns>
    public bool TryBeginCapture ( Transform carryPoint )
    {
        if ( CanBeginCapture == false || _follower.TryBegin( carryPoint ) == false )
            return false;

        //보유 지점 회전으로 덮어쓰기 전에 이번 포획의 정상 방향 보관
        _captureRotation = Quaternion.Euler( 0f, transform.eulerAngles.y, 0f );
#if UNITY_EDITOR
        _debugLandingReason = "포획 따라가기 중";
#endif
        //해제 시 복구할 충돌 설정 보관
        _colliderEnabled = _collider.enabled;
        _colliderIsTrigger = _collider.isTrigger;

        //몸체 Collider를 구조 탐지에 사용
        _collider.isTrigger = true;
        _collider.enabled = true;

        //포획 전 활성 상태였던 에이전트만 착지 후 복구
        _agentEnabled = _agent.enabled;

        //따라가기 중 이전 경로가 재개되지 않도록 정지
        Stop ( );

        _agent.enabled = false;

        _rigid.isKinematic = true;
        _rigid.useGravity = false;

        _isFalling = false;
        if ( _follower.TryFollow( ) == true )
            return true;

        EndCapture( Vector3.zero );
        return false;
    }

    /// <summary>
    /// 따라가기 종료와 낙하 시작
    /// </summary>
    /// <param name="impulse">해제 충격량</param>
    public void EndCapture ( Vector3 impulse )
    {
        if ( _follower == null || _follower.IsFollowing == false )
            return;

        _follower.End( );

        //착지나 NavMesh 조회 실패와 무관하게 보유 지점의 기울기 복구
        _rigid.rotation = _captureRotation;
        _isFalling = true;
#if UNITY_EDITOR
        _debugLandingReason = "자세 복원 완료 / 지면 접촉 대기";
        PlayDebugHistory.Record( "NPC 낙하 시작", this,
            $"위치 {Position:F3} / 해제 충격 {impulse:F3} / 복원 회전 {_captureRotation.eulerAngles:F1}" );
#endif

        //낙하 전에 원래 충돌 설정 복구
        _collider.isTrigger = _colliderIsTrigger;
        _collider.enabled = _colliderEnabled;

        //착지 전에는 Agent를 복구하지 않음
        _rigid.isKinematic = false;
        _rigid.useGravity = true;

        Physics.SyncTransforms ( );

        //비활성화 중 해제되면 재활성화 후 낙하
        if ( gameObject.activeInHierarchy == false )
            return;

        _rigid.linearVelocity = Vector3.zero;
        _rigid.angularVelocity = Vector3.zero;
        _rigid.AddForce ( impulse , ForceMode.Impulse );
    }

    /// <summary>
    /// 포획 따라가기 가능 여부
    /// </summary>
    /// <returns>따라가기 가능 여부</returns>
    bool CheckCanFollow ()
    {
        return isActiveAndEnabled && _follower != null && _follower.CanFollow;
    }

    /// <summary>
    /// 포획 따라가기 시작 조건
    /// </summary>
    /// <returns>시작 가능 여부</returns>
    bool CheckCanBeginCapture ()
    {
        return isActiveAndEnabled && _isFalling == false &&
            _rigid != null && _collider != null && _agent != null &&
            _follower != null && _follower.IsFollowing == false;
    }

    #endregion

    /// <summary>
    /// 착지 확인과 이동 복구
    /// </summary>
    /// <param name="collision">충돌 정보</param>
    void CheckLanding ( Collision collision )
    {
        if ( _isFalling == false )
            return;

        int groundLayer = collision.collider.gameObject.layer;

        if ( ( _groundLayerMask.value & ( 1 << groundLayer ) ) == 0 )
        {
#if UNITY_EDITOR
            _debugLandingReason = $"착지 레이어 아님: {collision.collider.name} / 레이어 {groundLayer}";
#endif
            return;
        }

        //위로 튀는 중에는 착지로 확정하지 않음
        if ( _rigid.linearVelocity.y > 0.1f )
        {
#if UNITY_EDITOR
            _debugLandingReason = "상승 중 / 착지 대기";
#endif
            return;
        }

        bool hasGroundContact = false;
        Vector3 groundPoint = Vector3.zero;		//착지한 바닥의 접촉 위치

        for ( int i = 0 ; i < collision.contactCount ; i++ )
        {
            ContactPoint contact = collision.GetContact ( i );
            if ( contact.normal.y >= 0.6f )
            {
                hasGroundContact = true;
                groundPoint = contact.point;
                break;
            }
        }

        if ( hasGroundContact == false )
        {
#if UNITY_EDITOR
            _debugLandingReason = "지면의 위쪽 접촉면 없음";
#endif
            return;
        }

        Vector3 position = transform.position;

        if ( _agentEnabled == true )
        {
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = _agent.agentTypeID ,
                areaMask = _agent.areaMask
            };

            //몸체 중심 높이와 무관하게 바닥 접촉점에서 NavMesh 조회
            //이동 가능한 위치가 없으면 복원된 자세로 물리 상태 유지
            if ( NavMesh.SamplePosition (
                groundPoint , out NavMeshHit hit , _checkDist , filter ) == false )
            {
#if UNITY_EDITOR
                _debugLandingReason = $"지면 접촉 성공 / NavMesh 조회 실패: 접촉점 {groundPoint:F3} / 거리 {_checkDist}";
#endif
                return;
            }

            position = hit.position;
        }

        _rigid.linearVelocity = Vector3.zero;
        _rigid.angularVelocity = Vector3.zero;
        _rigid.isKinematic = true;
        _rigid.useGravity = false;

        //낙하가 끝난 뒤 이동 가능한 위치에 맞춰 에이전트 재개
        SetPosition ( position );
        Physics.SyncTransforms ( );

        _isFalling = false;

        if ( _agentEnabled == true )
            _agent.enabled = true;
#if UNITY_EDITOR
        _debugLandingReason = "착지와 이동 복구 완료";
        PlayDebugHistory.Record( "NPC 착지 복원", this,
            $"위치 {Position:F3} / 회전 {transform.eulerAngles:F1} / 이동 가능 {CanNavigate}" );
#endif
    }


    /// <summary>
    /// NPC 위치 적용
    /// </summary>
    /// <param name="position">적용할 위치</param>
    public void SetPosition ( Vector3 position )
    {
        transform.position = position;
    }

    /// <summary>
    /// 납치 완료 후 비활성화
    /// </summary>
    public void Hide ( )
    {
        _follower.End( );
        _isFalling = false;
#if UNITY_EDITOR
        _debugLandingReason = "납치 완료 / 비활성화";
#endif

        _rigid.isKinematic = true;
        _rigid.useGravity = false;

        //납치 완료 시에도 원래 설정 복구
        _collider.isTrigger = _colliderIsTrigger;
        _collider.enabled = _colliderEnabled;

        gameObject.SetActive ( false );
    }


#if UNITY_EDITOR
    /// <summary>
    /// NPC 이동 상태 조회
    /// </summary>
    /// <param name="details">표시 항목</param>
    public void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add( new( "이동 시작 방 / 목적 방", $"{EnemyDebugUtility.Name( _room )} / {EnemyDebugUtility.Name( _destinationRoom )}" ) );
        details.Add( new( "로컬 목적지", _destination.ToString( "F2" ) ) );
        details.Add( new( "방 이동", $"{RoomMoveStatus} / {_navigator?.FailureReason}" ) );
        details.Add( new( "착지 대기", _isFalling.ToString( ) ) );
        details.Add( new( "따라가기 / 착지 검사", $"{_follower?.IsFollowing} / {_debugLandingReason}" ) );
        details.Add( new( "Agent 복원", _agent == null ? "초기화 전" :
            $"활성 {_agent.isActiveAndEnabled} / NavMesh 배치 {(_agent.isActiveAndEnabled == true && _agent.isOnNavMesh == true)} / 이동 가능 {CanNavigate}" ) );
    }
#endif

}
