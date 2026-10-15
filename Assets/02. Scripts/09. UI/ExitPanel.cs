using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 나가기 패널 - 타이틀 복귀, 게임 종료와 취소 요청 전달
/// </summary>
public class ExitPanel : MonoBehaviour
{
    [SerializeField] Button _titleButton; //타이틀 복귀
    [SerializeField] Button _quitButton; //게임 종료
    [SerializeField] Button _closeButton; //팝업 닫기

    /// <summary>
    /// 타이틀 복귀 요청
    /// </summary>
    public event Action OnTitleRequested;
    /// <summary>
    /// 게임 종료 요청
    /// </summary>
    public event Action OnQuitRequested;
    /// <summary>
    /// 팝업 닫기 요청
    /// </summary>
    public event Action OnCloseRequested;

    private void OnEnable ()
    {
        _titleButton.onClick.AddListener( HandleTitle );
        _quitButton.onClick.AddListener( HandleQuit );
        _closeButton.onClick.AddListener( HandleClose );
    }

    private void OnDisable ()
    {
        _titleButton.onClick.RemoveListener( HandleTitle );
        _quitButton.onClick.RemoveListener( HandleQuit );
        _closeButton.onClick.RemoveListener( HandleClose );
    }


    /// <summary>
    /// 타이틀 복귀 요청 전달
    /// </summary>
    void HandleTitle () => OnTitleRequested?.Invoke( );
    /// <summary>
    /// 게임 종료 요청 전달
    /// </summary>
    void HandleQuit () => OnQuitRequested?.Invoke( );
    /// <summary>
    /// 팝업 닫기 요청 전달
    /// </summary>
    void HandleClose () => OnCloseRequested?.Invoke( );

    /// <summary>
    /// 팝업 표시
    /// </summary>
    /// <param name="visible">표시 여부</param>
    /// <param name="immediate">즉시 적용 여부</param>
    public void SetVisible ( bool visible, bool immediate = false )
    {
        PanelEffect.SetVisible( gameObject, visible, immediate );
    }

    /// <summary>
    /// 씬 정리 중 중복 종료 요청 차단
    /// </summary>
    /// <param name="interactable">입력 허용 여부</param>
    public void SetInteractable ( bool interactable )
    {
        _titleButton.interactable = interactable;
        _quitButton.interactable = interactable;
        _closeButton.interactable = interactable;
    }

    /// <summary>
    /// 종료 정리 실패 후 복귀 또는 종료 재시도만 허용
    /// </summary>
    public void AllowExitRetry ()
    {
        SetInteractable( true );
        _closeButton.interactable = false;
    }


}
