using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매트리얼 변경 모델 - 후보 선택과 복원 상태 관리
/// </summary>
public class MaterialChangeModel
{
    readonly List<Material> _candidates = new ( );      //매트리얼 후보 목록
    Material _original;     //변경 직전 매트리얼
    bool _isChanged;        //변경 상태

    /// <summary>
    /// 복원 정보가 남아 있는 변경 여부
    /// </summary>
    public bool IsChanged => _isChanged;

    /// <summary>
    /// 변경 직전 매트리얼
    /// </summary>
    public Material Original => _original;

    /// <summary>
    /// 현재와 다른 매트리얼 무작위 선택
    /// </summary>
    /// <param name="materials">설정 후보 목록</param>
    /// <param name="current">현재 매트리얼</param>
    /// <param name="isHeld">보유 여부</param>
    /// <param name="selected">선택한 매트리얼</param>
    /// <returns>후보 선택 성공 여부</returns>
    public bool TrySelect (
        IReadOnlyList<Material> materials ,
        Material current ,
        bool isHeld ,
        out Material selected )
    {
        selected = null;

        if ( _isChanged == true || isHeld == true ||
             current == null || materials == null )
            return false;

        _candidates.Clear ( );

        foreach ( Material candidate in materials )
        {
            //같은 매트리얼을 여러 번 등록해도 선택 확률이 높아지지 않도록 제외
            if ( candidate == null || candidate == current ||
                 _candidates.Contains ( candidate ) == true )
                continue;

            _candidates.Add ( candidate );
        }

        if ( _candidates.Count == 0 )
            return false;

        selected = _candidates [ Random.Range ( 0 , _candidates.Count ) ];
        return true;
    }

    /// <summary>
    /// 복원할 매트리얼과 변경 상태 저장
    /// </summary>
    /// <param name="original">변경 직전 매트리얼</param>
    public void BeginChange ( Material original )
    {
        _original = original;
        _isChanged = true;
    }


    /// <summary>
    /// 복원 정보와 변경 상태 초기화
    /// </summary>
    public void EndChange ( )
    {
        _original = null;
        _isChanged = false;
    }

    /// <summary>
    /// 복원 가능 여부 확인
    /// </summary>
    /// <param name="isHeld">보유 여부</param>
    /// <returns>복원 가능 여부</returns>
    public bool CanRestore ( bool isHeld )
    {
        return _isChanged == true &&
               _original != null && isHeld == false;
    }

}
