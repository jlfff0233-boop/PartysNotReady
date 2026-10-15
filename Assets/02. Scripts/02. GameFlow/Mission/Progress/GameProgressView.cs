using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 게임 진행 뷰 - 시간, 분위기, 손님과 마무리 안내 표시
/// </summary>
public class GameProgressView : MonoBehaviour
{
    [Header( "----- 진행 화면 -----" )]
    [SerializeField] GameObject _progressBox;     //진행 박스
    [SerializeField] TMP_Text _remainingTimeText;     //남은 시간

    [SerializeField] TMP_Text _moodPercentText;     //분위기 비율
    [SerializeField] Image _moodBar;     //분위기 게이지

    [SerializeField] TMP_Text _guestCountText;     //현재/전체 손님 수
    [SerializeField] Image _guestCountBar;     //손님 수 게이지

    [SerializeField] GameObject _notificationText;     //마무리 구간 안내
    [SerializeField] TextEffect _noticeEffect;		//마무리 안내 연출

    int _displayedSeconds = -1;		//최근 표시한 남은 초
    bool _isRushVisible;		//마무리 안내 표시 여부
    bool _needsRushRestore;		//재활성화 후 점멸 복구 여부

    /// <summary>
    /// 진행 화면 초기화
    /// </summary>
    public void Init ()
    {
        _displayedSeconds = -1;
        _remainingTimeText.text = string.Empty;
        _moodPercentText.text = "--%";
        _moodBar.fillAmount = 0f;
        _guestCountText.text = "-- / --";
        _guestCountBar.fillAmount = 0f;
        ResetFinalRush( );
        _progressBox.SetActive( true );
    }

    private void OnEnable ()
    {
        _needsRushRestore = _isRushVisible;
    }

    private void OnDisable ()
    {
        StopFinalRush( );
    }

    private void LateUpdate ()
    {
        if ( _needsRushRestore == false )
            return;

        //자식 텍스트의 활성화 처리가 끝난 뒤 점멸 복구
        _needsRushRestore = false;
        _notificationText.SetActive( true );
        StartFinalRushBlink( );
    }


    /// <summary>
    /// 마무리 안내 연출 초기화
    /// </summary>
    void ResetFinalRush ()
    {
        _isRushVisible = false;
        StopFinalRush( );
    }

    /// <summary>
    /// 요청 상태를 유지하고 마무리 안내 중단
    /// </summary>
    void StopFinalRush ()
    {
        _needsRushRestore = false;
        _noticeEffect.ResetEffect( );
        _notificationText.SetActive( false );
    }


    /// <summary>
    /// 진행 화면과 마무리 안내 숨김
    /// </summary>
    public void Hide ()
    {
        ResetFinalRush( );
        _progressBox.SetActive( false );
    }


    /// <summary>
    /// 남은 시간 갱신
    /// </summary>
    /// <param name="remainingTime">남은 시간(초)</param>
    public void SetTime ( float remainingTime )
    {
        //시간이 남아 있는데 00:00으로 보이지 않도록 올림
        int seconds = Mathf.CeilToInt( Mathf.Max( 0f, remainingTime ) );

        if ( _displayedSeconds == seconds )
            return;

        _displayedSeconds = seconds;
        _remainingTimeText.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }

    /// <summary>
    /// 분위기 갱신
    /// </summary>
    /// <param name="ratio">분위기 비율(0~1)</param>
    public void SetMood ( float ratio )
    {
        float value = Mathf.Clamp01( ratio );
        _moodPercentText.text = $"{value * 100f:0}%";
        _moodBar.fillAmount = value;
    }

    /// <summary>
    /// 손님 수 갱신
    /// </summary>
    /// <param name="current">현재 손님 수</param>
    /// <param name="total">전체 손님 수</param>
    public void SetGuestCount ( int current, int total )
    {
        _guestCountText.text = $"{current} / {total}";
        _guestCountBar.fillAmount = total > 0
            ? Mathf.Clamp01( ( float ) current / total ) : 0f;
    }

    /// <summary>
    /// 마무리 안내 점멸 시작
    /// </summary>
    void StartFinalRushBlink ()
    {
        _noticeEffect.StartBlink( );
    }

    /// <summary>
    /// 마무리 구간 안내 설정
    /// </summary>
    /// <param name="isVisible">안내 활성 여부</param>
    public void SetFinalRush ( bool isVisible )
    {
        //같은 표시 요청으로 강조와 점멸을 다시 시작하지 않음
        if ( _isRushVisible == isVisible )
            return;

        if ( isVisible == false )
        {
            ResetFinalRush( );
            return;
        }

        _isRushVisible = true;

        if ( isActiveAndEnabled == false )
            return;

        _notificationText.SetActive( true );

        //최초 강조가 끝난 뒤 점멸로 전환
        _noticeEffect.PlayEmphasis( StartFinalRushBlink );
    }

}
