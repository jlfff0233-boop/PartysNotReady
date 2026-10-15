using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 따옴표와 줄바꿈을 지원하는 CSV 파서
/// </summary>
public static class DelimitedTextParser
{
    /// <summary>
    /// CSV 텍스트를 레코드와 필드로 분리
    /// </summary>
    /// <param name="text">CSV 원본</param>
    /// <returns>행별 필드 목록</returns>
    public static List<List<string>> ParseCsv ( string text )
    {
        List<List<string>> records = new( );
        List<string> record = new( );
        StringBuilder field = new( );
        bool quoted = false;
        bool quoteClosed = false;

        for ( int i = 0; i < text.Length; i++ )
        {
            char current = text [ i ];
            if ( quoted )
            {
                if ( current == '"' )
                {
                    if ( i + 1 < text.Length && text [ i + 1 ] == '"' )
                    {
                        field.Append( '"' );
                        i++;
                    }
                    else
                    {
                        quoted = false;
                        quoteClosed = true;
                    }
                }
                else
                    field.Append( current );
                continue;
            }

            if ( quoteClosed && current != ',' && current != '\r' && current != '\n' )
                throw new FormatException( "CSV 닫는 따옴표 뒤에는 구분자 또는 줄바꿈만 올 수 있습니다." );

            if ( current == '"' )
            {
                if ( field.Length != 0 )
                    throw new FormatException( "CSV 따옴표는 필드 시작에만 올 수 있습니다." );
                quoted = true;
            }
            else if ( current == ',' )
            {
                record.Add( field.ToString( ) );
                field.Clear( );
                quoteClosed = false;
            }
            else if ( current == '\r' || current == '\n' )
            {
                record.Add( field.ToString( ) );
                field.Clear( );
                quoteClosed = false;
                if ( HasValue( record ) )
                    records.Add( record );
                record = new List<string>( );

                if ( current == '\r' && i + 1 < text.Length && text [ i + 1 ] == '\n' )
                    i++;
            }
            else
                field.Append( current );
        }

        if ( quoted )
            throw new FormatException( "CSV 따옴표가 닫히지 않았습니다." );

        record.Add( field.ToString( ) );
        if ( HasValue( record ) )
            records.Add( record );
        return records;
    }

    static bool HasValue ( List<string> record )
    {
        for ( int i = 0; i < record.Count; i++ )
        {
            if ( string.IsNullOrWhiteSpace( record [ i ] ) == false )
                return true;
        }
        return false;
    }
}
