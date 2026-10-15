using UnityEngine;

/// <summary>
/// 게임 흐름 사운드 - 전체 미션 완료와 마무리 구간 진입음 재생
/// </summary>
public class GameFlowSound : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] SoundPlayer _soundPlayer;		//공통 화면 사운드 재생

    [Header( "----- 사운드 -----" )]
    [SerializeField] SoundData _missionCompletedSound;		//전체 미션 완료음
    [SerializeField] SoundData _finalRushSound;		//마무리 구간 진입음

    GameFlowController _flow;		//게임 진행 알림
    bool _hasPlayedMissionCompleted;		//전체 미션 완료음 처리 여부
    bool _hasPlayedFinalRush;		//마무리 구간 진입음 처리 여부

    /// <summary>
    /// 게임 진행 연결과 재생 기록 초기화
    /// </summary>
    /// <param name="flow">게임 진행 알림 제공 대상</param>
    public void Init ( GameFlowController flow )
    {
        Unsubscribe( );
        _flow = flow;
        _hasPlayedMissionCompleted = false;
        _hasPlayedFinalRush = false;

        if ( isActiveAndEnabled == true )
            Subscribe( );
    }

    /// <summary>
    /// 재활성화 시 알림 연결 복구
    /// </summary>
    void OnEnable ()
    {
        Subscribe( );
    }

    /// <summary>
    /// 비활성화 시 알림 연결 해제
    /// </summary>
    void OnDisable ()
    {
        Unsubscribe( );
    }

    /// <summary>
    /// 파괴 시 알림 연결 해제
    /// </summary>
    void OnDestroy ()
    {
        Unsubscribe( );
    }


    /// <summary>
    /// 전체 미션 완료 화면 표시 시 안내음 재생
    /// </summary>
    void HandleMissionCompleted ()
    {
        if ( _hasPlayedMissionCompleted == true )
            return;

        _hasPlayedMissionCompleted = true;

        if ( _soundPlayer != null )
            _soundPlayer.PlayOneShot( _missionCompletedSound );
    }

    /// <summary>
    /// 마무리 구간 최초 진입 시 안내음 재생
    /// </summary>
    /// <param name="state">변경된 진행 상태</param>
    void HandleStateChanged ( GameFlowState state )
    {
        if ( state != GameFlowState.FinalRush || _hasPlayedFinalRush == true )
            return;

        _hasPlayedFinalRush = true;

        if ( _soundPlayer != null )
            _soundPlayer.PlayOneShot( _finalRushSound );
    }

    /// <summary>
    /// 사운드 알림 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _flow == null )
            return;

        _flow.OnStateChanged -= HandleStateChanged;
        _flow.OnMissionCompleted -= HandleMissionCompleted;
    }

    /// <summary>
    /// 사운드 알림 연결
    /// </summary>
    void Subscribe ()
    {
        Unsubscribe( );
        //최초 활성화는 초기화보다 먼저 호출될 수 있음
        if ( _flow == null )
            return;

        _flow.OnStateChanged += HandleStateChanged;
        _flow.OnMissionCompleted += HandleMissionCompleted;
    }


}
