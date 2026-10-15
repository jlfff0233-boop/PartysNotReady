using UnityEngine;

/// <summary>
/// 상호작용 탐지 모듈 - 화면 중앙에서 가장 가까운 충돌체 탐지
/// </summary>
public static class InteractionRaycaster
{
    /// <summary>
    /// 화면 중앙의 시선과 거리 안에서 충돌체 탐지
    /// </summary>
    /// <param name="camera">시선 기준 카메라</param>
    /// <param name="distance">최대 탐지 거리</param>
    /// <param name="layerMask">탐지할 레이어</param>
    /// <param name="triggerInteraction">트리거 충돌체 탐지 방식</param>
    /// <param name="hit">탐지한 충돌 정보</param>
    /// <returns>충돌체 탐지 여부</returns>
    public static bool TryGetHit (
        Camera camera, float distance, LayerMask layerMask,
        QueryTriggerInteraction triggerInteraction, out RaycastHit hit )
    {
        hit = default;

        if ( camera == null )
            return false;

        Ray ray = camera.ViewportPointToRay( new Vector3( 0.5f, 0.5f, 0f ) );
        return Physics.Raycast(
            ray, out hit, distance, layerMask, triggerInteraction );
    }
}
