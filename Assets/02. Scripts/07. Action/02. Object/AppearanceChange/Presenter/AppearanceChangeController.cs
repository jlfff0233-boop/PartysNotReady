using UnityEngine;
using System;

/// <summary>
/// 외형 변경 제어 - 후보 배치, 외형 전환과 복원 순서 제어
/// </summary>
[RequireComponent( typeof( ItemWorldController ) )]
public class AppearanceChangeController : MonoBehaviour
{
    [Header( "----- 외형 설정 -----" )]
    [SerializeField] AppearanceMap _map;     //사용 가능한 외형 목록
    [SerializeField] AppearanceData _initialData;     //초기 외형 식별 정보
    [SerializeField] Transform _appearanceRoot;     //외형을 보관할 전용 부모

    [Header( "----- 배치 검사 -----" )]
    [SerializeField] Vector3 _supportDir = Vector3.down;     //월드 지지 표면 탐색 방향
    [SerializeField, Min( 0.001f )] float _supportDistance = 0.1f;     //지지 표면 추가 탐색 거리
    [SerializeField, Min( 0f )] float _surfaceGap = 0.002f;     //표면과 외형 사이 간격
    [SerializeField, Min( 0f )] float _contactDist = 0.001f;     //공간 검사 접촉 허용 거리
    [SerializeField] LayerMask _obstacleMask;     //벽, 바닥, 물건과 캐릭터 레이어

    readonly AppearanceChangeModel _model = new( );		//외형 변경 상태

    ItemWorldController _itemView;		//배치와 충돌 조회
    ItemCarry _carry;		//보유 상태 조회
    AppearanceLinker _bindings;     //공유 외형의 참조 연결과 실패 복구

    AppearanceData _currentData;     //현재 표시 외형 설정
    AppearanceComponents _current;     //현재 표시 외형
    AppearanceComponents _original;     //복원용 외형
    AppearanceComponents _pendingAppearance;     //적용 도중 또는 복구 실패로 보존한 외형
    AppearanceFactory _factory;     //후보 생성과 삭제

    bool _isRunning;     //변경 처리 중 재진입 차단
    bool _bindingFailed;     //참조 복원 실패 후 추가 변경 차단
    string _failureReason = string.Empty;     //최근 실패 사유

    /// <summary>
    /// 복원 정보가 남아 있는 외형 변경 여부
    /// </summary>
    public bool IsChanged => _model.IsChanged;

    /// <summary>
    /// 현재 표시 외형
    /// </summary>
    public AppearanceComponents Current => _current;

    /// <summary>
    /// 복원용으로 보관한 외형
    /// </summary>
    public AppearanceComponents Original => _original;

    /// <summary>
    /// 최근 변경 또는 복원 실패 사유
    /// </summary>
    public string FailureReason => _failureReason;

    /// <summary>
    /// 외형 참조 복구 실패 여부
    /// </summary>
    public bool HasBindingFailure => _bindingFailed;

    /// <summary>
    /// 새 외형으로 변경을 시도할 수 있는지 여부
    /// </summary>
    public bool CanChange => CheckCanChange( );

    private void Awake ()
    {
        //다른 기능의 초기화 완료를 요구하는 검사는 실제 요청 때 수행
        _itemView = GetComponent<ItemWorldController>( );
        _carry = GetComponent<ItemCarry>( );
        _bindings = new AppearanceLinker( gameObject );

        _factory = new AppearanceFactory( );
        _currentData = _initialData;
        if ( SetInitialAppearance( ) == false )
            Debug.LogError( _failureReason, this );
    }

    private void OnDestroy ()
    {
        //정상 종료에서는 현재 외형을 남기고 사용하지 않는 보관 외형만 정리
        //실패 또는 처리 중 제거는 참조가 남아 있을 수 있으므로 보존
        if ( _bindingFailed == false && _isRunning == false )
        {
            if ( _original != _current )
                _factory.DisposeAppearance( _original );
            if ( _pendingAppearance != _current && _pendingAppearance != _original )
                _factory.DisposeAppearance( _pendingAppearance );
        }

        _factory?.Dispose( );
    }


