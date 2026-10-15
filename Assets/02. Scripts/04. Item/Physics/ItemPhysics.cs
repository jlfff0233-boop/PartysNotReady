using System;
using UnityEngine;

/// <summary>
/// 아이템 물리 모듈 - 속도와 고정 및 투척 물리 설정 관리
/// </summary>
public class ItemPhysics
{
    Rigidbody _rigid;       //아이템 리지드바디
    Collider _collider;     //현재 아이템 충돌용 콜라이더
    Collider [ ] _ownerColliders = Array.Empty<Collider>( );       //충돌 제외 중인 투척자

    bool _isHidden;     //숨김 중 물리 정지 여부
    bool _visibleKinematic;     //숨기기 전 고정 설정
    bool _visibleGravity;       //숨기기 전 중력 설정
    bool _ignoreOwner;      //투척자 충돌 제외 여부

    float _originalLinearDamping;       //투척 전 이동 감쇠
    float _originalAngularDamping;       //투척 전 회전 감쇠

    /// <summary>
    /// 아이템 물리 참조 연결
    /// </summary>
    /// <param name="rigid">아이템 리지드바디</param>
    /// <param name="collider">아이템 충돌용 콜라이더</param>
    public ItemPhysics ( Rigidbody rigid, Collider collider )
    {
        _rigid = rigid;
        _collider = collider;
    }

    /// <summary>
    /// 물리 상태를 유지하며 대표 충돌체 교체
    /// </summary>
    /// <param name="collider">교체할 모델의 대표 충돌체</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>충돌체 교체 성공 여부</returns>
    public bool TrySetCollider ( Collider collider, out string reason )
    {
        if ( CheckAppearanceCollider( collider, out reason ) == false )
            return false;

        //속도, 중력, 고정과 감쇠 저장값은 변경하지 않음
        _collider = collider;
        return true;
    }

    #region ----- 기본 물리 설정 -----

    /// <summary>
    /// 현재 이동, 회전 속도 조회
    /// </summary>
    /// <param name="velocity">현재 이동 속도</param>
    /// <param name="angularVelocity">현재 회전 속도</param>
    public void GetVelocity (
        out Vector3 velocity,
        out Vector3 angularVelocity )
    {
        velocity = _rigid.linearVelocity;
        angularVelocity = _rigid.angularVelocity;
    }

    /// <summary>
    /// 이동, 회전 속도 적용
    /// </summary>
    /// <param name="velocity">적용할 이동 속도</param>
    /// <param name="angularVelocity">적용할 회전 속도</param>
    public void SetVelocity (
        Vector3 velocity, Vector3 angularVelocity )
    {
        if ( _isHidden == true ) return;

        _rigid.linearVelocity = velocity;
        _rigid.angularVelocity = angularVelocity;
    }


    /// <summary>
    /// 물리 이동과 중력 중단
    /// </summary>
    public void DisablePhysics ()
    {
        if ( _rigid.isKinematic == false )
        {
            _rigid.linearVelocity = Vector3.zero;
            _rigid.angularVelocity = Vector3.zero;
        }

        _rigid.isKinematic = true;
        _rigid.useGravity = false;
    }

    /// <summary>
    /// 숨김에 따른 물리 정지와 복구
    /// </summary>
    /// <param name="isHidden">숨김 여부</param>
    public void SetHidden ( bool isHidden )
    {
        if ( _rigid == null || _isHidden == isHidden ) return;

        if ( isHidden == true )
        {
            //처음 숨길 때 고정과 중력 설정을 저장하고 속도 제거
            _visibleKinematic = _rigid.isKinematic;
            _visibleGravity = _rigid.useGravity;
            DisablePhysics( );
        }
        else
        {
            //숨기기 전 속도는 재적용하지 않고 정지 상태에서 복귀
            _rigid.isKinematic = _visibleKinematic;
            _rigid.useGravity = _visibleGravity;
        }

        _isHidden = isHidden;
    }

