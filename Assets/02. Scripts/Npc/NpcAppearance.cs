using UnityEngine;

/// <summary>
/// NPC 외형 - 무늬 매트리얼 선택과 지정 슬롯 적용
/// </summary>
public class NpcAppearance : MonoBehaviour
{
    [Header( "----- 무늬 -----" )]
    [SerializeField] Renderer _target;        //무늬를 적용할 렌더러
    [SerializeField, Min( 0 )] int _materialIndex;        //무늬 매트리얼 슬롯

    bool _isReady;        //무늬 적용 완료 여부


    /// <summary>
    /// 전달받은 후보에서 무늬를 한 번 선택해 적용
    /// </summary>
    /// <param name="map">NPC 무늬 후보 목록</param>
    /// <returns>무늬 준비 성공 여부</returns>
    public bool Init ( MaterialMap map )
    {
        if ( _isReady == true )
            return true;

        if ( map == null || map.Materials == null || map.Materials.Count == 0 )
        {
            Debug.LogError( $"[NPC 무늬 초기화 실패] {name}: 무늬 Map에 매트리얼을 등록해 주세요.", this );
            return false;
        }

        if ( _target == null )
        {
            Debug.LogError( $"[NPC 무늬 초기화 실패] {name}: 몸체 Renderer를 연결해 주세요.", this );
            return false;
        }

        Material [ ] materials = _target.sharedMaterials;        //현재 슬롯별 매트리얼
        if ( _materialIndex < 0 || _materialIndex >= materials.Length )
        {
            Debug.LogError( $"[NPC 무늬 초기화 실패] {name}: 매트리얼 슬롯 {_materialIndex}를 확인해 주세요.", this );
            return false;
        }

        for ( int i = 0; i < map.Materials.Count; i++ )
        {
            if ( map.Materials [ i ] != null )
                continue;

            Debug.LogError( $"[NPC 무늬 초기화 실패] {map.name}: 후보 {i}의 매트리얼이 비어 있습니다.", map );
            return false;
        }

        //에셋의 속성을 변경하지 않고 무늬 슬롯의 참조만 교체
        materials [ _materialIndex ] = map.Materials [ Random.Range( 0, map.Materials.Count ) ];
        _target.sharedMaterials = materials;
        _isReady = true;
        return true;
    }
}
