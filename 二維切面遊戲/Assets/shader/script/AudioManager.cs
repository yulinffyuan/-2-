using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("預設音效剪輯 (AudioClips)")]
    public AudioClip jumpSFX;
    public AudioClip dashSFX;
    public AudioClip slashSFX;
    public AudioClip crateHitSFX;
    public AudioClip crateBreakSFX;
    public AudioClip sliceRotateSFX;

    private AudioSource sfxSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 動態配置 AudioSource 元件
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    // 播放 2D 音效（動作、UI、技能，音量均勻）
    public void PlaySFX(AudioClip clip, float volume = 1f, bool randomizePitch = true)
    {
        if (clip == null) return;

        if (randomizePitch)
        {
            sfxSource.pitch = Random.Range(0.92f, 1.08f); // 輕微隨機音高，增加打擊動態感
        }
        else
        {
            sfxSource.pitch = 1f;
        }

        sfxSource.PlayOneShot(clip, volume);
    }

    // 播放 3D 空間音效（例如地圖深處箱子破裂，隨距離衰減）
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}