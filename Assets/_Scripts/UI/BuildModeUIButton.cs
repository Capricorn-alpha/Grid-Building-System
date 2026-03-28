using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 在 Inspector 中把「切换建造模式」按钮与 <see cref="BuildingController"/> 关联；切换后同步更新按钮文字。
/// </summary>
public class BuildModeUIButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("留空则自动查找场景中的 BuildingController。")]
    [SerializeField] private BuildingController buildingController;
    [Tooltip("留空则在按钮子物体上查找 Unity UI Text。")]
    [SerializeField] private TMP_Text labelText;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (buildingController == null)
            buildingController = FindFirstObjectByType<BuildingController>();

        if (labelText == null && button != null)
            labelText = button.GetComponentInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(OnClicked);

        if (buildingController != null)
            buildingController.BuildModeChanged += OnBuildModeChanged;

        RefreshLabel();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);

        if (buildingController != null)
            buildingController.BuildModeChanged -= OnBuildModeChanged;
    }

    private void OnClicked()
    {
        if (buildingController != null)
            buildingController.ToggleMode();
    }

    private void OnBuildModeChanged(BuildingController.BuildMode mode)
    {
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (labelText == null || buildingController == null)
            return;

        labelText.text = buildingController.CurrentMode == BuildingController.BuildMode.Macro
            ? "Macro"
            : "Micro";
    }
}
