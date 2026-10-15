using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 타이틀 표시와 시작, 설정, 종료 버튼 입력 전달
/// </summary>
public class TitleView : MonoBehaviour
{
    [Header( "----- 버튼 -----" )]
    [SerializeField] Button _startButton;		//대기 화면 이동 요청
    [SerializeField] Button _settingButton;		//설정 요청
    [SerializeField] Button _exitButton;		//종료 요청

    [Header( "----- 로고 -----" )]
    [SerializeField] RectTransform _logo;		//반복 이동할 로고
    [SerializeField, Min( 0f )] float _logoHeight = 10f;		//위쪽 이동 높이
    [SerializeField, Min( 0.01f )] float _logoDuration = 1.8f;		//편도 이동 시간

    EffectPlayer _logoEffect;		//로고 연출 실행과 수명 관리
    Vector2 _logoPosition;		//로고 기준 위치

    /// <summary>
    /// 대기 화면 이동 요청
    /// </summary>
    public event Action OnStartRequested;
    /// <summary>
    /// 설정 화면 요청
    /// </summary>
    public event Action OnSettingRequested;
    /// <summary>
    /// 게임 종료 요청
    /// </summary>
    public event Action OnExitRequested;

    /// <summary>
    /// Hierarchy 설정과 무관하게 타이틀 초기 표시와 입력 준비
    /// </summary>
    public void Init ()
    {
        //초기 숨김으로 OnDisable이 호출되기 전에 연출 준비
        _logoEffect = new EffectPlayer( gameObject );
        _logoPosition = _logo.anchoredPosition;

        SetInteractable( false );
        PanelEffect.SetVisible( gameObject, false, true );
    }

    private void OnEnable ()
    {
        _startButton.onClick.AddListener( HandleStartClicked );
        _settingButton.onClick.AddListener( HandleSettingClicked );
        _exitButton.onClick.AddListener( HandleExitClicked );
    }

    private void OnDisable ()
    {
        _startButton.onClick.RemoveListener( HandleStartClicked );
        _settingButton.onClick.RemoveListener( HandleSettingClicked );
        _exitButton.onClick.RemoveListener( HandleExitClicked );

        StopLogoEffect( );
    }


    /// <summary>
    /// 시작 버튼 입력 전달
    /// </summary>
    void HandleStartClicked ()
    {
        OnStartRequested?.Invoke( );
    }

    /// <summary>
    /// 설정 버튼 입력 전달
    /// </summary>
    void HandleSettingClicked ()
    {
        OnSettingRequested?.Invoke( );
    }

    /// <summary>
    /// 종료 버튼 입력 전달
    /// </summary>
    void HandleExitClicked ()
    {
        OnExitRequested?.Invoke( );
    }


    /// <summary>
    /// 타이틀 화면 표시 설정
    /// </summary>
    /// <param name="visible">표시 여부</param>
    public void SetVisible ( bool visible )
    {
        //퇴장 연출이 끝날 때까지 기다리지 않고 반복 이동 정리
        if ( visible == false )
            StopLogoEffect( );

        PanelEffect.SetVisible( gameObject, visible );

        if ( visible == true )
            PlayLogoEffect( );
    }

    /// <summary>
    /// 타이틀 버튼 입력 허용 설정
    /// </summary>
    /// <param name="interactable">입력 허용 여부</param>
    public void SetInteractable ( bool interactable )
    {
        _startButton.interactable = interactable;
        _settingButton.interactable = interactable;
        _exitButton.interactable = interactable;
    }


    /// <summary>
    /// 로고의 위쪽 이동과 복귀 반복
    /// </summary>
    void PlayLogoEffect ()
    {
        if ( _logoEffect.IsPlaying == true )
            return;

        Tween tween = _logo.DOAnchorPosY(
            _logoPosition.y + _logoHeight, _logoDuration )
            .SetEase( Ease.InOutSine )
            .SetLoops( -1, LoopType.Yoyo );		//기준 위치와 위쪽 위치 왕복

        _logoEffect.Play( tween );
    }

    /// <summary>
    /// 로고 연출 중단과 기준 위치 복원
    /// </summary>
    void StopLogoEffect ()
    {
        //Init 이전 비활성화에는 실행 중인 연출이 없음
        if ( _logoEffect == null )
            return;

        _logoEffect.Stop( );
        _logo.anchoredPosition = _logoPosition;
    }
}
