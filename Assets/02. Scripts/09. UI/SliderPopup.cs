using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 슬라이더 팝업 - 정수 입력과 기준 위치 추적
/// </summary>
public class SliderPopup : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Slider _slider;        //값 조절
    [SerializeField] Button _minButton;        //최솟값
    [SerializeField] Button _maxButton;        //최댓값

    [Header( "----- 위치 -----" )]
    [SerializeField] Vector2 _offset;        //기준 상단과의 간격

    int _limit;		//선택 가능한 최대 수량
    RectTransform _rect;        //팝업 영역
    RectTransform _anchor;        //위치 기준
    bool _isSettingRange;        //범위 변경 중 입력 알림 차단
    Vector3 [ ] _corners = new Vector3 [ 4 ];        //기준 영역 모서리

    /// <summary>
    /// 입력값 변경 알림
    /// </summary>
    public event Action<int> OnValueChanged;

    /// <summary>
    /// 정수 입력 범위 설정
    /// </summary>
    /// <param name="min">최솟값</param>
    /// <param name="max">최댓값</param>
    public void Init ( int min, int max )
    {
        SetRange( min, max );
        SetValue( min );
    }

    /// <summary>
    /// 정수 입력과 버튼 연결
    /// </summary>
    void Awake ()
    {
        _rect = ( RectTransform ) transform;
        _slider.wholeNumbers = true;

        _slider.onValueChanged.AddListener( HandleValueChanged );
        _minButton.onClick.AddListener( HandleMinClicked );
        _maxButton.onClick.AddListener( HandleMaxClicked );
    }

    /// <summary>
    /// 비활성화 시 위치 기준 해제
    /// </summary>
    void OnDisable ()
    {
        _anchor = null;
    }

    /// <summary>
    /// 기준 위치를 따라 팝업 이동
    /// </summary>
    void LateUpdate ()
    {
        if ( _anchor == null )
        {
            Close( );
            return;
        }

        UpdatePosition( );
    }


    /// <summary>
    /// 입력 알림 없이 슬라이더 표시 범위 설정
    /// </summary>
    /// <param name="min">표시 최솟값</param>
    /// <param name="max">표시 최댓값</param>
    public void SetRange ( int min, int max )
    {
        _isSettingRange = true;

        try
        {
            _slider.minValue = min;
            _slider.maxValue = max;
            _limit = max;

            SetValue( Mathf.RoundToInt( _slider.value ) );
        }
        finally
        {
            _isSettingRange = false;
        }
    }


    /// <summary>
    /// 선택 상한과 현재 수량 표시 갱신
    /// </summary>
    /// <param name="max">선택 가능한 최대 수량</param>
    public void SetLimit ( int max )
    {
        _limit = Mathf.Clamp(
            max,
            Mathf.RoundToInt( _slider.minValue ),
            Mathf.RoundToInt( _slider.maxValue ) );

        SetValue( Mathf.RoundToInt( _slider.value ) );
    }


    /// <summary>
    /// 선택 상한을 적용한 슬라이더 수량 전달
    /// </summary>
    /// <param name="value">입력한 수량</param>
    void HandleValueChanged ( float value )
    {
        if ( _isSettingRange == true )
            return;

        //상한을 넘긴 손잡이 위치도 실제 선택 수량에 맞춰 보정
        SetValue( Mathf.RoundToInt( value ) );
        OnValueChanged?.Invoke( Mathf.RoundToInt( _slider.value ) );
    }

    /// <summary>
    /// 최솟값 입력
    /// </summary>
    void HandleMinClicked ()
    {
        _slider.value = _slider.minValue;
    }


    /// <summary>
    /// 선택 가능한 최대 수량 입력
    /// </summary>
    void HandleMaxClicked ()
    {
        _slider.value = _limit;
    }

    /// <summary>
    /// 입력 알림 없이 수량과 증감 가능 상태 갱신
    /// </summary>
    /// <param name="value">표시할 수량</param>
    public void SetValue ( int value )
    {
        int min = Mathf.RoundToInt( _slider.minValue );     //선택 최솟값
        int count = Mathf.Clamp( value, min, _limit );      //선택 범위에 맞춘 수량

        _slider.SetValueWithoutNotify( count );

        _minButton.interactable = count > min;
        _maxButton.interactable = count < _limit;
    }

    /// <summary>
    /// 기준 위치와 값 설정 후 팝업 활성화
    /// </summary>
    /// <param name="anchor">위치 기준 영역</param>
    /// <param name="value">현재 값</param>
    public void Open ( RectTransform anchor, int value )
    {
        gameObject.SetActive( true );

        _anchor = anchor;
        SetValue( value );

        transform.SetAsLastSibling( );
        UpdatePosition( );
    }

    /// <summary>
    /// 팝업 비활성화
    /// </summary>
    public void Close ()
    {
        gameObject.SetActive( false );
    }

    /// <summary>
    /// 기준 영역 상단으로 팝업 이동
    /// </summary>
    void UpdatePosition ()
    {
        _anchor.GetWorldCorners( _corners );

        //왼쪽 위와 오른쪽 위의 중간 위치
        Vector3 top = ( _corners [ 1 ] + _corners [ 2 ] ) * 0.5f;
        Vector3 position = _rect.parent.InverseTransformPoint( top );

        position.x += _offset.x;
        position.y += _offset.y;
        position.z = 0f;

        _rect.localPosition = position;
    }
}
