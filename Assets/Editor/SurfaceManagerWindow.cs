#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class SurfaceManagerWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private List<SurfaceTypeSO> surfaceTypes = new List<SurfaceTypeSO>();
    private SurfaceTypeSO selectedSurface;
    private Editor cachedEditor;
    private string newSurfaceName = "Glace"; // Valeur par défaut

    // Chemin par défaut basé sur ton arborescence (Assets/GO/Surfaces)
    private const string defaultSurfaceFolderPath = "Assets/GO/Surfaces";

    [MenuItem("Tools/Climbing/Surface Manager")]
    public static void ShowWindow()
    {
        GetWindow<SurfaceManagerWindow>("Gestionnaire de Surfaces");
    }

    private void OnEnable()
    {
        LoadSurfaces();
    }

    private void LoadSurfaces()
    {
        surfaceTypes.Clear();
        string[] guids = AssetDatabase.FindAssets("t:SurfaceTypeSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SurfaceTypeSO surface = AssetDatabase.LoadAssetAtPath<SurfaceTypeSO>(path);
            if (surface != null)
            {
                surfaceTypes.Add(surface);
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        GUILayout.Label("Gestionnaire des Types de Surfaces", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Crée et gère tes surfaces rapidement. Les fichiers sont automatiquement rangés et nommés sans fenêtres supplémentaires.", MessageType.Info);

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Rafraîchir la liste"))
        {
            LoadSurfaces();
        }

        EditorGUILayout.Space(10);

        // Section : Création rapide en 1 clic
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Créer un nouveau type de surface", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        newSurfaceName = EditorGUILayout.TextField("Nom de la surface", newSurfaceName);
        if (GUILayout.Button("Créer", GUILayout.Width(100)))
        {
            CreateNewSurface();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Disposition en deux colonnes
        EditorGUILayout.BeginHorizontal();

        // Colonne de gauche : Liste
        EditorGUILayout.BeginVertical(GUILayout.Width(250));
        GUILayout.Label($"Surfaces existantes ({surfaceTypes.Count})", EditorStyles.boldLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, "box", GUILayout.ExpandHeight(true));
        foreach (SurfaceTypeSO surface in surfaceTypes)
        {
            if (surface == null) continue;

            GUI.backgroundColor = (selectedSurface == surface) ? Color.cyan : Color.white;
            
            // Affiche le nom propre de la surface dans le bouton
            string displayName = string.IsNullOrEmpty(surface.surfaceName) ? surface.name : surface.surfaceName;
            if (GUILayout.Button(displayName, "Button", GUILayout.Height(30)))
            {
                selectedSurface = surface;
                GUI.changed = true;
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // Colonne de droite : Propriétés
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));
        GUILayout.Label("Propriétés de la surface sélectionnée", EditorStyles.boldLabel);

        if (selectedSurface != null)
        {
            if (cachedEditor == null || cachedEditor.target != selectedSurface)
            {
                cachedEditor = Editor.CreateEditor(selectedSurface);
            }

            EditorGUILayout.Space(5);
            cachedEditor.OnInspectorGUI();

            EditorGUILayout.Space(15);
            if (GUILayout.Button("Localiser dans le dossier Project"))
            {
                Selection.activeObject = selectedSurface;
                EditorGUIUtility.PingObject(selectedSurface);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Sélectionnez une surface à gauche pour modifier ses valeurs.", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void CreateNewSurface()
    {
        if (string.IsNullOrEmpty(newSurfaceName)) return;

        // S'assure que le dossier de destination existe (le crée si besoin)
        EnsureFolderExists(defaultSurfaceFolderPath);

        // Crée l'instance du ScriptableObject
        SurfaceTypeSO newSurface = ScriptableObject.CreateInstance<SurfaceTypeSO>();
        newSurface.surfaceName = newSurfaceName;

        // Génère automatiquement un nom de fichier propre (ex: Surface_Glace.asset)
        string assetName = $"Surface_{newSurfaceName}.asset";
        string path = Path.Combine(defaultSurfaceFolderPath, assetName);
        
        // Empêche l'écrasement s'il existe déjà un fichier du même nom en lui ajoutant un numéro auto
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        // Crée l'asset dans Unity
        AssetDatabase.CreateAsset(newSurface, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        LoadSurfaces();
        selectedSurface = newSurface;
    }

    private void EnsureFolderExists(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] folders = path.Split('/');
        string currentPath = folders[0]; // "Assets"

        for (int i = 1; i < folders.Length; i++)
        {
            string nextPath = currentPath + "/" + folders[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, folders[i]);
            }
            currentPath = nextPath;
        }
    }
}
#endif