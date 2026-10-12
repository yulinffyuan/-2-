using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogueSequence", menuName = "Dialogue/Dialogue Sequence", order = 1)]
public class DialogueSequence : ScriptableObject
{
    [Header("對話句列表")]
    public DialogueLine[] lines;
}