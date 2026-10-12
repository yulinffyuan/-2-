using UnityEngine;

public enum SpeakerSide
{
    NPC_Left,    // NPC 位於左側
    Player_Right // 玩家位於右側
}

[System.Serializable]
public struct DialogueLine
{
    public string speakerName;        // 說話者名字
    public SpeakerSide speakerSide;   // 說話者方位 (左側 NPC / 右側玩家)
    public Sprite avatarSprite;       // 角色頭像 Sprite
    [TextArea(2, 5)]
    public string content;            // 對話文字內容
}