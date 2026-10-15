using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일시정지 패널 - 메뉴 표시와 버튼 요청 전달
/// </summary>
public class PausePanel : MonoBehaviour
{
    [SerializeField] Button _resumeButton;      //재개 버튼
    [SerializeField] Button _settingsButton;    //설정 버튼
    [SerializeField] Button _exitButton;        //나가기 버튼

    /// <summary>
    /// 재개 요청
    /// </summary>
    public event Action OnResumeRequested;
    /// <summary>
    /// 설정 요청
    /// </summary>
    public event Action OnSettingsRequested;
    /// <summary>
    /// 나가기 요청
    /// </summary>
    public event Action OnExitRequested;

    private void OnEnable ()
    {
        _resumeButton.onClick.AddListener( HandleResume );
        _settingsButton.onClick.AddListener( HandleSettings );
        _exitButton.onClick.AddListener( HandleExit );
    }


    /// <summary>
    /// 재개 요청 전달
    /// </summary>
    void HandleResume () => OnResumeRequested?.Invoke( );
    /// <summary>
    /// 설정 요청 전달
    /// </summary>
    void HandleSettings () => OnSettingsRequested?.Invoke( );
    /// <summary>
    /// 나가기 요청 전달
    /// </summary>
    void HandleExit () => OnExitRequested?.Invoke( );

    /// <summary>
    /// 설정과 팝업을 유지하고 메뉴 버튼만 표시
    /// </summary>
    /// <param name="visible">메뉴 표시 여부</param>
    public void SetMenuVisible ( bool visible )
    {
        _resumeButton.gameObject.SetActive( visible );
        _settingsButton.gameObject.SetActive( visible );
        _exitButton.gameObject.SetActive( visible );
    }

    /// <summary>
    /// 전체 패널 표시
    /// </summary>
    /// <param name="visible">표시 여부</param>
    /// <param name="immediate">즉시 적용 여부</param>
    public void SetVisible ( bool visible, bool immediate = false )
    {
        if ( visible == true )
            GetComponentInParent<Canvas>( true ).gameObject.SetActive( true );
        PanelEffect.SetVisible( gameObject, visible, immediate );
    }



    private void OnDisable ()
    {
        _resumeButton.onClick.RemoveListener( HandleResume );
        _settingsButton.onClick.RemoveListener( HandleSettings );
        _exitButton.onClick.RemoveListener( HandleExit );
    }

}
