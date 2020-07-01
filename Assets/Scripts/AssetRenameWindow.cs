using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using UnityEditor.AnimatedValues;
using System.Linq;
using System.IO;

public class AssetRenameWindow : EditorWindow
{
    private Dictionary<string, List<AssetModel>> m_searchedResult = new Dictionary<string, List<AssetModel>>();
    private Dictionary<string, List<AssetModel>> m_filteredResult = new Dictionary<string, List<AssetModel>>();

    private List<bool> m_resultExpandState = new List<bool>();
    private List<Vector2> m_resultScrollPos = new List<Vector2>();
    private List<string> m_filterPaths = new List<string>();
    private List<string> m_names = new List<string>();
    private string m_currentSearchedName;
    private bool m_isFilterSingle = true;
    protected Vector2 m_scrollValue = Vector2.zero;

    private const int ELEMENT_PER_PAGE = 30;
    private int m_currentPage = 0;

    private class AssetModel
    {
        public Texture Texture;
        public string AssetPath;
        public bool IsToggleOn;
    }

    [MenuItem("Compass/Tools/Asset Rename Window")]
    static void Init()
    {
        var window = GetWindow(typeof(AssetRenameWindow));
        window.titleContent = new GUIContent("Asset Rename Window");
    }

    private void DrawTopButtons(Action OnClick = null)
    {
        EditorGUILayout.BeginVertical();
        if (GUILayout.Button("Load all PNG/JPG", GUILayout.Height(24)))
        {
            m_filteredResult.Clear();
            m_searchedResult.Clear();
            var assetsPaths = AssetDatabase.GetAllAssetPaths();
            m_filterPaths = assetsPaths.Where(path => !path.StartsWith("Package") && (Path.GetExtension(path) == ".png" || Path.GetExtension(path) == ".jpg")).ToList();
            m_filterPaths.Sort();
            var total = m_filterPaths.Count();
            var progress = 0;
            var index = 0;
            foreach (var path in m_filterPaths)
            {
                var title = $"filter asset: {++progress}/{total}";
                if (index % 30 == 0)
                {
                    EditorUtility.DisplayProgressBar(title, path, progress / (float)total);
                }
                if (!m_searchedResult.ContainsKey(Path.GetFileName(path)))
                {
                    m_searchedResult.Add(Path.GetFileName(path), new List<AssetModel>());
                }
                m_searchedResult[Path.GetFileName(path)].Add(new AssetModel()
                {
                    AssetPath = path
                });
                index++;
            }
            InitFilterResult(m_isFilterSingle);
            InitUiStates();
            EditorUtility.ClearProgressBar();

            OnClick?.Invoke();
        }
        EditorGUILayout.EndVertical();
    }

    private void InitFilterResult(bool filterSingle)
    {
        m_filteredResult.Clear();
        foreach (var pair in m_searchedResult)
        {
            m_filteredResult.Add(pair.Key, pair.Value);
        }
        if (filterSingle)
        {
            for (int i = m_filteredResult.Keys.Count - 1; i >= 0; i--)
            {
                var ele = m_filteredResult.ElementAt(i);
                if (ele.Value.Count <= 1)
                {
                    m_filteredResult.Remove(ele.Key);
                }
            }
        }
    }

    private void InitUiStates()
    {
        m_currentPage = 0;
        m_resultExpandState.Clear();
        m_resultScrollPos.Clear();
        m_names.Clear();
        for (int i = 0; i < m_filteredResult.Keys.Count; i++)
        {
            m_resultExpandState.Add(false);
            m_resultScrollPos.Add(Vector2.zero);
            m_names.Add(Path.GetFileNameWithoutExtension(m_filteredResult.ElementAt(i).Key.ToString()));
        }
    }

    private void DrawTopTools()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        if (m_currentPage != 0)
        {
            if (GUILayout.Button("◀", GUILayout.Width(60)))
            {
                m_currentPage--;
            }
        }
        else
        {
            GUILayout.Space(60);
        }
        var totalpage = Mathf.Ceil((float)m_filteredResult.Keys.Count / (float)ELEMENT_PER_PAGE) - 1;
        EditorGUILayout.LabelField("| " + m_currentPage + "/" + totalpage + " |", GUILayout.Width(60));
        if (m_currentPage < totalpage)
        {
            if (GUILayout.Button("▶", GUILayout.Width(60)))
            {
                m_currentPage++;
            }
        }
        EditorGUILayout.Space();

