using UnityEngine;

/// <summary>사격 외 전투 효과음.</summary>
public enum NCSfxKind
{
    Hit,      // 내 탄환이 맞음
    Kill,     // 내 탄환으로 처치
    Hurt,     // 내가 맞음
    Death,    // 내가 사망
    GhostHit, // 다른 사람이 유령을 맞힘
}

/// <summary>
/// 전투 효과음(순수 연출). 탄환 발사 RPC를 받은 각 화면이 재생한다.
/// 카메라가 캐릭터에서 멀리 떨어져 있어 3D 음향 대신 "내 캐릭터와의 거리"로 음량과 좌우를 정한다.
/// </summary>
public static class NCSfx
{
    private const float ShotVolume = 0.45f;
    private const float OverchargeVolume = 0.45f;
    private const float FullDistance = 3f;    // 이 거리까지는 최대 음량
    private const float HearDistance = 22f;   // 이 거리부터 들리지 않음
    private const float PanDistance = 14f;    // 좌우로 이만큼 떨어지면 한쪽으로 최대
    private const float MaxPan = 0.7f;
    private const float PitchJitter = 0.05f;
    private const int VoiceCount = 12;

    private static readonly float[] KindVolumes = { 0.35f, 0.55f, 0.55f, 0.6f, 0.25f };

    private static AudioClip[] _shots;
    private static AudioClip[] _kinds;
    private static AudioClip[] _overcharged;
    private static AudioSource[] _voices;
    private static int _nextVoice;
    private static int _lastClip = -1;

    /// <summary>첫 발에 끊기지 않게 미리 불러온다.</summary>
    public static void Preload()
    {
        _shots ??= Load("shot_1", "shot_2", "shot_3");
        _overcharged ??= Load("shot_over_1", "shot_over_2");

        _kinds ??= Load("hit", "kill", "hurt", "death", "ghost_hit");

        // 씬을 다시 부르면 이전 AudioSource가 파괴된다.
        if (_voices != null && _voices[0] != null)
        {
            return;
        }

        GameObject host = new GameObject("NC_Sfx");
        _voices = new AudioSource[VoiceCount];

        for (int i = 0; i < VoiceCount; i++)
        {
            _voices[i] = host.AddComponent<AudioSource>();
            _voices[i].playOnAwake = false;
            _voices[i].spatialBlend = 0f;
        }
    }

    public static void Shot(Vector3 position, bool overcharged)
    {
        Preload();

        AudioClip[] clips = overcharged ? _overcharged : _shots;
        if (clips.Length == 0)
        {
            return;
        }

        Vector3 offset = NCPlayer.Local != null ? NCMath.Flat(position - NCPlayer.Local.transform.position) : Vector3.zero;
        float volume = VolumeAt(offset.magnitude) * (overcharged ? OverchargeVolume : ShotVolume);
        if (volume <= 0.001f)
        {
            return;
        }

        // 같은 소리가 연달아 나지 않게 고른다.
        int index = Random.Range(0, clips.Length);
        if (index == _lastClip)
        {
            index = (index + 1) % clips.Length;
        }

        _lastClip = index;

        PlayClip(clips[index], volume, PanAt(offset.x));
    }

    /// <summary>내 화면에서 일어난 일(거리와 무관).</summary>
    public static void Play(NCSfxKind kind)
    {
        Preload();
        PlayKind(kind, 1f, 0f);
    }

    /// <summary>떨어진 곳에서 일어난 일.</summary>
    public static void PlayAt(NCSfxKind kind, Vector3 position)
    {
        Preload();
        Vector3 offset = NCPlayer.Local != null ? NCMath.Flat(position - NCPlayer.Local.transform.position) : Vector3.zero;
        PlayKind(kind, VolumeAt(offset.magnitude), PanAt(offset.x));
    }

    private static void PlayKind(NCSfxKind kind, float scale, float pan)
    {
        int index = (int)kind;
        if (index < _kinds.Length && scale > 0.001f)
        {
            PlayClip(_kinds[index], KindVolumes[index] * scale, pan);
        }
    }

    private static void PlayClip(AudioClip clip, float volume, float pan)
    {
        AudioSource voice = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % VoiceCount;
        voice.clip = clip;
        voice.volume = volume;
        voice.panStereo = pan;
        voice.pitch = 1f + Random.Range(-PitchJitter, PitchJitter);
        voice.Play();
    }

    /// <summary>거리에 따른 음량 비율(0~1).</summary>
    public static float VolumeAt(float distance)
    {
        float t = Mathf.InverseLerp(HearDistance, FullDistance, distance);
        return t * t;
    }

    /// <summary>화면 좌우 차이에 따른 방향(-는 왼쪽).</summary>
    public static float PanAt(float x)
    {
        return Mathf.Clamp(x / PanDistance, -1f, 1f) * MaxPan;
    }

    private static AudioClip[] Load(params string[] names)
    {
        var clips = new System.Collections.Generic.List<AudioClip>();

        foreach (string name in names)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null)
            {
                Debug.LogWarning($"[NCSfx] Resources/Audio/{name} 을 찾을 수 없습니다.");
                continue;
            }

            clip.LoadAudioData();
            clips.Add(clip);
        }

        return clips.ToArray();
    }
}
