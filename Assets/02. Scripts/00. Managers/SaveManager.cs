using System;
using UnityEngine;

/// <summary>
/// 저장 관리 - 사용자 설정과 코인, 서포트 아이템 저장 연결
/// </summary>
public class SaveManager : MonoBehaviour, IGameService
{
    const string SettingsKey = "3DRabbitPartyMulti.UserSettings";		//사용자 설정 저장 키


    /// <summary>
    /// 기존 저장 데이터 확인, 최초 저장 준비
    /// </summary>
    /// <param name="game">공통 매니저</param>
    /// <returns>저장 준비 성공 여부</returns>
    public bool Init ( GameManager game )
    {
        try
        {
            LoadSupportItems( );
            return true;
        }
        catch ( Exception exception ) when (
            exception is ArgumentException ||
            exception is InvalidOperationException )
        {
            Debug.LogError(
                $"[저장 초기화 실패] {exception.Message}", this );
            return false;
        }
    }

    /// <summary>
    /// 코인/아이템 보유량 조회
    /// </summary>
    /// <returns>저장 데이터</returns>
    public SupportItemSaveData LoadSupportItems ()
    {
        return SupportItemSave.Load( );
    }

    /// <summary>
    /// 코인/아이템 보유량 함께 저장
    /// </summary>
    /// <param name="data">저장할 데이터</param>
    public void SaveSupportItems (
        SupportItemSaveData data )
    {
        SupportItemSave.Save( data );
    }

    /// <summary>
    /// 사용자 설정 불러오기
    /// </summary>
    /// <returns>저장된 설정, 최초 실행이면 null</returns>
    public UserSettingsData LoadSettings ()
    {
        if ( PlayerPrefs.HasKey( SettingsKey ) == false )
            return null;

        string json = PlayerPrefs.GetString( SettingsKey );
        UserSettingsData data =
            JsonUtility.FromJson<UserSettingsData>( json );

        if ( data == null )
        {
            throw new InvalidOperationException(
                "[설정 저장 오류] 사용자 설정 데이터를 확인해 주세요." );
        }

        return data;
    }

    /// <summary>
    /// 확정한 사용자 설정 저장
    /// </summary>
    /// <param name="data">적용을 마친 설정</param>
    public void SaveSettings ( UserSettingsData data )
    {
        string json = JsonUtility.ToJson( data );
        PlayerPrefs.SetString( SettingsKey, json );
        PlayerPrefs.Save( );
    }

}
