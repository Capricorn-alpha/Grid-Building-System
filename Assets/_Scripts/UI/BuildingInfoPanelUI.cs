using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 默认隐藏建筑信息面板；点击场景中带 Collider 的 <see cref="Building"/> 时从右侧滑入并显示名称/缩略图。
/// 若 <see cref="panelRoot"/> 与本脚本挂在同一物体上，隐藏时使用 <see cref="CanvasGroup"/>，避免 SetActive(false) 停用本脚本导致 <see cref="Update"/> 无法检测点击。
/// </summary>
public class BuildingInfoPanelUI : MonoBehaviour
{
    private const string LogTag = "[BuildingInfoPanel]";

    [Header("面板")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private Button closeButton;

    [Header("射线")]
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private float raycastDistance = 500f;
    [SerializeField] private LayerMask buildingLayers = ~0;
    [Tooltip("Collide：可命中 Trigger 碰撞体；Ignore：仅实体碰撞体。建筑根上自动添加的为实体，模型子级常为 Trigger 时需 Collide。")]
    [SerializeField] private QueryTriggerInteraction raycastTriggerMode = QueryTriggerInteraction.Collide;
    [SerializeField] private BuildingSystem buildingSystem;

    [Header("调试")]
    [Tooltip("勾选后在 Console 输出点击检测各步骤，排查完请关闭。")]
    [SerializeField] private bool debugClickTrace = true;

    [Header("滑入动画")]
    [SerializeField] private float slideFromRightPixels = 420f;
    [SerializeField] private float slideDuration = 0.22f;

    private Vector2 _shownAnchoredPosition;
    private Coroutine _slideRoutine;
    private CanvasGroup _panelCanvasGroup;

    /// <summary>panelRoot 与本组件同物体时，不能用 SetActive(false)，否则本脚本也会被关掉。</summary>
    private bool PanelRootIsSameObjectAsScript =>
        panelRoot != null && panelRoot.gameObject == gameObject;

    private void Awake()
    {
        Debug.Log($"{LogTag} Awake 运行在游戏对象「{gameObject.name}」上。", this);

        ResolveRefs();

        if (panelRoot != null)
            _shownAnchoredPosition = panelRoot.anchoredPosition;

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (PanelRootIsSameObjectAsScript)
            Debug.Log($"{LogTag} Panel Root 与脚本在同一物体：隐藏将使用 CanvasGroup，避免 Update 被停用。", this);

        HideImmediate();

        if (debugClickTrace)
            Debug.Log($"{LogTag} Awake 结束: panelRoot={(panelRoot != null ? panelRoot.name : "null")}, camera={(raycastCamera != null ? raycastCamera.name : "null")}, buildingSystem={(buildingSystem != null ? buildingSystem.name : "null")}, rayDistance={raycastDistance}, layers={buildingLayers.value}", this);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
    }

    private void ResolveRefs()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;
        if (buildingSystem == null)
            buildingSystem = FindFirstObjectByType<BuildingSystem>();
    }

    private CanvasGroup GetOrAddPanelCanvasGroup()
    {
        if (_panelCanvasGroup == null && panelRoot != null)
            _panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
        if (_panelCanvasGroup == null && panelRoot != null)
            _panelCanvasGroup = panelRoot.gameObject.AddComponent<CanvasGroup>();
        return _panelCanvasGroup;
    }

