using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 결과 뷰 - 종료 기록과 정산 내역
/// </summary>
public class GameResultView : MonoBehaviour
{
    [Header( "----- 결과 화면 -----" )]
    [SerializeField] GameObject _resultCanvas;     //Canvas_Result
    [SerializeField] TMP_Text _titleText;     //종료 제목
    [SerializeField] TMP_Text _resultEtcText;     //결과 안내
    [SerializeField] TMP_Text _playTimeText;     //경과 시간
    [SerializeField] TMP_Text _guestText;     //현재/시작 손님 수
    [SerializeField] TMP_Text _missionResultText;     //단계별 완료 현황
    [SerializeField] Button _titleButton;        //시작 화면 복귀

    [Header( "----- 점수 정산 -----" )]
    [SerializeField] TMP_Text _scoreEtcText;     //정산 안내
    [SerializeField] Transform _content;     //항목 부모
    [SerializeField] TMP_Text _logTextPrefab;     //LogText 프리팹
    [SerializeField] TMP_Text _totalScoreText;     //총점

    [Header( "----- 결과 연출 -----" )]
    [SerializeField] PanelEffect _panelEffect;		//점수 패널 등장 연출
    [SerializeField, Min( 0f )] float _itemInterval = 0.15f;		//항목 표시 간격
    [SerializeField, Min( 0.01f )] float _countDuration = 0.6f;		//숫자 변화 시간

    /// <summary>
    /// 생성한 정산 항목
    /// </summary>
    List<TMP_Text> _scoreTexts = new( );
    StringBuilder _text = new( );     //미션 문구
    TMP_Text _rewardText;     //코인 지급 안내

    EffectPlayer _resultEffect;		//항목과 총점 연출
    EffectPlayer _rewardEffect;		//보상 숫자 연출
    GameResult _result;		//현재 표시 결과
    bool _scoresFinished;		//점수 표시 완료 여부
    bool _isWaitingPanel;		//점수 패널 등장 대기 여부
    bool _hasReward;		//보상 수신 여부
    int _rewardAmount;		//획득 코인
    int _rewardBalance;		//지급 후 잔액
    bool _isRewardGranted;		//지급 성공 여부


    /// <summary>
    /// 시작 화면 복귀 요청
    /// </summary>
    public event Action OnReturnRequested;

    /// <summary>
    /// 결과 화면 표시 알림 이벤트(게임 결과)
    /// </summary>
    public event Action<GameOutcome> OnResult;

    #region ----- 초기화 -----

    /// <summary>
    /// 결과 화면 초기화
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ()
    {
        StopEffect( );
        _resultEffect = new EffectPlayer( gameObject );
        _rewardEffect = new EffectPlayer( gameObject );
        _result = null;
        _hasReward = false;
        _scoresFinished = false;
        ClearScores( );

        _panelEffect.OnOpened -= HandlePanelShowCompleted;
        _panelEffect.OnOpened += HandlePanelShowCompleted;

        _titleButton.onClick.RemoveListener( HandleReturnClicked );
        _titleButton.onClick.AddListener( HandleReturnClicked );

        _resultCanvas.SetActive( false );
        return true;
    }

    private void OnEnable ()
    {
        if ( _result != null && _scoresFinished == true )
            DisplayFinalResult( );
    }

    private void OnDisable ()
    {
        StopEffect( );

        //파괴 중 UI를 생성하지 않고 재활성화에서 최종 표시 복구
        if ( _result != null )
            _scoresFinished = true;
    }

    /// <summary>
    /// 결과 버튼 연결 해제
    /// </summary>
    private void OnDestroy ()
    {
        StopEffect( );
        if ( _panelEffect != null )
            _panelEffect.OnOpened -= HandlePanelShowCompleted;

        if ( _titleButton != null )
            _titleButton.onClick.RemoveListener( HandleReturnClicked );
    }


    /// <summary>
    /// 정산 항목 정리
    /// </summary>
    void ClearScores ()
    {
        //Content의 고정 UI는 유지
        foreach ( TMP_Text item in _scoreTexts )
        {
            if ( item == null )
                continue;

            item.gameObject.SetActive( false );
            Destroy( item.gameObject );
        }
        _scoreTexts.Clear( );
        _rewardText = null;
    }


    /// <summary>
    /// 시작 화면 복귀 요청 전달
    /// </summary>
    void HandleReturnClicked ()
    {
        StopEffect( );
        DisplayFinalResult( );
        OnReturnRequested?.Invoke( );
    }

    /// <summary>
    /// 결과와 보상 연출 중단
    /// </summary>
    void StopEffect ()
    {
        _isWaitingPanel = false;
        _resultEffect?.Stop( );
        _rewardEffect?.Stop( );
    }

