using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype
{
    /// <summary>
    /// Free-fly camera for test scenes: WASD/QE to move, right mouse button to look, Shift to go faster,
    /// mouse wheel to change the base speed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlyCamera : MonoBehaviour
    {
        private const float MinSpeedMetersPerSecond = 0.5f;
        private const float MaxSpeedMetersPerSecond = 500f;
        private const float WheelSpeedStep = 1.2f;
        private const float MaxPitchDegrees = 89f;

        [SerializeField, Range(MinSpeedMetersPerSecond, MaxSpeedMetersPerSecond), Tooltip("Base move speed, in meters per second.")]
        private float _speedMetersPerSecond = 10f;

        [SerializeField, Min(1f), Tooltip("Speed multiplier while Shift is held (unitless).")]
        private float _boostMultiplier = 5f;

        [SerializeField, Min(0.01f), Tooltip("Look sensitivity, in degrees per mouse pixel.")]
        private float _lookDegreesPerPixel = 0.15f;

        private float _yawDegrees;
        private float _pitchDegrees;

        private void OnEnable()
        {
            Vector3 euler = transform.eulerAngles;
            _yawDegrees = euler.y;
            _pitchDegrees = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }

            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yawDegrees += delta.x * _lookDegreesPerPixel;
                _pitchDegrees = Mathf.Clamp(_pitchDegrees - delta.y * _lookDegreesPerPixel, -MaxPitchDegrees, MaxPitchDegrees);
                transform.rotation = Quaternion.Euler(_pitchDegrees, _yawDegrees, 0f);
            }

            float wheel = mouse.scroll.ReadValue().y;
            if (wheel != 0f)
            {
                float factor = wheel > 0f ? WheelSpeedStep : 1f / WheelSpeedStep;
                _speedMetersPerSecond = Mathf.Clamp(_speedMetersPerSecond * factor, MinSpeedMetersPerSecond, MaxSpeedMetersPerSecond);
            }

            Vector3 move = Vector3.zero;
            move.x = Axis(keyboard.dKey.isPressed, keyboard.aKey.isPressed);
            move.y = Axis(keyboard.eKey.isPressed, keyboard.qKey.isPressed);
            move.z = Axis(keyboard.wKey.isPressed, keyboard.sKey.isPressed);
            float speed = _speedMetersPerSecond * (keyboard.leftShiftKey.isPressed ? _boostMultiplier : 1f);
            transform.position += transform.TransformDirection(move) * (speed * Time.unscaledDeltaTime);
        }

        private static float Axis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }
}
