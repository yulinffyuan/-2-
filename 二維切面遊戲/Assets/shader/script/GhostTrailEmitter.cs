using System.Collections;
using UnityEngine;

public class GhostTrailEmitter : MonoBehaviour
{
    [Header("殘影外觀")]
    public Material ghostMaterial;
    public Color ghostColor = new Color(0.2f, 0.9f, 1f, 0.6f);
    public float ghostLifetime = 0.35f;
    public float spawnInterval = 0.04f; // 生成殘影的間隔時間

    private MeshFilter playerMeshFilter;
    private Coroutine trailRoutine;

    void Start()
    {
        playerMeshFilter = GetComponent<MeshFilter>();
    }

    public void StartTrail(float totalDashDuration)
    {
        if (trailRoutine != null) StopCoroutine(trailRoutine);
        trailRoutine = StartCoroutine(EmitRoutine(totalDashDuration));
    }

    IEnumerator EmitRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            SpawnSingleGhost();
            elapsed += spawnInterval;
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnSingleGhost()
    {
        GameObject ghost = new GameObject("DashGhost");
        ghost.transform.position = transform.position;
        ghost.transform.rotation = transform.rotation;
        ghost.transform.localScale = transform.localScale;

        // 若角色使用 3D Mesh (如膠囊體)
        if (playerMeshFilter != null)
        {
            MeshFilter mf = ghost.AddComponent<MeshFilter>();
            mf.sharedMesh = playerMeshFilter.sharedMesh;
            MeshRenderer mr = ghost.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ghostMaterial;
        }
        // 若角色使用 2D SpriteRenderer
        else if (TryGetComponent<SpriteRenderer>(out var sr))
        {
            SpriteRenderer gsr = ghost.AddComponent<SpriteRenderer>();
            gsr.sprite = sr.sprite;
            gsr.sharedMaterial = ghostMaterial;
        }

        GhostSpriteFade fadeComp = ghost.AddComponent<GhostSpriteFade>();
        fadeComp.Init(ghostColor, ghostLifetime);
    }
}