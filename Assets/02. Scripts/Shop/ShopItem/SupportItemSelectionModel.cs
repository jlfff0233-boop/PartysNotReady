using System.Collections.Generic;

/// <summary>
/// 서포트 아이템 선택 모델 - 보유 목록과 반입 선택 관리
/// </summary>
public class SupportItemSelectionModel
{
    /// <summary>
    /// 설정 확인을 마친 아이템 목록
    /// </summary>
    IReadOnlyList<SupportItemData> _items;

    /// <summary>
    /// 현재 보유한 아이템 목록
    /// </summary>
    List<SupportItemData> _ownedItems = new( );

    /// <summary>
    /// 선택 순서별 반입 아이템
    /// </summary>
    List<SupportItemData> _selectedItems = new( );

    /// <summary>
    /// 선택 가능한 보유 아이템
    /// </summary>
    public IReadOnlyList<SupportItemData> OwnedItems => _ownedItems;

    /// <summary>
    /// 선택 순서별 반입 아이템
    /// </summary>
    public IReadOnlyList<SupportItemData> SelectedItems => _selectedItems;


    /// <summary>
    /// 아이템 목록 연결
    /// </summary>
    /// <param name="items">아이템 목록</param>
    public SupportItemSelectionModel (
        IReadOnlyList<SupportItemData> items )
    {
        _items = items;
    }

    /// <summary>
    /// 보유 목록 갱신과 선택 초기화
    /// </summary>
    /// <param name="inventory">현재 보유량</param>
    public void Reset ( SupportItemInventory inventory )
    {
        _ownedItems.Clear( );
        _selectedItems.Clear( );

        foreach ( SupportItemData data in _items )
        {
            if ( inventory.GetCount( data.Id ) > 0 )
                _ownedItems.Add( data );
        }
    }

    /// <summary>
    /// 반입 아이템 선택 또는 해제
    /// </summary>
    /// <param name="data">선택한 아이템</param>
    /// <param name="message">선택 결과 안내</param>
    /// <returns>선택 변경 여부</returns>
    public bool TryToggle (
        SupportItemData data, out string message )
    {
        message = string.Empty;

        if ( _ownedItems.Contains( data ) == false )
        {
            message = "보유하지 않은 아이템입니다.";
            return false;
        }

        int index = _selectedItems.IndexOf( data );

        //이미 선택한 아이템은 해제
        if ( index >= 0 )
        {
            _selectedItems.RemoveAt( index );
            return true;
        }

        if ( _selectedItems.Count >= PlayerSupportItems.MaxCount )
        {
            message =
                $"아이템은 최대 " +
                $"{PlayerSupportItems.MaxCount}종까지 선택할 수 있습니다.";
            return false;
        }

        _selectedItems.Add( data );
        return true;
    }

    /// <summary>
    /// 선택 목록을 배열로 복사
    /// </summary>
    /// <returns>선택 순서별 아이템</returns>
    public SupportItemData [ ] CopySelectedItems ()
    {
        return _selectedItems.ToArray( );
    }

    /// <summary>
    /// 반입 선택 해제
    /// </summary>
    public void ClearSelection ()
    {
        _selectedItems.Clear( );
    }
}