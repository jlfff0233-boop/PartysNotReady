using UnityEngine;

/// <summary>
/// NPC 애니메이션 - 이동과 피포획 표시
/// </summary>
public class NpcAnimatorHandler : AnimatorHandler
{
    static int _capturedId = Animator.StringToHash( "IsCaptured" );        //피포획 ID


    /// <summary>
    /// 피포획 상태 적용
    /// </summary>
    /// <param name="isCaptured">포획된 상태 여부</param>
    public void SetCaptured ( bool isCaptured )
    {
        if ( TargetAnimator == null )
            return;

        TargetAnimator.SetBool( _capturedId, isCaptured );
    }

    /// <summary>
    /// 이동과 피포획 표시 초기화
    /// </summary>
    public override void ResetAnimation ()
    {
        base.ResetAnimation( );
        SetCaptured( false );
    }

#if UNITY_EDITOR
    /// <summary>
    /// 포획 표시와 현재 애니메이션 상태 조회
    /// </summary>
    /// <returns>포획 파라미터와 현재 및 다음 상태 해시</returns>
    public string GetCaptureDebugDetails ()
    {
        if ( CanApplyAnimation( ) == false || TargetAnimator.isInitialized == false )
            return "Animator 비활성, Controller 없음 또는 초기화 전";

        AnimatorStateInfo current = TargetAnimator.GetCurrentAnimatorStateInfo( 0 );
        bool transitioning = TargetAnimator.IsInTransition( 0 );
        int next = transitioning == true ? TargetAnimator.GetNextAnimatorStateInfo( 0 ).shortNameHash : 0;
        return $"IsCaptured {TargetAnimator.GetBool( _capturedId )} / 상태 해시 {current.shortNameHash} / " +
            $"진행 {current.normalizedTime:F2} / 전환 {transitioning} / 다음 해시 {next}";
    }
#endif
}
