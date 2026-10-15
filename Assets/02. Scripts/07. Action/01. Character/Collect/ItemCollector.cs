using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 수집 모듈 - 대상 선택, 접근과 확보
/// </summary>
public class ItemCollector
{
    CharacterMover _mover;     //이동 모듈
    ItemStealSelector _selector;     //절도 대상 선택
    Approacher _approacher;     //아이템 접근
    MultiItemHolder _holder;     //확보한 아이템 관리

    Transform _holdPoint;     //아이템 보유 지점
    Collider [ ] _ownerColliders;     //보유자 충돌체

    Room _room;     //현재 위치해 있는 방
    ItemStealTargetData _target;		//개별 아이템 또는 공급원 수집 대상
    bool _hasTarget;		//수집 대상 선택 여부
    uint _collectVersion;		//수집 회차
    /// <summary>
    /// 이번 수집에서 다시 선택하지 않을 개별 아이템 집합
    /// </summary>
    HashSet<ItemCarry> _excluded = new ( );
    /// <summary>
    /// 이번 수집에서 접근 또는 획득에 실패한 공급원 집합
    /// </summary>
    HashSet<IngredientSource> _excludedSources = new ( );

    float _timeLimit;     //수집 제한시간
    float _takeDist;		//정지 오차를 포함한 확보 거리
    float _takeHeight;		//확보 높이 제한
    float _elapsed;     //수집 경과 시간
    bool _isCollecting;     //수집 진행 여부

    /// <summary>
    /// 수집 진행 여부
    /// </summary>
    public bool IsCollecting => _isCollecting;


    /// <summary>
    /// 수집 기능과 보유 지점 연결
    /// </summary>
    /// <param name="mover">초기화된 이동 모듈</param>
    /// <param name="data">공격 적 설정</param>
    /// <param name="holder">복수 아이템 보유 모듈</param>
    /// <param name="holdPoint">아이템 보유 지점</param>
    /// <param name="ownerColliders">보유자 충돌체</param>
    /// <param name="stock">공급원 재고 중재자</param>
    public ItemCollector (
        CharacterMover mover , EnemyHarmData data ,
        MultiItemHolder holder , Transform holdPoint ,
        Collider [ ] ownerColliders, IngredientStockManager stock )
    {
        _mover = mover;
        _holder = holder;
        _holdPoint = holdPoint;
        _ownerColliders = ownerColliders;
        _timeLimit = data.CollectTimeLimit;
        _takeDist = data.TakeDist + data.ArriveTolerance;
        _takeHeight = data.TakeHeight;

        _selector = new ItemStealSelector ( data.StealItemMap, stock );

        //공격 거리와 구분하여 아이템 확보 거리와 높이 사용
        _approacher = new Approacher (
            mover , data.CollectSpeed , data.RotateSpeed ,
            data.TakeDist , data.SampleDist ,
            data.RefreshInterval , data.CollectTimeLimit ,
            data.TakeHeight , data.ArriveTolerance );
    }


    #region ----- 수집 -----

    /// <summary>
    /// 현재 목표의 접근 종료
    /// </summary>
    void ClearTarget ( )
    {
        _approacher.Stop ( );
        _target = default;
        _hasTarget = false;
    }

    /// <summary>
    /// 다음 아이템 선택과 접근 시작
    /// </summary>
    /// <returns>접근 시작 여부</returns>
    bool SelectNext ( )
    {
        if ( _selector.TrySelect (
            _room , _mover.Position ,
            _holder.RemainingCapacity , _excluded , _excludedSources ,
            out _target ) == false )
            return false;

        //실패하거나 회수된 물건도 이번 수집에서는 다시 선택하지 않음
        if ( _target.IsSource == false )
            _excluded.Add ( _target.Item );
        _hasTarget = true;
        _approacher.Begin ( _target.TargetTransform );
        return true;
    }

    /// <summary>
    /// 방의 절도 대상 유무 확인
    /// </summary>
    /// <param name="room">조회할 방</param>
    /// <returns>절도 대상 존재 여부</returns>
    public bool HasTarget ( Room room )
    {
        //남은 수용량과 이번 수집의 제외 목록은 반영하지 않음
        return _selector.HasTarget ( room );
    }

    /// <summary>
    /// 수집 종료
    /// </summary>
    public void Stop ( )
    {
        _isCollecting = false;
        _collectVersion++;
        ClearTarget ( );

        _room = null;
        _excluded.Clear ( );
        _excludedSources.Clear ( );

        //확보한 물건은 도주 중에도 유지하므로 여기서 해제하지 않음
    }

    /// <summary>
    /// 지정한 방에서 수집 시작
    /// </summary>
    /// <param name="room">수집할 방</param>
    /// <returns>수집 시작 여부</returns>
    public bool Begin ( Room room )
    {
        if ( CanBegin ( room ) == false )
            return false;

        ClearTarget ( );
        _excluded.Clear ( );
        _excludedSources.Clear ( );
        _collectVersion++;

        _room = room;
        _elapsed = 0f;
        _isCollecting = true;
        return true;
    }

