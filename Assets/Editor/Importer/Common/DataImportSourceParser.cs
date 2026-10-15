using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// dataType / records JSON과 CSV의 설정 원본 읽기
/// </summary>
public static class DataImportSourceParser
{
    /// <summary>
    /// 대상 형식의 원본 행 읽기
    /// </summary>
    /// <param name="path">JSON 또는 CSV 경로</param>
    /// <param name="dataType">JSON의 대상 데이터 종류</param>
    /// <returns>위치와 필드 값을 보관한 행</returns>
    public static List<DataImportRow> Parse ( string path, string dataType )
    {
        string text = File.ReadAllText( path );
        string extension = Path.GetExtension( path );
        if ( string.Equals( extension, ".json", StringComparison.OrdinalIgnoreCase ) == true )
            return ReadJson( text, dataType );
        if ( string.Equals( extension, ".csv", StringComparison.OrdinalIgnoreCase ) == true )
            return ReadCsv( text );
        throw new FormatException( "지원하는 파일 형식은 .json과 .csv입니다." );
    }

    /// <summary>
    /// 문자열 JSON 배열 읽기
    /// </summary>
    /// <param name="text">JSON 배열</param>
    /// <param name="location">원본 위치</param>
    /// <returns>문자열 목록</returns>
    public static string [ ] ReadStringArray ( string text, string location )
    {
        DataImportJsonReader reader = new( text, location );
        string [ ] values = reader.StringArray( );
        reader.End( );
        return values;
    }

    /// <summary>
    /// 대상 종류를 확인하고 JSON 레코드 읽기
    /// </summary>
    /// <param name="text">JSON 원문</param>
    /// <param name="expectedType">대상 데이터 종류</param>
    /// <returns>원본 행</returns>
    static List<DataImportRow> ReadJson ( string text, string expectedType )
    {
        DataImportJsonReader reader = new( text, "JSON" );
        List<DataImportRow> rows = null;
        string dataType = null;
        HashSet<string> fields = new( StringComparer.Ordinal );
        reader.Take( "{" );
        if ( reader.At( "}" ) == false )
        {
            do
            {
                string field = reader.String( );
                if ( fields.Add( field ) == false )
                    throw new FormatException( $"JSON 루트 필드 중복: {field}" );
                reader.Take( ":" );
                if ( field == "dataType" )
                    dataType = reader.String( );
                else if ( field == "records" )
                {
                    rows = new List<DataImportRow>( );
                    reader.Take( "[" );
                    if ( reader.At( "]" ) == false )
                    {
                        do
                        {
                            rows.Add( new DataImportRow( reader.Record( ), $"JSON records[{rows.Count}]" ) );
                        }
                        while ( reader.Accept( "," ) == true );
                    }
                    reader.Take( "]" );
                }
                else
                    throw new FormatException( $"JSON 루트의 지원하지 않는 필드: {field}" );
            }
            while ( reader.Accept( "," ) == true );
        }
        reader.Take( "}" );
        reader.End( );
        if ( dataType != expectedType || rows == null )
            throw new FormatException( $"JSON에는 dataType '{expectedType}'과 records 배열이 필요합니다." );
        return rows;
    }

    /// <summary>
    /// 헤더와 행의 열 수를 확인하고 CSV 읽기
    /// </summary>
    /// <param name="text">CSV 원문</param>
    /// <returns>원본 행</returns>
    static List<DataImportRow> ReadCsv ( string text )
    {
        List<List<string>> records = DelimitedTextParser.ParseCsv( text );
        if ( records.Count == 0 )
            throw new FormatException( "CSV에 헤더가 없습니다." );
        List<string> headers = records [ 0 ];
        HashSet<string> names = new( StringComparer.Ordinal );
        for ( int i = 0; i < headers.Count; i++ )
        {
            headers [ i ] = headers [ i ].Trim( ).TrimStart( '\uFEFF' );
            if ( headers [ i ].Length == 0 || names.Add( headers [ i ] ) == false )
                throw new FormatException( $"CSV 헤더가 비었거나 중복됐습니다: '{headers [ i ]}'" );
        }

        List<DataImportRow> rows = new( );
        for ( int i = 1; i < records.Count; i++ )
        {
            if ( records [ i ].Count != headers.Count )
                throw new FormatException( $"CSV {i + 1}행의 열 수가 헤더와 다릅니다." );
            Dictionary<string, string> values = new( StringComparer.Ordinal );
            for ( int column = 0; column < headers.Count; column++ )
                values.Add( headers [ column ], records [ i ] [ column ] );
            rows.Add( new DataImportRow( values, $"CSV {i + 1}행" ) );
        }
        return rows;
    }
}

/// <summary>
/// 설정 원본의 객체 / 문자열 배열 / 숫자 / bool JSON 읽기
/// </summary>
class DataImportJsonReader
{
    //문자열의 제어 문자 / 잘못된 이스케이프와 숫자의 선행 0을 거절
    static Regex _tokens = new( "\\G(\"(?:[^\"\\\\\\x00-\\x1f]|\\\\(?:[\"\\\\/bfnrt]|u[0-9a-fA-F]{4}))*\"|-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null|[{}\\[\\],:])" );		//JSON 토큰 표현식
    string _text;		//JSON 원문
    string _location;		//원본 위치
    int _position;		//다음 토큰 위치
    string _token;		//현재 토큰

