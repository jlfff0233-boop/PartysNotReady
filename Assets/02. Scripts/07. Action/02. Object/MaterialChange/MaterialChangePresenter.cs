using UnityEngine;

/// <summary>
/// 매트리얼 변경 중재자 - 색 변경, 복원과 유지 처리
/// </summary>
[RequireComponent ( typeof ( MaterialChangeView ) )]
public class MaterialChangePresenter : MonoBehaviour
{
    [SerializeField] MaterialMap _map;      //매트리얼 맵

    readonly MaterialChangeModel _model = new ( );
    MaterialChangeView _view;
    ItemCarry _item;        //대상 아이템

    /// <summary>
    /// 복원 정보가 남아 있는 변경 여부
    /// </summary>
    public bool IsChanged => _model.IsChanged;

    /// <summary>
    /// 복원용으로 보관한 매트리얼
    /// </summary>
    public Material Original => _model.Original;

    /// <summary>
    /// 새 색 변경을 시도할 수 있는지 여부
    /// </summary>
    public bool CanChange => CheckCanChange( );

    /// <summary>
    /// 현재 보유 여부
    /// </summary>
    bool IsHeld => _item != null && _item.IsHold;

    private void Awake ( )
    {
        //표시 슬롯은 뷰에서 관리하고 보유 여부는 아이템 기능에서 확인
        _view = GetComponent<MaterialChangeView> ( );
        _item = GetComponent<ItemCarry> ( );
    }


    #region ----- 외형 연결 -----

    /// <summary>
    /// 현재 표시 슬롯의 매트리얼 조회
    /// </summary>
    /// <param name="material">현재 매트리얼, 조회 실패 시 null</param>
    /// <returns>표시 매트리얼 조회 성공 여부</returns>
    public bool TryGetCurrentMaterial ( out Material material )
    {
        material = null;
        return _view != null && _view.TryGetMaterial( out material );
    }

    /// <summary>
    /// 현재 외형의 Renderer와 매트리얼 슬롯 연결
    /// </summary>
    /// <param name="appearance">적용 또는 복원할 외형</param>
    /// <param name="reason">연결 실패 사유, 성공 시 빈 문자열</param>
    /// <returns>참조 갱신 성공 여부</returns>
    public bool TrySetAppearance ( AppearanceComponents appearance, out string reason )
    {
        if ( CheckAppearanceBinding( appearance, out reason ) == false )
            return false;

        //색 변경 미지원 외형도 연결 성공으로 처리하되 이전 참조는 해제
        if ( appearance.TryGetColorSlot( out Renderer target, out int index ) == false )
            return _view.TrySetTarget( null, -1, out reason );

        //매트리얼 자체와 복원 정보는 변경하지 않고 현재 슬롯만 연결
        return _view.TrySetTarget( target, index, out reason );
    }

    #endregion

    #region ----- 변경/복원 -----

    /// <summary>
    /// 현재와 다른 매트리얼로 변경
    /// </summary>
    /// <returns>변경 성공 여부</returns>
    public bool TryChange ( )
    {
        if ( CheckCanChange( ) == false )
            return false;

        if ( _view.TryGetMaterial ( out Material current ) == false )
            return false;

        if ( _model.TrySelect (
            _map.Materials , current , IsHeld , out Material selected ) == false )
            return false;

        if ( _view.TrySetMaterial ( selected ) == false )
            return false;

        //실제 적용 성공 후에만 복원 정보 확정
        _model.BeginChange ( current );
        return true;
    }

    /// <summary>
    /// 변경 직전 매트리얼 복원
    /// </summary>
    /// <returns>복원 성공 여부</returns>
    public bool TryRestore ( )
    {
        if ( isActiveAndEnabled == false || _view == null ||
             _model.CanRestore ( IsHeld ) == false )
            return false;

        if ( _view.TrySetMaterial ( _model.Original ) == false )
            return false;

        //복원에 실패한 경우에는 재시도할 수 있도록 기존 정보를 유지
        _model.EndChange ( );
        return true;
    }

    /// <summary>
    /// 현재 매트리얼을 유지하고 복원 정보 삭제
    /// </summary>
    /// <returns>변경 확정 여부</returns>
    public bool TryKeep ( )
    {
        if ( _model.IsChanged == false )
            return false;

        //표시는 건드리지 않으며 보유 중에도 확정 가능
        _model.EndChange ( );
        return true;
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 새 색 변경의 설정과 현재 상태 확인
    /// </summary>
    bool CheckCanChange ()
    {
        return isActiveAndEnabled == true && _view != null &&
               _view.isActiveAndEnabled == true && _map != null &&
               _model.IsChanged == false && IsHeld == false &&
               _view.TryGetMaterial( out _ );
    }

    /// <summary>
    /// 색 변경 대상을 새 외형으로 바꿀 수 있는지 확인
    /// </summary>
    /// <param name="appearance">연결할 외형</param>
    /// <param name="reason">연결 불가 사유</param>
    /// <returns>외형 연결 가능 여부</returns>
    public bool CheckAppearanceBinding ( AppearanceComponents appearance, out string reason )
    {
        reason = string.Empty;

        if ( isActiveAndEnabled == false || _view == null ||
             _view.isActiveAndEnabled == false )
            reason = "매트리얼 변경 기능의 초기화와 활성 상태를 확인해 주세요.";
        else if ( _model.IsChanged == true )
            reason = "진행 중인 색 변경을 복원하거나 확정한 뒤 외형을 연결해 주세요.";
        else if ( IsHeld == true )
            reason = "보유 중에는 색 변경 대상 외형을 바꿀 수 없습니다.";
        else if ( appearance == null || appearance.transform == transform ||
                  appearance.transform.IsChildOf( transform ) == false )
            reason = "현재 아이템 루트 아래의 외형을 연결해 주세요.";

        //준비 중인 비활성 외형도 연결 가능하며 표시 전환은 외형 기능에서 담당
        return string.IsNullOrEmpty( reason );
    }

    #endregion

    #region ----- 테스트 -----

    /// <summary>
    /// 플레이 중 후보 매트리얼 변경
    /// </summary>
    [ContextMenu ( "Test Change Material" )]
    void TestChange ( )
    {
        if ( Application.isPlaying == true && TryChange ( ) == false )
            Debug.LogWarning (
                "매트리얼 후보, 대상 슬롯, 보유 또는 변경 상태를 확인해 주세요." , this );
    }

    /// <summary>
    /// 플레이 중 이전 매트리얼 복원
    /// </summary>
    [ContextMenu ( "Test Restore Material" )]
    void TestRestore ( )
    {
        if ( Application.isPlaying == true && TryRestore ( ) == false )
            Debug.LogWarning (
                "복원할 변경 상태와 대상 슬롯, 보유 상태를 확인해 주세요." , this );
    }

    /// <summary>
    /// 플레이 중 현재 매트리얼 유지
    /// </summary>
    [ContextMenu ( "Test Keep Material" )]
    void TestKeep ( )
    {
        if ( Application.isPlaying == true )
            TryKeep ( );
    }

    #endregion

}
