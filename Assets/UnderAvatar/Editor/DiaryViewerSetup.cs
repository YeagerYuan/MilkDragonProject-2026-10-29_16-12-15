#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DiaryViewerSetup
{
    [MenuItem("Tools/UnderAvatar/Set Up Page Curl Diary")]
    public static void SetUp()
    {
        if (Application.isPlaying) return;
        var manager = Object.FindFirstObjectByType<DocumentManager>(FindObjectsInactive.Include);
        if (manager == null) { Debug.LogError("Open the document viewer scene first."); return; }
        var serialized = new SerializedObject(manager);
        var panel = serialized.FindProperty("documentPanel").objectReferenceValue as GameObject;
        if (panel == null) { Debug.LogError("Assign Document Panel first.", manager); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Book-Page Curl/Example/prefabs/Book.prefab");
        if (prefab == null) { Debug.LogError("The installed Book-Page Curl Book prefab is missing."); return; }
        if (serialized.FindProperty("diaryBookTemplate").objectReferenceValue != null)
        { Debug.Log("Page-curl template is already assigned.", manager); return; }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set up page-curl diary");
        var old = panel.transform.Find("DiaryNavigation");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel.transform);
        Undo.RegisterCreatedObjectUndo(instance, "Create diary book template");
        instance.name = "DiaryBookTemplate";
        var book = instance.GetComponent<Book>();
        book.canvas = panel.GetComponentInParent<Canvas>();
        book.interactable = true;
        var rect = (RectTransform)instance.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        foreach (var flip in instance.GetComponentsInChildren<AutoFlip>(true))
            Undo.DestroyObjectImmediate(flip);
        instance.SetActive(false);
        Undo.RecordObject(manager, "Assign diary book template");
        serialized.FindProperty("diaryBookTemplate").objectReferenceValue = book;
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(book);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeObject = manager;
        Debug.Log("Page-curl diary assigned; previous button row removed. Save the scene. Keep one EventSystem with InputSystemUIInputModule and default UI actions.", manager);
    }
}
#endif
