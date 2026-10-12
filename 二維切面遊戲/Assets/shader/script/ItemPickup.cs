using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ItemPickup : MonoBehaviour
{
    [Header("道具設定")]
    [Tooltip("可直接掛載在此物件上的 UsableItem 組件")]
    public UsableItem itemComponent;

    public float floatSpeed = 2f;
    public float floatHeight = 0.15f;

    private Vector3 startPos;
    private bool hasPickedUp = false;

    void Start()
    {
        startPos = transform.position;
        GetComponent<Collider>().isTrigger = true;

        if (itemComponent == null)
        {
            itemComponent = GetComponent<UsableItem>();
        }
    }

    void Update()
    {
        // 拾取物上下浮動效果
        transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * floatSpeed) * floatHeight);
        transform.Rotate(Vector3.up, 60f * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasPickedUp) return;

        RotatableSlicePlayer player = other.GetComponent<RotatableSlicePlayer>();
        if (player == null && other.CompareTag("Player"))
        {
            player = other.GetComponentInParent<RotatableSlicePlayer>();
        }

        if (player != null)
        {
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = player.gameObject.AddComponent<PlayerInventory>();
            }

            if (itemComponent != null)
            {
                hasPickedUp = true;

                // 建立一個掛在 Player 身下的道具承載體，避免跟隨拾取外殼被 Destroy
                GameObject itemHolder = new GameObject(itemComponent.itemName);
                itemHolder.transform.SetParent(player.transform);
                itemHolder.transform.localPosition = Vector3.zero;

                // 將對應的道具組件動態掛載至新物件並複製基礎屬性
                if (itemComponent is BlinkItem sourceBlink)
                {
                    BlinkItem newBlink = itemHolder.AddComponent<BlinkItem>();
                    newBlink.maxBlinkDistance = sourceBlink.maxBlinkDistance;
                    newBlink.slowMotionScale = sourceBlink.slowMotionScale;
                    newBlink.obstacleLayer = sourceBlink.obstacleLayer;
                    newBlink.vfxColor = sourceBlink.vfxColor;
                    newBlink.aimSFX = sourceBlink.aimSFX;
                    newBlink.blinkSFX = sourceBlink.blinkSFX;
                    newBlink.cooldown = sourceBlink.cooldown;

                    inventory.EquipItem(newBlink);
                }

                Destroy(gameObject);
            }
        }
    }
}