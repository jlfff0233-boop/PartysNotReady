using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JSON / CSV 원본 파일을 선택하거나 드롭하는 공통 에디터 필드
/// </summary>
public static class ImportSourceFileField
{
    const float DropAreaHeight = 58f;


    /// <summary>
    /// 원본 파일 필드를 그리고 경로 변경 여부를 반환
    /// </summary>
    public static bool Draw ( ref string sourcePath, string dialogTitle,
        string lastDirectoryPreferenceKey, ref string feedback )
    {
        bool changed = false;

        EditorGUILayout.LabelField( "원본 파일" );
        Rect dropArea = GUILayoutUtility.GetRect(
            0f, DropAreaHeight, GUILayout.ExpandWidth( true ) );
        DrawDropArea( dropArea, sourcePath );

        if ( HandleDragAndDrop( dropArea, ref sourcePath, ref feedback,
                 lastDirectoryPreferenceKey ) )
            changed = true;

        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            EditorGUI.BeginChangeCheck( );
            string editedPath = EditorGUILayout.TextField( sourcePath );
            if ( EditorGUI.EndChangeCheck( ) )
            {
                sourcePath = editedPath.Trim( );
                feedback = null;
                changed = true;
            }

            if ( GUILayout.Button( "찾기", GUILayout.Width( 52f ) ) )
            {
                string selected = EditorUtility.OpenFilePanelWithFilters(
                    dialogTitle, GetInitialDirectory( sourcePath, lastDirectoryPreferenceKey ),
                    new [ ] { "데이터 파일", "json,csv", "JSON", "json", "CSV", "csv" } );
                if ( string.IsNullOrWhiteSpace( selected ) == false )
                {
                    sourcePath = NormalizePath( selected );
                    RememberDirectory( sourcePath, lastDirectoryPreferenceKey );
                    feedback = null;
                    changed = true;
                }
            }

            using ( new EditorGUI.DisabledScope( string.IsNullOrWhiteSpace( sourcePath ) ) )
            {
                if ( GUILayout.Button( "해제", GUILayout.Width( 52f ) ) )
                {
                    sourcePath = string.Empty;
                    feedback = null;
                    changed = true;
                    GUI.FocusControl( null );
                }
            }
        }

        if ( string.IsNullOrWhiteSpace( feedback ) == false )
            EditorGUILayout.HelpBox( feedback, MessageType.Warning );

        return changed;
    }

    static void DrawDropArea ( Rect area, string sourcePath )
    {
        string message;
        if ( string.IsNullOrWhiteSpace( sourcePath ) )
            message = "JSON 또는 CSV 파일을 여기에 드롭하세요.\nWindows 탐색기와 Unity Project 창을 지원합니다.";
        else
            message = $"{Path.GetFileName( sourcePath )}\n다른 파일을 드롭하면 교체됩니다.";

        GUIStyle style = new GUIStyle( EditorStyles.helpBox )
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        GUI.Box( area, message, style );
    }

    static bool HandleDragAndDrop ( Rect area, ref string sourcePath,
        ref string feedback, string lastDirectoryPreferenceKey )
    {
        Event current = Event.current;
        if ( area.Contains( current.mousePosition ) == false )
            return false;
        if ( current.type != EventType.DragUpdated && current.type != EventType.DragPerform )
            return false;

        bool valid = TryGetDroppedPath( out string droppedPath, out string error );
        DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

        if ( current.type == EventType.DragUpdated )
        {
            current.Use( );
            return false;
        }

        DragAndDrop.AcceptDrag( );
        current.Use( );
        if ( valid == false )
        {
            feedback = error;
            return false;
        }

        sourcePath = droppedPath;
        feedback = null;
        RememberDirectory( sourcePath, lastDirectoryPreferenceKey );
        GUI.FocusControl( null );
        return true;
    }

    static bool TryGetDroppedPath ( out string path, out string error )
    {
        path = null;
        error = null;

        string [ ] paths = DragAndDrop.paths;
        if ( paths == null || paths.Length == 0 )
        {
            UnityEngine.Object [ ] references = DragAndDrop.objectReferences;
            if ( references != null && references.Length == 1 )
            {
                string assetPath = AssetDatabase.GetAssetPath( references [ 0 ] );
                if ( string.IsNullOrWhiteSpace( assetPath ) == false )
                    paths = new [ ] { assetPath };
            }
        }

        if ( paths == null || paths.Length != 1 )
        {
            error = "JSON 또는 CSV 파일을 한 번에 하나만 드롭해 주세요.";
            return false;
        }

        string normalized = NormalizePath( paths [ 0 ] );
        if ( File.Exists( normalized ) == false )
        {
            error = "드롭한 항목은 파일이 아니거나 현재 위치에서 찾을 수 없습니다.";
            return false;
        }

        string extension = Path.GetExtension( normalized );
        if ( string.Equals( extension, ".json", StringComparison.OrdinalIgnoreCase ) == false &&
             string.Equals( extension, ".csv", StringComparison.OrdinalIgnoreCase ) == false )
        {
            error = "지원하는 원본 파일 형식은 .json과 .csv입니다.";
            return false;
        }

        path = normalized;
        return true;
    }

    static string GetInitialDirectory ( string sourcePath, string preferenceKey )
    {
        if ( string.IsNullOrWhiteSpace( sourcePath ) == false )
        {
            try
            {
                string normalized = NormalizePath( sourcePath );
                string currentDirectory = Path.GetDirectoryName( normalized );
                if ( Directory.Exists( currentDirectory ) )
                    return currentDirectory;
            }
            catch ( Exception )
            {
                // 직접 입력한 잘못된 경로는 검사 단계에서 안내하고 기본 폴더를 사용한다.
            }
        }

        string remembered = EditorPrefs.GetString( preferenceKey, string.Empty );
        return Directory.Exists( remembered ) ? remembered : Application.dataPath;
    }

    static void RememberDirectory ( string sourcePath, string preferenceKey )
    {
        string directory = Path.GetDirectoryName( sourcePath );
        if ( Directory.Exists( directory ) )
            EditorPrefs.SetString( preferenceKey, directory );
    }

    static string NormalizePath ( string path )
    {
        if ( string.IsNullOrWhiteSpace( path ) )
            return string.Empty;

        return Path.GetFullPath( path.Trim( ) ).Replace( '\\', '/' );
    }
}
