using UnityEngine;

/// <summary>
/// 클릭 목표 표시 - UI 객체와 게임플레이 ID 연결
/// </summary>
public class ProduceHitPointDisplay
{
    RectTransform _rect;        //목표 UI
    int _targetId;        //목표 ID

    /// <summary>
    /// 목표 UI
    /// </summary>
    public RectTransform Rect => _rect;

    /// <summary>
    /// 목표 ID
    /// </summary>
    public int TargetId => _targetId;


    /// <summary>
    /// 클릭 목표 표시와 판정 ID 연결
    /// </summary>
    /// <param name="rect">목표 UI</param>
    /// <param name="targetId">목표 ID</param>
    public ProduceHitPointDisplay ( RectTransform rect , int targetId )
    {
        _rect = rect;
        _targetId = targetId;
    }
}
