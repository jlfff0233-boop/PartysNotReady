using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// 게임 흐름 HUD - 개별 진행 화면 연결
/// </summary>
public class GameFlowHud : MonoBehaviour
{
    [Header( "----- 화면 -----" )]
    [SerializeField] GameProgressView _progressView;		//게임 진행 화면
    [SerializeField] MissionView _missionView;		//미션 화면
    [SerializeField] GameLogView _logView;		//로그 화면

    [Header( "----- 장소 -----" )]
    [SerializeField] TMPro.TMP_Text _locationText;     //장소 이름
    [SerializeField] TextEffect _locationEffect;     //장소 표시 연출
    [SerializeField, Min( 0.1f )] float _locationDuration = 3f;     //표시 시간

    float _locationRemaining;     //남은 표시 시간

    /// <summary>
    /// 전체 미션 완료 화면 표시 알림 이벤트
    /// </summary>
    public event Action OnMissionCompleted;

    /// <summary>
    /// 개별 화면 초기화
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ()
    {
        ClearLocation( );
        Unsubscribe( );

        if ( _missionView.Init( ) == false )
        {
            Hide( );
            return false;
        }

        Subscribe( );
        _progressView.Init( );
        _logView.Init( );
        return true;
    }

    private void Awake ()
    {
        ClearLocation( );
    }

    private void OnDisable ()
    {
        ClearLocation( );
    }

    private void Update ()
    {
        if ( _locationRemaining <= 0f )
            return;

        //일시정지 중에는 장소 표시 시간도 유지
        _locationRemaining -= Time.deltaTime;
        if ( _locationRemaining <= 0f )
            ClearLocation( );
    }

    /// <summary>
    /// 파괴 시 미션 화면 표시 알림 연결 해제
    /// </summary>
    void OnDestroy ()
    {
        Unsubscribe( );
    }

    /// <summary>
    /// 장소 이름 표시와 표시 시간 갱신
    /// </summary>
    /// <param name="locationName">장소 이름, 없으면 숨김</param>
    public void SetLocation ( string locationName )
    {
        ClearLocation( );
        if ( _locationText == null || string.IsNullOrWhiteSpace( locationName ) == true )
            return;

        _locationText.text = locationName;
        _locationText.gameObject.SetActive( true );
        _locationText.enabled = true;
        _locationRemaining = _locationDuration;
        if ( _locationEffect != null )
            _locationEffect.PlayEmphasis( );
    }

    /// <summary>
    /// 장소 이름과 이전 표시 연출 정리
    /// </summary>
    void ClearLocation ()
    {
        _locationRemaining = 0f;
        if ( _locationEffect != null )
            _locationEffect.ResetEffect( );
        if ( _locationText == null )
            return;

        _locationText.text = string.Empty;
        _locationText.gameObject.SetActive( false );
    }


    /// <summary>
    /// 전체 미션 완료 표시 알림 전달
    /// </summary>
    void HandleMissionCompleted ()
    {
        OnMissionCompleted?.Invoke( );
    }

    /// <summary>
    /// 미션 화면 표시 알림 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _missionView != null )
            _missionView.OnCompleted -= HandleMissionCompleted;
    }

    /// <summary>
    /// 미션 화면 표시 알림 연결
    /// </summary>
    void Subscribe ()
    {
        Unsubscribe( );
        _missionView.OnCompleted += HandleMissionCompleted;
    }


    /// <summary>
    /// 남은 시간 갱신
    /// </summary>
    /// <param name="remainingTime">남은 시간(초)</param>
    public void SetTime ( float remainingTime )
    {
        _progressView.SetTime( remainingTime );
    }

    /// <summary>
    /// 분위기 갱신
    /// </summary>
    /// <param name="ratio">분위기 비율(0~1)</param>
    public void SetMood ( float ratio )
    {
        _progressView.SetMood( ratio );
    }

    /// <summary>
    /// 손님 수 갱신
    /// </summary>
    /// <param name="current">현재 손님 수</param>
    /// <param name="total">전체 손님 수</param>
    public void SetGuestCount ( int current, int total )
    {
        _progressView.SetGuestCount( current, total );
    }

    /// <summary>
    /// 마무리 구간 안내 설정
    /// </summary>
    /// <param name="isVisible">안내 활성 여부</param>
    public void SetFinalRush ( bool isVisible )
    {
        _progressView.SetFinalRush( isVisible );
    }


    /// <summary>
    /// 지정한 목표의 완료 강조 연출 요청
    /// </summary>
    /// <param name="stageIndex">완료된 목표의 단계 번호</param>
    /// <param name="goalId">완료된 목표 아이디</param>
    public void PlayGoalCompleted ( int stageIndex, string goalId )
    {
        _missionView.PlayGoalCompleted( stageIndex, goalId );
    }

    /// <summary>
    /// 완료된 미션 화면 유지 요청 전달
    /// </summary>
    /// <param name="stageIndex">완료된 미션 인덱스</param>
    /// <param name="goalId">완료한 목표 아이디</param>
    public void HoldCompletedMission ( int stageIndex, string goalId )
    {
        _missionView.HoldCompletedMission( stageIndex, goalId );
    }

    /// <summary>
    /// 미션 단계와 목표 안내 문구 표시
    /// </summary>
    /// <param name="stageName">단계 이름</param>
    /// <param name="goals">목표별 안내 문구</param>
    public void SetMission ( string stageName, string goals )
    {
        _missionView.SetMission( stageName, goals );
    }

    /// <summary>
    /// 미션 단계와 목표별 진행량 표시
    /// </summary>
    /// <param name="stageIndex">표시할 단계 번호</param>
    /// <param name="stageName">단계 표시 이름</param>
    /// <param name="goals">목표별 표시 정보</param>
    public void SetMission (
        int stageIndex,
        string stageName,
        IReadOnlyList<MissionGoalDisplayData> goals )
    {
        _missionView.SetMission( stageIndex, stageName, goals );
    }

    /// <summary>
    /// 단계별 완료 기록과 전체 완료 안내 전달
    /// </summary>
    /// <param name="missions">단계별 완료 기록</param>
    public void ShowMissionCompleted (
        IReadOnlyList<MissionResult> missions )
    {
        _missionView.ShowMissionCompleted( missions );
    }


    /// <summary>
    /// 게임 로그 표시
    /// </summary>
    /// <param name="message">표시할 문구</param>
    public void ShowLog ( string message )
    {
        _logView.ShowLog( message );
    }

    /// <summary>
    /// 개별 진행 화면 숨김
    /// </summary>
    public void Hide ()
    {
        ClearLocation( );
        _progressView.Hide( );
        _missionView.Hide( );
        _logView.Hide( );
    }


}
