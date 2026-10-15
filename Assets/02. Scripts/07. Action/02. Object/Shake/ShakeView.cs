using UnityEngine;

/// <summary>
/// 흔들림 뷰 - 대상의 반복 이동과 기울기 적용
/// </summary>
public class ShakeView : MonoBehaviour
{
    [Header ( "----- 흔들림 대상 -----" )]
    [SerializeField] Transform _shakingTarget;     //흔들릴 오브젝트

    [Header ( "----- 흔들림 설정 -----" )]
    [SerializeField, Min ( 0f )] float _moveDist = 0.03f;       //좌우 이동 폭
    [SerializeField, Min ( 0f )] float _tiltAngle = 5f;     //앞뒤 기울기
    [SerializeField, Min ( 0f )] float _speed = 3f;     //흔들림 속도

    Vector3 _initialPosition;       //초기 로컬 위치
    Quaternion _initialRotation;        //초기 로컬 회전
    float _phase;       //현재 반복 구간
    bool _isInitialized;        //초기 위치와 회전 저장 여부

    /// <summary>
    /// 흔들림 가능 여부
    /// </summary>
    public bool IsReady => CheckReady( );

    /// <summary>
    /// 초기 위치와 회전 저장
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( )
    {
        //흔들리는 도중 재호출되어도 복원 기준은 처음 저장한 값으로 유지
        if ( _isInitialized == true )
            return IsReady;

        if ( _shakingTarget == null ||
             _shakingTarget == transform ||
             _shakingTarget.IsChildOf ( transform ) == false )
        {
            Debug.LogWarning ( "흔들림 대상에는 현재 오브젝트의 자식 Transform을 지정해 주세요." , this );
            return false;
        }

        _initialPosition = _shakingTarget.localPosition;
        _initialRotation = _shakingTarget.localRotation;
        _phase = 0f;
        _isInitialized = true;
        return true;
    }

    private void OnDisable ( )
    {
        //뷰만 비활성화되어도 이동과 기울기가 남지 않도록 복원
        ResetPose ( );
    }


    /// <summary>
    /// 경과시간에 따라 좌우 이동과 기울기 갱신
    /// </summary>
    /// <param name="deltaTime">이번 프레임 경과시간</param>
    public void UpdateShake ( float deltaTime )
    {
        if ( IsReady == false )
            return;

        //경과시간을 반복 구간으로 환산하고 한 주기 안에서 유지
        _phase = Mathf.Repeat (
            _phase + deltaTime * _speed * Mathf.PI * 2f , Mathf.PI * 2f );

        float movement = Mathf.Sin ( _phase );
        //좌우로 한 번 왕복하는 동안 기울기는 두 번 왕복
        float tilt = Mathf.Sin ( _phase * 2f );

        //현재 위치와 회전에 더하지 않고 초기 위치와 회전에서 계산
        _shakingTarget.localPosition =
            _initialPosition + _initialRotation * Vector3.right * ( movement * _moveDist );

        _shakingTarget.localRotation =
            _initialRotation * Quaternion.Euler ( tilt * _tiltAngle , 0f , 0f );
    }

    /// <summary>
    /// 반복 구간을 초기화하고 초기 위치와 회전 복원
    /// </summary>
    public void ResetPose ( )
    {
        _phase = 0f;

        //비활성화 중에도 저장한 위치와 회전은 복원
        if ( _isInitialized == false || _shakingTarget == null )
            return;

        _shakingTarget.localPosition = _initialPosition;
        _shakingTarget.localRotation = _initialRotation;
    }


    /// <summary>
    /// 초기화 여부와 흔들림 대상의 연결 확인
    /// </summary>
    /// <returns>흔들림 적용 가능 여부</returns>
    bool CheckReady ( )
    {
        return isActiveAndEnabled == true && _isInitialized == true && _shakingTarget != null;
    }


}
