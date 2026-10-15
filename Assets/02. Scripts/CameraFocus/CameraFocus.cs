using UnityEngine;

/// <summary>
/// 1인칭 시점 제어 - 입력 회전, 회전 제한과 잠금
/// </summary>
public class CameraFocus : MonoBehaviour
{
    [Header( "----- 회전 -----" )]
    [SerializeField] float _sense;      //회전 감도
    [SerializeField] float _smoothTime;     //목표 회전에 가까워지는 보간 시간
    [SerializeField] float _minPitch;       //최소 x 회전값
    [SerializeField] float _maxPitch;       //최대 x 회전값
    [SerializeField] float _minYaw = -90f;      //부모 기준 최소 좌우 회전 각도
    [SerializeField] float _maxYaw = 90f;       //부모 기준 최대 좌우 회전 각도

    bool _isLocked;       //시점 회전 잠금 여부

    float _targetPitch;     //목표 x 회전값
    float _targetYaw;       //목표 월드 y 회전값

    float _pitchVelocity;       //x 회전 속력
    float _yawVelocity;     //y 회전 속력

    float _currentPitch;        //현재 x 회전값
    float _currentYaw;      //현재 y 회전값


    private void Awake ()
    {
        //현재 회전값으로 목표값 초기화
        _targetPitch = NormalizePitch( transform.eulerAngles.x );
        _targetYaw = transform.eulerAngles.y;

        //시작 시 불필요한 회전 보간 방지
        _currentPitch = _targetPitch;
        _currentYaw = _targetYaw;
    }

    private void LateUpdate ()
    {
        //재개 프레임에도 시간이 흐르지 않으면 보간 속도가 NaN이 될 수 있음
        if ( _isLocked == true || Time.deltaTime <= 0f )
            return;

        //현재값에서 목표값으로 부드럽게 이동
        _currentYaw = Mathf.SmoothDampAngle(
            _currentYaw, _targetYaw, ref _yawVelocity, _smoothTime );

        _currentPitch = Mathf.SmoothDampAngle(
            _currentPitch, _targetPitch, ref _pitchVelocity, _smoothTime );

        //부모 회전이 중복 적용되지 않도록 계산한 월드 각도로 시점 적용
        transform.rotation = Quaternion.Euler(
            _currentPitch, _currentYaw, 0f );
    }


    /// <summary>
    /// 시점 회전 잠금과 남은 보간 초기화
    /// </summary>
    /// <param name="isLocked">시점 잠금 여부</param>
    public void SetLocked ( bool isLocked )
    {
        _isLocked = isLocked;

        //이전 입력으로 회전하지 않도록 정리
        _targetPitch = _currentPitch;
        _targetYaw = _currentYaw;
        _pitchVelocity = 0f;
        _yawVelocity = 0f;
    }

    /// <summary>
    /// 입력에 따른 목표 시점 갱신
    /// </summary>
    /// <param name="dir">좌우(x)와 상하(y) 회전 입력</param>
    public void Rotate ( Vector2 dir )
    {
        if ( _isLocked == true )
            return;

        //월드 목표 각도와 부모 각도의 차이를 구해
        //플레이어 기준 좌우 시점 계산
        float localYaw = Mathf.DeltaAngle(
            transform.parent.eulerAngles.y, _targetYaw );

        //플레이어 기준 목표 y축 회전값 계산 후 제한
        localYaw = Mathf.Clamp(
            localYaw + dir.x * _sense, _minYaw, _maxYaw );

        //제한된 로컬 회전값을 월드 회전값으로 변환
        _targetYaw = transform.parent.eulerAngles.y + localYaw;

        //상하 입력은 부호를 반대로 반영하고 목표 각도만 갱신
        _targetPitch = Mathf.Clamp(
            _targetPitch - dir.y * _sense, _minPitch, _maxPitch );
    }


    /// <summary>
    /// x축 회전값을 -180~180 범위로 변환 후 제한
    /// </summary>
    /// <param name="pitch">x축 회전값</param>
    /// <returns>x축 회전값</returns>
    float NormalizePitch ( float pitch )
    {
        //180도를 넘는 값을 음수 각도로 변환
        if ( pitch > 180f )
            pitch -= 360f;

        //상하 회전 범위 제한
        return Mathf.Clamp( pitch, _minPitch, _maxPitch );
    }

    /// <summary>
    /// 수평 회전을 현재 시점과 목표 시점에 반영
    /// </summary>
    /// <param name="yawDelta">플레이어 좌우 회전 변화량(도)</param>
    public void ApplyCaptureYaw ( float yawDelta )
    {
        //잠금 중에는 기존 월드 시점을 유지하고,
        //잠금이 없을 때만 부모의 회전 변화량 반영
        if ( _isLocked == false )
        {
            _currentYaw += yawDelta;
            _targetYaw += yawDelta;
        }

        //부모 회전의 영향을 계산된 시점으로 보정
        transform.rotation = Quaternion.Euler(
            _currentPitch, _currentYaw, 0f );
    }


}
