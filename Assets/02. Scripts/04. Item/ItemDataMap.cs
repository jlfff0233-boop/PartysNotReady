using UnityEngine;

/// <summary>
/// 아이템 맵 - 아이템 데이터 목록과 포함 여부 조회
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Item/ItemDataMap" )]
public class ItemDataMap : ScriptableObject
{
    [SerializeField] ItemData [ ] _items;     //아이템 데이터 목록

    /// <summary>
    /// 아이템 데이터의 등록 여부 확인
    /// </summary>
    /// <param name="data">확인할 아이템 데이터</param>
    /// <returns>목록에 포함되어 있으면 true</returns>
    public bool CheckRegister ( ItemData data )
    {
        if ( data == null || _items == null )
            return false;

        //같은 종류는 같은 데이터 에셋을 공유
        foreach ( ItemData item in _items )
        {
            if ( item == data )
                return true;
        }

        return false;
    }
}