    /// <summary>
    /// 대상 접근과 아이템 확보 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    public void UpdateCollect ( float deltaTime )
    {
        if ( _isCollecting == false )
            return;

        _elapsed += Mathf.Max ( 0f , deltaTime );

        //시간이나 수용량이 끝나면 확보한 만큼만 들고 수집 종료
        if ( CanContinue ( ) == false )
        {
            Stop ( );
            return;
        }

        if ( _hasTarget == false )
        {
            //남은 용량에 들어가는 후보가 없으면 이번 수집 종료
            if ( SelectNext ( ) == false )
            {
                Stop ( );
            }

            return;
        }

        //접근 중 보유, 변칙 적용 또는 방 이동이 발생한 대상 제외
        if ( _selector.CanSelect ( _room , _target ) == false )
        {
            FailTarget ( );
            return;
        }

        ApproachStatus status = _approacher.UpdateMove ( deltaTime );

        if ( status == ApproachStatus.Moving )
            return;

        if ( status == ApproachStatus.Failed )
        {
            FailTarget ( );
            return;
        }

        //도착 상태에서만 확보
        if ( status != ApproachStatus.Arrived )
        {
            FailTarget ( );
            return;
        }

        //보유 알림에서 수집이 중단돼도 접근 목표가 남지 않도록 먼저 정리
        ItemStealTargetData target = _target;
        uint version = _collectVersion;
        ClearTarget ( );

        //이동 완료 후에도 방 소속과 보유, 변칙 상태를 다시 확인
        if ( _selector.CanSelect ( _room , target ) == false )
        {
            if ( target.IsSource == true )
                _excludedSources.Add ( target.Source );
            return;
        }

        ItemCarry item;
        bool added;
        if ( target.IsSource == true )
        {
            IngredientSource source = target.Source;
            added = source.TrySteal (
                () => CanAcquire ( version ),
                candidate => CanAcquireSource ( version, source ) == true &&
                    _holder.TryAdd ( candidate, _holdPoint, _ownerColliders ),
                _holder.IsHolding, out item );
        }
        else
        {
            item = target.Item;
            added = _holder.TryAdd ( item, _holdPoint, _ownerColliders );
        }

        //보유 알림에서 종료되거나 새 수집이 시작됐다면 이전 결과를 적용하지 않음
        if ( CanAcquire ( version ) == false )
            return;

        if ( added == false && target.IsSource == true )
            _excludedSources.Add ( target.Source );

        //확보 알림 도중 수집 종료나 보유 변경이 발생했다면 숨기지 않음
        if ( added == true && _holder.IsHolding ( item ) == true )
        {
            item.SetHeldHidden ( true );
#if UNITY_EDITOR
            if ( target.IsSource == true )
                PlayDebugHistory.Record ( "공격 적 공급원 절도", _mover,
                    $"공급원 {target.Source.name} / 획득 1개 / " +
                    target.Source.GetStockStatus ( ) );
#endif
        }
    }

    /// <summary>
    /// 실패한 공급원을 제외하고 접근 종료
    /// </summary>
    void FailTarget ( )
    {
        if ( _hasTarget == true && _target.IsSource == true )
            _excludedSources.Add ( _target.Source );
        ClearTarget ( );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 이동 모듈과 보유 지점 활성 확인
    /// </summary>
    /// <returns>수집 환경 활성 여부</returns>
    bool HasActiveCollectionContext ( )
    {
        return _mover != null && _mover.isActiveAndEnabled &&
               _holdPoint != null && _holdPoint.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 수집 시작 조건 확인
    /// </summary>
    /// <param name="room">수집할 방</param>
    /// <returns>시작 가능 여부</returns>
    bool CanBegin ( Room room )
    {
        if ( _isCollecting || room == null ||
             room.isActiveAndEnabled == false )
            return false;

        if ( HasActiveCollectionContext ( ) == false )
            return false;

        //이전 운반이 끝나기 전에는 새로운 수집을 시작하지 않음
        if ( _holder == null || _holder.Count > 0 ||
             _holder.RemainingCapacity <= 0 )
            return false;

        return float.IsNaN ( _timeLimit ) == false &&
               float.IsInfinity ( _timeLimit ) == false &&
               _timeLimit > 0f;
    }

    /// <summary>
    /// 수집 유지 조건 확인
    /// </summary>
    /// <returns>계속 수집할 수 있는지 여부</returns>
    bool CanContinue ( )
    {
        return CanAcquire ( _collectVersion ) == true && _holder.RemainingCapacity > 0;
    }

    /// <summary>
    /// 확보 요청의 수집 회차와 활동 유지 확인
    /// </summary>
    /// <param name="version">확보를 시작한 수집 회차</param>
    /// <returns>같은 수집의 확보 결과를 적용할 수 있는지 여부</returns>
    bool CanAcquire ( uint version )
    {
        if ( _isCollecting == false || _collectVersion != version )
            return false;

        if ( _room == null || _room.isActiveAndEnabled == false )
            return false;

        if ( HasActiveCollectionContext ( ) == false )
            return false;

        //마지막 용량을 채운 성공 결과는 남은 수용량으로 거절하지 않음
        return _elapsed < _timeLimit;
    }

    /// <summary>
    /// 공급원 활성화 알림 이후 실제 확보 위치 재확인
    /// </summary>
    /// <param name="version">확보를 시작한 수집 회차</param>
    /// <param name="source">확보할 공급원</param>
    /// <returns>같은 방의 확보 범위에 있는지 여부</returns>
    bool CanAcquireSource ( uint version, IngredientSource source )
    {
        if ( CanAcquire ( version ) == false || source == null ||
             source.StealPoint == null ||
             source.StealPoint.gameObject.activeInHierarchy == false ||
             RoomRegister.TryGetRoom ( source.SupplyRoot.position, out Room room ) == false ||
             room != _room )
            return false;

        Vector3 offset = source.StealPoint.position - _mover.Position;
        if ( _takeHeight >= 0f )
        {
            if ( Mathf.Abs ( offset.y ) > _takeHeight )
                return false;
            offset.y = 0f;
        }

        return offset.magnitude <= _takeDist;
    }

    #endregion
}
