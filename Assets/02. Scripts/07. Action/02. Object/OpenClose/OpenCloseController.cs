using System;
using UnityEngine;

/// <summary>
/// 개폐 제어 - 상태 변경, 뚜껑 위치와 회전 적용
/// </summary>
[RequireComponent ( typeof ( OpenClosePlacement ) )]
public class OpenCloseController : MonoBehaviour, IInteractable
{
    [Header ( "----- 초기 설정 -----" )]
    [SerializeField] bool _startOpen;		//시작 시 열림 여부

    OpenClosePlacement _placement;		//뚜껑 배치 모듈
    OpenCloseModel _model;		//개폐 상태 모델

    /// <summary>
    /// 현재 열림 여부
    /// </summary>
    public bool IsOpen => _model != null && _model.IsOpen;

    /// <summary>
    /// 개폐 상호작용 가능 여부
    /// </summary>
    public bool CanInteract => CheckCanInteract( );

    /// <summary>
    /// 개폐 행동 이름
    /// </summary>
    public string ActionName => IsOpen == true ? "닫기" : "열기";

    /// <summary>
    /// 개폐 상태 변경 이벤트(열림 여부)
    /// </summary>
    public event Action<bool> OnOpenChanged;

    private void Awake ( )
    {
        _placement = GetComponent<OpenClosePlacement> ( );

        if ( _placement.Init ( ) == false )
            return;

        //닫힌 위치와 회전을 기준으로 초기화하고 시작 열림은 Start에서 요청
        _model = new OpenCloseModel ( false );
        _placement.SetOpen ( false );
    }

    private void Start ( )
    {
        if ( _startOpen == true )
            TrySetOpen ( true );
    }


    /// <summary>
    /// 상호작용으로 개폐 전환
    /// </summary>
    public void Interact ( )
    {
        Toggle ( );
    }

    /// <summary>
    /// 개폐 상태와 뚜껑의 위치/회전 변경
    /// </summary>
    /// <param name="isOpen">변경할 열림 여부</param>
    /// <returns>실제 상태 변경 여부</returns>
    public bool TrySetOpen ( bool isOpen )
    {
        if ( CanInteract == false )
            return false;

        if ( _model.IsOpen == isOpen )
            return false;

        //열림 위치와 회전 계산에 실패하면 현재 상태 유지
        if ( isOpen == true && _placement.TryCalculateOpenPose ( ) == false )
            return false;

        if ( _model.TrySetOpen ( isOpen ) == false )
            return false;		//개폐 상태와 뚜껑의 위치/회전을 반영한 뒤 변경 알림
        _placement.SetOpen ( _model.IsOpen );
        OnOpenChanged?.Invoke ( _model.IsOpen );
        return true;
    }

    /// <summary>
    /// 현재 개폐 상태를 반대로 전환
    /// </summary>
    public void Toggle ( )
    {
        TrySetOpen ( IsOpen == false );
    }

    /// <summary>
    /// 개폐 가능 여부 확인
    /// </summary>
    /// <returns>개폐 상호작용 가능 여부</returns>
    bool CheckCanInteract ( )
    {
        return isActiveAndEnabled == true &&
               _model != null && _placement != null &&
               _placement.IsReady == true;
    }


    /// <summary>
    /// 플레이 중 상자 열기 확인
    /// </summary>
    [ContextMenu ( "Test Open" )]
    void TestOpen ( )
    {
        if ( Application.isPlaying == false )
            return;

        TrySetOpen ( true );
    }

    /// <summary>
    /// 플레이 중 상자 닫기 확인
    /// </summary>
    [ContextMenu ( "Test Close" )]
    void TestClose ( )
    {
        if ( Application.isPlaying == false )
            return;

        TrySetOpen ( false );
    }

    /// <summary>
    /// 플레이 중 상자 개폐 전환 확인
    /// </summary>
    [ContextMenu ( "Test Toggle" )]
    void TestToggle ( )
    {
        if ( Application.isPlaying == false )
            return;

        Toggle ( );
    }


}
