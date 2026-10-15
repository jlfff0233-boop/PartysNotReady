using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 탭 버튼과 콘텐츠 표시 연결
/// </summary>
[Serializable]
public class SettingTabEntry
{
    [SerializeField] SettingPanel.SettingTab _tab;        //탭 종류
    [SerializeField] Button _button;        //전환 버튼
    [SerializeField] GameObject _content;        //표시할 그룹

    [NonSerialized] UnityAction _click;        //구독 콜백

    /// <summary>
    /// 탭 종류
    /// </summary>
    public SettingPanel.SettingTab Tab => _tab;

    /// <summary>
    /// 전환 버튼
    /// </summary>
    public Button Button => _button;

    /// <summary>
    /// 표시할 그룹
    /// </summary>
    public GameObject Content => _content;

    /// <summary>
    /// 구독 콜백
    /// </summary>
    public UnityAction Click
    {
        get => _click;
        set => _click = value;
    }


    /// <summary>
    /// 기본 설정 탭 지정
    /// </summary>
    /// <param name="tab">연결할 설정 탭</param>
    public SettingTabEntry ( SettingPanel.SettingTab tab )
    {
        _tab = tab;
    }
}
