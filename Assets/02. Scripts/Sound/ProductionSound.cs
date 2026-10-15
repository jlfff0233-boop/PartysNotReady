using UnityEngine;

/// <summary>
/// 제작 사운드 - 시작, 진행, 판정과 완료 재생
/// </summary>
public class ProductionSound : MonoBehaviour
{
    [Header ( "----- 사운드 -----" )]
    [SerializeField] SoundData _startSound;		//제작 시작
    [SerializeField] SoundData _processSound;		//제작 진행 반복음
    [SerializeField] SoundData _successSound;		//중간 성공
    [SerializeField] SoundData _failureSound;		//제작 실패
    [SerializeField] SoundData _completeSound;		//최종 완성

    ProductionSession _session;		//제작 세션
    SoundPlayer _soundPlayer;		//제작 전용 사운드 재생
    bool _isStarted;		//현재 제작의 시작음 처리 여부
    string _completedId;		//마지막 완성음 처리 결과 ID


    #region ----- 초기화 -----

    /// <summary>
    /// 제작 세션과 사운드 재생 모듈 연결
    /// </summary>
    /// <param name="session">그릇의 제작 세션</param>
    /// <param name="soundPlayer">제작 전용 사운드 재생 모듈</param>
    public void Init (
        ProductionSession session , SoundPlayer soundPlayer )
    {
        Unsubscribe ( );
        Stop ( );

        _session = session;
        _soundPlayer = soundPlayer;

        if ( isActiveAndEnabled == true )
        {
            Subscribe ( );
            RestoreLoop ( );
        }
    }

    private void OnEnable ( )
    {
        Subscribe ( );
        RestoreLoop ( );
    }

    private void OnDisable ( )
    {
        Unsubscribe ( );
        Stop ( );
    }

    /// <summary>
    /// 제작 결과 이벤트 구독
    /// </summary>
    void Subscribe ( )
    {
        //최초 활성화는 Init보다 먼저 호출될 수 있음
        if ( _session == null )
            return;

        Unsubscribe ( );

        _session.OnStartResult += HandleStartResult;
        _session.OnProductionEnded += HandleProductionEnded;
        _session.OnCompleted += HandleCompleted;
        _session.Minigame.OnResult += HandleMinigameResult;
    }

    /// <summary>
    /// 제작 결과 이벤트 구독 해제
    /// </summary>
    void Unsubscribe ( )
    {
        if ( _session == null )
            return;

        _session.OnStartResult -= HandleStartResult;
        _session.OnProductionEnded -= HandleProductionEnded;
        _session.OnCompleted -= HandleCompleted;
        _session.Minigame.OnResult -= HandleMinigameResult;
    }

    #endregion

    #region ----- 핸들 -----

    /// <summary>
    /// 실제 제작 시작음과 진행 반복음 재생
    /// </summary>
    /// <param name="result">제작 시작 결과</param>
    void HandleStartResult ( ProductionStartResult result )
    {
        if ( result != ProductionStartResult.Started ||
             CanPlay ( ) == false ||
             _session.IsProductionActive == false ||
             _session.State != ProductionState.Processing ||
             _isStarted == true )
            return;

        _isStarted = true;

        _soundPlayer.PlayOneShot ( _startSound );
        _soundPlayer.StartLoop ( _processSound );
    }

    /// <summary>
    /// 제작 종료에 따른 진행 반복음 정지
    /// </summary>
    void HandleProductionEnded ( )
    {
        _isStarted = false;

        //이후 실패 / 완성 결과가 전달되므로 단발음은 유지
        if ( _soundPlayer != null )
            _soundPlayer.StopLoop ( );
    }

    /// <summary>
    /// 실제 미니게임 성공과 실패음 재생
    /// </summary>
    /// <param name="result">제작에 반영된 미니게임 결과</param>
    void HandleMinigameResult ( ProduceMinigameResult result )
    {
        if ( CanPlay ( ) == false )
            return;

        if ( result == ProduceMinigameResult.Failed )
        {
            _soundPlayer.PlayOneShot ( _failureSound );
            return;
        }

        if ( result != ProduceMinigameResult.Succeeded )
            return;

        //최종 성공은 완료 이벤트에서 재생하므로 중간 성공만 처리
        if ( _session.IsProductionActive == true &&
             _session.State == ProductionState.Processing )
        {
            _soundPlayer.PlayOneShot ( _successSound );
        }
    }

    /// <summary>
    /// 최종 완성음 단회 재생
    /// </summary>
    /// <param name="result">제작 완료 결과</param>
    void HandleCompleted ( ProductionResult result )
    {
        if ( CanPlay ( ) == false )
            return;

        if ( _completedId == result.Id )
            return;

        _completedId = result.Id;
        _soundPlayer.PlayOneShot ( _completeSound );
    }

    #endregion

    #region ----- 재생 / 정리 -----

    /// <summary>
    /// 진행 중인 제작의 반복음 복구
    /// </summary>
    void RestoreLoop ( )
    {
        if ( CanPlay ( ) == false ||
             _session.IsProductionActive == false ||
             _session.State != ProductionState.Processing )
            return;

        //재연결에서는 시작음과 이전 결과음을 다시 재생하지 않음
        _isStarted = true;
        _soundPlayer.StartLoop ( _processSound );
    }

    /// <summary>
    /// 제작 사운드와 재생 기록 초기화
    /// </summary>
    public void Stop ( )
    {
        _isStarted = false;
        _completedId = null;

        //초기화 전 또는 재생 모듈 파괴 후에는 기록만 정리
        if ( _soundPlayer != null )
            _soundPlayer.StopAll ( );
    }

    /// <summary>
    /// 활성 제작 대상의 사운드 재생 가능 여부 확인
    /// </summary>
    /// <returns>재생 가능 여부</returns>
    bool CanPlay ( )
    {
        return isActiveAndEnabled == true &&
               _session != null &&
               _session.IsActive == true &&
               _soundPlayer != null;
    }

    #endregion
}