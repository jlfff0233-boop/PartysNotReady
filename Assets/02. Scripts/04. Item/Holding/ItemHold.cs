using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 보유 모듈 - 보유 레이어와 충돌 설정 저장 및 복구
/// </summary>
public class ItemHold
{
    const string HeldItemLayerName = "HoldItem";        //보유 레이어 레이어명

    bool _isHidden;     //보유 물건 임시 숨김 여부

    AppearanceComponents _currentAppearance;     //현재 모델의 표시/충돌 구성
    Transform _root;     //보유할 아이템 관리 루트
    Rigidbody _rigid;     //리지드바디
    Collider [ ] _holdColliders = Array.Empty<Collider>( );     //월드 보유자의 충돌 제외 대상

    /// <summary>
    /// 아이템 콜라이더 목록
    /// </summary>
    List<Collider> _itemColliders = new( );
    /// <summary>
    /// 원본 활성화 상태 목록
    /// </summary>
    List<bool> _originalEnabled = new( );
    /// <summary>
    /// 보유 레이어를 적용한 대상 목록
    /// </summary>
    List<Transform> _layerTargets = new( );
    /// <summary>
    /// 원본 레이어 목록
    /// </summary>
    List<int> _originalLayers = new( );


    /// <summary>
    /// 숨김 전 렌더링 제외 상태(렌더러, 상태)
    /// </summary>
    Dictionary<Renderer, bool> _hiddenRenderers = new( );
    /// <summary>
    /// 숨김 전 충돌 상태(콜라이더, 상태)
    /// </summary>
    Dictionary<Collider, bool> _hiddenColliders = new( );


    /// <summary>
    /// 참조 연결
    /// </summary>
    /// <param name="root">아이템 루트</param>
    public ItemHold ( Transform root )
    {
        _root = root;
        _rigid = root.GetComponent<Rigidbody>( );
    }

    /// <summary>
    /// 현재 모델의 표시/충돌 구성 연결
    /// </summary>
    /// <param name="appearance">현재 모델의 AppearanceComponents/null</param>
    public void SetCurrentAppearance ( AppearanceComponents appearance )
    {
        //미보유 상태의 초기 연결 또는 모델 교체와 복원 시 호출
        //기존 보유 상태 저장 목록은 다음 최초 집기 때 갱신
        _currentAppearance = appearance;
    }

    #region ----- 레이어 -----

    /// <summary>
    /// 기존 레이어 저장과 보유 레이어 적용
    /// </summary>
    void ApplyHeldItemLayer ()
    {
        int heldItemLayer = LayerMask.NameToLayer( HeldItemLayerName );

        //레이어가 없으면 원래 레이어와 복원 기록을 건드리지 않음
        if ( heldItemLayer < 0 )
            return;

        _layerTargets.Clear( );
        _originalLayers.Clear( );

        //현재 모델의 비활성 자식도 포함하고, 다른 모델은 아래에서 제외
        _root.GetComponentsInChildren<Transform>( true, _layerTargets );

        //복원용 또는 교체 준비 중인 다른 모델은 제외
        //역순 제거로 인덱스 밀림 방지
        for ( int i = _layerTargets.Count - 1; i >= 0; i-- )
        {
            if ( CheckHoldTarget( _layerTargets [ i ] ) == false )
                _layerTargets.RemoveAt( i );
        }

        for ( int i = 0; i < _layerTargets.Count; i++ )
        {
            GameObject target = _layerTargets [ i ].gameObject;

            //필터링이 끝난 목록 순서대로 저장
            //복원 시 대상과 인덱스 일치
            _originalLayers.Add( target.layer );
            target.layer = heldItemLayer;
        }
    }

    /// <summary>
    /// 아이템과 자식의 들기 전 레이어 복구
    /// </summary>
    public void RestoreItemLayers ()
    {
        for ( int i = 0; i < _layerTargets.Count; i++ )
        {
            Transform target = _layerTargets [ i ];

            //삭제된 자식은 복구 대상에서 제외
            if ( target == null ) continue;

            //각 자식의 원래 레이어 복구
            target.gameObject.layer = _originalLayers [ i ];
        }

        _layerTargets.Clear( );
        _originalLayers.Clear( );
    }

    #endregion


    /// <summary>
    /// 보유자와의 충돌 여부 설정
    /// </summary>
    /// <param name="ignore">충돌 제외 여부</param>
    public void SetHoldCollision ( bool ignore )
    {
        for ( int i = 0; i < _holdColliders.Length; i++ )
        {
            Collider ownerCollider = _holdColliders [ i ];

            //삭제된 대상과 자기 자신은 제외
            if ( ownerCollider == null )
                continue;

            //복합 충돌체도 보유자의 각 충돌체와 빠짐없이 제외 또는 복구
            foreach ( Collider itemCollider in _itemColliders )
            {
                if ( itemCollider == null || itemCollider == ownerCollider )
                    continue;

                Physics.IgnoreCollision( itemCollider, ownerCollider, ignore );
            }
        }
    }


    #region ----- 숨김 처리 -----

