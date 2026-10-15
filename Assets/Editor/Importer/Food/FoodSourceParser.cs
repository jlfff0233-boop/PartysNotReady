using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 음식과 레시피 JSON / CSV 원본 파서
/// </summary>
public static class FoodSourceParser
{
    /// <summary>
    /// 확장자에 맞춰 음식 데이터 읽기
    /// </summary>
    public static List<FoodImportRow> ParseFoods ( string path )
    {
        string text = File.ReadAllText( path );
        if ( string.Equals( Path.GetExtension( path ), ".json", StringComparison.OrdinalIgnoreCase ) )
        {
            FoodImportRoot root = JsonUtility.FromJson<FoodImportRoot>( text );
            if ( root == null || root.foods == null )
                throw new FormatException( "JSON 루트에 foods 배열이 없습니다." );
            return new List<FoodImportRow>( root.foods );
        }

        if ( string.Equals( Path.GetExtension( path ), ".csv", StringComparison.OrdinalIgnoreCase ) )
            return ParseFoodCsv( text );
        throw new FormatException( "지원하는 파일 형식은 .json과 .csv입니다." );
    }

    /// <summary>
    /// 확장자에 맞춰 레시피 데이터 읽기
    /// </summary>
    public static List<RecipeImportRow> ParseRecipes ( string path )
    {
        string text = File.ReadAllText( path );
        if ( string.Equals( Path.GetExtension( path ), ".json", StringComparison.OrdinalIgnoreCase ) )
        {
            RecipeImportRoot root = JsonUtility.FromJson<RecipeImportRoot>( text );
            if ( root == null || root.recipes == null )
                throw new FormatException( "JSON 루트에 recipes 배열이 없습니다." );
            return new List<RecipeImportRow>( root.recipes );
        }

        if ( string.Equals( Path.GetExtension( path ), ".csv", StringComparison.OrdinalIgnoreCase ) )
            return ParseRecipeCsv( text );
        throw new FormatException( "지원하는 파일 형식은 .json과 .csv입니다." );
    }

    static List<FoodImportRow> ParseFoodCsv ( string text )
    {
        List<List<string>> records = DelimitedTextParser.ParseCsv( text );
        CsvTable table = new CsvTable( records,
            "foodId", "foodName", "category", "size", "satisfaction", "placementType" );
        List<FoodImportRow> foods = new( );

        for ( int i = 1; i < records.Count; i++ )
        {
            string satisfaction = table.Get( records [ i ], "satisfaction", i + 1 );
            if ( float.TryParse( satisfaction, NumberStyles.Float, CultureInfo.InvariantCulture, out float value ) == false )
                throw new FormatException( $"CSV {i + 1}행 satisfaction '{satisfaction}'은 숫자가 아닙니다." );

            foods.Add( new FoodImportRow
            {
                foodId = table.Get( records [ i ], "foodId", i + 1 ).Trim( ),
                foodName = table.Get( records [ i ], "foodName", i + 1 ).Trim( ),
                category = table.Get( records [ i ], "category", i + 1 ).Trim( ),
                size = table.Get( records [ i ], "size", i + 1 ).Trim( ),
                satisfaction = value,
                placementType = SplitFlags( table.Get( records [ i ], "placementType", i + 1 ) )
            } );
        }
        return foods;
    }

    static List<RecipeImportRow> ParseRecipeCsv ( string text )
    {
        List<List<string>> records = DelimitedTextParser.ParseCsv( text );
        CsvTable table = new CsvTable( records,
            "recipeId", "resultFoodId", "ingredientFoodId", "requiredCount" );
        Dictionary<string, RecipeImportRow> map = new( StringComparer.Ordinal );
        Dictionary<string, List<RecipeIngredientImportRow>> ingredientMap = new( StringComparer.Ordinal );
        List<RecipeImportRow> recipes = new( );

        for ( int i = 1; i < records.Count; i++ )
        {
            string recipeId = table.Get( records [ i ], "recipeId", i + 1 ).Trim( );
            string resultFoodId = table.Get( records [ i ], "resultFoodId", i + 1 ).Trim( );
            string displayName = table.GetOptional( records [ i ], "displayName" ).Trim( );
            string countText = table.Get( records [ i ], "requiredCount", i + 1 ).Trim( );
            if ( int.TryParse( countText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int count ) == false )
                throw new FormatException(
                    $"CSV {i + 1}행 requiredCount '{countText}'은 정수가 아닙니다." );

            if ( map.TryGetValue( recipeId, out RecipeImportRow recipe ) == false )
            {
                recipe = new RecipeImportRow
                {
                    recipeId = recipeId,
                    resultFoodId = resultFoodId,
                    displayName = displayName
                };
                map.Add( recipeId, recipe );
                ingredientMap.Add( recipeId, new List<RecipeIngredientImportRow>( ) );
                recipes.Add( recipe );
            }
            else if ( recipe.resultFoodId != resultFoodId ||
                      recipe.displayName != displayName )
                throw new FormatException(
                    $"CSV {i + 1}행의 결과 ID 또는 표시 이름이 같은 recipeId의 앞선 행과 다릅니다." );

            ingredientMap [ recipeId ].Add( new RecipeIngredientImportRow
            {
                foodId = table.Get( records [ i ], "ingredientFoodId", i + 1 ).Trim( ),
                requiredCount = count
            } );
        }

        for ( int i = 0; i < recipes.Count; i++ )
            recipes [ i ].ingredients = ingredientMap [ recipes [ i ].recipeId ].ToArray( );
        return recipes;
    }

    static string [ ] SplitFlags ( string value )
    {
        string [ ] values = value.Split( new [ ] { '|' }, StringSplitOptions.RemoveEmptyEntries );
        for ( int i = 0; i < values.Length; i++ )
            values [ i ] = values [ i ].Trim( );
        return values;
    }

    sealed class CsvTable
    {
        readonly Dictionary<string, int> _columns = new( StringComparer.Ordinal );

        public CsvTable ( List<List<string>> records, params string [ ] requiredColumns )
        {
            if ( records.Count == 0 )
                throw new FormatException( "CSV에 헤더가 없습니다." );

            for ( int i = 0; i < records [ 0 ].Count; i++ )
            {
                string header = records [ 0 ] [ i ].Trim( ).TrimStart( '\uFEFF' );
                if ( _columns.ContainsKey( header ) )
                    throw new FormatException( $"CSV 헤더 '{header}'가 중복되었습니다." );
                _columns.Add( header, i );
            }

            for ( int i = 0; i < requiredColumns.Length; i++ )
            {
                if ( _columns.ContainsKey( requiredColumns [ i ] ) == false )
                    throw new FormatException( $"CSV 헤더에 '{requiredColumns [ i ]}' 열이 없습니다." );
            }
        }

        public string Get ( List<string> record, string column, int rowNumber )
        {
            int index = _columns [ column ];
            if ( index >= record.Count )
                throw new FormatException( $"CSV {rowNumber}행에 '{column}' 값이 없습니다." );
            return record [ index ];
        }

        public string GetOptional ( List<string> record, string column )
        {
            if ( _columns.TryGetValue( column, out int index ) == false || index >= record.Count )
                return string.Empty;
            return record [ index ];
        }
    }
}
