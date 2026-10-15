using UnityEngine;

/// <summary>
/// 이동 애니메이션 - 이동 속력 표시와 초기화
/// </summary>
public class AnimatorHandler : MonoBehaviour
{
    static int _moveSpeedId = Animator.StringToHash ( "MoveSpeed" );        //이동 속력 ID

    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] Animator _animator;		//애니메이터

    /// <summary>
    /// 전용 동작을 적용할 애니메이터
    /// </summary>
    protected Animator TargetAnimator => _animator;


    protected virtual void OnEnable ( )
    {
        ResetAnimation ( );
    }

    protected virtual void OnDisable ( )
    {
        ResetAnimation ( );
    }


    /// <summary>
    /// 수평 이동 속력 적용
    /// </summary>
    /// <param name="speed">실제 수평 이동 속력</param>
    public void SetMoveSpeed ( float speed )
    {
        if ( CanApplyAnimation( ) == false )
            return;

        _animator.SetFloat ( _moveSpeedId , Mathf.Max ( 0f , speed ) );
    }

    /// <summary>
    /// 이동 표시 초기화
    /// </summary>
    public virtual void ResetAnimation ( )
    {
        if ( CanApplyAnimation( ) == false )
            return;

        _animator.SetFloat ( _moveSpeedId , 0f );
    }
    /// <summary>
    /// 애니메이션 파라미터 적용 가능 여부 확인
    /// </summary>
    /// <returns>활성 Animator와 Controller 연결 여부</returns>
    protected bool CanApplyAnimation ()
    {
        return _animator != null &&
               _animator.isActiveAndEnabled == true &&
               _animator.runtimeAnimatorController != null;
    }

}
