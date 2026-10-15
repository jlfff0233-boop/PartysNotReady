#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 적 테스트 공통 조건과 방 활동 표시
/// </summary>
public static class EnemyDebugUtility
{
    /// <summary>
    /// 활동 재준비에 필요한 기본 테스트 조건 확인
    /// </summary>
    /// <param name="enemy">테스트할 적</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>테스트 준비 가능 여부</returns>
    public static bool CheckReady ( Enemy enemy, out string reason )
    {
        reason = string.Empty;
        if ( Application.isPlaying == false || enemy.DebugIsReady == false || enemy.isActiveAndEnabled == false )
            reason = "플레이 중 초기화된 활성 적이 필요합니다.";
        return reason.Length == 0;
    }

    /// <summary>
    /// 같은 씬의 가까운 포획 대상 선택
    /// </summary>
    /// <typeparam name="T">NPC 또는 플레이어</typeparam>
    /// <param name="enemy">테스트할 적</param>
    /// <returns>포획 가능한 대상 또는 없음</returns>
    public static T FindCaptureTarget<T> ( Enemy enemy ) where T : MonoBehaviour, ICaptureTarget, IAttackTarget
    {
        T selected = null;
        float minDist = float.PositiveInfinity;
        foreach ( T candidate in Object.FindObjectsByType<T>( FindObjectsSortMode.None ) )
        {
            Transform target = ( ( IAttackTarget ) candidate ).TargetTransform;
            if ( candidate.isActiveAndEnabled == false ||
                 candidate.gameObject.scene != enemy.gameObject.scene || candidate.CanCapture == false ||
                 RoomRegister.TryGetRoom( target.position, out _ ) == false )
                continue;

            float dist = ( target.position - enemy.transform.position ).sqrMagnitude;
            if ( dist >= minDist )
                continue;
            minDist = dist;
            selected = candidate;
        }
        return selected;
    }

    /// <summary>
    /// 대상 이름 표시
    /// </summary>
    /// <param name="value">표시 대상</param>
    /// <returns>대상 이름</returns>
    public static string Name ( Object value )
    {
        return value != null ? value.name : "없음";
    }
}
#endif