    /// <summary>
    /// JSON 원문과 오류 위치 준비
    /// </summary>
    /// <param name="text">JSON 원문</param>
    /// <param name="location">원본 위치</param>
    public DataImportJsonReader ( string text, string location )
    {
        _text = text.TrimStart( '\uFEFF' );
        _location = location;
        Next( );
    }

    /// <summary>
    /// 현재 토큰 일치 여부
    /// </summary>
    /// <param name="token">비교할 토큰</param>
    /// <returns>일치 여부</returns>
    public bool At ( string token )
    {
        return _token == token;
    }

    /// <summary>
    /// 일치하는 토큰을 읽고 다음으로 이동
    /// </summary>
    /// <param name="token">읽을 토큰</param>
    /// <returns>읽기 여부</returns>
    public bool Accept ( string token )
    {
        if ( At( token ) == false )
            return false;
        Next( );
        return true;
    }

    /// <summary>
    /// 필요한 토큰을 읽거나 형식 오류 보고
    /// </summary>
    /// <param name="token">필수 토큰</param>
    public void Take ( string token )
    {
        if ( Accept( token ) == false )
            throw Error( $"'{token}'이 필요합니다." );
    }

    /// <summary>
    /// 이스케이프를 복원하여 문자열 읽기
    /// </summary>
    /// <returns>문자열 값</returns>
    public string String ()
    {
        if ( _token == null || _token.StartsWith( "\"", StringComparison.Ordinal ) == false )
            throw Error( "문자열이 필요합니다." );
        string raw = _token;
        Next( );
        StringBuilder value = new( );
        for ( int i = 1; i < raw.Length - 1; i++ )
        {
            char character = raw [ i ];
            if ( character != '\\' )
                value.Append( character );
            else
            {
                char escape = raw [ ++i ];
                switch ( escape )
                {
                    case 'b': value.Append( '\b' ); break;
                    case 'f': value.Append( '\f' ); break;
                    case 'n': value.Append( '\n' ); break;
                    case 'r': value.Append( '\r' ); break;
                    case 't': value.Append( '\t' ); break;
                    case 'u':
                        value.Append( ( char )Convert.ToInt32( raw.Substring( i + 1, 4 ), 16 ) );
                        i += 4;
                        break;
                    default: value.Append( escape ); break;
                }
            }
        }
        return value.ToString( );
    }

    /// <summary>
    /// 문자열 배열 읽기
    /// </summary>
    /// <returns>문자열 목록</returns>
    public string [ ] StringArray ()
    {
        List<string> values = new( );
        Take( "[" );
        if ( At( "]" ) == false )
        {
            do
            {
                values.Add( String( ) );
            }
            while ( Accept( "," ) == true );
        }
        Take( "]" );
        return values.ToArray( );
    }

    /// <summary>
    /// 필드 이름별 레코드 값 읽기
    /// </summary>
    /// <returns>필드 이름과 원본 값</returns>
    public Dictionary<string, string> Record ()
    {
        Dictionary<string, string> values = new( StringComparer.Ordinal );
        Take( "{" );
        if ( At( "}" ) == false )
        {
            do
            {
                string field = String( );
                Take( ":" );
                if ( values.ContainsKey( field ) == true )
                    throw Error( $"필드 중복: {field}" );
                string value;
                if ( At( "[" ) == true )
                {
                    int start = _position - _token.Length;
                    StringArray( );
                    //다음 토큰의 앞까지 읽은 배열을 보관
                    int end = _position - ( _token == null ? 0 : _token.Length );
                    value = _text.Substring( start, end - start ).Trim( );
                }
                else if ( _token != null && _token.StartsWith( "\"", StringComparison.Ordinal ) == true )
                    value = String( );
                else if ( _token == "true" || _token == "false" ||
                    ( _token != null && ( _token [ 0 ] == '-' || char.IsDigit( _token [ 0 ] ) == true ) ) )
                {
                    value = _token;
                    Next( );
                }
                else
                    throw Error( $"{field}: 값은 문자열 / 숫자 / bool / 문자열 배열이어야 합니다." );
                values.Add( field, value );
            }
            while ( Accept( "," ) == true );
        }
        Take( "}" );
        return values;
    }

    /// <summary>
    /// 원본 끝의 불필요한 토큰 검사
    /// </summary>
    public void End ()
    {
        if ( _token != null )
            throw Error( "JSON 끝에 불필요한 값이 있습니다." );
    }

    /// <summary>
    /// 다음 JSON 토큰 읽기
    /// </summary>
    void Next ()
    {
        while ( _position < _text.Length && " \t\r\n".IndexOf( _text [ _position ] ) >= 0 )
            _position++;
        if ( _position == _text.Length )
        {
            _position = _text.Length;
            _token = null;
            return;
        }
        Match match = _tokens.Match( _text, _position );
        if ( match.Success == false )
            throw Error( "잘못된 JSON 토큰입니다." );
        _position = match.Index + match.Length;
        _token = match.Groups [ 1 ].Value;
    }

    /// <summary>
    /// 현재 문자 위치를 포함한 형식 오류 준비
    /// </summary>
    /// <param name="reason">오류 사유</param>
    /// <returns>형식 오류</returns>
    FormatException Error ( string reason )
    {
        return new FormatException( $"{_location} / 문자 {_position}: {reason}" );
    }
}
