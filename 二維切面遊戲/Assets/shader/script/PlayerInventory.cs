using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("目前裝備的快捷道具 (R 鍵)")]
    public UsableItem equippedItem;

    [Header("快捷鍵設定")]
    public KeyCode useItemKey = KeyCode.R;

    private bool isAiming = false;
    private RotatableSlicePlayer player;

    void Start()
    {
        player = GetComponent<RotatableSlicePlayer>();
        if (equippedItem != null && player != null)
        {
            equippedItem.Init(player);
        }
    }

    void Update()
    {
        if (player == null || player.IsDead || equippedItem == null) return;

        // 當小地圖開啟時，強制取消當前瞄準並鎖死所有道具輸入
        if (MapViewerController.Instance != null && MapViewerController.Instance.IsMapOpen)
        {
            if (isAiming)
            {
                isAiming = false;
                equippedItem.OnCancel();
            }
            return;
        }

        // 1. 按下 R 鍵開始瞄準
        if (Input.GetKeyDown(useItemKey) && equippedItem.IsReady)
        {
            isAiming = true;
            equippedItem.OnStartAim();
        }

        // 2. 長按 R 鍵更新瞄準
        if (isAiming)
        {
            equippedItem.OnUpdateAim();
        }

        // 3. 鬆開 R 鍵確認釋放
        if (Input.GetKeyUp(useItemKey) && isAiming)
        {
            isAiming = false;
            equippedItem.OnReleaseUse();
        }

        // 4. 按下滑鼠右鍵或 Esc 取消使用
        if (isAiming && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
        {
            isAiming = false;
            equippedItem.OnCancel();
        }
    }

    public void EquipItem(UsableItem newItem)
    {
        if (equippedItem != null)
        {
            equippedItem.OnCancel();
        }

        equippedItem = newItem;
        if (player != null)
        {
            equippedItem.Init(player);
        }
        Debug.Log($"[道具系統] 已成功裝備: {newItem.itemName}");
    }
}