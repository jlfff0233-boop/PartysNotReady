using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 표시와 게임 시작, 상점, 홈 입력 전달
/// </summary>
public class LobbyView : MonoBehaviour
{
    [Header( "----- 버튼 -----" )]
    [SerializeField] Button _startButton;		//게임 시작 요청
    [SerializeField] Button _shopButton;		//상점 요청
    [SerializeField] Button _homeButton;		//타이틀 복귀 요청
    [SerializeField] Button _difficultyButton;		//난이도 선택 요청

    /// <summary>
    /// 게임 시작 준비 요청
    /// </summary>
    public event Action OnStartRequested;
    /// <summary>
    /// 상점 요청
    /// </summary>
    public event Action OnShopRequested;
    /// <summary>
    /// 타이틀 복귀 요청
    /// </summary>
    public event Action OnHomeRequested;

    /// <summary>
    /// 난이도 선택 화면 요청
    /// </summary>
    public event Action OnDifficultyRequested;

    /// <summary>
    /// Hierarchy 설정과 무관하게 로비 초기 표시와 입력 준비
    /// </summary>
    public void Init ()
    {
        SetInteractable( false );
        SetButtonsVisible( false );
        PanelEffect.SetVisible( gameObject, false, true );
    }

    private void OnEnable ()
    {
        _startButton.onClick.AddListener( HandleStartClicked );
        _shopButton.onClick.AddListener( HandleShopClicked );
        _homeButton.onClick.AddListener( HandleHomeClicked );
        _difficultyButton.onClick.AddListener( HandleDifficultyClicked );
    }

    private void OnDisable ()
    {
        _startButton.onClick.RemoveListener( HandleStartClicked );
        _shopButton.onClick.RemoveListener( HandleShopClicked );
        _homeButton.onClick.RemoveListener( HandleHomeClicked );
        _difficultyButton.onClick.RemoveListener( HandleDifficultyClicked );
        SetButtonsVisible( false );
    }


    /// <summary>
    /// 게임 시작 버튼 입력 전달
    /// </summary>
    void HandleStartClicked ()
    {
        OnStartRequested?.Invoke( );
    }

    /// <summary>
    /// 상점 버튼 입력 전달
    /// </summary>
    void HandleShopClicked ()
    {
        OnShopRequested?.Invoke( );
    }

    /// <summary>
    /// 홈 버튼 입력 전달
    /// </summary>
    void HandleHomeClicked ()
    {
        OnHomeRequested?.Invoke( );
    }

    /// <summary>
    /// 난이도 버튼 입력 전달
    /// </summary>
    void HandleDifficultyClicked ()
    {
        OnDifficultyRequested?.Invoke( );
    }


    /// <summary>
    /// 로비 버튼 오브젝트 표시 설정
    /// </summary>
    /// <param name="visible">표시 여부</param>
    void SetButtonsVisible ( bool visible )
    {
        //기존 패널 외부에 배치한 버튼도 동일하게 표시 상태 적용
        _startButton.gameObject.SetActive( visible );
        _shopButton.gameObject.SetActive( visible );
        _homeButton.gameObject.SetActive( visible );
        _difficultyButton.gameObject.SetActive( visible );
    }

    /// <summary>
    /// 로비 등장과 퇴장 연출
    /// </summary>
    /// <param name="visible">표시 여부</param>
    public void SetVisible ( bool visible )
    {
        //퇴장 때는 연출 완료로 OnDisable이 실행될 때 버튼을 숨김
        if ( visible == true )
            SetButtonsVisible( true );

        PanelEffect.SetVisible( gameObject, visible );
    }

    /// <summary>
    /// 로비 버튼 입력 허용 설정
    /// </summary>
    /// <param name="interactable">입력 허용 여부</param>
    public void SetInteractable ( bool interactable )
    {
        _startButton.interactable = interactable;
        _shopButton.interactable = interactable;
        _homeButton.interactable = interactable;
        _difficultyButton.interactable = interactable;
    }
}
