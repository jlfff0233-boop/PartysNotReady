using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// 제작 미니게임 패널 - 타이밍과 클릭 목표 표시, 입력 전달
/// </summary>
public class ProduceMinigamePanel : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Button _actionButton;		//제작 판정 영역
    [SerializeField] TMP_Text _successCountText;		//성공 횟수 텍스트
    [SerializeField] Image _progressBar;		//제작 진행률 바
    [SerializeField] Image _roundTimeBar;		//회차 제한시간 바

    [Header( "--- 미니게임 그룹 ---" )]
    [SerializeField] GameObject _timingBarGroup;		//타이밍 미니게임 그룹
    [SerializeField] GameObject _clickAreaGroup;		//영역 클릭 미니게임 그룹

    [Header( "--- 타이밍 ---" )]
    [SerializeField] RectTransform _timingBar;		//성공 영역
    [SerializeField] RectTransform _handle;		//현재 진행 위치

    [Header( "--- 목표 클릭 ---" )]
    [SerializeField] RectTransform _hitPointShowArea;		//목표 배치 영역
    [SerializeField] RectTransform _hitPointPrefab;		//목표 UI 프리팹

    EventTrigger _actionTrigger;		//타이밍 버튼 입력
    EventTrigger.Entry _actionPressEntry;		//타이밍 누름 연결
    EventTrigger _showAreaTrigger;		//빈 영역 입력
    EventTrigger.Entry _showAreaPressEntry;		//빈 영역 누름 연결

    bool _isReady;		//UI 초기화 여부
    Guid _displayedAttempt;		//표시한 시도 ID

    CookController _product;		//제작 대상
    ProduceMinigameController _displayedMinigame;		//표시한 미니게임

    /// <summary>
    /// 클릭 목표 UI 목록
    /// </summary>
    List<ProduceHitPointDisplay> _hitPoints = new( );

    #region ----- 초기화 -----

    /// <summary>
    /// 미니게임 UI 초기화
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ()
    {
        if ( _isReady == true )
            return true;

        PreparePointerInput( );

        //시간 바는 입력을 가로채지 않음
        if ( _roundTimeBar != null )
        {
            _roundTimeBar.raycastTarget = false;
            _roundTimeBar.gameObject.SetActive( false );
        }

        _isReady = true;
        return true;
    }


    /// <summary>
    /// 제작 대상 연결
    /// </summary>
    /// <param name="product">제작 대상</param>
    public void Bind ( CookController product )
    {
        if ( _product == product )
            return;

        Disconnect( );
        _product = product;
        Connect( );
    }

    /// <summary>
    /// 입력과 미니게임 상태 구독
    /// </summary>
    public void Connect ()
    {
        if ( _isReady == false )
            return;

        //중복 구독 방지
        Disconnect( );
        SubscribePointerEvents( );

        if ( _product == null )
            return;

        _product.Session.OnStateChanged += HandleStateChanged;
        _product.Session.Minigame.OnProgressChanged += SetMinigameProgress;
        _product.Session.Minigame.OnTimeChanged += SetRemainingTime;
        _product.Session.Minigame.OnSuccessChanged += SetSuccessCount;
    }

    /// <summary>
    /// 입력과 미니게임 상태 구독 해제
    /// </summary>
    public void Disconnect ()
    {
        if ( _isReady == false )
            return;

        ClearHitPoints( );
        UnsubscribePointerEvents( );

        if ( _product == null )
            return;

        _product.Session.OnStateChanged -= HandleStateChanged;
        _product.Session.Minigame.OnProgressChanged -= SetMinigameProgress;
        _product.Session.Minigame.OnTimeChanged -= SetRemainingTime;
        _product.Session.Minigame.OnSuccessChanged -= SetSuccessCount;
    }

    /// <summary>
    /// 미니게임 입력 영역 준비
    /// </summary>
    void PreparePointerInput ()
    {
        _actionTrigger =
            _actionButton.GetComponent<EventTrigger>( );

        if ( _actionTrigger == null )
            _actionTrigger = _actionButton.gameObject.AddComponent<EventTrigger>( );

        _showAreaTrigger =
            _hitPointShowArea.GetComponent<EventTrigger>( );

        if ( _showAreaTrigger == null )
            _showAreaTrigger = _hitPointShowArea.gameObject.AddComponent<EventTrigger>( );

        //빈 영역 입력을 받을 투명 표면 확보
        Image showAreaImage = _hitPointShowArea.GetComponent<Image>( );

        if ( showAreaImage == null )
        {
            showAreaImage = _hitPointShowArea.gameObject.AddComponent<Image>( );
            showAreaImage.color = Color.clear;
        }

        showAreaImage.raycastTarget = true;
    }

    /// <summary>
    /// 미니게임 누름 입력 구독
    /// </summary>
    void SubscribePointerEvents ()
    {
        _actionPressEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };

        _actionPressEntry.callback.AddListener( _ => HandleTimingPressed( ) );
        _actionTrigger.triggers.Add( _actionPressEntry );

        _showAreaPressEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };

        _showAreaTrigger.triggers.Add( _showAreaPressEntry );
    }

    /// <summary>
    /// 미니게임 누름 입력 구독 해제
    /// </summary>
    void UnsubscribePointerEvents ()
    {
        if ( _actionTrigger != null && _actionPressEntry != null )
            _actionTrigger.triggers.Remove( _actionPressEntry );

        if ( _showAreaTrigger != null && _showAreaPressEntry != null )
            _showAreaTrigger.triggers.Remove( _showAreaPressEntry );

        _actionPressEntry = null;
        _showAreaPressEntry = null;
    }

    #endregion


    #region ----- 핸들 -----


    /// <summary>
    /// 클릭 목표 입력 전달
    /// </summary>
    /// <param name="minigame">목표를 표시한 미니게임</param>
    /// <param name="attemptId">시도 ID</param>
    /// <param name="targetId">목표 ID</param>
    void HandleHitPointPressed (
        ProduceMinigameController minigame, Guid attemptId, int targetId )
    {
        if ( IsCurrentClickInput( minigame ) == false )
            return;

        minigame.TryCheckTarget( attemptId, targetId, out _ );
        RefreshTargets( );
    }

    /// <summary>
    /// 빈 영역 입력 전달
    /// </summary>
    /// <param name="minigame">제작 미니게임</param>
    /// <param name="attemptId">시도 ID</param>
    void HandleShowAreaPressed (
        ProduceMinigameController minigame, Guid attemptId )
    {
        if ( IsCurrentClickInput( minigame ) == false )
            return;

        minigame.TryCheckMiss( attemptId, out _ );
        RefreshTargets( );
    }

    /// <summary>
    /// 타이밍 입력 전달
    /// </summary>
    void HandleTimingPressed ()
    {
        if ( _product == null )
            return;

        //표시 중인 입력 영역 확인
        if ( isActiveAndEnabled == false ||
             _timingBarGroup.activeInHierarchy == false ||
             _actionButton.isActiveAndEnabled == false ||
             _actionButton.IsInteractable( ) == false )
            return;

        ProduceMinigameController minigame =
            _product.Session.Minigame;       //현재 미니게임

        minigame.TryCheckTiming( out _ );
    }

    /// <summary>
    /// 목표 정리와 성공 영역 갱신
    /// </summary>
    /// <param name="state">제작 상태</param>
    void HandleStateChanged ( ProductionState state )
    {
        //결과 알림에 앞서 이전 목표 입력 정리
        if ( state != ProductionState.Processing )
            ClearHitPoints( );

        if ( _product.Session.IsReady == true )
            SetSuccessRange(
                _product.Session.Minigame.RangeStart,
                _product.Session.Minigame.RangeEnd );
    }


    /// <summary>
    /// 현재 표시 중인 미니게임 입력 확인
    /// </summary>
    /// <param name="minigame">입력을 받은 미니게임</param>
    /// <returns>현재 클릭 입력 여부</returns>
    bool IsCurrentClickInput ( ProduceMinigameController minigame )
    {
        return _clickAreaGroup.activeInHierarchy && _product != null &&
               ReferenceEquals( _product.Session.Minigame, minigame );
    }

    #endregion

    #region ----- 표시 -----

    /// <summary>
    /// 회차 남은 시간 표시
    /// </summary>
    /// <param name="ratio">잔여 비율</param>
    void SetRemainingTime ( float ratio )
    {
        if ( _isReady == false || _roundTimeBar == null )
            return;

        _roundTimeBar.fillAmount = Mathf.Clamp01( ratio );
    }

    /// <summary>
    /// 미니게임 표시 전환
    /// </summary>
    /// <param name="isVisible">표시 여부</param>
    public void SetVisible ( bool isVisible )
    {
        //두 미니게임에서 같은 회차 시간 바 사용
        if ( _roundTimeBar != null )
        {
            SetRemainingTime( _product == null ? 0f : _product.Session.Minigame.RemainingTimeRatio );
            _roundTimeBar.gameObject.SetActive( isVisible && _product != null );
        }

        bool isTiming =
            _product != null &&
            _product.Session.Minigame.MinigameType == ProduceMinigameType.Timing;

        _timingBarGroup.SetActive( isVisible && isTiming );
        _actionButton.interactable = isVisible && isTiming;
        _clickAreaGroup.SetActive( isVisible && isTiming == false );

        if ( isVisible && isTiming == false )
            RefreshTargets( );
        else
            ClearHitPoints( );
    }

    /// <summary>
    /// 미니게임 표시 갱신
    /// </summary>
    public void Refresh ()
    {
        if ( _product == null )
            return;

        if ( _product.Session.IsReady == true )
            SetSuccessRange(
                _product.Session.Minigame.RangeStart,
                _product.Session.Minigame.RangeEnd );

        SetMinigameProgress( _product.Session.Minigame.Progress );
        SetRemainingTime( _product.Session.Minigame.RemainingTimeRatio );
        SetSuccessCount(
            _product.Session.SuccessCount, _product.Session.RequiredSuccessCount );
    }

    /// <summary>
    /// 제작 미니게임 현재 위치 표시
    /// </summary>
    /// <param name="progress">현재 진행률, 0~1</param>
    void SetMinigameProgress ( float progress )
    {
        if ( _isReady == false )
            return;

        float value = Mathf.Clamp01( progress );

        Vector2 anchorMin = _handle.anchorMin;
        Vector2 anchorMax = _handle.anchorMax;

        anchorMin.x = value;
        anchorMax.x = value;

        _handle.anchorMin = anchorMin;
        _handle.anchorMax = anchorMax;

        Vector2 position = _handle.anchoredPosition;
        position.x = 0f;
        _handle.anchoredPosition = position;
    }

    /// <summary>
    /// 제작 성공 영역 위치 설정
    /// </summary>
    /// <param name="start">성공 영역 시작, 0~1</param>
    /// <param name="end">성공 영역 끝, 0~1</param>
    void SetSuccessRange ( float start, float end )
    {
        float min = Mathf.Clamp01( start );
        float max = Mathf.Clamp( end, min, 1f );

        Vector2 anchorMin = _timingBar.anchorMin;
        Vector2 anchorMax = _timingBar.anchorMax;

        anchorMin.x = min;
        anchorMax.x = max;

        _timingBar.anchorMin = anchorMin;
        _timingBar.anchorMax = anchorMax;

        Vector2 offsetMin = _timingBar.offsetMin;
        Vector2 offsetMax = _timingBar.offsetMax;

        offsetMin.x = 0f;
        offsetMax.x = 0f;

        _timingBar.offsetMin = offsetMin;
        _timingBar.offsetMax = offsetMax;
    }

    /// <summary>
    /// 제작 성공 횟수와 진행바 갱신
    /// </summary>
    /// <param name="currentCount">현재 성공 횟수</param>
    /// <param name="requiredCount">필요 성공 횟수</param>
    void SetSuccessCount ( int currentCount, int requiredCount )
    {
        if ( _isReady == false )
            return;

        _successCountText.text = $"{currentCount} / {requiredCount}";

        //정수 나눗셈을 피하고 초기화 전에는 빈 진행바 적용
        _progressBar.fillAmount = requiredCount > 0
            ? Mathf.Clamp01( ( float ) currentCount / requiredCount )
            : 0f;
    }

    #endregion

    #region ----- 클릭 목표 -----

    /// <summary>
    /// 클릭 목표 UI 동기화
    /// </summary>
    public void RefreshTargets ()
    {
        if ( _isReady == false || _product == null || _showAreaPressEntry == null ||
             _clickAreaGroup.activeInHierarchy == false )
            return;

        ProduceMinigameController minigame = _product.Session.Minigame;
        Guid attempt = minigame.AttemptId;

        if ( ReferenceEquals( minigame, _displayedMinigame ) == false ||
            attempt != _displayedAttempt )
        {
            ClearHitPoints( );
            _displayedMinigame = minigame;
            _displayedAttempt = attempt;

            //이전 UI 이벤트는 새 시도의 ID를 읽지 않고 원래 표시 ID를 전달
            _showAreaPressEntry.callback.RemoveAllListeners( );
            _showAreaPressEntry.callback.AddListener( _ => HandleShowAreaPressed( minigame, attempt ) );
        }

        List<int> ids = new( minigame.TargetIds );

        for ( int i = _hitPoints.Count - 1; i >= 0; i-- )
        {
            ProduceHitPointDisplay point = _hitPoints [ i ];

            if ( point.Rect != null && ids.Contains( point.TargetId ) )
                continue;

            if ( point.Rect != null )
            {
                point.Rect.gameObject.SetActive( false );
                Destroy( point.Rect.gameObject );
            }

            _hitPoints.RemoveAt( i );
        }

        foreach ( int id in ids )
        {
            if ( _hitPoints.Exists(
                point => point.TargetId == id ) == false )
                SpawnHitPoint( minigame, attempt, id );
        }
    }

    /// <summary>
    /// 클릭 목표 UI 생성
    /// </summary>
    /// <param name="minigame">표시를 요청한 미니게임</param>
    /// <param name="attemptId">표시할 시도</param>
    /// <param name="targetId">표시할 목표</param>
    void SpawnHitPoint (
        ProduceMinigameController minigame,
        Guid attemptId, int targetId )
    {
        Rect area = _hitPointShowArea.rect;
        List<Rect> occupied = new( );

        foreach ( ProduceHitPointDisplay current in _hitPoints )
        {
            RectTransform rect = current.Rect;
            Vector2 size = GetHitPointSize( rect );
            occupied.Add(
                new Rect( rect.anchoredPosition - size * 0.5f, size ) );
        }

        RectTransform hitPoint = Instantiate(
            _hitPointPrefab, _hitPointShowArea, false );

        hitPoint.anchorMin = new Vector2( 0.5f, 0.5f );
        hitPoint.anchorMax = new Vector2( 0.5f, 0.5f );
        hitPoint.pivot = new Vector2( 0.5f, 0.5f );

        Vector2 hitPointSize = GetHitPointSize( hitPoint );

        if ( TryFindHitPointPosition( area, hitPointSize, occupied,
            out Vector2 position ) == false )
        {
            Destroy( hitPoint.gameObject );
            minigame.RejectDisplay( attemptId, targetId );
            return;
        }

        hitPoint.anchoredPosition = position;

        _hitPoints.Add( new ProduceHitPointDisplay( hitPoint , targetId ) );

        EventTrigger trigger =
            hitPoint.GetComponent<EventTrigger>( );

        if ( trigger == null )
            trigger = hitPoint.gameObject.AddComponent<EventTrigger>( );

        EventTrigger.Entry entry = new( )
        {
            eventID = EventTriggerType.PointerDown
        };

        entry.callback.AddListener(
            _ => HandleHitPointPressed( minigame, attemptId, targetId ) );
        trigger.triggers.Add( entry );
    }

    /// <summary>
    /// 클릭 목표 UI 정리
    /// </summary>
    void ClearHitPoints ()
    {
        foreach ( ProduceHitPointDisplay point in _hitPoints )
        {
            if ( point.Rect == null )
                continue;

            point.Rect.gameObject.SetActive( false );
            Destroy( point.Rect.gameObject );
        }

        _hitPoints.Clear( );
        _displayedAttempt = Guid.Empty;
        _displayedMinigame = null;
        if ( _showAreaPressEntry != null )
            _showAreaPressEntry.callback.RemoveAllListeners( );
    }

    /// <summary>
    /// 영역 안에서 기존 목표와 겹치지 않는 위치 탐색
    /// </summary>
    /// <param name="area">목표 출현 영역</param>
    /// <param name="size">목표의 실제 UI 크기</param>
    /// <param name="occupied">이미 배치된 목표 영역</param>
    /// <param name="position">선정한 중심 위치</param>
    /// <returns>배치 가능한 위치를 찾았는지 여부</returns>
    bool TryFindHitPointPosition (
        Rect area, Vector2 size,
        List<Rect> occupied,
        out Vector2 position )
    {
        position = default;

        float minX = area.xMin - area.center.x + size.x * 0.5f;
        float maxX = area.xMax - area.center.x - size.x * 0.5f;
        float minY = area.yMin - area.center.y + size.y * 0.5f;
        float maxY = area.yMax - area.center.y - size.y * 0.5f;

        if ( minX > maxX || minY > maxY ||
            size.x <= 0f || size.y <= 0f )
            return false;

        for ( int attempt = 0; attempt < 100; attempt++ )
        {
            Vector2 candidate = new(
                Random.Range( minX, maxX ),
                Random.Range( minY, maxY ) );

            Rect bounds = new( candidate - size * 0.5f, size );
            bool overlaps = false;

            foreach ( Rect other in occupied )
            {
                if ( bounds.Overlaps( other ) == false )
                    continue;

                overlaps = true;
                break;
            }

            if ( overlaps == true )
                continue;

            position = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 목표의 UI 크기 계산
    /// </summary>
    /// <param name="hitPoint">크기를 확인할 목표</param>
    /// <returns>스케일이 반영된 크기</returns>
    Vector2 GetHitPointSize ( RectTransform hitPoint )
    {
        return Vector2.Scale(
            hitPoint.rect.size,
            new Vector2( Mathf.Abs( hitPoint.localScale.x ),
                          Mathf.Abs( hitPoint.localScale.y ) ) );
    }

    #endregion
}
