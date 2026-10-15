using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 음식 데이터 맵 - ID별 등록과 조회
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Food/FoodDataMap" )]
public class FoodDataMap : ScriptableObject
{
    [SerializeField] FoodData [ ] _foodDatas;       //음식 데이터 배열

    /// <summary>
    /// 음식 데이터 딕셔너리(음식 ID, 음식 데이터)
    /// </summary>
    Dictionary<string, FoodData> _foodDataMap =
        new Dictionary<string, FoodData>( );


    private void OnEnable ()
    {
        //재활성화 시 현재 목록으로 조회 정보를 다시 구성
        int count = _foodDatas != null ? _foodDatas.Length : 0;
        _foodDataMap = new Dictionary<string, FoodData>( count );

        for ( int i = 0; i < count; i++ )
        {
            FoodData foodData = _foodDatas [ i ];

            //비어 있거나 ID가 없는 데이터는 조회 목록에서 제외
            if ( foodData == null || string.IsNullOrWhiteSpace( foodData.FoodId ) )
                continue;

            //같은 ID가 중복되면 목록의 뒤 항목으로 갱신
            _foodDataMap [ foodData.FoodId ] = foodData;
        }
    }


    /// <summary>
    /// 음식 데이터 조회
    /// </summary>
    /// <param name="foodId">음식 ID</param>
    /// <param name="data">음식 데이터</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetData ( string foodId, out FoodData data )
    {
        return _foodDataMap.TryGetValue( foodId, out data );
    }
}
