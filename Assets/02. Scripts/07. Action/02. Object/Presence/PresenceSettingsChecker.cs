using UnityEngine;

/// <summary>
/// 존재 설정 검사 - 표시 대상과 빈자리 조사 영역의 구성 확인
/// </summary>
public static class PresenceSettingsChecker
{

    /// <summary>
    /// 표시 대상과 콜라이더 설정 확인
    /// </summary>
    /// <param name="root">존재 기능 관리 루트</param>
    /// <param name="target">등장과 숨김 대상</param>
    /// <param name="collider">대상의 대표 충돌체</param>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>표시 기능 준비 여부</returns>
    public static bool CheckDisplayConfig (
        Transform root, GameObject target, Collider collider, out string reason )
    {
        reason = string.Empty;

        if ( target == null )
        {
            reason = "PresenceView의 Target에 표시할 자식 오브젝트를 연결해 주세요.";
            return false;
        }

        if ( target == root.gameObject ||
             target.transform.IsChildOf( root ) == false )
        {
            reason = "Target은 관리 루트 자신이 아닌 자식 오브젝트여야 합니다.";
            return false;
        }

        if ( collider == null )
        {
            reason = "PresenceView의 Collider에 표시 대상의 콜라이더를 연결해 주세요.";
            return false;
        }

        if ( collider.gameObject != target &&
             collider.transform.IsChildOf( target.transform ) == false )
        {
            reason = "Collider는 Target 또는 그 자식 오브젝트에 있어야 합니다.";
            return false;
        }

        if ( collider.isTrigger == true )
        {
            reason = "표시 대상 Collider의 Is Trigger를 꺼 주세요.";
            return false;
        }

        return true;
    }


    /// <summary>
    /// 지정한 표시 대상과 빈자리 조사 영역의 분리 확인
    /// </summary>
    /// <param name="root">존재 기능 관리 루트</param>
    /// <param name="target">검사할 표시 대상</param>
    /// <param name="collider">표시 대상의 대표 충돌체</param>
    /// <param name="restoreCollider">빈자리 조사 영역</param>
    /// <param name="reason">설정 실패 사유</param>
    public static bool CheckRestoreConfig (
        Transform root, GameObject target, Collider collider,
        Collider restoreCollider, out string reason )
    {
        reason = string.Empty;

        if ( restoreCollider == null )
        {
            reason = "복구 조사에 사용할 Restore Check Collider를 연결해 주세요.";
            return false;
        }

        if ( target == null || restoreCollider == collider )
        {
            reason = "표시 대상과 별도의 복구 조사 Collider를 연결해 주세요.";
            return false;
        }

        if ( restoreCollider.isTrigger == true )
        {
            reason = "복구 조사 Collider의 Is Trigger를 꺼 주세요.";
            return false;
        }

        Transform restoreTransform = restoreCollider.transform;

        //조사 영역은 관리 루트 아래에서 표시 대상과 별도로 유지
        if ( restoreTransform == root ||
             restoreTransform.IsChildOf( root ) == false ||
             restoreTransform.IsChildOf( target.transform ) == true ||
             target.transform.IsChildOf( restoreTransform ) == true )
        {
            reason = "복구 조사 영역은 관리 루트의 자식이며 표시 대상과 서로 별도인 계층이어야 합니다.";
            return false;
        }

        return true;
    }

}