    #endregion

    #region ----- 결과 화면 -----

    /// <summary>
    /// 패널 등장 완료 후 점수 연출 시작
    /// </summary>
    void HandlePanelShowCompleted ()
    {
        if ( _isWaitingPanel == false )
            return;

        _isWaitingPanel = false;
        _resultEffect.Current.Play( );
    }

    /// <summary>
    /// 미션 기록 출력
    /// </summary>
    /// <param name="result">종료 기록</param>
    void ShowMissions ( GameResult result )
    {
        _text.Clear( );
        _text.Append( "미션 완료 현황" );
        foreach ( MissionResult mission in result.Missions )
            _text.Append( '\n' ).Append( mission.Name ).Append( "  " )
                .Append( mission.Completed ).Append( " / " ).Append( mission.Total );

        _missionResultText.text = _text.ToString( );
    }

    /// <summary>
    /// 정산 내역 출력
    /// </summary>
    /// <param name="result">종료 기록</param>
    /// <param name="sequence">점수 표시 흐름</param>
    void ShowScores ( GameResult result, Sequence sequence )
    {
        ClearScores( );

        _scoreEtcText.text = result.IsScored
            ? $"평가 등급  {result.Grade}"
            : "점수 미정산";

        _totalScoreText.text = result.IsScored
            ? "총점  0"
            : "총점  --";

        _scoreEtcText.gameObject.SetActive( false );
        _totalScoreText.gameObject.SetActive( false );

        foreach ( GameScoreResult score in result.Scores )
        {
            TMP_Text item = Instantiate( _logTextPrefab, _content );
            item.text = score.IsExcluded
                ? $"{score.Name}  평가 제외"
                : $"{score.Name}  0";
            item.raycastTarget = false;
            item.gameObject.SetActive( false );
            _scoreTexts.Add( item );

            sequence.AppendInterval( _itemInterval );
            sequence.AppendCallback( () => item.gameObject.SetActive( true ) );

            if ( score.IsExcluded == false )
            {
                sequence.Append( CreateCount( score.Points,
                    value => item.text = $"{score.Name}  {value:+0;-0;0}" ) );
            }
        }

        //총점이 항목 목록 안에 있으면 마지막에 배치
        if ( _totalScoreText.transform.parent == _content )
            _totalScoreText.transform.SetAsLastSibling( );

        sequence.AppendInterval( _itemInterval );
        sequence.AppendCallback( () =>
        {
            _scoreEtcText.gameObject.SetActive( true );
            _totalScoreText.gameObject.SetActive( true );
        } );

        if ( result.IsScored == true )
            sequence.Append( CreateCount( result.TotalScore, DisplayTotalScore ) );
    }

    /// <summary>
    /// 지정한 최종 숫자까지 변화하는 연출 생성
    /// </summary>
    /// <param name="target">최종 숫자</param>
    /// <param name="display">숫자 표시 처리</param>
    /// <returns>숫자 변화 연출</returns>
    Tween CreateCount ( long target, Action<long> display )
    {
        Tween tween = DOVirtual.Float( 0f, 1f, _countDuration,
            ratio => display( ( long ) decimal.Truncate(
                target * ( decimal ) ratio ) ) ).SetEase( Ease.OutCubic );

        //큰 점수도 마지막에는 원래 정수값으로 표시
        tween.OnComplete( () => display( target ) );
        return tween;
    }

    /// <summary>
    /// 총점 숫자 표시
    /// </summary>
    /// <param name="value">표시할 총점</param>
    void DisplayTotalScore ( long value )
    {
        _totalScoreText.text = $"총점  {value}";
    }

    /// <summary>
    /// 점수 연출 완료 후 보상 표시
    /// </summary>
    void FinishScores ()
    {
        _scoresFinished = true;

        if ( _hasReward == true )
            DisplayReward( true );
    }

    /// <summary>
    /// 중단된 결과 화면을 최종값으로 표시
    /// </summary>
    void DisplayFinalResult ()
    {
        for ( int i = 0; i < _result.Scores.Count; i++ )
        {
            GameScoreResult score = _result.Scores [ i ];
            TMP_Text item = _scoreTexts [ i ];
            item.text = score.IsExcluded
                ? $"{score.Name}  평가 제외"
                : $"{score.Name}  {score.Points:+0;-0;0}";
            item.gameObject.SetActive( true );
        }

        _scoreEtcText.gameObject.SetActive( true );
        _totalScoreText.gameObject.SetActive( true );
        _totalScoreText.text = _result.IsScored
            ? $"총점  {_result.TotalScore}"
            : "총점  --";
        _scoresFinished = true;

        if ( _hasReward == true )
            DisplayReward( false );
    }

