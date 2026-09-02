using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================================
//  BGM 매니저 — 씬 편집/인스펙터 연결 없이 게임 실행 시 자동 생성된다.
//  씬 이름에 맞는 곡을 Resources/Audio 에서 골라 루프 재생하며,
//  씬 전환 사이에도 살아남아(DontDestroyOnLoad) 곡을 부드럽게 바꾼다.
//
//  사용법: Resources/Audio 폴더에 아래 이름으로 음원만 넣으면 끝.
//    - bgm_title    → start 씬(시작화면)
//    - bgm_ingame   → SampleScene(인게임)
//    - bgm_clear    → end 씬 — 클리어(승리)
//    - bgm_gameover → end 씬 — 게임오버(패배)
//    - bgm_levelup  → 레벨업 시 메인 브금 위에 짧게 겹쳐 재생되는 스팅어
//    - bgm_uiclick  → UI 버튼 클릭 시 재생되는 효과음(UiClickSfx 컴포넌트가 호출)
//  파일이 없으면 해당 씬/이벤트에서는 조용히 무음 처리(에러 없음).
//
//  볼륨: AudioSource.volume = BalanceConfig.bgmVolume.
//        마스터 볼륨(옵션 슬라이더)은 AudioListener.volume이 곱하므로 자동 반영.
// =========================================================================
public class BgmManager : MonoBehaviour
{
    private static readonly Dictionary<string, string> SceneToClip = new()
    {
        { "start",       "Audio/bgm_title"  },
        { "SampleScene", "Audio/bgm_ingame" },
    };

    private const string EndSceneName       = "end";
    private const string ClipPathClear      = "Audio/bgm_clear";
    private const string ClipPathGameOver   = "Audio/bgm_gameover";
    private const string ClipPathLevelUp    = "Audio/bgm_levelup";
    private const string ClipPathUiClick    = "Audio/bgm_uiclick";
    private const float  DuckVolumeScale    = 0.35f;
    private const float  UiClickVolumeScale = 0.5f; 

    private static string ResolveClipPath(string sceneName)
    {
        if (sceneName == EndSceneName)
            return GameResult.Win ? ClipPathClear : ClipPathGameOver;
        return SceneToClip.TryGetValue(sceneName, out var path) ? path : null;
    }

    private static BgmManager _instance;

    private AudioSource _source;
    private AudioSource _stingerSource;
    private readonly Dictionary<string, AudioClip> _clipCache = new();
    private AudioClip _currentClip;
    private Coroutine _fade;
    private Coroutine _duck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("BgmManager");
        _instance = go.AddComponent<BgmManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        _source = gameObject.AddComponent<AudioSource>();
        _source.loop = true;
        _source.playOnAwake = false;

        _stingerSource = gameObject.AddComponent<AudioSource>();
        _stingerSource.loop = false;
        _stingerSource.playOnAwake = false;

        SceneManager.sceneLoaded += OnSceneLoaded;
        GameEvents.OnAbilityTrigger += PlayLevelUpStinger;
    }

    private void Start()
    {
        ApplyForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameEvents.OnAbilityTrigger -= PlayLevelUpStinger;
        if (_instance == this) _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForScene(scene.name);
    }

    private void ApplyForScene(string sceneName)
    {
        string path = sceneName != null ? ResolveClipPath(sceneName) : null;
        AudioClip clip = path != null ? LoadClip(path) : null;
        bool loop = sceneName != EndSceneName;

        if (clip == _currentClip && (_source.isPlaying || clip == null)) return;

        _currentClip = clip;

        float targetVolume = clip != null ? GetBgmVolume() : 0f;
        float fade = GetFadeSeconds();

        if (_fade != null) StopCoroutine(_fade);

        if (fade <= 0f || !isActiveAndEnabled)
        {
            _source.Stop();
            _source.clip = clip;
            _source.loop = loop;
            _source.volume = targetVolume;
            if (clip != null) _source.Play();
        }
        else
        {
            _fade = StartCoroutine(CrossFade(clip, targetVolume, fade, loop));
        }
    }

    private IEnumerator CrossFade(AudioClip next, float targetVolume, float fade, bool loop)
    {
        float startVolume = _source.volume;
        for (float t = 0f; t < fade && _source.isPlaying; t += Time.unscaledDeltaTime)
        {
            _source.volume = Mathf.Lerp(startVolume, 0f, t / fade);
            yield return null;
        }

        _source.Stop();
        _source.clip = next;
        _source.loop = loop;
        _source.volume = 0f;

        if (next == null) { _fade = null; yield break; }

        _source.Play();
        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            _source.volume = Mathf.Lerp(0f, targetVolume, t / fade);
            yield return null;
        }
        _source.volume = targetVolume;
        _fade = null;
    }

    private void PlayLevelUpStinger()
    {
        AudioClip clip = LoadClip(ClipPathLevelUp);
        if (clip == null) return;

        _stingerSource.volume = GetBgmVolume();
        _stingerSource.PlayOneShot(clip);

        if (_duck != null) StopCoroutine(_duck);
        _duck = StartCoroutine(DuckMainBgm(clip.length));
    }

    private IEnumerator DuckMainBgm(float duckDuration)
    {
        if (_source.clip == null) yield break;

        float original = GetBgmVolume();
        float ducked    = original * DuckVolumeScale;
        float fade       = Mathf.Min(GetFadeSeconds(), duckDuration * 0.5f);

        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            _source.volume = Mathf.Lerp(original, ducked, t / fade);
            yield return null;
        }
        _source.volume = ducked;

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, duckDuration - fade * 2f));

        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            _source.volume = Mathf.Lerp(ducked, original, t / fade);
            yield return null;
        }
        _source.volume = original;
        _duck = null;
    }

    public static void PlayUiClick()
    {
        if (_instance != null) _instance.PlayUiClickInternal();
    }

    private void PlayUiClickInternal()
    {
        AudioClip clip = LoadClip(ClipPathUiClick);
        if (clip == null) return;
        _stingerSource.PlayOneShot(clip, GetBgmVolume() * UiClickVolumeScale);
    }

    private AudioClip LoadClip(string path)
    {
        if (_clipCache.TryGetValue(path, out var cached)) return cached;
        var clip = Resources.Load<AudioClip>(path);
        _clipCache[path] = clip;
        return clip;
    }

    private static float GetBgmVolume()
    {
        var c = BalanceConfig.Resolve(null);
        return c != null ? Mathf.Clamp01(c.bgmVolume) : 0.6f;
    }

    private static float GetFadeSeconds()
    {
        var c = BalanceConfig.Resolve(null);
        return c != null ? Mathf.Max(0f, c.bgmFadeSeconds) : 0.5f;
    }
}