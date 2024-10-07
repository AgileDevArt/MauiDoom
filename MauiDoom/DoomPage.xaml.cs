using CommunityToolkit.Maui.Views;
using ManagedDoom;
using ManagedDoom.Maui;
using ManagedDoom.UserInput;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace MauiDoom;

public partial class DoomPage : ContentPage
{
    InputState _state;
    MediaElement[] _channels;

    public DoomPage()
    {
        InitializeComponent();
        Loaded += (_, __) =>
        {
            DeviceDisplay.KeepScreenOn = true;

#if WINDOWS
            _state = new WindowsInputState(DoomGraphicsView);
#else
            _state = new InputState(DoomGraphicsView);
#endif
            _channels = [
                Channel1,
                Channel2,
                Channel3,
                Channel4,
                Channel5,
                Channel6,
                Channel7,
                Channel8,
                ChannelUi,
                ChannelMusic
            ];
            _ = new ManagedDoom.Maui.MauiDoom(new CommandLineArgs(Environment.GetCommandLineArgs()), _state, _channels, DoomGraphicsView);
        };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DeviceDisplay.KeepScreenOn = false;
    }
}

public class InputState : IInputState
{
    public const int DOUBLE_TAP_TIME = 200;

    protected Dictionary<long, (float X, float Y)> TouchPoints = new(5);
    protected HashSet<DoomKey> Keys = [];
    protected DateTime TouchStart;

    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }

    public float MouseX { get; set; }
    public float MouseY { get; set; }

    public Action<DoomKey>? KeyDown { get; set; }
    public Action<DoomKey>? KeyUp { get; set; }

    public InputState(SKCanvasView view)
    {
        view.Touch += OnTouch;
    }

    protected virtual void OnTouch(object? sender, SKTouchEventArgs e)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                OnStart(e.Id, e.Location.X, e.Location.Y);
                break;
            case SKTouchAction.Moved:
                OnMove(e.Id, e.Location.X, e.Location.Y);
                break;
            case SKTouchAction.Released:
                OnEnd(e.Id, e.Location.X, e.Location.Y);
                break;
        }
        e.Handled = true;
    }

    protected virtual void OnStart(long touchId, float x, float y)
    {
        var delta = TouchStart != default ? (int)(DateTime.UtcNow - TouchStart).TotalMilliseconds : 0;
        TouchStart = DateTime.UtcNow;
        TouchPoints[touchId] = (x, y);
        if (delta < DOUBLE_TAP_TIME)
        {
            SetKeyState(DoomKey.LControl, true);
            SetKeyState(DoomKey.Enter, true);
        }
        else if (TouchPoints.Count == 2)
        {
            SetKeyState(DoomKey.Space, true);
        }
        else if (TouchPoints.Count == 3)
        {
            SetKeyState(DoomKey.Escape, true);
        }
    }

    protected virtual void OnEnd(long touchId, float x, float y)
    {
        SetKeyState(DoomKey.LControl, false);
        SetKeyState(DoomKey.Space, false);
        SetKeyState(DoomKey.Enter, false);
        SetKeyState(DoomKey.Up, false);
        SetKeyState(DoomKey.Down, false);
        SetKeyState(DoomKey.Left, false);
        SetKeyState(DoomKey.Right, false);
        SetKeyState(DoomKey.Escape, false);
        TouchPoints.Remove(touchId);
    }

    protected virtual void OnMove(long touchId, float x, float y)
    {
        if (TouchPoints.TryGetValue(touchId, out var old))
        {
            var relativeX = x - old.X;
            var relativeY = y - old.Y;
            if (relativeY < -5 || relativeY > 5)
            {
                SetKeyState(DoomKey.Up, relativeY < -1);
                SetKeyState(DoomKey.Down, relativeY > 1);
            }
            MouseX += Math.Clamp(relativeX, -25, 25);
        }
        TouchPoints[touchId] = (x, y);
    }

    protected void SetKeyState(DoomKey key, bool down)
    {
        if (down && !Keys.Contains(key))
        {
            Keys.Add(key);
            KeyDown?.Invoke(key);
        }
        else if (!down && Keys.Contains(key))
        {
            Keys.Remove(key);
            KeyUp?.Invoke(key);
        }
    }

    public virtual bool IsButtonDown(DoomMouseButton button) => false;
    public virtual bool IsKeyDown(DoomKey button) => Keys.Contains(button);
}