using System;
using UnityEngine;

/// <summary>
/// 플레이어 포획 - 컨트롤러 복구, 리스폰과 따라가기 회전 전달
/// </summary>
[RequireComponent( typeof( CharacterController ) )]
public class PlayerCapture : MonoBehaviour
{
    CharacterController _ctrl;		//플레이어 컨트롤러
    CaptureFollower _follower;		//보유 지점 따라가기

    bool _ctrlEnabled;		//포획 전 컨트롤러 활성 상태

    /// <summary>
    /// 따라가기 적용 중 여부
    /// </summary>
    public bool IsFollowing => _follower != null && _follower.IsFollowing;

    /// <summary>
    /// 포획 따라가기 시작 가능 여부
    /// </summary>
    public bool CanBeginCapture => CheckCanBeginCapture( );

    /// <summary>
    /// 포획 따라가기 가능 여부
    /// </summary>
    public bool CanFollow => CheckCanFollow( );

    /// <summary>
    /// 따라가기 중단 이벤트
    /// </summary>
    public event Action OnCaptureInterrupted;


    /// <summary>
    /// 컨트롤러 참조 연결
    /// </summary>
    public void Init ()
    {
        _ctrl = GetComponent<CharacterController>( );
        _follower = new CaptureFollower( transform );
    }

    private void OnDisable ()
    {
        if ( IsFollowing == true )
            OnCaptureInterrupted?.Invoke( );
    }


    /// <summary>
    /// 포획 따라가기 시작
    /// </summary>
    /// <param name="carryPoint">보유 지점</param>
    /// <returns>따라가기 시작 여부</returns>
    public bool TryBeginCapture ( Transform carryPoint )
    {
        if ( CanBeginCapture == false || _follower.TryBegin( carryPoint ) == false )
            return false;

        //Transform으로 위치를 직접 맞추는 동안 컨트롤러 충돌 간섭 방지
        _ctrlEnabled = _ctrl.enabled;
        _ctrl.enabled = false;

        return true;
    }

    /// <summary>
    /// 보유 지점의 위치와 회전 따라가기
    /// </summary>
    /// <param name="yawDelta">적용한 수평 회전 변화량</param>
    /// <returns>따라가기 성공 여부</returns>
    public bool TryFollow ( out float yawDelta )
    {
        yawDelta = 0f;

        if ( CanFollow == false )
            return false;

        float previousYaw = transform.eulerAngles.y;
        if ( _follower.TryFollow( ) == false )
            return false;

        //카메라에도 같은 회전 변화를 전달할 수 있도록 최단 각도 차이 반환
        yawDelta = Mathf.DeltaAngle(
            previousYaw, transform.eulerAngles.y );

        return true;
    }

    /// <summary>
    /// 따라가기 종료와 선택적 리스폰, 컨트롤러 설정 복구
    /// </summary>
    /// <param name="respawnPoint">리스폰 지점, 없으면 현재 위치 유지</param>
    public void EndCapture ( Transform respawnPoint = null )
    {
        if ( IsFollowing == false )
            return;

        _follower.End( );

        //컨트롤러가 꺼진 상태에서 리스폰 위치 적용
        if ( respawnPoint != null )
            transform.position = respawnPoint.position;

        //일반 이동 복귀 시 몸체의 기울어짐 제거
        transform.rotation = Quaternion.Euler(
            0f, transform.eulerAngles.y, 0f );

        _ctrl.enabled = _ctrlEnabled;
    }


    /// <summary>
    /// 포획 따라가기 가능 여부 확인
    /// </summary>
    /// <returns>따라가기 가능 여부</returns>
    bool CheckCanFollow ()
    {
        return isActiveAndEnabled && _follower != null && _follower.CanFollow;
    }

    /// <summary>
    /// 포획 따라가기 준비 확인
    /// </summary>
    /// <returns>포획 따라가기 시작 가능 여부</returns>
    bool CheckCanBeginCapture ( )
    {
        return isActiveAndEnabled == true &&
               _ctrl != null &&
               _ctrl.enabled == true &&
               IsFollowing == false;
    }


}
