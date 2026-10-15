using UnityEngine;

/// <summary>
/// 외형 연결 모듈 - 색, 존재와 아이템 참조의 일괄 연결과 실패 복구
/// </summary>
public class AppearanceLinker
{
    ItemWorldController _itemView;     //아이템 뷰
    ItemCarry _carry;     //보유 아이템
    PresenceController _presence;     //존재 중재자
    MaterialChangePresenter _materialChange;     //매트리얼 변경 중재자


    /// <summary>
    /// 외형을 공유할 아이템 기능 연결
    /// </summary>
    /// <param name="root">각 기능이 배치된 아이템 루트</param>
    public AppearanceLinker ( GameObject root )
    {
        _itemView = root.GetComponent<ItemWorldController>( );
        _carry = root.GetComponent<ItemCarry>( );
        _presence = root.GetComponent<PresenceController>( );
        _materialChange = root.GetComponent<MaterialChangePresenter>( );
    }

    /// <summary>
    /// 보유 상태를 확인하고 아이템 외형 충돌체 연결
    /// </summary>
    /// <param name="appearance">연결할 외형</param>
    /// <param name="reason">연결 실패 사유</param>
    /// <returns>충돌체 연결 성공 여부</returns>
    bool TryBindItemAppearance ( AppearanceComponents appearance, out string reason )
    {
        //활성화 콜백과 복구 중에도 보유 조건을 다시 확인
        if ( CheckItemAppearance( out reason ) == false )
            return false;

        return _itemView.TrySetAppearanceCollider( appearance.MainCollider, out reason );
    }

    /// <summary>
    /// 색, 존재와 아이템 기능에 외형 연결
    /// </summary>
    /// <param name="appearance">활성화한 외형</param>
    /// <param name="completed">통과한 연결 단계, 색 변경 1, 존재 2, 아이템 3</param>
    /// <param name="reason">연결 실패 사유</param>
    /// <returns>전체 참조 연결 성공 여부</returns>
    public bool TryBind ( AppearanceComponents appearance, out int completed, out string reason )
    {
        completed = 0;
        reason = string.Empty;

        //선택 기능이 없으면 해당 단계는 통과하며 실패 복구 범위를 기록
        if ( _materialChange != null &&
             _materialChange.TrySetAppearance( appearance, out reason ) == false )
            return false;
        completed = 1;

        //Presence는 기존 표시가 유지되는 동안 새 참조를 받음
        if ( _presence != null &&
             _presence.TrySetAppearance( appearance, out reason ) == false )
            return false;
        completed = 2;

        if ( TryBindItemAppearance( appearance, out reason ) == false )
            return false;

        completed = 3;
        return true;
    }

    /// <summary>
    /// 변경한 기능 참조를 이전 외형으로 복구
    /// </summary>
    /// <param name="previous">다시 연결할 이전 외형</param>
    /// <param name="completed">복구할 참조 범위를 정하는 마지막 통과 단계</param>
    /// <param name="reason">복구 실패 사유</param>
    /// <returns>참조 복구 성공 여부</returns>
    public bool TryRestoreBindings ( AppearanceComponents previous, int completed, out string reason )
    {
        reason = string.Empty;
        //연결한 순서의 반대로 아이템 / 존재 / 색 변경 참조 복구
        //한 기능의 복구가 실패해도 나머지 참조 복구는 계속 시도
        if ( completed >= 3 &&
             TryBindItemAppearance( previous, out string itemReason ) == false )
            reason += $"아이템: {itemReason} ";
        if ( completed >= 2 && _presence != null &&
             _presence.TryRestoreAppearance( previous, out string presenceReason ) == false )
            reason += $"존재: {presenceReason} ";
        if ( completed >= 1 && _materialChange != null &&
             _materialChange.TrySetAppearance( previous, out string colorReason ) == false )
            reason += $"색 변경: {colorReason} ";

        return string.IsNullOrEmpty( reason );
    }


    /// <summary>
    /// 아이템 외형을 연결할 수 있는 보유 상태 확인
    /// </summary>
    /// <param name="reason">연결 불가 사유</param>
    /// <returns>외형 연결 가능 여부</returns>
    bool CheckItemAppearance ( out string reason )
    {
        reason = string.Empty;
        if ( _carry != null && _carry.IsHold == true )
        {
            reason = "보유 중에는 외형 충돌체를 변경할 수 없습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 각 기능에 새 외형을 연결할 수 있는지 확인
    /// </summary>
    /// <param name="appearance">활성화한 후보 외형</param>
    /// <param name="reason">연결 불가 사유</param>
    /// <returns>참조 연결 가능 여부</returns>
    public bool CheckBindings ( AppearanceComponents appearance, out string reason )
    {
        reason = string.Empty;
        if ( _materialChange != null &&
             _materialChange.CheckAppearanceBinding( appearance, out reason ) == false )
            return false;
        if ( _presence != null &&
             _presence.CheckAppearanceBinding( appearance, false, out reason ) == false )
            return false;

        return _itemView != null && CheckItemAppearance( out reason ) &&
               _itemView.CheckAppearanceBinding( appearance.MainCollider, out reason );
    }

    /// <summary>
    /// 외형을 공유하는 기능의 변경 가능 상태 확인
    /// </summary>
    /// <param name="reason">변경 불가 사유</param>
    /// <returns>외형 변경 시작 가능 여부</returns>
    public bool CheckReady ( out string reason )
    {
        reason = string.Empty;
        if ( _materialChange != null &&
             ( _materialChange.isActiveAndEnabled == false ||
               _materialChange.IsChanged == true ) )
        {
            reason = "색 변경 기능의 활성 상태와 진행 중인 변경을 확인해 주세요.";
            return false;
        }

        if ( _presence != null &&
             ( _presence.IsReady == false || _presence.IsPresent == false ||
               _presence.IsVisible == false || _presence.IsRestoring == true ||
               _presence.InvestigationType != PresenceInvestigationType.None ) )
        {
            reason = "존재 기능의 조사와 복구를 종료하고 정상 표시 상태로 전환해 주세요.";
            return false;
        }

        return true;
    }

}
