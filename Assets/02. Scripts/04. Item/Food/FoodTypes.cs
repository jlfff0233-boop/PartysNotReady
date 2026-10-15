using System;


/// <summary>
/// 음식 분류
/// </summary>
public enum FoodCategory
{
    Ingredient,     //재료
    Prepared        //완성 음식
}

/// <summary>
/// 음식 크기
/// </summary>
public enum FoodSize
{
    Small,      //소형
    Medium,     //중형
    Large       //대형
}

/// <summary>
/// 음식 배치 허용 대상 - 여러 타입 조합 가능
/// </summary>
[Flags]
public enum FoodPlacementType
{
    None = 0,       //배치 불가
    Table = 1 << 0,     //테이블
    Bowl = 1 << 1       //그릇
}
