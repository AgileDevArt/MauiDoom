using System;
using System.Collections.Generic;
using System.Text;

namespace ManagedDoom.UserInput
{
    public interface IInputState
    {
        int ScreenWidth { get; }
        int ScreenHeight { get; }
        float MouseX { get; }
        float MouseY { get; }
        Action<DoomKey> KeyDown { get; set; }
        Action<DoomKey> KeyUp { get; set; }

        public bool IsButtonDown(DoomMouseButton button);
        public bool IsKeyDown(DoomKey button);
    }
}