    /// <summary>
    /// 종료 판정에 맞는 안내 문구 조회
    /// </summary>
    /// <param name="result">확정된 게임 결과</param>
    /// <returns>결과 안내</returns>
    string GetOutcomeMessage ( GameResult result )
    {
        if ( result.Outcome == GameOutcome.Unscored )
            return "평가 결과를 확인해 주세요.";

        if ( result.Reason == GameEndReason.AllNpcDeparted )
            return "모든 손님이 이탈했습니다.";

        if ( result.IsMissionCompleted == false )
            return "완료하지 못한 미션이 있습니다.";

        return result.Outcome == GameOutcome.Success
            ? string.Empty
            : "평가 등급이 B 미만입니다.";
    }

    /// <summary>
    /// 결과 화면 열기
    /// </summary>
    /// <param name="result">종료 기록</param>
    public void ShowResult ( GameResult result )
    {
        StopEffect( );

        //같은 결과를 다시 표시하면 받은 보상 유지
        if ( ReferenceEquals( _result, result ) == false )
            _hasReward = false;

        _result = result;
        _scoresFinished = false;

        int seconds = Mathf.FloorToInt( result.ElapsedTime );
        _titleText.text = result.Outcome switch
        {
            GameOutcome.Success => "게임 성공",
            GameOutcome.Failure => "게임 실패",
            _ => "결과 미정산"
        };
        string message = GetOutcomeMessage( result );       //종료 사유 안내
        _resultEtcText.text = message;
        _resultEtcText.gameObject.SetActive(
            string.IsNullOrEmpty( message ) == false );
        _playTimeText.text = $"플레이 시간  {seconds / 60:00}:{seconds % 60:00}";
        _guestText.text = $"남은 손님  {result.RemainingNpcCount} / {result.InitialNpcCount}";
        ShowMissions( result );
        Sequence sequence = DOTween.Sequence( ).Pause( );
        ShowScores( result, sequence );
        sequence.OnComplete( FinishScores );

        //즉시 등장 완료 알림도 받을 수 있도록 활성화 전에 대기 준비
        _resultEffect.Play( sequence );
        sequence.Pause( );
        _isWaitingPanel = true;
        _resultCanvas.SetActive( true );

        if ( isActiveAndEnabled == true &&
             _resultCanvas.activeInHierarchy == true )
        {
            _panelEffect.Show( );

            if ( _panelEffect.IsShowCompleted == true )
                HandlePanelShowCompleted( );
        }
        else
        {
            StopEffect( );
            DisplayFinalResult( );
        }

        //실제 결과 화면 표시 후 확정된 판정 전달
        OnResult?.Invoke( result.Outcome );
    }

    /// <summary>
    /// 코인 지급 결과 출력
    /// </summary>
    /// <param name="amount">획득 코인</param>
    /// <param name="balance">지급 후 잔액</param>
    /// <param name="isGranted">지급 성공 여부</param>
    public void ShowReward ( int amount, int balance, bool isGranted )
    {
        _rewardAmount = amount;
        _rewardBalance = balance;
        _isRewardGranted = isGranted;
        _hasReward = true;

        if ( _scoresFinished == true && isActiveAndEnabled == true &&
             _resultCanvas.activeInHierarchy == true )
        {
            DisplayReward( true );
        }
    }

    /// <summary>
    /// 받은 보상을 표시하거나 숫자 연출 실행
    /// </summary>
    /// <param name="animate">숫자 연출 여부</param>
    void DisplayReward ( bool animate )
    {
        _rewardEffect.Stop( );

        if ( _rewardText == null )
        {
            _rewardText = Instantiate( _logTextPrefab, _content );
            _rewardText.raycastTarget = false;
            _rewardText.gameObject.SetActive( true );
            _scoreTexts.Add( _rewardText );
        }

        _rewardText.transform.SetAsLastSibling( );

        if ( _isRewardGranted == false )
        {
            _rewardText.text = $"코인 미지급 / 보유 코인  {_rewardBalance}";
            return;
        }

        if ( animate == false )
        {
            DisplayRewardAmount( _rewardAmount );
            return;
        }

        DisplayRewardAmount( 0 );
        _rewardEffect.Play( CreateCount( _rewardAmount, DisplayRewardAmount ) );
    }

    /// <summary>
    /// 획득 코인 숫자 표시
    /// </summary>
    /// <param name="value">표시할 획득 코인</param>
    void DisplayRewardAmount ( long value )
    {
        _rewardText.text = $"획득 코인  +{value} / 보유 코인  {_rewardBalance}";
    }

    #endregion

}
