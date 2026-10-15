using UnityEngine;

/// <summary>
/// 회전 뷰 - 초기 상태를 기준으로 회전
/// </summary>
public class RotateView : MonoBehaviour
{
    [Header( "----- 회전 대상 -----" )]
    [SerializeField] Transform _rotatingTarget;     //회전할 자식 오브젝트

    [Header( "----- 회전 설정 -----" )]
    [SerializeField] Vector3 _axis = Vector3.forward;       //초기 상태 기준 로컬 축

    Transform _target;      //초기화 시 확정한 회전 대상
    Quaternion _initialRotation;        //초기 로컬 회전
    Vector3 _rotationAxis;      //회전 축
    bool _isInitialized;        //초기 상태 저장 여부

    /// <summary>
    /// 회전 적용 가능 여부
    /// </summary>
    public bool IsReady => CheckReady( );


    /// <summary>
    /// 회전 대상, 초기 회전과 회전축 저장
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ()
    {
        //회전한 값을 초기값으로 다시 저장하지 않도록 최초 한 번만 초기화
        if ( _isInitialized == true )
            return _target != null;

        //회전 대상 확인
        if ( _rotatingTarget == null ||
             _rotatingTarget == transform ||
             _rotatingTarget.IsChildOf( transform ) == false )
            return false;

        //유한한 값인지 확인
        if ( IsFinite( _axis.x ) == false ||
             IsFinite( _axis.y ) == false ||
             IsFinite( _axis.z ) == false )
            return false;

        float axisLength = _axis.magnitude;

        if ( IsFinite( axisLength ) == false || axisLength <= 0.00001f )
            return false;

        _target = _rotatingTarget;
        _initialRotation = _target.localRotation;
        //설정 벡터의 길이에 관계없이 방향만 회전축으로 사용
        _rotationAxis = _axis / axisLength;
        _isInitialized = true;
        return true;
    }

    /// <summary>
    /// 초기 회전값을 기준으로 회전 적용
    /// </summary>
    /// <param name="angle">적용할 회전 각도</param>
    /// <returns>회전 적용 여부</returns>
    public bool TrySetAngle ( float angle )
    {
        //준비 상태 및 각도 확인
        if ( IsReady == false ||
             IsFinite( angle ) == false ||
             angle < 0f || angle >= 360f )
            return false;

        //0도는 저장된 초기 회전을 직접 적용하고 나머지는 초기 회전에 각도를 더함
        _target.localRotation = angle == 0f
            ? _initialRotation
            : _initialRotation * Quaternion.AngleAxis( angle, _rotationAxis );

        return true;
    }


    /// <summary>
    /// 유한한 수인지 확인
    /// </summary>
    /// <param name="value">확인할 값</param>
    /// <returns>NaN과 무한대가 아닌지 여부</returns>
    bool IsFinite ( float value )
    {
        return float.IsNaN( value ) == false &&
               float.IsInfinity( value ) == false;
    }

    /// <summary>
    /// 초기화 여부와 회전 대상의 활성 상태 확인
    /// </summary>
    /// <returns>회전 적용 가능 여부</returns>
    bool CheckReady ( )
    {
        return isActiveAndEnabled == true &&
               _isInitialized == true &&
               _target != null &&
               _target.gameObject.activeInHierarchy == true;
    }


}
