using System;
using System.Numerics;
using ManagedDoom.UserInput;
using System.Runtime.ExceptionServices;

namespace ManagedDoom.Maui
{
    public class MauiUserInput : IUserInput, IDisposable
    {
        private Config config;

        private bool[] weaponKeys;
        private int turnHeld;

        private IInputState inputState;

        private bool mouseGrabbed;
        private float mouseX;
        private float mouseY;
        private float mousePrevX;
        private float mousePrevY;
        private float mouseDeltaX;
        private float mouseDeltaY;

        public MauiUserInput(Config config, IInputState inputState, bool useMouse)
        {
            try
            {
                System.Diagnostics.Debug.Write("Initialize user input: ");

                this.config = config;
                this.inputState = inputState;
                weaponKeys = new bool[7];
                turnHeld = 0;

                if (useMouse)
                {
                    mouseGrabbed = false;
                }

                System.Diagnostics.Debug.WriteLine("OK");
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Failed");
                Dispose();
                ExceptionDispatchInfo.Throw(e);
            }
        }

        public void BuildTicCmd(TicCmd cmd)
        {
            var keyForward = IsPressed(config.key_forward);
            var keyBackward = IsPressed(config.key_backward);
            var keyStrafeLeft = IsPressed(config.key_strafeleft);
            var keyStrafeRight = IsPressed(config.key_straferight);
            var keyTurnLeft = IsPressed(config.key_turnleft);
            var keyTurnRight = IsPressed(config.key_turnright);
            var keyFire = IsPressed(config.key_fire);
            var keyUse = IsPressed(config.key_use);
            var keyRun = IsPressed(config.key_run);
            var keyStrafe = IsPressed(config.key_strafe);

            weaponKeys[0] = IsKeyPressed(DoomKey.Num1);
            weaponKeys[1] = IsKeyPressed(DoomKey.Num2);
            weaponKeys[2] = IsKeyPressed(DoomKey.Num3);
            weaponKeys[3] = IsKeyPressed(DoomKey.Num4);
            weaponKeys[4] = IsKeyPressed(DoomKey.Num5);
            weaponKeys[5] = IsKeyPressed(DoomKey.Num6);
            weaponKeys[6] = IsKeyPressed(DoomKey.Num7);

            cmd.Clear();

            var strafe = keyStrafe;
            var speed = keyRun ? 1 : 0;
            var forward = 0;
            var side = 0;

            if (config.game_alwaysrun)
            {
                speed = 1 - speed;
            }

            if (keyTurnLeft || keyTurnRight)
            {
                turnHeld++;
            }
            else
            {
                turnHeld = 0;
            }

            int turnSpeed;
            if (turnHeld < PlayerBehavior.SlowTurnTics)
            {
                turnSpeed = 2;
            }
            else
            {
                turnSpeed = speed;
            }

            if (strafe)
            {
                if (keyTurnRight)
                {
                    side += PlayerBehavior.SideMove[speed];
                }
                if (keyTurnLeft)
                {
                    side -= PlayerBehavior.SideMove[speed];
                }
            }
            else
            {
                if (keyTurnRight)
                {
                    cmd.AngleTurn -= (short)PlayerBehavior.AngleTurn[turnSpeed];
                }
                if (keyTurnLeft)
                {
                    cmd.AngleTurn += (short)PlayerBehavior.AngleTurn[turnSpeed];
                }
            }

            if (keyForward)
            {
                forward += PlayerBehavior.ForwardMove[speed];
            }
            if (keyBackward)
            {
                forward -= PlayerBehavior.ForwardMove[speed];
            }

            if (keyStrafeLeft)
            {
                side -= PlayerBehavior.SideMove[speed];
            }
            if (keyStrafeRight)
            {
                side += PlayerBehavior.SideMove[speed];
            }

            if (keyFire)
            {
                cmd.Buttons |= TicCmdButtons.Attack;
            }

            if (keyUse)
            {
                cmd.Buttons |= TicCmdButtons.Use;
            }

            // Check weapon keys.
            for (var i = 0; i < weaponKeys.Length; i++)
            {
                if (weaponKeys[i])
                {
                    cmd.Buttons |= TicCmdButtons.Change;
                    cmd.Buttons |= (byte)(i << TicCmdButtons.WeaponShift);
                    break;
                }
            }

            UpdateMouse();
            var ms = 0.5F * config.mouse_sensitivity;
            var mx = (int)MathF.Round(ms * mouseDeltaX);
            var my = (int)MathF.Round(ms * -mouseDeltaY);
            forward += my;
            if (strafe)
            {
                side += mx * 2;
            }
            else
            {
                cmd.AngleTurn -= (short)(mx * 0x8);
            }

            if (forward > PlayerBehavior.MaxMove)
            {
                forward = PlayerBehavior.MaxMove;
            }
            else if (forward < -PlayerBehavior.MaxMove)
            {
                forward = -PlayerBehavior.MaxMove;
            }
            if (side > PlayerBehavior.MaxMove)
            {
                side = PlayerBehavior.MaxMove;
            }
            else if (side < -PlayerBehavior.MaxMove)
            {
                side = -PlayerBehavior.MaxMove;
            }

            cmd.ForwardMove += (sbyte)forward;
            cmd.SideMove += (sbyte)side;
        }

        private bool IsPressed(KeyBinding keyBinding)
        {
            foreach (var key in keyBinding.Keys)
            {
                if (IsKeyPressed(key))
                {
                    return true;
                }
            }

            if (mouseGrabbed)
            {
                foreach (var mouseButton in keyBinding.MouseButtons)
                {
                    if (inputState.IsButtonDown(mouseButton))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsKeyPressed(DoomKey key)
        {
            return inputState.IsKeyDown(key);
        }

        public void Reset()
        {
            if (inputState == null)
            {
                return;
            }

            mouseX = inputState.MouseX;
            mouseY = inputState.MouseY;
            mousePrevX = mouseX;
            mousePrevY = mouseY;
            mouseDeltaX = 0;
            mouseDeltaY = 0;
        }

        public void GrabMouse()
        {
            if (!mouseGrabbed)
            {
                //inputState.CursorState = CursorState.Grabbed;
                mouseGrabbed = true;
                mouseX = inputState.MouseX;
                mouseY = inputState.MouseY;
                mousePrevX = mouseX;
                mousePrevY = mouseY;
                mouseDeltaX = 0;
                mouseDeltaY = 0;
            }
        }

        public void ReleaseMouse()
        {
            if (mouseGrabbed)
            {
                //inputState.CursorState = CursorState.Normal;
                mouseGrabbed = false;
            }
        }

        private void UpdateMouse()
        {
            if (mouseGrabbed)
            {
                mousePrevX = mouseX;
                mousePrevY = mouseY;
                mouseX = inputState.MouseX;
                mouseY = inputState.MouseY;
                mouseDeltaX = mouseX - mousePrevX;
                mouseDeltaY = mouseY - mousePrevY;

                if (config.mouse_disableyaxis)
                {
                    mouseDeltaY = 0;
                }
            }
        }

        public void Dispose()
        {
            System.Diagnostics.Debug.WriteLine("Shutdown user input.");
        }

        public int MaxMouseSensitivity => 15;

        public int MouseSensitivity
        {
            get => config.mouse_sensitivity;
            set => config.mouse_sensitivity = value;
        }
    }
}
