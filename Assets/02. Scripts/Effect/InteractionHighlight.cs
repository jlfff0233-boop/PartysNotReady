using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 상호작용 강조 - 선택한 대상의 표시 전환과 해제
/// </summary>
public class InteractionHighlight
{
    Component _target;		//현재 선택 대상
    TargetHighlight _highlight;		//현재 표시 중인 강조

    //외곽선 전용 레이어 InteractionOutline에 해당하는 비트를 조회
    uint _outlineLayer = RenderingLayerMask.GetMask( "InteractionOutline" );

    /// <summary>
    /// 자동 강조 대상에 속한 모델 Renderer 목록
    /// </summary>
    List<Renderer> _renderers = new( );

    /// <summary>
    /// Renderer별 표시 전 외곽선 비트 목록
    /// </summary>
    List<uint> _savedLayers = new( );


    /// <summary>
    /// 수동 및 자동 강조를 해제하고 이전 외곽선 비트 복원
    /// </summary>
    public void Clear ()
    {
        if ( _highlight != null )
            _highlight.SetVisible( false );

        for ( int i = 0; i < _savedLayers.Count; i++ )
        {
            Renderer target = _renderers [ i ];		//복원할 Renderer
            if ( target == null )
                continue;

            //다른 Rendering Layer 변경은 유지
            target.renderingLayerMask =
                ( target.renderingLayerMask & ~_outlineLayer ) | _savedLayers [ i ];
        }

        _renderers.Clear( );
        _savedLayers.Clear( );
        _highlight = null;
        _target = null;
    }

    /// <summary>
    /// 선택 대상에 연결된 강조 조회
    /// </summary>
    /// <param name="target">실제 상호작용 대상</param>
    /// <returns>대상의 강조 컴포넌트</returns>
    TargetHighlight FindHighlight ( Component target )
    {
        if ( target is CookController product && product.Highlight != null )
            return product.Highlight;

        return target.GetComponentInParent<TargetHighlight>( );
    }

    /// <summary>
    /// 상호작용 대상이 소유한 외형 범위 조회
    /// </summary>
    /// <param name="target">실제 상호작용 대상</param>
    /// <returns>아이템 루트 또는 대상 자체</returns>
    Transform GetRendererRoot ( Component target )
    {
        ItemCarry item = target.GetComponentInParent<ItemCarry>( );		//소유 아이템
        return item != null ? item.transform : target.transform;
    }

    /// <summary>
    /// 대상에 속한 모델 Renderer를 수집하고 자동 강조 적용
    /// </summary>
    /// <param name="target">실제 상호작용 대상</param>
    void ShowAutomatic ( Component target )
    {
        if ( _outlineLayer == 0 )
            return;

        Transform root = GetRendererRoot( target );		//외형 탐색 범위
        ItemCarry owner = root.GetComponentInParent<ItemCarry>( );		//소유 아이템

        //비활성 교체 모델도 수집하여 활성 전환에 대응
        root.GetComponentsInChildren<Renderer>( true, _renderers );

        for ( int i = _renderers.Count - 1; i >= 0; i-- )
        {
            Renderer renderer = _renderers [ i ];		//검사할 Renderer

            //파티클, 선, UI 등은 자동 강조에서 제외
            bool isModel = renderer is MeshRenderer
                || renderer is SkinnedMeshRenderer;

            //그릇의 재료나 캐릭터가 운반하는 다른 아이템은 제외
            bool isOwned = renderer != null
                && renderer.GetComponentInParent<ItemCarry>( ) == owner;

            if ( isModel == false || isOwned == false )
                _renderers.RemoveAt( i );
        }

        foreach ( Renderer renderer in _renderers )
        {
            uint currentLayer = renderer.renderingLayerMask;		//기존 레이어

            //외곽선 비트만 보관하고 추가
            _savedLayers.Add( currentLayer & _outlineLayer );
            renderer.renderingLayerMask = currentLayer | _outlineLayer;
        }
    }

    /// <summary>
    /// 직접 지정된 강조를 우선 사용하고 없으면 모델 자동 강조
    /// </summary>
    /// <param name="target">새로운 대상, 없으면 표시 해제</param>
    public void SetTarget ( Component target )
    {
        if ( target == null || target.gameObject.activeInHierarchy == false )
        {
            Clear( );
            return;
        }

        //동일한 대상을 보는 동안 Renderer 탐색을 반복하지 않음
        if ( _target == target )
        {
            if ( _highlight != null && _highlight.isActiveAndEnabled == false )
                Clear( );

            return;
        }

        Clear( );

        TargetHighlight highlight = FindHighlight( target );		//대상의 강조
        //명시적으로 비활성화한 설정을 자동 강조로 대체하지 않음
        if ( highlight != null && highlight.isActiveAndEnabled == false )
            return;

        _target = target;
        _highlight = highlight;
        if ( _highlight != null )
            _highlight.SetVisible( true );
        else
            ShowAutomatic( target );
    }

    /// <summary>
    /// 현재 대상의 외형 구성 변경 후 강조 다시 적용
    /// </summary>
    public void Refresh ()
    {
        Component target = _target;		//다시 적용할 대상

        Clear( );
        SetTarget( target );
    }
}
