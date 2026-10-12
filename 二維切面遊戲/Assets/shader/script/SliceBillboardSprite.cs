using UnityEngine;

public class SliceBillboardSprite : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        // 1. 永遠面向 2D 切面正交相機（杜絕切面旋轉時 Sprite 變成紙片側面）
        if (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
        {
            transform.rotation = TransformSliceManager.Instance.mainOrthoCamera.transform.rotation;
        }

        // 2. 死亡時自動隱藏
        // （由 Player 死亡邏輯統一調控）
    }

    // 供主角控制腳本呼叫的翻轉方法
    public void SetFacing(float direction)
    {
        if (spriteRenderer == null) return;

        // direction > 0 面向右，< 0 面向左
        if (direction > 0)
        {
            spriteRenderer.flipX = false;
        }
        else if (direction < 0)
        {
            spriteRenderer.flipX = true;
        }
    }

    public void SetVisible(bool isVisible)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = isVisible;
        }
    }
}