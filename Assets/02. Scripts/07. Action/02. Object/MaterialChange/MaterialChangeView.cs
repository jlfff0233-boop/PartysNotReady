using UnityEngine;

/// <summary>
/// 매트리얼 변경 뷰 - 지정 Renderer 슬롯 조회와 적용
/// </summary>
public class MaterialChangeView : MonoBehaviour
{
    [SerializeField] Renderer _target;      //변경 대상
    [SerializeField, Min ( 0 )] int _materialIndex;     //변경할 매트리얼 슬롯 인덱스

    /// <summary>
    /// 색 변경 Renderer와 슬롯 설정
    /// </summary>
    /// <param name="target">새 Renderer, 색 변경 미지원 시 null</param>
    /// <param name="materialIndex">새 슬롯 번호, 미지원 시 -1</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>참조 갱신 성공 여부</returns>
    public bool TrySetTarget (
        Renderer target, int materialIndex, out string reason )
    {
        reason = string.Empty;

        if ( target == null )
        {
            //옛 외형을 다시 변경하지 않도록 참조와 슬롯을 함께 해제
            _target = null;
            _materialIndex = -1;
            return true;
        }

        if ( materialIndex < 0 || materialIndex >= target.sharedMaterials.Length )
        {
            reason = "새 외형의 매트리얼 슬롯 범위를 확인해 주세요.";
            return false;
        }

        _target = target;
        _materialIndex = materialIndex;
        return true;
    }

    /// <summary>
    /// 지정 슬롯의 현재 매트리얼 조회
    /// </summary>
    /// <param name="material">현재 매트리얼</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetMaterial ( out Material material )
    {
        material = null;

        if ( TryGetSlots ( out Material [ ] materials ) == false )
            return false;

        material = materials [ _materialIndex ];
        return true;
    }

    /// <summary>
    /// 지정 슬롯에 매트리얼 적용
    /// </summary>
    /// <param name="material">적용할 매트리얼</param>
    /// <returns>적용 성공 여부</returns>
    public bool TrySetMaterial ( Material material )
    {
        if ( material == null || TryGetSlots ( out Material [ ] materials ) == false )
            return false;

        //슬롯 배열의 지정 항목만 바꿔 다시 할당하며 다른 슬롯은 유지
        //매트리얼 에셋 자체의 색이나 속성은 수정하지 않음
        materials [ _materialIndex ] = material;
        _target.sharedMaterials = materials;
        return true;
    }

    /// <summary>
    /// 유효한 매트리얼 슬롯 배열 조회
    /// </summary>
    /// <param name="materials">현재 슬롯 배열</param>
    /// <returns>지정 슬롯 사용 가능 여부</returns>
    bool TryGetSlots ( out Material [ ] materials )
    {
        materials = null;

        if ( _target == null || _materialIndex < 0 )
            return false;

        materials = _target.sharedMaterials;
        return _materialIndex < materials.Length;
    }
}
