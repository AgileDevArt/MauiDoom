using ManagedDoom;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Windows.System;

namespace MauiDoom
{
    public class WindowsInputState : InputState
    {
        protected HashSet<DoomMouseButton> MouseButtons { get; } = [];

        public WindowsInputState(SKCanvasView view) : base(view)
        {
            if (view.Handler?.PlatformView is not UIElement nativeView)
                return;

            nativeView.IsTabStop = true;
            nativeView.Focus(FocusState.Programmatic);
            nativeView.KeyDown += OnKeyDown;
            nativeView.KeyUp += OnKeyUp;
        }

        protected void OnKeyUp(object sender, KeyRoutedEventArgs e) => SetKeyState(ToDoomKey(e.Key), false);
        protected void OnKeyDown(object sender, KeyRoutedEventArgs e) => SetKeyState(ToDoomKey(e.Key), true);

        protected override void OnTouch(object? sender, SKTouchEventArgs e)
        {
            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    MouseButtons.Add(ToDoomMouseButton(e.MouseButton));
                    break;
                case SKTouchAction.Moved when TouchPoints.TryGetValue(e.Id, out var old):
                    MouseX += e.Location.X - old.X;
                    break;
                case SKTouchAction.Released:
                    MouseButtons.Remove(ToDoomMouseButton(e.MouseButton));
                    break;
            }
            TouchPoints[e.Id] = (e.Location.X, e.Location.Y);
            e.Handled = true;
        }

        public override bool IsButtonDown(DoomMouseButton button) => MouseButtons.Contains(button);

