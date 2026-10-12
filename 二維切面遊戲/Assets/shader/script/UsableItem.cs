using UnityEngine;

public abstract class UsableItem : MonoBehaviour
{
    [Header("道具資訊")]
    public string itemName = "Usable Item";
    public Sprite itemIcon;
    public float cooldown = 3f;

    protected float lastUseTime = -999f;
    protected RotatableSlicePlayer player;

    public virtual void Init(RotatableSlicePlayer playerRef)
    {
        player = playerRef;
    }

    public bool IsReady => Time.time >= lastUseTime + cooldown;
    public float CooldownRemaining => Mathf.Max(0f, (lastUseTime + cooldown) - Time.time);

    // 長按開始瞄準
    public abstract void OnStartAim();
    // 瞄準更新
    public abstract void OnUpdateAim();
    // 釋放使用
    public abstract void OnReleaseUse();
    // 取消
    public abstract void OnCancel();
}