using System;
using UnityEngine;

/// <summary>
/// 아이템 투척 중재 - 투척, 감속/물리 설정 복구
/// </summary>
public class ItemThrow : ItemCarry, IThrowable
{
    [Header( "----- 지면 감속 -----" )]
    [SerializeField] float _groundDeceleration;     //지면 이동 감속량
    [SerializeField] float _angularDeceleration;     //지면 회전 감속량
    [SerializeField] float _stopSpeed;     //이동 정지 기준
    [SerializeField] float _stopAngularSpeed;     //회전 정지 기준
    [SerializeField] float _stopDelay;     //정지 조건 유지 시간

    ThrowModel _throwModel;     //투척 진행 모델

    /// <summary>
    /// 아이템 로컬 기준 콜라이더 중심
    /// </summary>
    public Vector3 LocalBoundsCenter => _view.LocalBoundsCenter;
    /// <summary>
    /// 투척 궤적의 시작 중심
    /// </summary>
    public Vector3 ThrowPosition => _view.ThrowPosition;
    /// <summary>
    /// 궤적 검사 반경
    /// </summary>
    public float ThrowRadius => PlacementBoundsSize.magnitude * 0.5f;
    /// <summary>
    /// 투척 이벤트
    /// </summary>
    public event Action OnThrown;


    /// <summary>
    /// 공통 뷰 및 투척 모델 초기화
    /// </summary>
    protected override void Init ()
    {
        base.Init( );
        _throwModel = new ThrowModel(
            _groundDeceleration, _angularDeceleration,
            _stopSpeed, _stopAngularSpeed, _stopDelay );
    }

    /// <summary>
    /// 준비된 아이템의 접촉 알림 연결
    /// </summary>
    protected override void OnEnable ()
    {
        base.OnEnable( );
        if ( IsReady )
            _view.OnContact += HandleContact;
    }

    /// <summary>
    /// 투척 중 충돌 복구와 지면 감속 적용
    /// </summary>
    void FixedUpdate ()
    {
        if ( IsReady == false ||
            _throwModel == null || _throwModel.IsThrown == false )
            return;

        //투척 직후 몸체와 겹치는 동안만 투척자 충돌 제외
        if ( _view.HasOwnerOverlap( ) == false )
            _view.RestoreOwnerCollision( );     //던진 투척자와의 충돌 복구

        //현재 이동, 회전 속도 가져오기
        _view.GetVelocity( out Vector3 velocity, out Vector3 angularVelocity );

        bool isSleeping = _view.IsSleeping( );      //리지드바디 정지 여부
        bool isGrounded = _throwModel.IsGrounded;       //지면 접촉 여부
        //완전 정지 적용 여부
        bool shouldStop = _throwModel.UpdateDeceleration(
            ref velocity, ref angularVelocity, Time.fixedDeltaTime, isSleeping );

        //이미 멈춘 물체는 속도를 다시 설정하지 않음
        if ( isSleeping == true ) return;

        if ( shouldStop == true )
            _view.StopMotion( );
        else if ( isGrounded == true )
            _view.SetVelocity( velocity, angularVelocity );
    }

    /// <summary>
    /// 접촉 구독과 투척 상태 정리
    /// </summary>
    protected override void OnDisable ()
    {
        if ( _view != null )
            _view.OnContact -= HandleContact;
        base.OnDisable( );
        ResetThrowState( );
    }


    #region ----- 투척 -----

    /// <summary>
    /// 접촉면 정보를 투척 모델에 전달
    /// </summary>
    /// <param name="normal">접촉면 방향</param>
    void HandleContact ( Vector3 normal )
    {
        _throwModel.RecordContact( normal );
    }

    /// <summary>
    /// 보유 아이템 해제/초기 속도 적용
    /// </summary>
    /// <param name="velocity">초기 속도</param>
    /// <param name="ownerColliders">투척자 콜라이더</param>
    /// <returns>투척 성공 여부</returns>
    public bool TryThrow (
        Vector3 velocity, Collider [ ] ownerColliders )
    {
        //카메라 보유 상태에서만 투척 가능
        if ( isActiveAndEnabled == false || IsReady == false ||
             _throwModel == null || CurrentType != HoldType.View ) 
            return false;

        //투척 전 설정 저장
        _view.SaveThrowSettings( );

        if ( TryRelease( ) == false )
            return false;

        //해제 알림에서 소비되거나 비활성화됐다면
        //투척 물리를 적용하지 않음
        if ( isActiveAndEnabled == false )
            return false;

        _throwModel.BeginThrow( );
        _view.SetThrow( velocity, ownerColliders );

        //투척 이벤트 발행
        OnThrown?.Invoke( );
        return true;
    }

    /// <summary>
    /// 투척 상태와 투척 전 감쇠 설정 복구
    /// </summary>
    void ResetThrowState ()
    {
        if ( _view == null || _view.IsReady == false || _throwModel == null )
            return;

        //충돌 처리 복구
        _view.RestoreOwnerCollision( );

        //감쇠 설정 복구
        if ( _throwModel.IsThrown == true )
            _view.RestoreThrowSettings( );

        //투척 상태 초기화
        _throwModel.ResetThrowState( );
    }

    /// <summary>
    /// 보유 전환 전 투척 상태 정리
    /// </summary>
    protected override void PrepareHold ()
    {
        base.PrepareHold( );
        ResetThrowState( );
    }

    /// <summary>
    /// 강제 이동 전 투척 상태와 물리 설정 복구
    /// </summary>
    protected override void PrepareDisplacement ()
    {
        base.PrepareDisplacement( );
        ResetThrowState( );
    }

    /// <summary>
    /// 강제 배치 완료 후 투척 종료와 감쇠 설정 복구
    /// </summary>
    public override void CompleteForcedPlacement ()
    {
        base.CompleteForcedPlacement( );

        ResetThrowState( );
    }

    #endregion

}
