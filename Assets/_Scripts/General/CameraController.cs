using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float mousePanSensitivity = 0.08f;

    [Header("缩放")]
    [SerializeField] private float zoomSpeed = 2.5f;

    [Header("旋转（按住滚轮）")]
    [SerializeField] private float rotateSensitivity = 2f;
    [SerializeField] private float minPitchAngle = 15f;
    [SerializeField] private float maxPitchAngle = 85f;

    [Header("与地面距离")]
    [SerializeField] private float minGroundDistance = 2f;

    public void SetMinGroundDistance(float value)
    {
        minGroundDistance = Mathf.Max(0f, value);
    }

    public float MinGroundDistance => minGroundDistance;
    [SerializeField] private float groundRayMaxDistance = 500f;
    [SerializeField] private LayerMask groundLayers = ~0;

    private float _yawDeg;
    private float _pitchDeg;

    private void Start()
    {
        Vector3 e = transform.eulerAngles;
        _yawDeg = e.y;
        _pitchDeg = e.x;
        if (_pitchDeg > 180f) _pitchDeg -= 360f;
    }

    private void LateUpdate()
    {
        bool overUI = GameInput.IsPointerOverGameObject();

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        float dt = Time.deltaTime;

        Vector3 wasd = Vector3.zero;
        if (GameInput.KeyHeld(KeyCode.W)) wasd += forward;
        if (GameInput.KeyHeld(KeyCode.S)) wasd -= forward;
        if (GameInput.KeyHeld(KeyCode.A)) wasd -= right;
        if (GameInput.KeyHeld(KeyCode.D)) wasd += right;
        if (wasd.sqrMagnitude > 0.01f)
            transform.position += wasd.normalized * (moveSpeed * dt);

        Vector2 mouseDelta = GameInput.MouseDelta();
        if (!overUI && GameInput.RightButtonPressed())
        {
            float mx = mouseDelta.x;
            float my = mouseDelta.y;
            transform.position -= (right * mx + forward * my) * mousePanSensitivity;
        }

        float scroll = GameInput.ScrollY();
        if (Mathf.Abs(scroll) > 0.01f)
            transform.position += transform.forward * (scroll * zoomSpeed);

        if (!overUI && GameInput.MiddleButtonPressed())
        {
            float mx = mouseDelta.x;
            float my = mouseDelta.y;
            _yawDeg += mx * rotateSensitivity;
            _pitchDeg -= my * rotateSensitivity;
            _pitchDeg = Mathf.Clamp(_pitchDeg, minPitchAngle, maxPitchAngle);
            transform.rotation = Quaternion.Euler(_pitchDeg, _yawDeg, 0f);
        }

        EnforceMinGroundDistance();
    }

    private void EnforceMinGroundDistance()
    {
        if (minGroundDistance <= 0f) return;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundRayMaxDistance, groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            if (hit.distance < minGroundDistance)
                transform.position += Vector3.up * (minGroundDistance - hit.distance);
        }
    }
}
