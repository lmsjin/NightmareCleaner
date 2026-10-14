using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 성능 진단 표시(F3로 켜고 끔, 내 화면에만). 끊김 원인을 좁히는 용도.
///  - 프레임 시간(ms)·FPS, 최근 1초 중 가장 느린 프레임
///  - GC 횟수: 끊길 때 이 숫자가 같이 오르면 쓰레기 메모리 정리(GC)가 원인이다.
/// NCUi가 자동으로 붙인다.
/// </summary>
public class NCDebugOverlay : MonoBehaviour
{
    private bool _visible;
    private float _smoothedMs;
    private float _worstMs;
    private float _worstShown;
    private float _windowTimer;
    private GUIStyle _style;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
        {
            _visible = _visible == false;
        }

        float ms = Time.unscaledDeltaTime * 1000f;
        _smoothedMs = Mathf.Lerp(_smoothedMs, ms, 0.1f);
        _worstMs = Mathf.Max(_worstMs, ms);

        _windowTimer += Time.unscaledDeltaTime;
        if (_windowTimer >= 1f)
        {
            _worstShown = _worstMs;
            _worstMs = 0f;
            _windowTimer = 0f;
        }
    }

    private void OnGUI()
    {
        if (_visible == false)
        {
            return;
        }

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16 };
        }

        string text =
            $"{1000f / Mathf.Max(0.01f, _smoothedMs):0} FPS  ({_smoothedMs:0.0} ms)\n" +
            $"최근 1초 최악: {_worstShown:0.0} ms\n" +
            $"GC 횟수: {System.GC.CollectionCount(0)}";

        GUI.Box(new Rect(Screen.width - 250f, 80f, 240f, 70f), text, _style);
    }
}