        m_isFilterSingle = EditorGUILayout.Toggle("Show Duplicate Only", m_isFilterSingle, GUILayout.Width(170), GUILayout.Height(24));
        m_currentSearchedName = EditorGUILayout.TextField(m_currentSearchedName, EditorStyles.textField, GUILayout.MinWidth(30), GUILayout.Height(24));
        if (GUILayout.Button(EditorGUIUtility.IconContent("d_ViewToolZoom"), GUILayout.Width(24), GUILayout.Height(24)))
        {
            InitFilterResult(m_isFilterSingle);
            var matchedResult = m_filteredResult.Where(e => e.Key.ToLower().Contains(m_currentSearchedName.ToLower())).ToList();
            m_filteredResult.Clear();
            foreach (var pair in matchedResult)
            {
                m_filteredResult.Add(pair.Key, pair.Value);
            }
            InitUiStates();
        }

        if (GUILayout.Button(EditorGUIUtility.IconContent("back@2x"), GUILayout.Width(24), GUILayout.Height(24)))
        {
            InitFilterResult(m_isFilterSingle);
            InitUiStates();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void OnGUI()
    {
        m_scrollValue = EditorGUILayout.BeginScrollView(m_scrollValue);
        {
            DrawTopButtons();
            EditorGUILayout.Space();
            DrawTopTools();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical();
            {
                for (int i = m_currentPage * ELEMENT_PER_PAGE; i < m_currentPage * ELEMENT_PER_PAGE + ELEMENT_PER_PAGE; i++)
                {
                    if (i >= m_filteredResult.Keys.Count)
                        break;
                    GUI.backgroundColor = Color.gray;
                    m_resultExpandState[i] = DisplayExpandDetail(i, m_resultExpandState[i]);
                }
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private bool DisplayExpandDetail(int index, bool isUIExpand)
    {
        var expandable = EditorGUILayout.Foldout(isUIExpand, Path.GetFileNameWithoutExtension(m_filteredResult.ElementAt(index).Key) + "[" + m_filteredResult.ElementAt(index).Value.Count + "]", true);
        if (!isUIExpand && expandable)
        {
            m_names[index] = Path.GetFileNameWithoutExtension(m_filteredResult.ElementAt(index).Key);
        }
        if (expandable)
        {
            EditorGUI.indentLevel++;
            m_resultScrollPos[index] = EditorGUILayout.BeginScrollView(m_resultScrollPos[index], EditorStyles.helpBox, GUILayout.Width(EditorGUIUtility.currentViewWidth - 10), GUILayout.Height(130));
            {
                EditorGUILayout.BeginHorizontal();
                {
                    foreach (var model in m_filteredResult.ElementAt(index).Value)
                    {
                        if (model.Texture == null)
                        {
                            model.Texture = AssetDatabase.LoadAssetAtPath<Texture>(model.AssetPath);
                        }
                        EditorGUILayout.ObjectField(model.Texture, typeof(Texture), true, GUILayout.Width(130), GUILayout.Height(130));
                        model.IsToggleOn = EditorGUILayout.Toggle("", model.IsToggleOn, GUILayout.Width(30), GUILayout.Height(30));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUI.indentLevel--;
            if (m_filteredResult.ElementAt(index).Value.Any(e => e.IsToggleOn))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    EditorGUILayout.LabelField("Rename :", EditorStyles.largeLabel, GUILayout.Width(112));
                    m_names[index] = EditorGUILayout.TextField(m_names[index], GUILayout.Width(150));
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    if (GUILayout.Button("SAVE", GUILayout.Height(24)))
                    {
                        var extStr = Path.GetExtension(m_filteredResult.ElementAt(index).Key);
                        var models = m_filteredResult.ElementAt(index).Value.Where(e => e.IsToggleOn).ToList();
                        m_filteredResult.ElementAt(index).Value.RemoveAll(e => e.IsToggleOn);
                        var fileNameWithExtension = m_names[index] + extStr;
                        foreach (var model in models)
                        {
                            AssetDatabase.RenameAsset(model.AssetPath, m_names[index]);
                            if (!m_filteredResult.ContainsKey((fileNameWithExtension)))
                            {
                                m_filteredResult.Add(fileNameWithExtension, new List<AssetModel>());
                                m_resultExpandState.Add(false);
                                m_resultScrollPos.Add(Vector2.zero);
                                m_names.Add(m_names[index]);
                            }
                            m_filteredResult[fileNameWithExtension].Add(new AssetModel()
                            {
                                AssetPath = Path.GetDirectoryName(model.AssetPath) + fileNameWithExtension,
                            });
                        }
                        AssetDatabase.SaveAssets();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }
        return expandable;
    }
}