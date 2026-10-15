using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// 원본 위치와 필드 값을 보관하고 필요한 값으로 변환
/// </summary>
public class DataImportRow
{
    /// <summary>
    /// 열 이름별 원본 값, JSON 배열은 배열 문자열로 보관
    /// </summary>
    Dictionary<string, string> _values;
    public string Location;		//CSV 행 또는 JSON 레코드 위치

    /// <summary>
    /// 원본 행 준비
    /// </summary>
    /// <param name="values">열 이름과 원본 값</param>
    /// <param name="location">원본 위치</param>
    public DataImportRow ( Dictionary<string, string> values, string location )
    {
        _values = values;
        Location = location;
    }


    /// <summary>
    /// 필수 문자열 읽기
    /// </summary>
    /// <param name="field">필드 이름</param>
    /// <returns>양쪽 공백을 제거한 값</returns>
    public string Text ( string field )
    {
        if ( Has( field ) == false )
            throw Error( field, "값이 없습니다." );
        return _values [ field ].Trim( );
    }

    /// <summary>
    /// 유한한 실수 읽기
    /// </summary>
    /// <param name="field">필드 이름</param>
    /// <returns>변환한 값</returns>
    public float Number ( string field )
    {
        if ( float.TryParse( Text( field ), NumberStyles.Float, CultureInfo.InvariantCulture,
            out float value ) == false || float.IsNaN( value ) == true || float.IsInfinity( value ) == true )
            throw Error( field, "유한한 숫자여야 합니다." );
        return value;
    }

    /// <summary>
    /// 정수 읽기
    /// </summary>
    /// <param name="field">필드 이름</param>
    /// <returns>변환한 값</returns>
    public int Integer ( string field )
    {
        if ( int.TryParse( Text( field ), NumberStyles.Integer, CultureInfo.InvariantCulture,
            out int value ) == false )
            throw Error( field, "정수여야 합니다." );
        return value;
    }

    /// <summary>
    /// 선택 여부 읽기
    /// </summary>
    /// <param name="field">필드 이름</param>
    /// <returns>변환한 값</returns>
    public bool Flag ( string field )
    {
        if ( bool.TryParse( Text( field ), out bool value ) == false )
            throw Error( field, "true 또는 false여야 합니다." );
        return value;
    }

    /// <summary>
    /// 정의된 enum 이름 읽기
    /// </summary>
    /// <param name="field">필드 이름</param>
    /// <returns>변환한 종류</returns>
    public T Kind<T> ( string field ) where T : struct, Enum
    {
        return ParseKind<T>( Text( field ), field );
    }

    /// <summary>
    /// JSON 문자열 배열 또는 파이프로 구분한 목록 읽기
    /// </summary>
    /// <param name="field">필드 이름</param>
    /// <returns>원본 목록</returns>
    public string [ ] List ( string field )
    {
        string value = Text( field );
        if ( value.StartsWith( "[", StringComparison.Ordinal ) == true )
            return DataImportSourceParser.ReadStringArray( value, $"{Location} / {field}" );
        return value.Split( '|' );
    }

    /// <summary>
    /// 중복 없는 enum 목록 읽기
    /// </summary>
    /// <param name="field">필드 이름</param>
    /// <returns>변환한 종류 목록</returns>
    public T [ ] Kinds<T> ( string field ) where T : struct, Enum
    {
        string [ ] source = List( field );
        T [ ] values = new T [ source.Length ];
        HashSet<T> kinds = new( );
        for ( int i = 0; i < source.Length; i++ )
        {
            values [ i ] = ParseKind<T>( source [ i ].Trim( ), field );
            if ( kinds.Add( values [ i ] ) == false )
                throw Error( field, $"종류가 중복됐습니다: {source [ i ]}" );
        }
        return values;
    }

    /// <summary>
    /// 선언된 필드 외 오타나 다른 형식의 열 검사
    /// </summary>
    /// <param name="fields">지원하는 필드 이름</param>
    public void CheckFields ( params string [ ] fields )
    {
        HashSet<string> allowed = new( fields, StringComparer.Ordinal );
        foreach ( string field in _values.Keys )
        {
            if ( allowed.Contains( field ) == false )
                throw Error( field, "지원하지 않는 필드입니다." );
        }
    }

    /// <summary>
    /// 원본 위치를 포함한 입력 오류 준비
    /// </summary>
    /// <param name="field">필드 이름</param>
    /// <param name="reason">오류 사유</param>
    /// <returns>입력 오류</returns>
    public FormatException Error ( string field, string reason )
    {
        return new FormatException( $"{Location} / {field}: {reason}" );
    }

    /// <summary>
    /// enum 이름을 정의된 종류로 변환
    /// </summary>
    /// <param name="value">종류 이름</param>
    /// <param name="field">원본 필드</param>
    /// <returns>변환한 종류</returns>
    T ParseKind<T> ( string value, string field ) where T : struct, Enum
    {
        //숫자 enum을 허용하지 않고 선언된 이름만 사용
        if ( Array.IndexOf( Enum.GetNames( typeof( T ) ), value ) < 0 )
            throw Error( field, $"정의되지 않은 종류: '{value}'" );
        return ( T )Enum.Parse( typeof( T ), value );
    }

    /// <summary>
    /// 선택 필드가 포함됐는지 확인
    /// </summary>
    /// <param name="field">필드 이름</param>
    /// <returns>필드 포함 여부</returns>
    public bool Has ( string field )
    {
        return _values.TryGetValue( field, out string value ) && string.IsNullOrWhiteSpace( value ) == false;
    }

}