    #region ----- 초기 외형 -----

    /// <summary>
    /// 초기 외형을 찾아 현재 외형으로 연결
    /// </summary>
    /// <returns>초기 외형 연결 여부</returns>
    bool SetInitialAppearance ()
    {
        if ( _appearanceRoot == null || _appearanceRoot == transform ||
             _appearanceRoot.IsChildOf( transform ) == false )
            return Fail( "아이템 자식에 Appearance Root를 지정해 주세요." );

        AppearanceComponents selected = null;
        for ( int i = 0; i < _appearanceRoot.childCount; i++ )
        {
            AppearanceComponents [ ] views = _appearanceRoot.GetChild( i ).GetComponents<AppearanceComponents>( );
            foreach ( AppearanceComponents view in views )
            {
                //비활성 자식도 개수에 포함하여 잘못된 중복 구성을 숨기지 않음
                if ( selected != null )
                    return Fail( "Appearance Root 바로 아래에는 AppearanceComponents가 하나만 있어야 합니다." );
                selected = view;
            }
        }

        if ( selected == null || selected.gameObject.activeInHierarchy == false )
            return Fail( "Appearance Root 바로 아래에 활성 초기 외형과 AppearanceComponents가 필요합니다." );
        if ( AppearanceChecker.Check( selected, out _failureReason ) == false )
            return false;

        _current = selected;
        return true;
    }

    #endregion

    #region ----- 외형 배치 -----

    /// <summary>
    /// 현재 외형을 받치는 표면 조회
    /// </summary>
    /// <param name="hit">조회한 지지 표면</param>
    /// <returns>지지 표면 조회 성공 여부</returns>
    bool TryGetSupport ( out RaycastHit hit )
    {
        _itemView.Init( );

        Quaternion rotationOffset = _carry != null
            ? _carry.PlacementRotationOffset : Quaternion.identity;
        if ( _itemView.TryGetPlacementSupport(
            _supportDir, rotationOffset, _supportDistance, out hit ) == true )
            return true;

        return Fail( "현재 외형을 지지하는 표면을 찾지 못했습니다." );
    }

    /// <summary>
    /// 후보 외형의 배치 위치 보정과 공간 검사
    /// </summary>
    /// <param name="candidate">준비하거나 복원할 비활성 외형</param>
    /// <param name="support">현재 지지 표면</param>
    /// <returns>위치 보정과 공간 검사 성공 여부</returns>
    bool TryPreparePlacement (
        AppearanceComponents candidate, RaycastHit support )
    {
        if ( AppearanceChecker.Check( candidate, out _failureReason ) == false )
            return false;

        if ( AppearancePlacement.TryGetOffset(
            candidate, support.point, support.normal, _surfaceGap,
            out Vector3 offset, out _failureReason ) == false )
            return false;

        //아이템 루트는 움직이지 않고 외형의 표면 방향 위치만 보정
        candidate.transform.position += offset;

        //크기가 달라져 표면 가장자리를 벗어나는 후보는 겹침 검사 전에 제외
        if ( AppearancePlacement.CheckSurfaceSupport(
            candidate, support, _contactDist, out _failureReason ) == false )
            return false;

        return AppearanceSpaceChecker.IsClear(
            candidate, transform, _obstacleMask,
            _contactDist, out _failureReason );
    }

    #endregion

    #region ----- 변경/복원 -----

