using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 잠복 활성 제어 - 표시, 충돌과 이동 기능 복구
/// </summary>
public class HideActivationController : MonoBehaviour
{
    [SerializeField] GameObject _hideRoot;        //숨길 모델과 몸체 루트
    [SerializeField] Collider _rootCollider;      //제어 루트의 콜라이더(선택)
    [SerializeField] NavMeshAgent _agent;          //지상 이동 에이전트

    bool _rootActive;          //숨기기 전 루트 활성 상태
    bool _colliderEnabled;     //숨기기 전 제어 루트 충돌 상태
    bool _agentEnabled;        //숨기기 전 에이전트 상태
    bool _isHidden;            //현재 숨김 여부

    /// <summary>
    /// 잠복으로 숨겨진 상태
    /// </summary>
    public bool IsHidden => _isHidden;

    //비활성화·씬 해제에서는 Show를 호출하지 않는다. NavMesh가 먼저 해제될 수 있으며,
    //저장한 상태는 다음 등장 위치 준비와 명시적인 Show/RestoreMovement에서 복구한다.

    /// <summary>
    /// 현재 표시와 충돌 상태 저장 후 숨김
    /// </summary>
    public void Hide ()
    {
        //숨김 상태에서 이동만 복구했어도 다시 정지
        if ( _isHidden == true )
        {
            if ( _agent != null )
                _agent.enabled = false;

            return;
        }

        _rootActive = _hideRoot.activeSelf;

        //숨길 루트 밖에 있는 몸체 콜라이더만 별도로 처리
        if ( _rootCollider != null )
        {
            _colliderEnabled = _rootCollider.enabled;
            _rootCollider.enabled = false;
        }

        //비행 적 등 에이전트가 없는 경우도 사용 가능
        if ( _agent != null )
        {
            _agentEnabled = _agent.enabled;
            _agent.enabled = false;
        }

        _isHidden = true;
        //자식의 개별 활성 설정을 유지하며 함께 숨김
        _hideRoot.SetActive( false );
    }

    /// <summary>
    /// 숨기기 전 표시와 충돌 상태 복구
    /// </summary>
    public void Show ()
    {
        if ( _isHidden == false )
            return;

        //루트 활성화 중 재호출돼도 중복 복구하지 않도록 먼저 상태 해제
        _isHidden = false;

        //모두 켜는 대신 숨기기 전의 개별 활성 상태 복원
        _hideRoot.SetActive( _rootActive );

        if ( _rootCollider != null )
            _rootCollider.enabled = _colliderEnabled;

        if ( _agent != null )
            _agent.enabled = _agentEnabled;
    }

    /// <summary>
    /// 잠복 표시와 충돌을 유지하며 이동 에이전트 복구
    /// </summary>
    public void RestoreMovement ( )
    {
        if ( _isHidden == false || _agent == null )
            return;

        _agent.enabled = _agentEnabled;
    }

    /// <summary>
    /// 표시와 충돌을 숨긴 상태에서 등장 위치와 이동 기능 준비
    /// </summary>
    /// <param name="mover">적 이동 모듈</param>
    /// <param name="point">등장 지점</param>
    /// <param name="searchDistance">NavMesh 위치 탐색 거리</param>
    /// <returns>등장 위치 준비 성공 여부</returns>
    public bool TryPreparePosition (
        CharacterMover mover, Transform point, float searchDistance )
    {
        Hide( );

        if ( point == null || point.gameObject.activeInHierarchy == false )
            return false;

        //Agent를 켜기 전에 유효한 NavMesh 위치로 배치
        if ( mover.TryGetMovePosition(
            point.position, searchDistance, out Vector3 position ) == false ||
             mover.TryRelocate( position, point.rotation ) == false )
            return false;

        RestoreMovement( );

        //Agent의 위치와 이전 이동 상태도 함께 초기화
        if ( mover.TryResetPosition( point, searchDistance ) == false )
        {
            Hide( );
            return false;
        }

        return true;
    }
    /// <summary>
    /// 숨김 대상이 표시 제어와 적 제어 루트를 포함하는지 검사
    /// </summary>
    /// <param name="controlRoot">적 제어 루트</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>설정 유효 여부</returns>
    public bool CheckSettings ( Transform controlRoot, out string reason )
    {
        reason = string.Empty;

        if ( _hideRoot == null )
        {
            reason = "숨길 모델 루트를 연결해 주세요.";
            return false;
        }

        //숨김으로 표시 제어 또는 적 제어가 함께 꺼지는 구성 차단
        if ( transform.IsChildOf( _hideRoot.transform ) == true ||
             controlRoot.IsChildOf( _hideRoot.transform ) == true )
        {
            reason = "숨길 모델 루트에 제어 루트 자신이나 조상을 지정할 수 없습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 자체 숨김을 복구했을 때 대상이 활성 상태가 되는지 검사
    /// </summary>
    /// <param name="target">검사 대상</param>
    /// <returns>표시 복구 후 활성 가능 여부</returns>
    public bool IsAvailableWhenShown ( Transform target )
    {
        if ( target == null )
            return false;

        //자체 숨김만 예외로 인정하고 다른 비활성 부모는 거절
        for ( Transform current = target; current != null; current = current.parent )
        {
            bool isActive = _isHidden == true && current == _hideRoot.transform
                ? _rootActive
                : current.gameObject.activeSelf;

            if ( isActive == false )
                return false;
        }

        return true;
    }

}
