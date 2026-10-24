#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class SurfaceManagerWindow : EditorWindow
{
    private Vector2 globalScrollPosition;
    private Vector2 scrollPosition;
    private List<SurfaceTypeSO> surfaceTypes = new List<SurfaceTypeSO>();
    private SurfaceTypeSO selectedSurface;
    private Editor cachedEditor;
    private string newSurfaceName = "Glace";

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
        globalScrollPosition = EditorGUILayout.BeginScrollView(globalScrollPosition);

        EditorGUILayout.Space(10);
        GUILayout.Label("Gestionnaire des Types de Surfaces", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Crée, gère et supprime tes surfaces rapidement depuis cet outil centralisé.", MessageType.Info);

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
        EditorGUILayout.BeginVertical(GUILayout.Width(250), GUILayout.MinHeight(200));
        GUILayout.Label($"Surfaces existantes ({surfaceTypes.Count})", EditorStyles.boldLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, "box", GUILayout.ExpandHeight(true));
        foreach (SurfaceTypeSO surface in surfaceTypes)
        {
            if (surface == null) continue;

            GUI.backgroundColor = (selectedSurface == surface) ? Color.cyan : Color.white;
            
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

        // Colonne de droite : Propriétés et Aperçu
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

            // --- SECTION APERÇU DU MATÉRIAU ---
            if (selectedSurface.surfaceMaterial != null)
            {
                EditorGUILayout.Space(10);
                GUILayout.Label("Aperçu du Matériau", EditorStyles.boldLabel);

                // Génère une texture d'aperçu de l'asset (matériau)
                Texture2D previewTex = AssetPreview.GetAssetPreview(selectedSurface.surfaceMaterial);
                if (previewTex != null)
                {
                    // Affiche l'aperçu dans un carré de 120x120 pixels centré
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(previewTex, GUILayout.Width(120), GUILayout.Height(120));
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                else
                {
                    // Pendant que Unity génère l'aperçu en arrière-plan
                    EditorGUILayout.HelpBox("Génération de l'aperçu en cours...", MessageType.Info);
                    Repaint(); // Force l'UI à se rafraîchir pour afficher l'image dès qu'elle est prête
                }
            }
            // -----------------------------------

            EditorGUILayout.Space(15);

            if (GUILayout.Button("Localiser dans le dossier Project"))
            {
                Selection.activeObject = selectedSurface;
                EditorGUIUtility.PingObject(selectedSurface);
            }

            EditorGUILayout.Space(5);

            // Bouton de suppression sécurisé
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("Supprimer cette surface", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Confirmer la suppression",
                    $"Voulez-vous vraiment supprimer définitivement la surface '{selectedSurface.surfaceName}' ?",
                    "Oui, supprimer",
                    "Annuler"))
                {
                    DeleteSelectedSurface();
                }
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            EditorGUILayout.HelpBox("Sélectionnez une surface à gauche pour modifier ses valeurs ou voir son aperçu.", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);
        EditorGUILayout.EndScrollView();
    }

    private void CreateNewSurface()
    {
        if (string.IsNullOrEmpty(newSurfaceName)) return;

        EnsureFolderExists(defaultSurfaceFolderPath);

        SurfaceTypeSO newSurface = ScriptableObject.CreateInstance<SurfaceTypeSO>();
        newSurface.surfaceName = newSurfaceName;

        string assetName = $"Surface_{newSurfaceName}.asset";
        string path = Path.Combine(defaultSurfaceFolderPath, assetName);
        
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        AssetDatabase.CreateAsset(newSurface, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        LoadSurfaces();
        selectedSurface = newSurface;
    }

    private void DeleteSelectedSurface()
    {
        if (selectedSurface == null) return;

        string path = AssetDatabase.GetAssetPath(selectedSurface);
        
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        selectedSurface = null;
        cachedEditor = null;
        LoadSurfaces();
    }

    private void EnsureFolderExists(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] folders = path.Split('/');
        string currentPath = folders[0];

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