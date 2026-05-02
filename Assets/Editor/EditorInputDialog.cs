using UnityEditor;
using UnityEngine;

public class EditorInputDialog : EditorWindow
{
    string _value = "";
    string _message = "";
    bool _firstFrame = true;

    static string _result;

    public static string Show(string title, string message, string defaultValue = "")
    {
        _result = null;
        var win = CreateInstance<EditorInputDialog>();
        win.titleContent = new GUIContent(title);
        win._message = message;
        win._value = defaultValue ?? "";
        win.minSize = new Vector2(340, 120);
        win.maxSize = new Vector2(340, 120);
        win.ShowModal();
        return _result;
    }

    void OnGUI()
    {
        GUILayout.Space(12);
        GUILayout.Label(_message, EditorStyles.wordWrappedLabel);
        GUILayout.Space(4);

        GUI.SetNextControlName("InputField");
        _value = EditorGUILayout.TextField(_value);

        if (_firstFrame)
        {
            EditorGUI.FocusTextInControl("InputField");
            _firstFrame = false;
        }

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("确定", GUILayout.Width(80)))
        {
            _result = _value;
            Close();
        }
        if (GUILayout.Button("取消", GUILayout.Width(80)))
        {
            _result = null;
            Close();
        }
        GUILayout.EndHorizontal();

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
        {
            _result = _value;
            Close();
        }
    }
}

/// <summary>
/// 成就场景配置弹窗：输入皮肤总数 + 每个皮肤的解锁星星数。
/// </summary>
public class AchievementConfigDialog : EditorWindow
{
    int _skinCount = 5;
    int[] _stars;
    Vector2 _scroll;
    bool _confirmed;

    static AchievementConfigDialog _instance;
    public static bool confirmed;
    public static int resultSkinCount;
    public static int[] resultStars;

    public static bool Show(int defaultCount, int[] defaultStars)
    {
        confirmed = false;
        var win = CreateInstance<AchievementConfigDialog>();
        win.titleContent = new GUIContent("Achievement Scene 配置");
        win._skinCount = Mathf.Max(1, defaultCount);
        if (defaultStars != null && defaultStars.Length == defaultCount)
            win._stars = (int[])defaultStars.Clone();
        else
        {
            win._stars = new int[win._skinCount];
            for (int i = 0; i < win._skinCount; i++) win._stars[i] = i * 2;
        }
        win.minSize = new Vector2(380, 320);
        win.maxSize = new Vector2(380, 600);
        win.ShowModal();
        return confirmed;
    }

    void SyncArraySize()
    {
        if (_stars == null || _stars.Length != _skinCount)
        {
            var old = _stars;
            _stars = new int[_skinCount];
            for (int i = 0; i < _skinCount; i++)
            {
                if (old != null && i < old.Length) _stars[i] = old[i];
                else _stars[i] = i * 2;
            }
        }
    }

    void OnGUI()
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField("基本设置", EditorStyles.boldLabel);
        int newCount = EditorGUILayout.IntField("皮肤总数", _skinCount);
        if (newCount != _skinCount)
        {
            _skinCount = Mathf.Max(1, newCount);
            SyncArraySize();
        }

        GUILayout.Space(8);
        EditorGUILayout.LabelField("每个皮肤的解锁星星数", EditorStyles.boldLabel);
        SyncArraySize();

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(360));
        for (int i = 0; i < _skinCount; i++)
        {
            _stars[i] = Mathf.Max(0, EditorGUILayout.IntField("皮肤 " + (i + 1) + " 需要星星", _stars[i]));
        }
        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("生成场景", GUILayout.Width(100)))
        {
            confirmed = true;
            resultSkinCount = _skinCount;
            resultStars = (int[])_stars.Clone();
            Close();
        }
        if (GUILayout.Button("取消", GUILayout.Width(80)))
        {
            confirmed = false;
            Close();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
    }
}
