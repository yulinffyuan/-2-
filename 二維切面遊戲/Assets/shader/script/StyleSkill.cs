using UnityEngine;

public abstract class StyleSkill : MonoBehaviour
{
    [Header("技能基礎資訊")]
    public string skillName = "Skill";
    public KeyCode triggerKey = KeyCode.Mouse1; // 預設滑鼠右鍵
    public float cooldown = 2.5f;

    protected float lastUseTime = -999f;
    protected RotatableSlicePlayer player;

    public virtual void Init(RotatableSlicePlayer playerRef)
    {
        player = playerRef;
    }

    public bool IsReady => Time.time >= lastUseTime + cooldown;
    public float CooldownRemaining => Mathf.Max(0f, (lastUseTime + cooldown) - Time.time);

    public abstract bool TryExecute();
}