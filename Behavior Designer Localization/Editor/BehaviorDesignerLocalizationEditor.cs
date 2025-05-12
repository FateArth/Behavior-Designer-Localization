using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BehaviorDesigner.Editor;
using UnityEditor;
using UnityEngine;
using BehaviorDesigner.Runtime.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using Sirenix.Utilities;
public class BehaviorDesignerLocalizationEditor : EditorWindow
{
    private const string PrefKey = "BD_Language";
    private static Harmony harmony;
    private static LocalizationData localizationData;
    private static readonly string[] languages = { "English", "Chinese" };
    private static int selectedLanguageIndex;

    [MenuItem("Tools/Behavior Designer Localization")]
    public static void ShowWindow()
    {
        GetWindow<BehaviorDesignerLocalizationEditor>("Behavior Designer Localization");
    }
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }
 
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            SetLanguage(EditorPrefs.GetString(PrefKey, "English"));
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SetLanguage(EditorPrefs.GetString(PrefKey, "English"));
        }
    }

    private void OnEnable()
    {
        string currentLanguage = EditorPrefs.GetString(PrefKey, "English");
        selectedLanguageIndex = Array.IndexOf(languages, currentLanguage);
        ApplyLocalization(currentLanguage);
    }

    private void OnGUI()
    {
        GUILayout.Label("Select Language", EditorStyles.boldLabel);

        int newSelectedLanguageIndex = EditorGUILayout.Popup("Language", selectedLanguageIndex, languages);
        if (newSelectedLanguageIndex != selectedLanguageIndex)
        {
            selectedLanguageIndex = newSelectedLanguageIndex;
            SetLanguage(languages[selectedLanguageIndex]);
        }
    }

    private static void SetLanguage(string language)
    {
        EditorPrefs.SetString(PrefKey, language);
        if (language != "English")
        {
            ApplyLocalization(language);
        }
    }

    private static void ApplyLocalization(string language)
    {
        if (harmony == null)
        {
            harmony = new Harmony("com.behaviordesigner.localization");
        }

        //翻译任务列表
        PatchTaskList();
        //翻译任务节点
        PatchTaskName();
        //翻译任务说明
        PatchTaskInfo();
        LoadLocalizationData(language);
    }

    private static void PatchTaskList()
    {
        //翻译TaskList.SearchableType.Name
        Type taskListType = typeof(TaskList);
        // 获取私有的 SearchableType 嵌套类
        Type searchableType = taskListType.GetNestedType("SearchableType", BindingFlags.NonPublic);

        if (searchableType == null)
        {
            Debug.LogError("无法找到 TaskList.SearchableType 类");
            return;
        }

        // 获取 SearchableType 类的 Name 属性
        PropertyInfo nameProperty = searchableType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);

        if (nameProperty == null)
        {
            Debug.LogError("无法找到 TaskList.SearchableType.Name 属性");
            return;
        }

        // 修补 Name 属性的 getter 方法
        harmony.Patch(
            original: nameProperty.GetGetMethod(),
            postfix: new HarmonyMethod(typeof(BehaviorDesignerLocalizationEditor), nameof(ModifyTaskList))
        );
    }

    private static void ModifyTaskList(ref string __result)
    {
        string currentLanguage = EditorPrefs.GetString(PrefKey, "English");
        if (currentLanguage != "English" && localizationData.GetTaskName().TryGetValue(__result, out var localized))
        {
            __result = localized;
        }
    }


    private static void PatchTaskName()
    {
        //翻译名称
        //修改NodeDesigner.ToString()
        Type nodeDesignerType = typeof(NodeDesigner);
        MethodInfo toStringMethod = nodeDesignerType.GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance);
        if (toStringMethod == null)
        {
            Debug.LogError("无法找到 NodeDesigner.ToString 方法");
            return;
        }

        harmony.Patch(
            original: toStringMethod,
            postfix: new HarmonyMethod(typeof(BehaviorDesignerLocalizationEditor), nameof(ModifyTaskName))
        );
    }

    private static void ModifyTaskName(ref string __result)
    {
        string currentLanguage = EditorPrefs.GetString(PrefKey, "English");
        if (currentLanguage != "English" && localizationData.GetTaskName().TryGetValue(__result, out var localized))
        {
            __result = localized;
        }
    }

    private static void PatchTaskInfo()
    {
        //翻译信息
        Type taskDescriptionType = typeof(TaskDescriptionAttribute);
        PropertyInfo descProperty = taskDescriptionType.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);

        if (descProperty == null)
        {
            Debug.LogError("无法找到 TaskDescriptionAttribute.Description 属性");
            return;
        }

        harmony.Patch(
            original: descProperty.GetGetMethod(),
            postfix: new HarmonyMethod(typeof(BehaviorDesignerLocalizationEditor), nameof(ModifyTaskInfo))
        );
    }

    private static void ModifyTaskInfo(ref string __result)
    {
        string currentLanguage = EditorPrefs.GetString(PrefKey, "English");
        if (currentLanguage != "English" && localizationData.Description.TryGetValue(__result, out var localized))
        {
            __result = localized;
        }
    }

    private static void LoadLocalizationData(string language)
    {
        string filePath = Path.Combine(Application.dataPath + $"/Plugins/Behavior Designer Localization/Localization/{language}.json");

        if (File.Exists(filePath))
        {
            string jsonContent = File.ReadAllText(filePath);
            localizationData = JsonConvert.DeserializeObject<LocalizationData>(jsonContent);
        }
        else
        {
            Debug.LogError($"Localization file not found: {filePath}");
            localizationData = null;
        }
    }

    [Serializable]
    private class LocalizationData
    {
        public Dictionary<string, string> Description;
        public Dictionary<string, string> TaskName;

        public Dictionary<string, string> GetTaskName()
        {
            if (TaskName != null)
            {
                var dict = new Dictionary<string, string>();
                foreach (var v in TaskName)
                {
                    dict.Add(BehaviorDesignerUtility.SplitCamelCase(v.Key), v.Value);
                }

                return dict;
            }

            return null;
        }
    }
}