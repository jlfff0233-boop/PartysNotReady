using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 목표 표시 뷰 - 진행량 표시, 완료 연출
/// </summary>
public class MissionGoalView : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] TMP_Text _nameText;        //목표 이름과 진행량
    [SerializeField] TextEffect _effect;        //완료 강조 연출

    [Header( "----- 표시 색상 -----" )]
    [SerializeField] Color _normalColor = Color.white;      //진행 중 색상
    [SerializeField]
    Color _completedColor = new Color( 0.45f, 1f, 0.55f, 1f );      //완료 색상

    string _goalId;     //현재 표시 중인 목표 아이디
    bool _isCompleted;      //현재 표시의 완료 여부
    bool _hasEmphasized;       //완료 연출 요청 여부

    /// <summary>
    /// 비활성화 시 진행 중인 연출과 요청 상태 정리
    /// </summary>
    void OnDisable ()
    {
        ResetEffect( );
    }


    /// <summary>
    /// 현재 연출 중단과 완료 연출 요청 상태 초기화
    /// </summary>
    public void ResetEffect ()
    {
        if ( _effect != null )
            _effect.ResetEffect( );

        _hasEmphasized = false;
    }

    /// <summary>
    /// 목표 정보와 표시 상태 초기화
    /// </summary>
    public void Clear ()
    {
        ResetEffect( );

        _goalId = null;
        _isCompleted = false;

        _nameText.text = string.Empty;
        _nameText.color = _normalColor;
    }


    /// <summary>
    /// 목표 이름과 진행량 및 완료 상태 표시
    /// </summary>
    /// <param name="data">표시할 목표 정보</param>
    public void SetProgress ( MissionGoalDisplayData data )
    {
        if ( data == null )
        {
            Clear( );
            return;
        }

        //항목 교체 또는 진행 초기화 시 이전 연출 상태 해제
        if ( string.Equals(
                 _goalId, data.Id, StringComparison.Ordinal ) == false ||
             ( _isCompleted == true && data.IsCompleted == false ) )
        {
            ResetEffect( );
        }

        _goalId = data.Id;
        _isCompleted = data.IsCompleted;

        //완료 여부에 따른 표시 색상
        Color color = _isCompleted == true
            ? _completedColor
            : _normalColor;

        _nameText.text = $"{data.Name} {data.Current} / {data.Required}";
        _nameText.color = color;
    }

    /// <summary>
    /// 단계 완료 기록 또는 안내 문구 표시
    /// </summary>
    /// <param name="message">표시할 문구</param>
    /// <param name="isCompleted">완료 색상 사용 여부</param>
    public void SetMessage ( string message, bool isCompleted )
    {
        Clear( );
        _isCompleted = isCompleted;
        _nameText.text = message;
        _nameText.color = isCompleted == true ? _completedColor : _normalColor;
    }

    /// <summary>
    /// 완료된 목표의 강조 연출을 한 번 요청
    /// </summary>
    public void PlayCompleted ()
    {
        if ( _isCompleted == false ||
             _hasEmphasized == true ||
             isActiveAndEnabled == false ||
             _effect == null )
            return;

        _hasEmphasized = true;
        _effect.PlayEmphasis( );
    }


}
