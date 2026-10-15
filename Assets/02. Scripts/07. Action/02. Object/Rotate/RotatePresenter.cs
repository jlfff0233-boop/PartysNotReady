using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 회전 중재자 - 각도 변경, 회전 적용과 상호작용 연결
/// </summary>
[RequireComponent( typeof( RotateView ) )]
public class RotatePresenter : MonoBehaviour, IInteractable
{
    [Header( "----- 회전 설정 -----" )]
    [SerializeField] float _minAngle = 30f;     //최소 회전량
    [SerializeField] float _maxAngle = 90f;     //최대 회전량

    float _minRotationAngle;     //초기화 시 확정한 최소 회전량
    float _maxRotationAngle;     //초기화 시 확정한 최대 회전량

    RotateModel _model;     //회전 모델
    RotateView _view;       //회전 뷰
    ItemCarry _item;        //선택적 보유 상태 확인
    bool _interactionAllowed;     //상위 기능에서 허용한 회전 입력

    /// <summary>
    /// 초기 회전 기준 현재 회전 각도
    /// </summary>
    public float Angle => _model != null ? _model.Angle : 0f;

    /// <summary>
    /// 회전 기능 초기화 및 활성 상태
    /// </summary>
    public bool IsReady => CheckReady( );

    /// <summary>
    /// 회전 적용 가능 여부
    /// </summary>
    public bool CanRotate => CheckCanRotate( );

    /// <summary>
    /// 회전 상호작용 가능 여부
    /// </summary>
    public bool CanInteract => CheckCanInteract( );

    /// <summary>
    /// 회전 행동 이름
    /// </summary>
    public string ActionName => "회전";

    /// <summary>
    /// 회전 각도 변경 이벤트(회전 각도)
    /// </summary>
    public event Action<float> OnAngleChanged;


    private void Awake ()
    {
        //뷰 초기화 성공 여부와 관계없이 현재 상태 모델 생성
        _model = new RotateModel( );
        _item = GetComponent<ItemCarry>( );

        //유효한 범위만 저장하고 이후 회전 요청에서 같은 범위를 사용
        if ( CheckRotationRange( ) == false )
            return;

        _minRotationAngle = _minAngle;
        _maxRotationAngle = _maxAngle;

        _view = GetComponent<RotateView>( );
        _view.Init( );
    }


    #region ----- 회전 -----

    /// <summary>
    /// 회전 상호작용 허용 여부 변경
    /// </summary>
    /// <param name="allowed">입력 허용 여부</param>
    public void SetInteractionAllowed ( bool allowed )
    {
        _interactionAllowed = allowed;
    }

    /// <summary>
    /// 대상의 회전과 현재 각도 변경
    /// </summary>
    /// <param name="angle">변경할 각도, 0 이상 360 미만</param>
    /// <returns>실제 변경 여부</returns>
    public bool TrySetAngle ( float angle )
    {
        //입력 허용과 별개로 사건 발생과 복원에서도 회전 적용 가능
        if ( CanRotate == false ||
             float.IsNaN( angle ) == true ||
             float.IsInfinity( angle ) == true ||
             angle < 0f || angle >= 360f ||
             _model.Angle == angle )
            return false;

        //회전 적용에 실패하면 모델을 변경하거나 알림을 보내지 않음
        if ( _view.TrySetAngle( angle ) == false )
            return false;

        //회전을 적용한 뒤 모델을 갱신하여 알림에서 같은 각도를 조회하도록 처리
        _model.TrySetAngle( angle );
        OnAngleChanged?.Invoke( angle );
        return true;
    }

    /// <summary>
    /// 무작위 각도만큼 회전하고 한 바퀴에 도달하면 초기 회전으로 복귀
    /// </summary>
    /// <returns>회전 성공 여부</returns>
    public bool TryRotate ()
    {
        if ( CanRotate == false )
            return false;

        float amount = Random.Range(
            _minRotationAngle, _maxRotationAngle );

        float nextAngle = _model.GetNextAngle( amount );
        return TrySetAngle( nextAngle );
    }


    /// <summary>
    /// 월드 상호작용 완료 시 한 단계 회전
    /// </summary>
    public void Interact ()
    {
        //일반 상태에서는 회전 입력을 소비하지 않고 집기 대상으로 처리
        if ( CanInteract == false )
            return;

        TryRotate( );
    }

    #endregion


    /// <summary>
    /// 플레이 중 한 단계 회전 확인
    /// </summary>
    [ContextMenu( "Test Rotate" )]
    void TestRotate ()
    {
        if ( Application.isPlaying == false )
            return;

        TryRotate( );
    }

    /// <summary>
    /// 플레이 중 초기 회전 복귀 확인
    /// </summary>
    [ContextMenu( "Test Reset Angle" )]
    void TestResetAngle ()
    {
        if ( Application.isPlaying == false )
            return;

        TrySetAngle( 0f );
    }


    #region ----- 설정/상태 검사 -----

    /// <summary>
    /// 무작위 회전량의 최소 / 최대 범위 확인
    /// </summary>
    /// <returns>0도 초과 360도 미만의 유효한 범위 여부</returns>
    bool CheckRotationRange ()
    {
        return !( float.IsNaN( _minAngle ) == true ||
                  float.IsInfinity( _minAngle ) == true ||
                  float.IsNaN( _maxAngle ) == true ||
                  float.IsInfinity( _maxAngle ) == true ||
                  _minAngle <= 0f ||
                  _maxAngle < _minAngle ||
                  _maxAngle >= 360f );
    }

    /// <summary>
    /// 회전 모델과 뷰의 사용 가능 여부 확인
    /// </summary>
    /// <returns>회전 기능 준비 여부</returns>
    bool CheckReady ( )
    {
        return isActiveAndEnabled == true &&
               _model != null &&
               _view != null &&
               _view.IsReady == true;
    }

    /// <summary>
    /// 회전 기능 준비와 아이템 보유 상태 확인
    /// </summary>
    /// <returns>회전 적용 가능 여부</returns>
    bool CheckCanRotate ( )
    {
        return IsReady && ( _item == null || _item.IsHold == false );
    }

    /// <summary>
    /// 입력 허용과 회전 적용 조건 확인
    /// </summary>
    /// <returns>회전 상호작용 가능 여부</returns>
    bool CheckCanInteract ()
    {
        return _interactionAllowed && CanRotate;
    }

    #endregion

}
