using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 封装 Unity 新 Input System（<see cref="Mouse"/> / <see cref="Keyboard"/>），
/// 在 Player Settings 仅启用 Input System Package 时替代 <see cref="UnityEngine.Input"/>。
/// </summary>
public static class GameInput
{
    public static bool TryGetPointerScreen(out Vector2 screen)
    {
        if (Mouse.current != null)
        {
            screen = Mouse.current.position.ReadValue();
            return true;
        }

        screen = default;
        return false;
    }

    public static bool LeftButtonDownThisFrame()
    {
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    public static bool RightButtonPressed()
    {
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
    }

    public static bool MiddleButtonPressed()
    {
        return Mouse.current != null && Mouse.current.middleButton.isPressed;
    }

    public static Vector2 MouseDelta()
    {
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    }

    public static float ScrollY()
    {
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
    }

    public static bool KeyHeld(KeyCode code)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;
        return code switch
        {
            KeyCode.W => kb.wKey.isPressed,
            KeyCode.S => kb.sKey.isPressed,
            KeyCode.A => kb.aKey.isPressed,
            KeyCode.D => kb.dKey.isPressed,
            _ => false
        };
    }

    public static bool KeyDown(KeyCode code)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;
        return code switch
        {
            KeyCode.E => kb.eKey.wasPressedThisFrame,
            KeyCode.R => kb.rKey.wasPressedThisFrame,
            KeyCode.Q => kb.qKey.wasPressedThisFrame,
            KeyCode.Escape => kb.escapeKey.wasPressedThisFrame,
            KeyCode.Alpha1 => kb.digit1Key.wasPressedThisFrame,
            KeyCode.Alpha2 => kb.digit2Key.wasPressedThisFrame,
            KeyCode.Alpha3 => kb.digit3Key.wasPressedThisFrame,
            KeyCode.Alpha4 => kb.digit4Key.wasPressedThisFrame,
            KeyCode.Alpha5 => kb.digit5Key.wasPressedThisFrame,
            KeyCode.Alpha6 => kb.digit6Key.wasPressedThisFrame,
            KeyCode.Alpha7 => kb.digit7Key.wasPressedThisFrame,
            KeyCode.Alpha8 => kb.digit8Key.wasPressedThisFrame,
            KeyCode.Alpha9 => kb.digit9Key.wasPressedThisFrame,
            _ => false
        };
    }

    /// <summary>与 InputSystemUIInputModule 配合时，需传入鼠标 deviceId。</summary>
    public static bool IsPointerOverGameObject()
    {
        if (EventSystem.current == null)
            return false;
        if (Mouse.current != null)
            return EventSystem.current.IsPointerOverGameObject(Mouse.current.deviceId);
        return EventSystem.current.IsPointerOverGameObject();
    }
}
