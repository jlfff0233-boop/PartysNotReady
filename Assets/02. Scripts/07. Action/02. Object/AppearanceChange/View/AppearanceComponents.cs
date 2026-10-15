using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외형 구성 - Renderer, Collider와 색 변경 슬롯 제공
/// </summary>
public class AppearanceComponents : MonoBehaviour
{
    [Header( "----- 표시/충돌 -----" )]
    [SerializeField] Renderer [ ] _renderers;     //외형 표시 대상
    [SerializeField] Collider [ ] _colliders;     //외형 충돌 대상
    [SerializeField] Collider _mainCollider;     //배치와 중심 계산 기준

    [Header( "----- 색 변경 -----" )]
    [SerializeField] Renderer _colorRenderer;     //색 변경 대상, 미지정 시 미지원
    [SerializeField, Min( 0 )] int _materialIndex;     //색 변경 슬롯

    /// <summary>
    /// 외형 표시 대상 목록
    /// </summary>
    public IReadOnlyList<Renderer> Renderers => _renderers;

    /// <summary>
    /// 외형 충돌 대상 목록
    /// </summary>
    public IReadOnlyList<Collider> Colliders => _colliders;

    /// <summary>
    /// 배치와 중심 계산용 대표 Collider
    /// </summary>
    public Collider MainCollider => _mainCollider;

    /// <summary>
    /// 색 변경 Renderer와 슬롯 조회
    /// </summary>
    /// <param name="target">색 변경 Renderer, 미지원 시 null</param>
    /// <param name="materialIndex">색 변경 슬롯, 미지원 시 -1</param>
    /// <returns>색 변경 슬롯 사용 가능 여부</returns>
    public bool TryGetColorSlot (
        out Renderer target, out int materialIndex )
    {
        target = null;
        materialIndex = -1;

        if ( CheckColorSlot( ) == false )
            return false;

        target = _colorRenderer;
        materialIndex = _materialIndex;
        return true;
    }

    /// <summary>
    /// 색 변경 Renderer와 슬롯이 유효한지 확인
    /// </summary>
    /// <returns>유효한 색 변경 슬롯 여부</returns>
    bool CheckColorSlot ()
    {
        if ( _colorRenderer == null || _materialIndex < 0 )
            return false;

        //외형 바깥의 Renderer를 변경 대상으로 사용하지 않음
        Transform colorTransform = _colorRenderer.transform;
        if ( colorTransform != transform &&
             colorTransform.IsChildOf( transform ) == false )
            return false;

        return _materialIndex < _colorRenderer.sharedMaterials.Length;
    }
}
