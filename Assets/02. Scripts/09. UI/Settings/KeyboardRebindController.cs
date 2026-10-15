using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 키 지정 입력 제어 - 키 감지와 게임 및 UI 입력 차단, 복원
/// </summary>
public class KeyboardRebindController : IDisposable
{
    bool _isCapturing;		//키 입력 대기 여부
    KeyControl _releaseKey;		//입력 복원 전 해제를 기다릴 키
    int _captureFrame;		//대기 시작 프레임

    /// <summary>
    /// 키 입력 대기 전에 활성 상태였던 액션 목록
    /// </summary>
    List<InputAction> _suspendedActions = new( );

    /// <summary>
    /// 지정한 키 알림(키 바인딩 경로)
    /// </summary>
    public event Action<string> OnKeyCaptured;

    /// <summary>
    /// 키 지정 취소 알림(키보드 연결 필요 여부)
    /// </summary>
    public event Action<bool> OnCaptureCanceled;

    /// <summary>
    /// 키 입력 대기 시작
    /// </summary>
    public void BeginCapture ()
    {
        CancelCapture( );
        _isCapturing = true;
        _captureFrame = Time.frameCount;

        //게임 입력과 UI 키 탐색만 중단하고 마우스 버튼 입력은 유지
        foreach ( InputActionMap map in InputSystem.actions.actionMaps )
        {
            foreach ( InputAction action in map.actions )
            {
                bool suspend = map.name == "Player" ||
                    ( map.name == "UI" && ( action.name == "Navigate" ||
                      action.name == "Submit" || action.name == "Cancel" ) );		//대기 중 중단할 입력

                if ( suspend == true && action.enabled == true )
                    _suspendedActions.Add( action );
            }
        }

        foreach ( InputAction action in _suspendedActions )
            action.Disable( );
    }

    /// <summary>
    /// 키 입력 대기를 취소하고 입력 즉시 복원
    /// </summary>
    public void CancelCapture ()
    {
        _isCapturing = false;
        RestoreInput( );
    }

    /// <summary>
    /// 패널 종료 시 남은 입력 차단 정리
    /// </summary>
    public void Dispose ()
    {
        CancelCapture( );
    }

    /// <summary>
    /// 키 감지와 입력한 키의 해제 대기
    /// </summary>
    public void Tick ()
    {
        if ( _releaseKey != null )
        {
            if ( _releaseKey.device.added == false || _releaseKey.isPressed == false )
                RestoreInput( );

            return;
        }

        if ( _isCapturing == false || Time.frameCount == _captureFrame )
            return;

        Keyboard keyboard = Keyboard.current;		//현재 입력 키보드
        if ( keyboard == null )
        {
            CancelCapture( );
            OnCaptureCanceled?.Invoke( true );
            return;
        }

        foreach ( KeyControl key in keyboard.allKeys )
        {
            if ( key.wasPressedThisFrame == false )
                continue;

            _isCapturing = false;
            _releaseKey = key;

            //입력한 키를 놓은 뒤 복원하여 즉시 이동이나 UI 확인이 실행되지 않게 함
            if ( key == keyboard.escapeKey )
                OnCaptureCanceled?.Invoke( false );
            else
                OnKeyCaptured?.Invoke( $"<Keyboard>/{key.name}" );

            break;
        }
    }

    /// <summary>
    /// 화면 닫기보다 키 지정 취소를 우선 처리
    /// </summary>
    /// <returns>키 지정에서 취소 입력을 처리했는지 여부</returns>
    public bool TryCancelInput ()
    {
        Keyboard keyboard = Keyboard.current;		//취소 입력 키보드
        //같은 ESC를 Tick이 먼저 처리한 경우 화면 유지
        if ( keyboard != null && _releaseKey == keyboard.escapeKey )
            return true;

        if ( _isCapturing == false )
            return false;

        _isCapturing = false;
        _releaseKey = keyboard == null ? null : keyboard.escapeKey;
        OnCaptureCanceled?.Invoke( false );
        return true;
    }

    /// <summary>
    /// 원래 활성 상태였던 입력만 복원
    /// </summary>
    void RestoreInput ()
    {
        foreach ( InputAction action in _suspendedActions )
            action.Enable( );

        _suspendedActions.Clear( );
        _releaseKey = null;
    }
}