    /// <summary>
    /// 렌더러와 충돌을 임시로 비활성화하거나 복구
    /// </summary>
    /// <param name="hidden">숨김 여부</param>
    public void SetHeldHidden ( bool hidden )
    {
        if ( _isHidden == hidden )
            return;

        _isHidden = hidden;

        if ( hidden == true )
        {
            foreach ( Renderer renderer in
                _root.GetComponentsInChildren<Renderer>( true ) )
            {
                //다른 아이템과 보관 중인 교체 모델은 제외
                ItemCarry owner = renderer.GetComponentInParent<ItemCarry>( true );

                if ( owner == null || owner.transform != _root ||
                     CheckHoldTarget( renderer.transform ) == false )
                    continue;

                _hiddenRenderers.Add( renderer, renderer.forceRenderingOff );
                renderer.forceRenderingOff = true;
            }

            //보유 처리에서 수집한 해당 아이템의 충돌체만 사용
            foreach ( Collider collider in _itemColliders )
            {
                if ( collider == null )
                    continue;

                _hiddenColliders.Add( collider, collider.enabled );
                collider.enabled = false;
            }

            return;
        }

        foreach ( KeyValuePair<Renderer, bool> pair in _hiddenRenderers )
        {
            if ( pair.Key != null )
                pair.Key.forceRenderingOff = pair.Value;
        }

        foreach ( KeyValuePair<Collider, bool> pair in _hiddenColliders )
        {
            if ( pair.Key != null )
                pair.Key.enabled = pair.Value;
        }

        _hiddenRenderers.Clear( );
        _hiddenColliders.Clear( );

        //충돌체 재활성화 후 월드 보유자의 충돌 제외 관계 재적용
        SetHoldCollision( true );
    }

    #endregion

    #region ----- 설정 저장/적용/복원 -----

    /// <summary>
    /// 보유 레이어와 충돌 설정 복구
    /// </summary>
    /// <param name="type">복구할 보유 방식</param>
    public void RestoreHoldSettings ( HoldType type )
    {
        //임시 숨김 해제
        SetHeldHidden( false );

        if ( type == HoldType.View )
        {
            //플레이어 보유에서 바꾼 레이어와 충돌체 활성 상태를 각각 복원
            RestoreItemLayers( );

            for ( int i = 0; i < _itemColliders.Count; i++ )
            {
                if ( _itemColliders [ i ] != null )
                    _itemColliders [ i ].enabled = _originalEnabled [ i ];
            }
        }
        else if ( type == HoldType.World )
        {
            //보유자 참조를 비우기 전에 기존 충돌 제외 관계부터 해제
            SetHoldCollision( false );
            _holdColliders = Array.Empty<Collider>( );
        }
    }

    /// <summary>
    /// 현재 보유 대상의 최초 Collider 활성 상태 저장
    /// </summary>
    /// <param name="saveOriginal">원래 활성화 상태 저장 여부</param>
    public void CaptureHoldSettings ( bool saveOriginal )
    {
        //월드 보유에서 플레이어에게 전달할 때는 최초 상태 유지
        if ( saveOriginal == false )
            return;

        //최초 집기 때만 새 목록을 만들고
        //보유자 사이의 전달에서는 기존 기록 유지
        _itemColliders.Clear( );
        _originalEnabled.Clear( );

        foreach ( Collider candidate in
            _root.GetComponentsInChildren<Collider>( true ) )
        {
            //별도 물리 아이템과 보관 중인 다른 모델은 제외
            if ( candidate.attachedRigidbody != _rigid ||
                 CheckHoldTarget( candidate.transform ) == false )
                continue;

            //원래 꺼져 있던 충돌체도 기록하여
            //내려놓을 때 임의로 켜지지 않도록 함
            _itemColliders.Add( candidate );
            _originalEnabled.Add( candidate.enabled );
        }
    }

    /// <summary>
    /// 보유 방식에 따른 레이어와 충돌 설정 적용
    /// </summary>
    /// <param name="type">적용할 보유 방식</param>
    /// <param name="ownerColliders">충돌 제외 대상</param>
    public void ApplyHoldSettings ( HoldType type, Collider [ ] ownerColliders )
    {
        if ( type == HoldType.View )
        {
            //플레이어 시점 보유는 충돌을 끄고 보유 레이어용 레이어 적용
            foreach ( Collider itemCollider in _itemColliders )
            {
                if ( itemCollider != null )
                    itemCollider.enabled = false;
            }
            ApplyHeldItemLayer( );
        }
        else
        {
            //월드 보유는 충돌체를 유지하고 보유자와의 충돌만 제외
            _holdColliders = ownerColliders ?? Array.Empty<Collider>( );
            SetHoldCollision( true );
        }
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 보유 설정 적용 대상 확인
    /// </summary>
    /// <param name="target">확인할 자식 Transform</param>
    /// <returns>보유 처리에 포함할 대상 여부</returns>
    bool CheckHoldTarget ( Transform target )
    {
        if ( target == null )
            return false;

        //AppearanceComponents가 없는 아이템은 기존 처리 유지
        if ( _currentAppearance == null )
            return true;

        Transform current = target;

        //가장 가까운 AppearanceComponents가 현재 모델인지 확인
        while ( current != null && current != _root )
        {
            if ( current.TryGetComponent(
                out AppearanceComponents appearance ) == true )
                return appearance == _currentAppearance;

            current = current.parent;
        }

        //아이템 루트와 AppearanceComponents 밖의 기능용 자식은 포함
        return current == _root;
    }

    #endregion

}