    /// <summary>
    /// 외형을 전환하고 실패하면 이전 외형 복구
    /// </summary>
    /// <param name="candidate">적용할 외형</param>
    /// <returns>적용 성공 여부</returns>
    bool TryApply ( AppearanceComponents candidate )
    {
        AppearanceComponents previous = _current;
        int completed = 0;
        _failureReason = string.Empty;
        _pendingAppearance = candidate;

        //후보의 활성 콜백이 끝난 상태로 연결 조건을 검사하고 이전 외형을 끔
        candidate.gameObject.SetActive( true );

        Physics.SyncTransforms( );
        if ( CheckRuntimeAppearance( previous ) == true &&
             CheckRuntimeAppearance( candidate ) == true &&
             _bindings.CheckBindings( candidate, out _failureReason ) == true &&
             _bindings.TryBind( candidate, out completed, out _failureReason ) == true )
        {
            previous.gameObject.SetActive( false );
            //이전 외형의 비활성 콜백까지 마친 뒤 성공 확정
            if ( CheckRuntimeAppearance( candidate ) == true && previous != null &&
                 previous.gameObject.activeSelf == false &&
                 _bindings.CheckBindings( candidate, out _failureReason ) == true )
            {
                _current = candidate;
                _pendingAppearance = null;
                return true;
            }
        }

        string applyReason = string.IsNullOrEmpty( _failureReason )
            ? "외형 활성화 중 대상 상태가 변경됐습니다."
            : _failureReason;

        //일부 기능이 후보를 참조할 수 있으므로 이전 외형에 다시 연결한 뒤 후보를 끔
        if ( previous != null )
            previous.gameObject.SetActive( true );

        if ( CheckRuntimeAppearance( previous ) == false )
        {
            //활성 콜백에서 대상이 제거된 경우 정상적인 되돌리기를 보장할 수 없음
            _bindingFailed = true;
            return Fail( $"{applyReason} 참조 상태를 확인해 주세요." );
        }

        if ( _bindings.TryRestoreBindings( previous, completed, out string restoreReason ) == false )
        {
            //참조가 남아 있을 수 있는 외형은 삭제하지 않고 추가 동작 차단
            _bindingFailed = true;
            return Fail( $"{applyReason} 이전 참조 복원 실패: {restoreReason}" );
        }

        if ( candidate != null )
            candidate.gameObject.SetActive( false );

        //비활성 콜백이 기존 표시까지 손상한 경우에도 성공 복구로 처리하지 않음
        if ( CheckRuntimeAppearance( previous ) == false ||
             ( candidate != null && candidate.gameObject.activeSelf == true ) ||
             _bindings.CheckBindings( previous, out _ ) == false )
        {
            _bindingFailed = true;
            return Fail( $"{applyReason} 이전 외형의 표시 복구에 실패했습니다." );
        }

        _pendingAppearance = null;
        return Fail( applyReason );
    }

    /// <summary>
    /// 지정한 외형을 적용하고 이전 외형을 복원용으로 보관
    /// </summary>
    /// <param name="data">적용할 외형 설정</param>
    /// <param name="support">현재 아이템의 지지 표면</param>
    /// <returns>외형 적용 여부</returns>
    bool TryApplyData (
        AppearanceData data, RaycastHit support )
    {
        if ( _factory.TryCreateCandidate(
            data, _appearanceRoot,
            out AppearanceComponents candidate,
            out _failureReason ) == false )
            return false;

        if ( TryPreparePlacement( candidate, support ) == false )
        {
            _factory.DisposeAppearance( candidate );
            return false;
        }

        AppearanceComponents previous = _current;

        if ( TryApply( candidate ) == false )
        {
            //참조 복구 실패 시 연결된 후보를 임의로 삭제하지 않음
            if ( _bindingFailed == false )
                _factory.DisposeAppearance( candidate );

            return false;
        }

        //외형과 기능 참조가 모두 적용된 뒤 복원 정보 확정
        _original = previous;
        _model.BeginChange( _currentData );
        _currentData = data;
        _failureReason = string.Empty;
        return true;
    }

