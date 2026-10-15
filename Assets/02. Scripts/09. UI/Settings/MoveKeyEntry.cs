using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 이동 방향별 키 변경 버튼과 표시 연결
/// </summary>
[Serializable]
public class MoveKeyEntry
{
    [SerializeField] MoveDirection _direction;        //연결할 이동 방향
    [SerializeField] Button _button;        //키 입력 대기 버튼
    [SerializeField] TMP_Text _text;        //현재 키 표시

    [NonSerialized] string _bindingId;        //초기 조회한 바인딩 ID
    [NonSerialized] UnityAction _click;        //구독과 해제에 사용할 콜백

    /// <summary>
    /// 연결할 이동 방향
    /// </summary>
    public MoveDirection Direction => _direction;

    /// <summary>
    /// 키 입력 대기 버튼
    /// </summary>
    public Button Button => _button;

    /// <summary>
    /// 현재 키 표시
    /// </summary>
    public TMP_Text Text => _text;

    /// <summary>
    /// 초기 조회한 바인딩 ID
    /// </summary>
    public string BindingId
    {
        get => _bindingId;
        set => _bindingId = value;
    }

    /// <summary>
    /// 구독과 해제에 사용할 콜백
    /// </summary>
    public UnityAction Click
    {
        get => _click;
        set => _click = value;
    }


    /// <summary>
    /// 기본 이동 방향 지정
    /// </summary>
    /// <param name="direction">연결할 이동 방향</param>
    public MoveKeyEntry ( MoveDirection direction )
    {
        _direction = direction;
    }
}
