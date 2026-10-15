using UnityEngine;

/// <summary>
/// 게임 결과 사운드 - 결과 화면의 성공과 실패음 재생
/// </summary>
public class GameResultSound : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] SoundPlayer _soundPlayer;		//공통 화면 사운드 재생

    [Header( "----- 사운드 -----" )]
    [SerializeField] SoundData _successSound;		//게임 성공음
    [SerializeField] SoundData _failureSound;		//게임 실패음

    GameFlowController _flow;		//결과 표시 알림
    bool _hasPlayedResult;		//최초 결과 표시 처리 여부

    /// <summary>
    /// 게임 진행 연결과 결과음 재생 기록 초기화
    /// </summary>
    /// <param name="flow">결과 표시 알림 제공 대상</param>
    public void Init ( GameFlowController flow )
    {
        Unsubscribe( );
        _flow = flow;
        _hasPlayedResult = false;

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
    /// 최초 결과 화면의 판정에 맞는 안내음 재생
    /// </summary>
    /// <param name="outcome">표시한 게임 결과</param>
    void HandleResult ( GameOutcome outcome )
    {
        if ( _hasPlayedResult == true )
            return;

        //미정산 결과도 최초 표시로 기록하여 반복 표시의 재생 방지
        _hasPlayedResult = true;

        if ( _soundPlayer != null && outcome != GameOutcome.Unscored )
            _soundPlayer.PlayOneShot(
                outcome == GameOutcome.Success ? _successSound : _failureSound );
    }

    /// <summary>
    /// 사운드 알림 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _flow != null )
            _flow.OnResult -= HandleResult;
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

        _flow.OnResult += HandleResult;
    }


}
