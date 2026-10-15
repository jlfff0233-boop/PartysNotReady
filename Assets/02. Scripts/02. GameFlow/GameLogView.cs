using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 게임 로그 뷰 - 로그 생성, 스크롤과 표시 시간 관리
/// </summary>
public class GameLogView : MonoBehaviour
{
    [Header( "----- 로그 -----" )]
    [SerializeField] GameObject _logBox;     //게임 진행 로그 박스
    [SerializeField] TMP_Text _logTextPrefab;     //로그 항목 프리팹, 없으면 기존 항목 사용
    [SerializeField, Min( 0.1f )] float _logDuration = 5f;     //로그 표시 시간

    bool _isInitialized;		//초기화 완료 여부
    float _logHideAt;		//마지막 로그 이후 숨김 시각
    ScrollRect _logScroll;		//로그 스크롤 화면
    TMP_Text _logTemplate;		//로그 항목 원본

    /// <summary>
    /// 현재 표시 중인 로그 항목
    /// </summary>
    List<TMP_Text> _logEntries = new( );

    /// <summary>
    /// 로그 목록 초기화
    /// </summary>
    public void Init ()
    {
        ClearLog( );
        _isInitialized = true;

        if ( _logBox == null )
            return;

        _logScroll = _logBox.GetComponentInChildren<ScrollRect>( true );
        if ( _logScroll == null || _logScroll.content == null )
        {
            Debug.LogWarning( "[게임 로그] LogBox의 Scroll View를 확인해 주세요.", this );
            _logBox.SetActive( false );
            return;
        }

        _logTemplate = _logTextPrefab != null
            ? _logTextPrefab
            : _logScroll.content.GetComponentInChildren<TMP_Text>( true );

        //씬에 배치된 예시 로그는 원본으로만 사용
        foreach ( Transform child in _logScroll.content )
            child.gameObject.SetActive( false );

        if ( _logTemplate == null )
            Debug.LogWarning( "[게임 로그] LogText 프리팹을 연결해 주세요.", this );

        _logBox.SetActive( false );
    }

    private void Update ()
    {
        if ( _logEntries.Count == 0 || Time.unscaledTime < _logHideAt )
            return;

        Hide( );
    }

    private void OnDisable ()
    {
        Hide( );
    }


    /// <summary>
    /// 표시 중인 로그 정리
    /// </summary>
    void ClearLog ()
    {
        foreach ( TMP_Text entry in _logEntries )
        {
            if ( entry == null )
                continue;

            entry.gameObject.SetActive( false );
            Destroy( entry.gameObject );
        }

        _logEntries.Clear( );
        _logHideAt = 0f;
    }

    /// <summary>
    /// 로그 화면 숨김과 항목 정리
    /// </summary>
    public void Hide ()
    {
        ClearLog( );

        if ( _logBox != null )
            _logBox.SetActive( false );
    }

    /// <summary>
    /// 게임 로그 표시
    /// </summary>
    /// <param name="message">표시할 문구</param>
    public void ShowLog ( string message )
    {
        if ( _isInitialized == false || string.IsNullOrWhiteSpace( message ) == true ||
             _logBox == null || _logScroll == null || _logTemplate == null )
            return;

        TMP_Text entry = Instantiate( _logTemplate, _logScroll.content );
        entry.text = message;
        entry.gameObject.SetActive( true );
        _logEntries.Add( entry );
        _logHideAt = Time.unscaledTime + _logDuration;

        _logBox.SetActive( true );
        Canvas.ForceUpdateCanvases( );
        _logScroll.verticalNormalizedPosition = 0f;
    }

}
