using UnityEngine;

public enum DocumentType
{
    SinglePage,
    Diary
}

[CreateAssetMenu(fileName = "New Document", menuName = "Game/Document")]
public class DocumentData : ScriptableObject
{
    public string documentName;
    [TextArea(4,20)] public string text;
    public DocumentType documentType;

    public Sprite[] pages;
}