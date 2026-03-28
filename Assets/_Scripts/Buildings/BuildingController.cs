using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 宏观 / 微观建造模式切换；切换时把摄像机推到对应观察距离。
/// UI 按钮请使用 <see cref="BuildModeUIButton"/> 在 Inspector 中绑定。
/// </summary>
[DefaultExecutionOrder(20)]
public class BuildingController : MonoBehaviour
{
    public enum BuildMode
    {
        Macro,
        Micro
    }

    [Header("引用")]
    [Tooltip("留空则使用本物体上的 Camera，否则再尝试 Camera.main。")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("留空则在 Target Camera 所在物体上查找 CameraController（脚本挂在同一相机上时可不填）。")]
    [SerializeField] private CameraController cameraController;

    [Header("模式与摄像机（透视：沿视线方向与「屏幕中心落地点」保持距离）")]
    [SerializeField] private BuildMode initialMode = BuildMode.Macro;
    [SerializeField] private float macroFocusDistance = 22f;
    [SerializeField] private float microFocusDistance = 6f;
    [SerializeField] private float zoomTransitionDuration = 0.4f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundRayMaxDistance = 500f;

    [Header("CameraController 离地限制（仅当已引用 CameraController 时生效）")]
    [SerializeField] private float macroMinGroundDistance = 2f;
    [SerializeField] private float microMinGroundDistance = 0.6f;

    [Header("事件")]
    [SerializeField] private UnityEvent<BuildMode> onBuildModeChanged;

    public BuildMode CurrentMode { get; private set; }

    /// <summary>Inspector 中的初始模式；编辑 Scene 视图时 Gizmo 按此值预览（未进入播放模式）。</summary>
    public BuildMode InitialMode => initialMode;

    /// <summary>供 <see cref="BuildingGrid"/> / <see cref="MiniBuildingGrid"/> 使用：当前应对应显示的建造模式。</summary>
    public bool IsGizmoActiveFor(BuildMode gridMode)
    {
        BuildMode active = Application.isPlaying ? CurrentMode : initialMode;
        return active == gridMode;
    }

    /// <summary>与 <see cref="onBuildModeChanged"/> 等价的代码订阅。</summary>
    public event Action<BuildMode> BuildModeChanged;

    private Coroutine _zoomRoutine;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (cameraController == null && targetCamera != null)
            cameraController = targetCamera.GetComponent<CameraController>();

        CurrentMode = initialMode;
        ApplyCameraControllerGroundForMode(CurrentMode, immediate: true);
    }

    public void ToggleMode()
    {
        SetMode(CurrentMode == BuildMode.Macro ? BuildMode.Micro : BuildMode.Macro);
    }

    public void SetMode(BuildMode mode)
    {
        if (mode == CurrentMode && _zoomRoutine == null)
            return;

        CurrentMode = mode;
        ApplyCameraControllerGroundForMode(CurrentMode, immediate: false);
        RaiseModeChanged(mode);

        if (targetCamera != null)
        {
            if (_zoomRoutine != null)
                StopCoroutine(_zoomRoutine);
            _zoomRoutine = StartCoroutine(ZoomCameraToMode(mode));
        }
    }

    private void RaiseModeChanged(BuildMode mode)
    {
        BuildModeChanged?.Invoke(mode);
        onBuildModeChanged?.Invoke(mode);
    }

    private void ApplyCameraControllerGroundForMode(BuildMode mode, bool immediate)
    {
        if (cameraController == null)
            return;
        float v = mode == BuildMode.Macro ? macroMinGroundDistance : microMinGroundDistance;
        cameraController.SetMinGroundDistance(v);
    }

    private bool TryGetViewportGroundFocus(out Vector3 focusWorld)
    {
        focusWorld = default;
        if (targetCamera == null)
            return false;

        var ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, groundRayMaxDistance, groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            focusWorld = hit.point;
            return true;
        }

        var plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            focusWorld = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    private IEnumerator ZoomCameraToMode(BuildMode mode)
    {
        float targetDist = mode == BuildMode.Macro ? macroFocusDistance : microFocusDistance;
        if (!TryGetViewportGroundFocus(out Vector3 focus))
        {
            _zoomRoutine = null;
            yield break;
        }

        Transform ct = targetCamera.transform;
        Vector3 endPos = focus - ct.forward * targetDist;
        Vector3 startPos = ct.position;
        float dur = Mathf.Max(0.01f, zoomTransitionDuration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = Mathf.SmoothStep(0f, 1f, t);
            ct.position = Vector3.LerpUnclamped(startPos, endPos, k);
            yield return null;
        }

        ct.position = endPos;
        _zoomRoutine = null;
    }
}