    /// <summary>
    /// 지지 표면에 맞는 외형 후보 적용
    /// </summary>
    /// <param name="support">현재 아이템의 지지 표면</param>
    /// <returns>후보 적용 성공 여부</returns>
    bool TryApplyCandidate ( RaycastHit support )
    {
        while ( _model.TryTakeCandidate( out AppearanceData data ) == true )
        {
            //프리팹 내부에서 외형을 찾지 못하면 사유를 남기고 다음 후보 확인
            if ( _factory.TryCreateCandidate(
                data, _appearanceRoot,
                out AppearanceComponents candidate, out _failureReason ) == false )
                continue;

            //생성된 외형의 배치와 공간 검사
            if ( TryPreparePlacement( candidate, support ) == false )
            {
                _factory.DisposeAppearance( candidate );
                continue;
            }

            AppearanceComponents previous = _current;

            if ( TryApply( candidate ) == false )
            {
                //이전 참조 복원이 실패했다면 참조가 남은 후보를 임의로 삭제하지 않음
                if ( _bindingFailed == false )
                    _factory.DisposeAppearance( candidate );

                //참조 연결 실패는 후보 형상 문제와 구분하여 즉시 종료
                return false;
            }

            //표시와 기능 참조 연결이 모두 성공한 뒤에만 복원 정보 확정
            _original = previous;
            _model.BeginChange( _currentData );
            _currentData = data;
            _failureReason = string.Empty;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 지정한 외형으로 변경하고 이전 외형을 복원용으로 보관
    /// </summary>
    /// <param name="data">적용할 외형 설정</param>
    /// <param name="isChanged">실제 외형 전환 발생 여부</param>
    /// <returns>요청 처리 성공 여부</returns>
    public bool TryChange (
        AppearanceData data, out bool isChanged )
    {
        isChanged = false;
        _failureReason = string.Empty;

        if ( CheckReady( ) == false )
            return false;

        if ( _model.IsChanged == true )
            return Fail( "이미 진행 중인 외형 변경이 있습니다." );

        if ( data == null ||
             data.Prefab == null ||
             string.IsNullOrWhiteSpace( data.Id ) == true )
        {
            return Fail( "적용할 외형 설정을 확인해 주세요." );
        }

        //이미 필요한 외형이면 정상 성공으로 처리
        if ( data == _currentData ||
             string.Equals( data.Id, _currentData.Id,
                 StringComparison.Ordinal ) == true )
        {
            if ( CheckRuntimeAppearance( _current ) == false ||
                 _bindings.CheckBindings( _current, out _failureReason ) == false )
                return false;

            return true;
        }

        if ( TryGetSupport( out RaycastHit support ) == false )
            return false;

        _isRunning = true;

        try
        {
            if ( TryApplyData( data, support ) == false )
                return false;

            isChanged = true;
            return true;
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// 현재와 다른 후보 외형으로 변경
    /// </summary>
    /// <returns>외형 변경 성공 여부</returns>
    public bool TryChange ()
    {
        _failureReason = string.Empty;

        //보유와 기능 상태를 확인하고 기존 변경 위에 새 변경이 겹치지 않도록 차단
        if ( CheckReady( ) == false )
            return false;

        if ( _model.IsChanged == true )
            return Fail( "이미 진행 중인 외형 변경이 있습니다." );

        //현재 외형과 중복 후보를 제외한 이번 요청의 후보 목록 준비
        if ( _map == null ||
             _model.TryPrepareCandidates(
                 _map.Appearances, _currentData,
                 _factory.GetSources( _map.Appearances, _currentData ), false ) == false )
            return Fail( "현재와 다른 유효 외형 후보가 없습니다." );

        //모든 후보를 현재 물건이 놓인 동일한 표면에 맞춰 검사
        if ( TryGetSupport( out RaycastHit support ) == false )
            return false;

        //외형 활성화 콜백에서 변경 요청이 다시 들어오는 경우 차단
        _isRunning = true;

        try
        {
            return TryApplyCandidate( support );
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// 이전 외형을 배치하고 복원 정보 정리
    /// </summary>
    /// <param name="support">복원할 지지 표면</param>
    /// <param name="savedPosition">실패 시 되돌릴 보관 위치</param>
    /// <returns>외형 복원 성공 여부</returns>
    bool TryRestoreAppearance ( RaycastHit support, Vector3 savedPosition )
    {
        //현재 표시 외형을 유지한 채 이전 외형의 배치 위치와 공간부터 확인
        if ( TryPreparePlacement( _original, support ) == false )
        {
            _original.transform.localPosition = savedPosition;
            return false;
        }

        AppearanceComponents changed = _current;

        if ( TryApply( _original ) == false )
        {
            //참조가 정상 복구됐을 때만 보관 위치를 되돌려 재시도 가능하게 유지
            if ( _bindingFailed == false )
                _original.transform.localPosition = savedPosition;

            return false;
        }

        //복원이 성공한 뒤 설정을 되돌리고 사용이 끝난 외형 삭제
        _currentData = _model.Original;
        _original = null;
        _model.EndChange( );

        _factory.DisposeAppearance( changed );
        _failureReason = string.Empty;
        return true;
    }

    /// <summary>
    /// 현재 위치에서 변경 직전 외형 복원
    /// </summary>
    /// <returns>외형 복원 성공 여부</returns>
    public bool TryRestore ()
    {
        _failureReason = string.Empty;

        //복원 가능한 기능 상태와 보관된 이전 외형 확인
        if ( CheckReady( ) == false )
            return false;

        if ( _model.CanRestore( false ) == false || _original == null )
            return Fail( "복원할 외형이 없습니다." );

        if ( TryGetSupport( out RaycastHit support ) == false )
            return false;

        //발생 당시 월드 위치가 아닌 현재 지지 표면에서 복원 시도
        //실패하면 준비 과정에서 이동한 복원용 외형만 원래 보관 위치로 되돌림
        Vector3 savedPosition = _original.transform.localPosition;
        _isRunning = true;

        try
        {
            return TryRestoreAppearance( support, savedPosition );
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// 현재 외형을 유지하고 이전 외형 삭제
    /// </summary>
    /// <returns>변경 확정 성공 여부</returns>
    public bool TryKeep ()
    {
        if ( _isRunning == true || _bindingFailed == true ||
             _model.IsChanged == false || _current == null )
            return false;

        //보유 중에도 현재 외형과 기능 참조는 그대로 유지
        AppearanceComponents original = _original;
        _original = null;
        _model.EndChange( );

        _factory.DisposeAppearance( original );
        return true;
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 외형 변경 설정과 현재 상태 확인
    /// </summary>
    /// <returns>변경 가능 여부</returns>
    bool CheckCanChange ()
    {
        return _map != null && _model.IsChanged == false && CheckReady( );
    }

    /// <summary>
    /// 외형 변경과 복원을 시작할 수 있는 상태 확인
    /// </summary>
    /// <returns>요청 가능 여부</returns>
    bool CheckReady ()
    {
        if ( isActiveAndEnabled == false || _itemView == null ||
             _itemView.isActiveAndEnabled == false )
            return Fail( "활성 외형 변경 기능과 ItemWorldController가 필요합니다." );

        if ( _isRunning == true || _bindingFailed == true )
            return Fail( "외형 처리 중이거나 이전 참조 연결에 실패했습니다." );

        if ( _carry != null && _carry.IsHold == true )
            return Fail( "보유 중에는 외형을 변경하거나 복원할 수 없습니다." );

        if ( _currentData == null || string.IsNullOrWhiteSpace( _currentData.Id ) )
            return Fail( "현재 외형의 설정과 ID가 필요합니다." );

        if ( _appearanceRoot == null || _appearanceRoot == transform ||
             _appearanceRoot.IsChildOf( transform ) == false ||
             _appearanceRoot.gameObject.activeInHierarchy == false )
            return Fail( "활성 아이템 자식에 외형 전용 부모를 지정해 주세요." );

        if ( CheckCurrentAppearance( ) == false )
            return false;

        if ( _bindings.CheckReady( out string bindingReason ) == false )
            return Fail( bindingReason );

        if ( ColliderCalculator.CheckFinite( _supportDir ) == false ||
             ColliderCalculator.CheckFinite( _supportDir.magnitude ) == false ||
             _supportDir.sqrMagnitude < 0.000001f ||
             ColliderCalculator.CheckFinite( _supportDistance ) == false ||
             _supportDistance <= 0f ||
             ColliderCalculator.CheckFinite( _surfaceGap ) == false ||
             _surfaceGap < 0f ||
             ColliderCalculator.CheckFinite( _contactDist ) == false ||
             _contactDist < 0f || _obstacleMask.value == 0 )
            return Fail( "지지 방향, 검사 거리, 표면 간격과 장애물 레이어를 확인해 주세요." );

        return true;
    }

    /// <summary>
    /// 현재 외형의 계층과 구성 확인
    /// </summary>
    /// <returns>올바른 현재 외형 구성 여부</returns>
    bool CheckCurrentAppearance ()
    {
        if ( _current == null )
            return Fail(
                "현재 외형이 없습니다. 시작 시 Appearance Root 바로 아래에" +
                " 활성 AppearanceComponents가 하나 있는지 확인해 주세요." );

        if ( _current.transform.parent != _appearanceRoot )
        {
            return Fail(
                $"현재 외형의 AppearanceComponents는 Appearance Root의" +
                $" 바로 아래 자식 오브젝트에 붙어 있어야 합니다. " +
                $"현재 연결: {_current.name} / 지정 부모: {_appearanceRoot.name}" );
        }

        if ( _current.gameObject.activeInHierarchy == false )
            return Fail(
                $"{_current.name}: 현재 외형과 그 부모 오브젝트가 활성 상태여야 합니다." );

        return AppearanceChecker.Check( _current, out _failureReason );
    }

    /// <summary>
    /// 활성 전환 후 외형을 계속 사용할 수 있는지 확인
    /// </summary>
    /// <param name="appearance">활성화한 외형</param>
    /// <returns>참조 연결 가능한 외형 여부</returns>
    bool CheckRuntimeAppearance ( AppearanceComponents appearance )
    {
        return this != null && isActiveAndEnabled == true &&
               appearance != null &&
               appearance.gameObject.activeInHierarchy == true &&
               appearance.transform.parent == _appearanceRoot &&
               _itemView != null && _itemView.isActiveAndEnabled == true &&
               AppearanceChecker.Check( appearance, out _ );
    }

    /// <summary>
    /// 최근 실패 사유 저장
    /// </summary>
    /// <param name="reason">실패 설명</param>
    /// <returns>처리 실패</returns>
    bool Fail ( string reason )
    {
        _failureReason = reason;
        return false;
    }

    #endregion

    #region ----- 테스트 -----

    /// <summary>
    /// 플레이 중 외형 변경 요청
    /// </summary>
    [ContextMenu( "Test Change Appearance" )]
    void TestChange ()
    {
        if ( Application.isPlaying == true && TryChange( ) == false )
            Debug.LogWarning( _failureReason, this );
    }

    /// <summary>
    /// 플레이 중 이전 외형 복원 요청
    /// </summary>
    [ContextMenu( "Test Restore Appearance" )]
    void TestRestore ()
    {
        if ( Application.isPlaying == true && TryRestore( ) == false )
            Debug.LogWarning( _failureReason, this );
    }

    /// <summary>
    /// 플레이 중 현재 외형 유지
    /// </summary>
    [ContextMenu( "Test Keep Appearance" )]
    void TestKeep ()
    {
        if ( Application.isPlaying == true )
            TryKeep( );
    }

    #endregion

}