    /// <summary>
    /// 배치 후 고정 여부에 따른 물리 적용
    /// </summary>
    /// <param name="isFixed">물리 고정 여부</param>
    public void SetPlacementPhysics ( bool isFixed )
    {
        if ( _isHidden == true ) return;

        //리지드바디 처리
        _rigid.isKinematic = isFixed;
        _rigid.useGravity = isFixed == false;

        //고정이 아니라면 리지드바디 속도 초기화
        if ( isFixed == false )
        {
            _rigid.linearVelocity = Vector3.zero;
            _rigid.angularVelocity = Vector3.zero;
        }
    }


    /// <summary>
    /// 리지드바디 수면 상태 확인
    /// </summary>
    /// <returns>수면 상태 여부</returns>
    public bool IsSleeping ()
    {
        return _rigid.IsSleeping( );
    }

    /// <summary>
    /// 고정/정지 여부 확인
    /// </summary>
    /// <returns>정착 표면 확인 가능 상태 여부</returns>
    public bool IsSettled ()
    {
        if ( _rigid == null ||
             _rigid.gameObject.activeInHierarchy == false ||
             _isHidden == true )
            return false;

        //고정 물건은 수면 상태에 의존하지 않음
        return _rigid.isKinematic || _rigid.IsSleeping( );
    }

    #endregion

    #region ----- 배치 물리 상태 -----

    /// <summary>
    /// 배치 적용 전 물리 상태 저장
    /// </summary>
    /// <param name="state">저장한 물리 상태</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>상태 저장 성공 여부</returns>
    public bool TrySavePlacementState (
        out ItemPhysicsState state,
        out string reason )
    {
        state = default;

        if ( CheckPlacementState( out reason ) == false )
            return false;

        ItemPhysicsState current = new ItemPhysicsState( _rigid );

        if ( ColliderCalculator.CheckFinite( current.Velocity ) == false ||
             ColliderCalculator.CheckFinite( current.AngularVelocity ) == false )
        {
            reason = "저장할 아이템의 이동 또는 회전 속도가 유효하지 않습니다.";
            return false;
        }

        state = current;
        return true;
    }

    /// <summary>
    /// 배치 적용 전 물리 상태로 복구
    /// </summary>
    /// <param name="state">이전 물리 상태</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>물리 상태 복구 성공 여부</returns>
    public bool TryRestorePlacementState (
        ItemPhysicsState state, out string reason )
    {
        if ( CheckPlacementState( out reason ) == false )
            return false;

        //기본값이나 다른 아이템의 상태를 잘못 적용하지 않도록 확인
        if ( state.Body == null || state.Body != _rigid )
        {
            reason = "현재 아이템에서 저장한 물리 상태가 필요합니다.";
            return false;
        }

        if ( ColliderCalculator.CheckFinite( state.Velocity ) == false ||
             ColliderCalculator.CheckFinite( state.AngularVelocity ) == false )
        {
            reason = "복구할 이동 또는 회전 속도가 유효하지 않습니다.";
            return false;
        }

        //일반 배치 함수는 속도를 초기화하므로 저장한 설정을 직접 복구
        _rigid.isKinematic = state.IsKinematic;
        _rigid.useGravity = state.UseGravity;

        //고정된 리지드바디에는 속도를 설정하지 않음
        if ( state.IsKinematic == false )
        {
            _rigid.linearVelocity = state.Velocity;
            _rigid.angularVelocity = state.AngularVelocity;

            //속도 적용으로 깨어날 수 있으므로 수면 여부는 마지막에 복구
            if ( state.IsSleeping )
                _rigid.Sleep( );
            else
                _rigid.WakeUp( );
        }

        return true;
    }

    #endregion

    #region ----- 투척 물리 -----

    /// <summary>
    /// 투척 전 감쇠 설정 저장
    /// </summary>
    public void SaveThrowSettings ()
    {
        //다시 집었을 때 복구할 기존 감쇠 설정 저장
        _originalLinearDamping = _rigid.linearDamping;
        _originalAngularDamping = _rigid.angularDamping;
    }

    /// <summary>
    /// 투척 전 감쇠 설정 복구
    /// </summary>
    public void RestoreThrowSettings ()
    {
        _rigid.linearDamping = _originalLinearDamping;
        _rigid.angularDamping = _originalAngularDamping;
    }

