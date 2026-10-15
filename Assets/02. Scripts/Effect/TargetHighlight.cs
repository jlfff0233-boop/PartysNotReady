using UnityEngine;

/// <summary>
/// 대상 강조 - 외곽선 렌더링 레이어 적용과 복원
/// </summary>
public class TargetHighlight : MonoBehaviour
{
    [Header( "----- 강조 대상 -----" )]
    [SerializeField] Renderer [ ] _renderers;		//강조할 렌더러

    [Header( "----- 외곽선 -----" )]
    [SerializeField] RenderingLayerMask _outlineLayer;		//외곽선 전용 렌더링 레이어

    uint [ ] _savedLayers;		//렌더러별 표시 전 외곽선 레이어 값
    uint _activeLayer;		//현재 강조에 적용한 레이어
    bool _isVisible;		//강조 표시 여부

    private void OnDisable ()
    {
        SetVisible( false );
    }


    /// <summary>
    /// 외곽선 레이어 적용 또는 표시 전 값 복원
    /// </summary>
    /// <param name="isVisible">강조 표시 여부</param>
    public void SetVisible ( bool isVisible )
    {
        if ( _isVisible == isVisible )
            return;

        if ( isVisible == true )
        {
            _savedLayers ??= new uint [ _renderers.Length ];
            _activeLayer = _outlineLayer;
        }

        for ( int i = 0; i < _renderers.Length; i++ )
        {
            Renderer target = _renderers [ i ];		//강조할 렌더러

            //표시 중 파괴된 렌더러는 복원 대상에서 제외
            if ( target == null )
                continue;

            uint currentLayer = target.renderingLayerMask;		//현재 렌더링 레이어

            if ( isVisible == true )
            {
                //외곽선에 사용하는 비트만 보관하고 추가
                _savedLayers [ i ] = currentLayer & _activeLayer;
                target.renderingLayerMask = currentLayer | _activeLayer;
            }
            else
            {
                //다른 레이어 변경은 유지하고 외곽선 비트만 복원
                target.renderingLayerMask =
                    ( currentLayer & ~_activeLayer ) | _savedLayers [ i ];
            }
        }

        _isVisible = isVisible;
    }

}
