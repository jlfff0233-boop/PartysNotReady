using System;
using UnityEngine;

/// <summary>
/// 아이템별 저장 수량
/// </summary>
[Serializable]
public class SupportItemCount
{
    [SerializeField] string _id;        //아이템 ID
    [SerializeField] int _count;        //보유 수량

    /// <summary>
    /// 아이템 ID
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// 보유 수량
    /// </summary>
    public int Count
    {
        get => _count;
        set => _count = value;
    }


    /// <summary>
    /// 아이템 식별자와 초기 보유 수량 저장
    /// </summary>
    /// <param name="id">아이템 ID</param>
    /// <param name="count">초기 보유 수량</param>
    public SupportItemCount ( string id , int count )
    {
        _id = id;
        _count = count;
    }
}
