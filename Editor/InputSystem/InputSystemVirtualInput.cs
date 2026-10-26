using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Xorin.Editor.Tools.Debugging;
using InputKey = UnityEngine.InputSystem.Key;

namespace Xorin.Editor.InputSystem
{
    /// <summary>
    /// Phase 92c: play_and_check's virtual mouse and keyboard. They become the
    /// current devices, so a pointer handler (through the UI input module) and a
    /// script reading Mouse.current or Keyboard.current both see the input, and
    /// OnMouseDown does not, as for a user in an Input-System-only project.
    /// </summary>
    internal sealed class InputSystemVirtualInput : IVirtualInput
    {
        private Mouse _mouse;
        private Keyboard _keyboard;
        private readonly HashSet<InputKey> _held = new HashSet<InputKey>();
        private Vector2 _position;
        // Held buttons as MouseState's bits (left 1, right 2, middle 4).
        private ushort _buttons;

        [InitializeOnLoadMethod]
        private static void Register() =>
            VirtualInputRegistry.Factory = () => new InputSystemVirtualInput();

        public void Begin()
        {
            if (_mouse != null) return;
            _mouse = UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>("XorinVirtualMouse");
            _keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>("XorinVirtualKeyboard");
            _mouse.MakeCurrent();
            _keyboard.MakeCurrent();
        }

        public void Pointer(Vector2 screenPosition, bool pressed)
        {
            _position = screenPosition;
            SetButton(MouseButton.Left, pressed);
            Send(Vector2.zero, Vector2.zero);
        }

        public void PointerDelta(Vector2 delta)
        {
            _position += delta;
            Send(delta, Vector2.zero);
        }

        public void PointerTo(Vector2 screenPosition)
        {
            Vector2 delta = screenPosition - _position;
            _position = screenPosition;
            Send(delta, Vector2.zero);
        }

        public bool Button(string button, bool pressed)
        {
            MouseButton which;
            switch ((button ?? "left").Trim().ToLowerInvariant())
            {
                case "left": which = MouseButton.Left; break;
                case "right": which = MouseButton.Right; break;
                case "middle": which = MouseButton.Middle; break;
                default: return false;
            }
            SetButton(which, pressed);
            Send(Vector2.zero, Vector2.zero);
            return true;
        }

        public void Scroll(float delta) => Send(Vector2.zero, new Vector2(0f, delta));

        private void SetButton(MouseButton button, bool pressed)
        {
            ushort bit = (ushort)(1 << (int)button);
            _buttons = pressed ? (ushort)(_buttons | bit) : (ushort)(_buttons & ~bit);
        }

        private void Send(Vector2 delta, Vector2 scroll)
        {
            Begin();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_mouse,
                new MouseState { position = _position, delta = delta, scroll = scroll, buttons = _buttons });
        }

        public bool Key(string key, bool pressed)
        {
            if (!TryParseKey(key, out InputKey parsed)) return false;
            Begin();
            if (pressed) _held.Add(parsed); else _held.Remove(parsed);
            var held = new InputKey[_held.Count];
            _held.CopyTo(held);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState(held));
            return true;
        }

        public void End()
        {
            if (_mouse != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(_mouse);
            if (_keyboard != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(_keyboard);
            _mouse = null;
            _keyboard = null;
            _held.Clear();
            _buttons = 0;
        }

        // "space", "W", "left arrow", "1", "enter": the Input System's Key names.
        internal static bool TryParseKey(string key, out InputKey parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(key)) return false;
            string name = key.Replace(" ", "").Replace("_", "");
            if (name.Length == 1 && char.IsDigit(name[0])) name = "Digit" + name;
            if (string.Equals(name, "return", StringComparison.OrdinalIgnoreCase)) name = "Enter";
            if (string.Equals(name, "esc", StringComparison.OrdinalIgnoreCase)) name = "Escape";
            return Enum.TryParse(name, true, out parsed) && parsed != InputKey.None;
        }
    }
}
