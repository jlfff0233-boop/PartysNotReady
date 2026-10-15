using UnityEngine;
using System;

/// <summary>
/// 난이도 선택 중재 - 설정 조회, 선택 상태와 표시 전달
/// </summary>
public class DifficultySelectPanel : MonoBehaviour
{
    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] DifficultySelectView _view;		//난이도 입력과 표시

    GameDifficultyDataMap _dataMap;		//난이도별 설정
    GameDifficultySettings _settings;		//현재 선택 설정
    bool _isReady;		//설정 준비 여부
    bool _isInteractable;		//선택 입력 허용 여부
    GameDifficulty _confirmedDifficulty;		//확정한 난이도

    /// <summary>
    /// 확정한 난이도
    /// </summary>
    public GameDifficulty SelectedDifficulty => _confirmedDifficulty;

    /// <summary>
    /// 확정한 난이도 전달
    /// </summary>
    public event Action<GameDifficulty> OnConfirmed;

    /// <summary>
    /// 선택 취소 알림
    /// </summary>
    public event Action OnCancelled;


    #region ----- 초기화 -----

    /// <summary>
    /// 난이도 설정 확인 후 선택 표시
    /// </summary>
    /// <param name="dataMap">난이도별 설정</param>
    /// <param name="difficulty">초기 선택 난이도</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        GameDifficultyDataMap dataMap , GameDifficulty difficulty )
    {
        if ( dataMap == null )
        {
            Debug.LogError (
                "[난이도 선택 초기화 실패] 난이도 데이터맵을 연결해 주세요." , this );

            return false;
        }

        if ( dataMap.CheckSettings ( out string reason ) == false )
        {
            Debug.LogError (
                $"[난이도 선택 초기화 실패] {reason}" , dataMap );

            return false;
        }

        _dataMap = dataMap;
        _view.Init ( );
        _isReady = true;
        SetInteractable ( false );

        if ( SelectDifficulty ( difficulty ) == false )
            return false;

        _confirmedDifficulty = difficulty;
        return true;
    }

    private void OnEnable ( )
    {
        _view.OnDifficultyRequested += HandleDifficultyRequested;
        _view.OnConfirmed += HandleConfirmed;
        _view.OnCancelled += HandleCancelled;
    }

    private void OnDisable ( )
    {
        _view.OnDifficultyRequested -= HandleDifficultyRequested;
        _view.OnConfirmed -= HandleConfirmed;
        _view.OnCancelled -= HandleCancelled;
    }

    #endregion


    /// <summary>
    /// 입력이 허용된 난이도 선택 처리
    /// </summary>
    /// <param name="difficulty">선택할 난이도</param>
    void HandleDifficultyRequested ( GameDifficulty difficulty )
    {
        if ( _isInteractable == false )
            return;

        SelectDifficulty ( difficulty );
    }


    #region ----- 선택과 표시 -----

    /// <summary>
    /// 입력 차단 후 선택 화면 닫기
    /// </summary>
    void Close ( )
    {
        SetInteractable ( false );
        SetVisible ( false );
    }

    /// <summary>
    /// 선택한 난이도 확정 후 화면 닫기
    /// </summary>
    void HandleConfirmed ( )
    {
        if ( _isInteractable == false )
            return;

        _confirmedDifficulty = _settings.Difficulty;
        Close ( );
        OnConfirmed?.Invoke ( _confirmedDifficulty );
    }

    /// <summary>
    /// 변경 중인 난이도를 버리고 화면 닫기
    /// </summary>
    void HandleCancelled ( )
    {
        if ( _isInteractable == false )
            return;

        SelectDifficulty ( _confirmedDifficulty );
        Close ( );
        OnCancelled?.Invoke ( );
    }

    /// <summary>
    /// 확정한 난이도로 선택 화면 열기
    /// </summary>
    public void Open ( )
    {
        SelectDifficulty ( _confirmedDifficulty );
        SetVisible ( true );
        SetInteractable ( true );
    }

    /// <summary>
    /// 난이도 선택 후 강조와 설명 갱신
    /// </summary>
    /// <param name="difficulty">선택할 난이도</param>
    /// <returns>선택 성공 여부</returns>
    public bool SelectDifficulty ( GameDifficulty difficulty )
    {
        if ( _isReady == false )
            return false;

        if ( _dataMap.TryGetSettings (
            difficulty , out GameDifficultySettings settings ) == false )
        {
            Debug.LogError (
                $"[난이도 선택 실패] 설정이 없습니다: {difficulty}" , this );

            return false;
        }

        _settings = settings;

        UpdateSelection ( );
        UpdateDescription ( );
        return true;
    }

    /// <summary>
    /// 선택한 난이도의 테두리 표시
    /// </summary>
    void UpdateSelection ( )
    {
        _view.ShowSelection ( _settings.Difficulty );
    }

    /// <summary>
    /// 플레이 시간과 제작 및 발생 안내 표시
    /// </summary>
    void UpdateDescription ( )
    {
        int seconds = Mathf.CeilToInt ( _settings.Flow.Duration );		//표시할 제한시간
        int minutes = seconds / 60;		//분
        int remainingSeconds = seconds % 60;		//남은 초

        string description =
            $"플레이 시간: {minutes}분 {remainingSeconds}초\n" +
            $"{_settings.ProductionDescription}\n" +
            $"{_settings.AnomalyDescription}";		//선택한 설정의 안내

        _view.SetDescription ( description );
    }

    /// <summary>
    /// 난이도 패널과 뷰 표시 설정
    /// </summary>
    /// <param name="visible">표시 여부</param>
    /// <param name="immediate">초기 표시를 즉시 적용할지 여부</param>
    public void SetVisible ( bool visible, bool immediate = false )
    {
        //패널과 뷰가 별도 오브젝트여도 표시 상태를 함께 적용
        //부모 퇴장 연출이 끝나기 전에 자식 뷰를 끄지 않음
        if ( visible == true )
            _view.gameObject.SetActive ( true );

        PanelEffect.SetVisible ( gameObject, visible, immediate );
    }

    /// <summary>
    /// 난이도 선택 입력 설정
    /// </summary>
    /// <param name="interactable">입력 허용 여부</param>
    public void SetInteractable ( bool interactable )
    {
        _isInteractable = interactable && _isReady;

        _view.SetInteractable ( _isInteractable );
    }

    #endregion
}