    private void Update()
    {
        if (!GameInput.LeftButtonDownThisFrame())
            return;

        ResolveRefs();

        if (!GameInput.TryGetPointerScreen(out Vector2 screenPos))
        {
            if (debugClickTrace)
                Debug.LogWarning($"{LogTag} Mouse.current 为空，新 Input System 未检测到鼠标设备。", this);
            return;
        }

        if (debugClickTrace)
            Debug.Log($"{LogTag} ① 左键按下 (screen={screenPos})", this);

        if (IsPointerOverUI())
        {
            if (debugClickTrace)
                Debug.Log($"{LogTag} ② 指针在 UI 上，忽略场景射线", this);
            return;
        }

        if (buildingSystem != null && buildingSystem.HasActivePlacementPreview)
        {
            if (debugClickTrace)
                Debug.Log($"{LogTag} ③ BuildingSystem 正在放置预览，忽略选中建筑", this);
            return;
        }

        if (raycastCamera == null)
        {
            if (debugClickTrace)
                Debug.LogWarning($"{LogTag} ④ 无可用 Camera（raycastCamera / main 均为空）", this);
            return;
        }

        var ray = raycastCamera.ScreenPointToRay(screenPos);
        var hits = Physics.RaycastAll(ray, raycastDistance, buildingLayers, raycastTriggerMode);

        if (debugClickTrace)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{LogTag} ⑤ RaycastAll 命中数={hits.Length}, distance={raycastDistance}, mask={buildingLayers.value}, triggers={raycastTriggerMode}");
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                var path = h.collider != null ? GetTransformPath(h.collider.transform) : "?";
                var b = h.collider != null ? h.collider.GetComponentInParent<Building>() : null;
                sb.AppendLine($"   [{i}] dist={h.distance:F3} collider={h.collider?.name} path={path} Building={(b != null ? b.name : "null")} Data={(b != null && b.Data != null ? b.Data.name : "null")}");
            }
            Debug.Log(sb.ToString(), this);
        }

        if (hits.Length == 0)
        {
            Hide();
            if (debugClickTrace)
                Debug.Log($"{LogTag} ⑥ 无命中 → Hide", this);
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Building building = null;
        foreach (var h in hits)
        {
            var b = h.collider.GetComponentInParent<Building>();
            if (b != null && b.Data != null)
            {
                building = b;
                break;
            }
        }

        if (building == null)
        {
            Hide();
            if (debugClickTrace)
                Debug.Log($"{LogTag} ⑦ 命中物体上无带 Data 的 Building → Hide", this);
            return;
        }

        if (debugClickTrace)
            Debug.Log($"{LogTag} ⑧ 选中建筑 → Show: {building.name}, Data={building.Data.name}", this);

        Show(building);
    }

    private static string GetTransformPath(Transform t)
    {
        if (t == null) return "";
        var sb = new StringBuilder();
        while (t != null)
        {
            sb.Insert(0, "/" + t.name);
            t = t.parent;
        }
        return sb.ToString();
    }

    private static bool IsPointerOverUI()
    {
        return GameInput.IsPointerOverGameObject();
    }

    private bool IsVisuallyHidden()
    {
        if (panelRoot == null) return true;
        if (PanelRootIsSameObjectAsScript)
        {
            var cg = panelRoot.GetComponent<CanvasGroup>();
            return cg == null || cg.alpha < 0.01f;
        }
        return !panelRoot.gameObject.activeSelf;
    }

    public void Show(Building building)
    {
        if (panelRoot == null)
        {
            if (debugClickTrace)
                Debug.LogWarning($"{LogTag} Show 中止: panelRoot 未赋值", this);
            return;
        }

        if (building == null || building.Data == null)
        {
            if (debugClickTrace)
                Debug.LogWarning($"{LogTag} Show 中止: building 或 Data 为空", this);
            return;
        }

        if (nameText != null)
            nameText.text = string.IsNullOrEmpty(building.Description) ? building.Data.name : building.Description;

        if (thumbnailImage != null)
        {
            var thumb = building.Data.UiThumbnail;
            thumbnailImage.enabled = thumb != null;
            thumbnailImage.sprite = thumb;
        }

        if (PanelRootIsSameObjectAsScript)
        {
            var cg = GetOrAddPanelCanvasGroup();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        else
        {
            panelRoot.gameObject.SetActive(true);
        }

        if (_slideRoutine != null)
            StopCoroutine(_slideRoutine);
        _slideRoutine = StartCoroutine(SlideIn());
    }

    public void Hide()
    {
        if (panelRoot == null)
        {
            HideImmediate();
            return;
        }

        if (IsVisuallyHidden())
        {
            HideImmediate();
            return;
        }

        if (_slideRoutine != null)
            StopCoroutine(_slideRoutine);
        _slideRoutine = StartCoroutine(SlideOutAndDeactivate());
    }

    private void HideImmediate()
    {
        if (panelRoot == null)
            return;

        if (PanelRootIsSameObjectAsScript)
        {
            var cg = GetOrAddPanelCanvasGroup();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
        else
        {
            panelRoot.gameObject.SetActive(false);
        }
    }

    private IEnumerator SlideIn()
    {
        Vector2 from = _shownAnchoredPosition + new Vector2(slideFromRightPixels, 0f);
        panelRoot.anchoredPosition = from;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, slideDuration);
            float k = Mathf.SmoothStep(0f, 1f, t);
            panelRoot.anchoredPosition = Vector2.LerpUnclamped(from, _shownAnchoredPosition, k);
            yield return null;
        }

        panelRoot.anchoredPosition = _shownAnchoredPosition;
        _slideRoutine = null;
    }

    private IEnumerator SlideOutAndDeactivate()
    {
        Vector2 from = panelRoot.anchoredPosition;
        Vector2 to = _shownAnchoredPosition + new Vector2(slideFromRightPixels, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, slideDuration);
            float k = Mathf.SmoothStep(0f, 1f, t);
            panelRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            yield return null;
        }

        panelRoot.anchoredPosition = _shownAnchoredPosition;

        if (PanelRootIsSameObjectAsScript)
        {
            var cg = GetOrAddPanelCanvasGroup();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
        else
        {
            panelRoot.gameObject.SetActive(false);
        }

        _slideRoutine = null;
    }
}