        public static DoomKey ToDoomKey(VirtualKey virtualKey) => virtualKey switch
        {
            VirtualKey.Space => DoomKey.Space,
            //case VirtualKey.Apostrophe: return DoomKey.Apostrophe;
            //case VirtualKey.Comma: return DoomKey.Comma;
            //case VirtualKey.Minus: return DoomKey.Subtract;
            //case VirtualKey.Period: return DoomKey.Period;
            //case VirtualKey.Slash: return DoomKey.Slash;
            VirtualKey.Number0 => DoomKey.Num0,
            VirtualKey.Number1 => DoomKey.Num1,
            VirtualKey.Number2 => DoomKey.Num2,
            VirtualKey.Number3 => DoomKey.Num3,
            VirtualKey.Number4 => DoomKey.Num4,
            VirtualKey.Number5 => DoomKey.Num5,
            VirtualKey.Number6 => DoomKey.Num6,
            VirtualKey.Number7 => DoomKey.Num7,
            VirtualKey.Number8 => DoomKey.Num8,
            VirtualKey.Number9 => DoomKey.Num9,
            //case VirtualKey.Semicolon: return DoomKey.Semicolon;
            //case VirtualKey.Equal: return DoomKey.Equal;
            VirtualKey.A => DoomKey.A,
            VirtualKey.B => DoomKey.B,
            VirtualKey.C => DoomKey.C,
            VirtualKey.D => DoomKey.D,
            VirtualKey.E => DoomKey.E,
            VirtualKey.F => DoomKey.F,
            VirtualKey.G => DoomKey.G,
            VirtualKey.H => DoomKey.H,
            VirtualKey.I => DoomKey.I,
            VirtualKey.J => DoomKey.J,
            VirtualKey.K => DoomKey.K,
            VirtualKey.L => DoomKey.L,
            VirtualKey.M => DoomKey.M,
            VirtualKey.N => DoomKey.N,
            VirtualKey.O => DoomKey.O,
            VirtualKey.P => DoomKey.P,
            VirtualKey.Q => DoomKey.Q,
            VirtualKey.R => DoomKey.R,
            VirtualKey.S => DoomKey.S,
            VirtualKey.T => DoomKey.T,
            VirtualKey.U => DoomKey.U,
            VirtualKey.V => DoomKey.V,
            VirtualKey.W => DoomKey.W,
            VirtualKey.X => DoomKey.X,
            VirtualKey.Y => DoomKey.Y,
            VirtualKey.Z => DoomKey.Z,
            //case VirtualKey.LeftBracket: return DoomKey.LBracket;
            //case VirtualKey.Backslash: return DoomKey.Backslash;
            //case VirtualKey.RightBracket: return DoomKey.RBracket;
            //case VirtualKey.GraveAccent: return DoomKey.GraveAccent;
            //case VirtualKey.World1: return DoomKey.World1;
            //case VirtualKey.World2: return DoomKey.World2;
            VirtualKey.Escape => DoomKey.Escape,
            VirtualKey.Enter => DoomKey.Enter,
            VirtualKey.Tab => DoomKey.Tab,
            //case VirtualKey.Backspace: return DoomKey.Backspace;
            VirtualKey.Insert => DoomKey.Insert,
            VirtualKey.Delete => DoomKey.Delete,
            VirtualKey.Right => DoomKey.Right,
            VirtualKey.Left => DoomKey.Left,
            VirtualKey.Down => DoomKey.Down,
            VirtualKey.Up => DoomKey.Up,
            VirtualKey.PageUp => DoomKey.PageUp,
            VirtualKey.PageDown => DoomKey.PageDown,
            VirtualKey.Home => DoomKey.Home,
            VirtualKey.End => DoomKey.End,
            // case VirtualKey.CapsLock: return DoomKey.CapsLock;
            // case VirtualKey.ScrollLock: return DoomKey.ScrollLock;
            // case VirtualKey.NumLock: return DoomKey.NumLock;
            // case VirtualKey.PrintScreen: return DoomKey.PrintScreen;
            VirtualKey.Pause => DoomKey.Pause,
            VirtualKey.F1 => DoomKey.F1,
            VirtualKey.F2 => DoomKey.F2,
            VirtualKey.F3 => DoomKey.F3,
            VirtualKey.F4 => DoomKey.F4,
            VirtualKey.F5 => DoomKey.F5,
            VirtualKey.F6 => DoomKey.F6,
            VirtualKey.F7 => DoomKey.F7,
            VirtualKey.F8 => DoomKey.F8,
            VirtualKey.F9 => DoomKey.F9,
            VirtualKey.F10 => DoomKey.F10,
            VirtualKey.F11 => DoomKey.F11,
            VirtualKey.F12 => DoomKey.F12,
            VirtualKey.F13 => DoomKey.F13,
            VirtualKey.F14 => DoomKey.F14,
            VirtualKey.F15 => DoomKey.F15,
            // case VirtualKey.F16: return DoomKey.F16;
            // case VirtualKey.F17: return DoomKey.F17;
            // case VirtualKey.F18: return DoomKey.F18;
            // case VirtualKey.F19: return DoomKey.F19;
            // case VirtualKey.F20: return DoomKey.F20;
            // case VirtualKey.F21: return DoomKey.F21;
            // case VirtualKey.F22: return DoomKey.F22;
            // case VirtualKey.F23: return DoomKey.F23;
            // case VirtualKey.F24: return DoomKey.F24;
            // case VirtualKey.F25: return DoomKey.F25;
            VirtualKey.NumberPad0 => DoomKey.Numpad0,
            VirtualKey.NumberPad1 => DoomKey.Numpad1,
            VirtualKey.NumberPad2 => DoomKey.Numpad2,
            VirtualKey.NumberPad3 => DoomKey.Numpad3,
            VirtualKey.NumberPad4 => DoomKey.Numpad4,
            VirtualKey.NumberPad5 => DoomKey.Numpad5,
            VirtualKey.NumberPad6 => DoomKey.Numpad6,
            VirtualKey.NumberPad7 => DoomKey.Numpad7,
            VirtualKey.NumberPad8 => DoomKey.Numpad8,
            VirtualKey.NumberPad9 => DoomKey.Numpad9,
            // case VirtualKey.KeypadDecimal: return DoomKey.Decimal;
            VirtualKey.Divide => DoomKey.Divide,
            VirtualKey.Multiply => DoomKey.Multiply,
            VirtualKey.Subtract => DoomKey.Subtract,
            VirtualKey.Add => DoomKey.Add,
            //case VirtualKey.NumberPadEnter: return DoomKey.Enter;
            //case VirtualKey.NumberPadEqual: return DoomKey.Equal;
            VirtualKey.LeftShift => DoomKey.LShift,
            VirtualKey.LeftControl => DoomKey.LControl,
            VirtualKey.LeftMenu => DoomKey.LAlt,
            VirtualKey.LeftWindows => DoomKey.LSystem,
            VirtualKey.RightShift => DoomKey.RShift,
            VirtualKey.RightControl => DoomKey.RControl,
            VirtualKey.RightMenu => DoomKey.RAlt,
            VirtualKey.RightWindows => DoomKey.RSystem,
            VirtualKey.Shift => DoomKey.LShift,
            VirtualKey.Control => DoomKey.LControl,
            VirtualKey.Menu => DoomKey.LAlt,
            _ => DoomKey.Unknown,
        };

        public static DoomMouseButton ToDoomMouseButton(SKMouseButton mouseButton) => mouseButton switch
        {
            SKMouseButton.Left => DoomMouseButton.Mouse1,
            SKMouseButton.Right => DoomMouseButton.Mouse2,
            SKMouseButton.Middle => DoomMouseButton.Mouse3,
            _ => DoomMouseButton.Unknown,
        };
    }
}
