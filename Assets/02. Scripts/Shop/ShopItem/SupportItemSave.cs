using System;
using UnityEngine;

/// <summary>
/// 서포트 아이템 저장 - 코인, 서포트 아이템 보유량 저장
/// </summary>
public static class SupportItemSave
{
    const string SaveKey = "3DRabbitPartyMulti.SupportItemSave";        //통합 저장 키
    const string LegacyBalanceKey = "3DRabbitPartyMulti.CoinBalance";        //기존 코인 저장 키


    /// <summary>
    /// 코인과 보유량 함께 저장
    /// </summary>
    /// <param name="data">저장 데이터</param>
    public static void Save ( SupportItemSaveData data )
    {
        string json = JsonUtility.ToJson( data );
        PlayerPrefs.SetString( SaveKey, json );
        PlayerPrefs.Save( );
    }

    /// <summary>
    /// 저장 데이터 불러오기
    /// </summary>
    /// <returns>코인과 아이템 보유량</returns>
    public static SupportItemSaveData Load ()
    {
        if ( PlayerPrefs.HasKey( SaveKey ) == true )
        {
            string json = PlayerPrefs.GetString( SaveKey );
            SupportItemSaveData data = JsonUtility.FromJson<SupportItemSaveData>( json );

            //잘못된 저장값을 새 데이터로 덮어쓰지 않음
            if ( data == null || data.Inventory == null ||
                 data.Balance < 0 )
            {
                throw new InvalidOperationException(
                    "[상점 저장 오류] " +
                    "코인과 아이템 저장 데이터를 확인해 주세요." );
            }

            return data;
        }

        //통합 저장이 없을 때만 기존 코인 가져오기
        SupportItemSaveData initial = new( )
        {
            Balance = Mathf.Max(
                0, PlayerPrefs.GetInt( LegacyBalanceKey, 0 ) )
        };

        Save( initial );
        return initial;
    }
}