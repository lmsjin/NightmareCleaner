using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대기실 카드 한 장의 캐릭터 미리보기. NC_Player의 "Body"만 복제해 맵에서 먼 곳(지하)에 세우고,
/// 전용 카메라로 찍어 카드 가운데 RawImage에 띄운다. 순수 연출(네트워크 상태 아님).
///  - 참가 대기: 검은 실루엣 / 준비 중: 슬롯색으로 천천히 회전 / 준비 완료: 정면 + 한 번 점프 + 발밑 녹색 빛
/// 대기실이 보일 때만 켜진다(SetActive).
/// </summary>
public class NCLobbyPreview
{
    // 연출 상수(판정과 무관)
    private const int TextureSize = 256;
    private const float ImageSize = 270f;
    private const float ImageY = 22f;
    private const float StageDepth = -200f;   // 맵 아래(메인 카메라에 안 보이는 곳)
    private const float StageSpacing = 40f;   // 슬롯끼리 x 간격(서로의 카메라에 안 찍히게)
    private const float TurnSpeed = 45f;      // 준비 중 회전(도/초)
    private const float FaceSpeed = 360f;     // 준비 완료 때 정면으로 돌아서는 속도(도/초)
    private const float HopSeconds = 0.35f;
    private const float HopHeight = 0.35f;
    private const float GlowSize = 1.15f;
    private const float GlowAlpha = 0.3f;     // 숨쉬는 빛의 기본 투명도(±0.15)
    private static readonly Color Silhouette = new Color(0.05f, 0.05f, 0.07f, 1f);

    private readonly GameObject _stage;
    private readonly Transform _model;
    private readonly Renderer[] _renderers;
    private readonly Color[] _colors;   // 렌더러별 원래 색(몸통은 슬롯색)
    private readonly Renderer _glow;
    private readonly Camera _camera;
    private readonly RawImage _image;

    private int _state = -1;            // 0 참가 대기 · 1 준비 중 · 2 준비 완료
    private float _yaw;
    private float _hopAge = HopSeconds;

    public NCLobbyPreview(int slot, RectTransform card, Transform deco)
    {
        _stage = new GameObject($"NC_LobbyPreview_{slot + 1}P");
        _stage.transform.position = new Vector3(slot * StageSpacing, StageDepth, 0f);

        // 캐릭터 모양: 플레이어 프리팹의 Body 부분만 복제(NetworkObject·콜라이더 없음)
        NetworkObject prefab = Resources.Load<NetworkObject>("NC_Player");
        Transform body = prefab != null ? prefab.transform.Find("Body") : null;
        _model = body != null
            ? Object.Instantiate(body.gameObject, _stage.transform, false).transform
            : new GameObject("Body").transform;
        _model.SetParent(_stage.transform, false);
        _model.localPosition = Vector3.zero;

        _renderers = _model.GetComponentsInChildren<Renderer>();
        _colors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material material = r.material; // 미리보기 전용 복제본
            _colors[i] = r.name == "Torso" ? NCStyle.SlotColor(slot) : material.color;
        }

        // 발밑 녹색 빛(준비 완료)
        _glow = NCFx.NewShape(Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"), "ReadyGlow", _stage.transform);
        _glow.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        _glow.transform.localScale = new Vector3(GlowSize, 0.005f, GlowSize);
        _glow.enabled = false;

        // 전용 조명: 지하라 장면 조명을 제대로 못 받는다. 맵과 멀어 게임 화면에는 영향 없음
        Light light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(_stage.transform, false);
        light.transform.localPosition = new Vector3(1.2f, 2.4f, 2.6f);
        light.type = LightType.Point;
        light.range = 8f;
        light.intensity = 6f;
        light.shadows = LightShadows.None;

        // 전용 카메라: 정면 약간 위에서 발밑까지 보이게, 배경 투명
        _camera = new GameObject("Camera").AddComponent<Camera>();
        _camera.transform.SetParent(_stage.transform, false);
        _camera.transform.localPosition = new Vector3(0f, 1.7f, 4.4f);
        _camera.transform.LookAt(_stage.transform.position + Vector3.up * 0.7f);
        _camera.fieldOfView = 30f;
        _camera.nearClipPlane = 0.1f;
        _camera.farClipPlane = 14f;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _camera.allowHDR = false;
        _camera.allowMSAA = false;
        _camera.targetTexture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32) { name = $"NC_LobbyPreview_{slot + 1}P" };

        // 카드 가운데(장식 위, 상태 칸 아래)
        GameObject imageObject = new GameObject("CharacterPreview", typeof(RectTransform));
        imageObject.transform.SetParent(card, false);
        _image = imageObject.AddComponent<RawImage>();
        _image.texture = _camera.targetTexture;
        _image.raycastTarget = false;
        NCUiKit.Place(_image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, ImageY), Vector2.one * ImageSize);
        if (deco != null)
        {
            imageObject.transform.SetSiblingIndex(deco.GetSiblingIndex() + 1);
        }

        _yaw = slot == 0 ? 25f : -25f;
        SetActive(false);
    }

    public void SetActive(bool active)
    {
        if (_stage.activeSelf != active)
        {
            _stage.SetActive(active);
        }
    }

    /// <summary>0 참가 대기 · 1 준비 중 · 2 준비 완료. 매 프레임 부른다.</summary>
    public void Tick(int state)
    {
        if (state != _state)
        {
            if (state == 2 && _state >= 0)
            {
                _hopAge = 0f; // 준비 완료로 바뀌는 순간 한 번 점프
            }

            _state = state;
            ApplyColors(state == 0);
            _glow.enabled = state == 2;
        }

        float dt = Time.deltaTime;

        if (state == 2)
        {
            _yaw = Mathf.MoveTowardsAngle(_yaw, 0f, FaceSpeed * dt);
            Color glow = NCStyle.Success;
            glow.a = GlowAlpha + 0.15f * Mathf.Sin(Time.time * 3f);
            NCFx.SetColor(_glow, glow);
        }
        else
        {
            _yaw += TurnSpeed * dt;
        }

        _hopAge += dt;
        float hop = _hopAge < HopSeconds ? Mathf.Sin(Mathf.PI * _hopAge / HopSeconds) * HopHeight : 0f;

        _model.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        _model.localPosition = new Vector3(0f, hop, 0f);
    }

    private void ApplyColors(bool silhouette)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].material.color = silhouette ? Silhouette : _colors[i];
        }

        _image.color = silhouette ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
    }
}
