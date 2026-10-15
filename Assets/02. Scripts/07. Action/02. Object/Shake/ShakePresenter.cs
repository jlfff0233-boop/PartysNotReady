using System;
using UnityEngine;


/// <summary>
/// 흔들림 중재자 - 시작과 종료, 움직임 갱신 연결
/// </summary>
[RequireComponent ( typeof ( ShakeView ) )]
public class ShakePresenter : MonoBehaviour, IInvestigatable
{
    ShakeModel _model = new ( );        //흔들림 모델
    ShakeView _view;        //흔들림 뷰

    /// <summary>
    /// 현재 흔들림 여부
    /// </summary>
    public bool IsShaking => _model.IsShaking;

    /// <summary>
    /// 흔들림 상태와 이동 / 회전 적용 가능 여부
    /// </summary>
    public bool IsReady => CheckReady( );

    /// <summary>
    /// 흔들리는 오브젝트의 조사 가능 여부
    /// </summary>
    public bool CanInvestigate => CheckCanInvestigate( );

    /// <summary>
    /// 흔들림 조사 행동 이름
    /// </summary>
    public string ActionName => "멈추기";

    /// <summary>
    /// 현재 조사할 흔들림의 ID
    /// </summary>
    public int InvestigationId => _model.ShakeId;

    /// <summary>
    /// 흔들림 상태 변경 이벤트(흔들림 여부)
    /// </summary>
    public event Action<bool> OnShakingChanged;
    /// <summary>
    /// 조사 완료 이벤트(조사 대상 ID)
    /// </summary>
    public event Action<int> OnInvestigated;


    private void Awake ( )
    {
        //흔들림을 시작하기 전에 복원할 초기 위치와 회전 저장
        _view = GetComponent<ShakeView> ( );
        _view.Init ( );
    }

    private void Update ( )
    {
        if ( IsShaking == false )
            return;

        //움직임을 갱신할 수 없으면 흔들림 상태만 남지 않도록 종료
        if ( _view == null || _view.IsReady == false )
        {
            TrySetShaking ( false );
            return;
        }

        _view.UpdateShake ( Time.deltaTime );
    }

    private void OnDisable ( )
    {
        //비활성화 시 흔들림을 종료하고 초기 위치와 회전 복원
        TrySetShaking ( false );
    }


    /// <summary>
    /// 흔들림 상태 변경과 초기 위치 / 회전 복원
    /// </summary>
    /// <param name="isShaking">변경할 흔들림 여부</param>
    /// <returns>실제 상태 변경 여부</returns>
    public bool TrySetShaking ( bool isShaking )
    {
        //종료 요청은 비활성화 중에도 허용
        if ( isShaking == true &&
             ( isActiveAndEnabled == false || _view == null || _view.IsReady == false ) )
            return false;

        if ( _model.TrySetShaking ( isShaking ) == false )
            return false;

        //시작은 원래 위치와 회전부터, 종료는 원래 위치와 회전으로 복귀
        if ( _view != null )
            _view.ResetPose ( );

        OnShakingChanged?.Invoke ( isShaking );
        return true;
    }


    /// <summary>
    /// 조사하던 흔들림을 종료하고 조사 완료 알림
    /// </summary>
    /// <param name="expectedInvestigationId">조사 시작 시 저장한 ID</param>
    /// <returns>조사 완료 여부</returns>
    public bool TryInvestigate ( int expectedInvestigationId )
    {
        if ( CanInvestigate == false || InvestigationId != expectedInvestigationId )
            return false;

        if ( TrySetShaking ( false ) == false )
            return false;

        //정지 알림의 콜백에서 새 흔들림이 시작될 수 있어 ID와 상태를 다시 확인
        //이전 조사로 새 흔들림까지 해결한 것으로 처리하지 않음
        if ( InvestigationId != expectedInvestigationId ||
             IsShaking == true )
            return false;

        OnInvestigated?.Invoke ( expectedInvestigationId );
        return true;
    }


    /// <summary>
    /// 플레이 중 흔들림 시작 확인
    /// </summary>
    [ContextMenu ( "Test Start Shake" )]
    void TestStartShake ( )
    {
        if ( Application.isPlaying == false )
            return;

        TrySetShaking ( true );
    }

    /// <summary>
    /// 플레이 중 흔들림 종료 확인
    /// </summary>
    [ContextMenu ( "Test Stop Shake" )]
    void TestStopShake ( )
    {
        if ( Application.isPlaying == false )
            return;

        TrySetShaking ( false );
    }


    #region ----- 상태 검사 -----

    /// <summary>
    /// 흔들림 기능과 움직임 갱신 가능 여부 확인
    /// </summary>
    /// <returns>흔들림 기능 사용 가능 여부</returns>
    bool CheckReady ()
    {
        return isActiveAndEnabled == true &&
               _view != null && _view.IsReady == true;
    }

    /// <summary>
    /// 진행 중인 흔들림의 조사 가능 여부 확인
    /// </summary>
    /// <returns>흔들림 조사 가능 여부</returns>
    bool CheckCanInvestigate ( )
    {
        return isActiveAndEnabled == true &&
               IsShaking == true &&
               _view != null &&
               _view.IsReady == true;
    }

    #endregion

}