    /// <summary>
    /// 투척자 충돌 제외 및 초기 속도 적용
    /// </summary>
    /// <param name="velocity">초기 속도</param>
    /// <param name="ownerColliders">투척자 콜라이더</param>
    public void SetThrow (
        Vector3 velocity, Collider [ ] ownerColliders )
    {
        _ownerColliders = ownerColliders;

        for ( int i = 0; i < _ownerColliders.Length; i++ )
            Physics.IgnoreCollision( _collider, _ownerColliders [ i ], true );

        _ignoreOwner = true;

        //공중에서는 중력으로 이동하고 지면에서 별도 감속
        _rigid.linearDamping = 0f;
        _rigid.angularDamping = 0f;

        SetVelocity( velocity, Vector3.zero );
        _rigid.WakeUp( );
    }

    /// <summary>
    /// 던진 플레이어와의 충돌 복구
    /// </summary>
    public void RestoreOwnerCollision ()
    {
        if ( _ignoreOwner == true )
        {
            foreach ( Collider owner in _ownerColliders )
            {
                //씬 정리 중 먼저 파괴된 투척자는 제외
                if ( owner != null )
                    Physics.IgnoreCollision( _collider, owner, false );
            }
        }

        _ignoreOwner = false;
        _ownerColliders = Array.Empty<Collider>( );
    }

    /// <summary>
    /// 충돌 제외 중인 투척자와 영역 겹침 확인
    /// </summary>
    /// <returns>겹치는 투척자 존재 여부</returns>
    public bool HasOwnerOverlap ()
    {
        if ( _ignoreOwner == false ) return false;

        for ( int i = 0; i < _ownerColliders.Length; i++ )
        {
            //플레이어와 영역이 겹치는 동안 충돌 제외 유지
            if ( _collider.bounds.Intersects( _ownerColliders [ i ].bounds ) == true )
                return true;
        }

        return false;
    }

    #endregion


    /// <summary>
    /// 속도 제거와 수면 상태 적용
    /// </summary>
    public void StopMotion ()
    {
        SetVelocity( Vector3.zero, Vector3.zero );
        _rigid.Sleep( );
    }

    /// <summary>
    /// 고정 해제된 아이템에 충격량 적용
    /// </summary>
    /// <param name="impulse">충격량</param>
    public void ApplyImpulse ( Vector3 impulse )
    {
        if ( _isHidden == true ) return;

        _rigid.AddForce( impulse, ForceMode.Impulse );
    }


    #region ----- 상태 검사 -----

    /// <summary>
    /// 대표 충돌체 변경 가능 여부 확인
    /// </summary>
    /// <param name="collider">연결할 충돌체</param>
    /// <param name="reason">연결 불가 사유</param>
    public bool CheckAppearanceCollider ( Collider collider, out string reason )
    {
        reason = string.Empty;

        if ( collider == null || collider.attachedRigidbody != _rigid )
        {
            reason = "현재 아이템 Rigidbody에 연결된 Collider가 필요합니다.";
            return false;
        }

        //숨김 복원과 투척자 충돌 제외가 진행 중이면 기존 참조 유지
        if ( _isHidden == true )
        {
            reason = "숨김 상태에서는 대표 충돌체를 변경할 수 없습니다.";
            return false;
        }

        if ( _ignoreOwner == true )
        {
            reason = "투척자와의 충돌 제외가 끝난 뒤 대표 충돌체를 변경해 주세요.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 배치 상태 저장/복구 가능 여부 확인
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>배치 물리 상태 처리 가능 여부</returns>
    public bool CheckPlacementState ( out string reason )
    {
        reason = string.Empty;

        if ( _rigid == null || _rigid.gameObject.activeInHierarchy == false )
        {
            reason = "활성 아이템의 Rigidbody가 필요합니다.";
            return false;
        }

        //숨김이나 투척자 충돌 제외 상태를 배치 처리에서 덮어쓰지 않음
        if ( _isHidden == true || _ignoreOwner == true )
        {
            reason = "숨김 또는 투척자 충돌 제외가 끝난 뒤 배치할 수 있습니다.";
            return false;
        }

        return true;
    }

    #endregion

}
