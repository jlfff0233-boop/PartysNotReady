using System;
using UnityEngine;

/// <summary>
/// 코인과 보유량 저장 데이터
/// </summary>
[Serializable]
public class SupportItemSaveData
{
    [SerializeField] int _balance;        //보유 코인
    [SerializeField] SupportItemInventory _inventory = new( );        //아이템 보유량

    /// <summary>
    /// 보유 코인
    /// </summary>
    public int Balance
    {
        get => _balance;
        set => _balance = value;
    }

    /// <summary>
    /// 아이템 보유량
    /// </summary>
    public SupportItemInventory Inventory => _inventory;
}
