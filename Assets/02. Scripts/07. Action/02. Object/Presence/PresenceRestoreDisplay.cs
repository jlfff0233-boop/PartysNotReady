using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 존재 복구 대기 모듈 - 충돌체 상태 보관과 대기 표시 전환
/// </summary>
public class PresenceRestoreDisplay
{
    Collider [ ] _colliders;     //복구 대기 동안 충돌을 차단할 콜라이더
    bool [ ] _states;     //대기 시작 직전의 활성 상태
    bool _isActive;     //충돌을 차단한 복구 대기 여부

    /// <summary>
    /// 복구 대기 여부
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 현재 외형의 충돌체 목록
    /// </summary>
    public IReadOnlyList<Collider> Colliders => _colliders;


    /// <summary>
    /// 복구 대기에 사용할 충돌체 목록 연결
    /// </summary>
    /// <param name="colliders">현재 외형의 충돌체 목록</param>
    public void SetColliders ( Collider [ ] colliders )
    {
        _colliders = colliders;
        _states = new bool [ colliders.Length ];
    }


    /// <summary>
    /// 대기 전에 저장한 충돌체 설정 복원
    /// </summary>
    void RestoreColliders ()
    {
        for ( int i = 0; i < _colliders.Length; i++ )
        {
            if ( _colliders [ i ] != null )
                _colliders [ i ].enabled = _states [ i ];
        }
    }


    /// <summary>
    /// 충돌을 차단하고 복구 대기 물건 활성화
    /// </summary>
    /// <param name="target">표시할 숨겨진 물건</param>
    /// <param name="physics">정지 상태를 유지할 물리 모듈</param>
    public void Begin ( GameObject target, ItemPhysics physics )
    {
        //활성 콜백 전에 복원 정보를 저장하고 충돌을 차단
        for ( int i = 0; i < _colliders.Length; i++ )
        {
            Collider collider = _colliders [ i ];
            if ( collider == null ) continue;
            _states [ i ] = collider.enabled;
            collider.enabled = false;
        }

        _isActive = true;
        physics?.SetHidden( true );
        //현재는 매트리얼을 바꾸지 않고 물건만 활성화
        target.SetActive( true );
    }

    /// <summary>
    /// 물건을 숨기고 대기 전 충돌체 설정 복원
    /// </summary>
    /// <param name="target">숨길 물건</param>
    /// <param name="physics">숨김 상태를 유지할 물리 모듈</param>
    public void Cancel ( GameObject target, ItemPhysics physics )
    {
        if ( _isActive == false )
            return;

        //충돌체가 복원되는 순간 충돌하지 않도록 물건부터 숨김
        if ( target != null )
            target.SetActive( false );

        RestoreColliders( );
        physics?.SetHidden( true );
        _isActive = false;
    }

    /// <summary>
    /// 충돌체와 물리를 복원하고 대기 종료
    /// </summary>
    /// <param name="physics">숨김 상태를 해제할 물리 모듈</param>
    public void Complete ( ItemPhysics physics )
    {
        //호출부에서 공간과 대상 상태를 확인한 뒤 실행
        RestoreColliders( );
        physics?.SetHidden( false );
        _isActive = false;
    }

    /// <summary>
    /// 공간 검사에 사용할 충돌체 활성 상태 조회
    /// </summary>
    /// <param name="index">충돌체 목록 인덱스</param>
    /// <returns>대기 전 또는 현재 활성 상태</returns>
    public bool IsColliderEnabled ( int index )
    {
        return _isActive ? _states [ index ] : _colliders [ index ].enabled;
    }

}
