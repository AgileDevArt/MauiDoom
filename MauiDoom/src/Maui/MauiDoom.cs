using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Runtime.ExceptionServices;
using ManagedDoom.UserInput;
using ManagedDoom.Video;
using ManagedDoom.Audio;
using CommunityToolkit.Maui.Views;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp;

namespace ManagedDoom.Maui
{
    public class MauiDoom : IDrawable, IDisposable
    {
        const int MIN_FRAME_TIME = 30; // Redraw every 30ms (~33 FPS)

        private readonly CommandLineArgs args;
        private readonly Config config;
        private readonly GameContent content;
        private readonly IInputState inputState;
        private readonly MauiVideo video;
        private readonly MauiSound sound;
        private readonly MauiMusic music;
        private readonly MauiUserInput userInput;
        private readonly Doom doom;
        private readonly IDispatcherTimer gameLoopTimer;
        private readonly int fpsScale;

        private int frameCount;
        private DateTime frameStart;
        private Exception exception;

        public MauiDoom(CommandLineArgs args, IInputState inputState, MediaElement[] channels, GraphicsView view)
            : this(args, inputState, channels)
        {
            // Set up GraphicsView for rendering
            view.Drawable = this;

            gameLoopTimer = view.Dispatcher.CreateTimer();
            gameLoopTimer.Interval = TimeSpan.FromMilliseconds(MIN_FRAME_TIME); 
            gameLoopTimer.Tick += (s, e) => view.Invalidate();
            gameLoopTimer.Start();
        }


        public MauiDoom(CommandLineArgs args, IInputState inputState, MediaElement[] channels, SKCanvasView view)
            : this(args, inputState, channels)
        {
            // Set up GraphicsView for rendering
            view.PaintSurface += onPaintSurface;

            gameLoopTimer = view.Dispatcher.CreateTimer();
            gameLoopTimer.Interval = TimeSpan.FromMilliseconds(MIN_FRAME_TIME);
            gameLoopTimer.Tick += (s, e) => view.InvalidateSurface();
            gameLoopTimer.Start();
        }

        public MauiDoom(CommandLineArgs args, IInputState inputState, MediaElement[] channels)
        {
            try
            {
                this.args = args;
                this.inputState = inputState;
                this.inputState.KeyDown += KeyDown;
                this.inputState.KeyUp += KeyUp;

                config = MauiConfigUtilities.GetConfig();
                content = new GameContent(args);

                config.video_screenwidth = Math.Clamp(config.video_screenwidth, 320, 3200);
                config.video_screenheight = Math.Clamp(config.video_screenheight, 200, 2000);

                video = new MauiVideo(config, content);

                if (!args.nosound.Present && !(args.nosfx.Present && args.nomusic.Present))
                {
                    if (!args.nosfx.Present)
                        sound = new MauiSound(config, content, channels);

                    if (!args.nomusic.Present)
                        music = MauiConfigUtilities.GetMusicInstance(config, content, channels[9]);
                }

                userInput = new MauiUserInput(config, inputState, !args.nomouse.Present);

                doom = new Doom(args, config, content, video, sound, music, userInput);

                fpsScale = args.timedemo.Present ? 1 : config.video_fpsscale;
                frameCount = -1;
            }
            catch (Exception e)
            {
                Dispose();
                ExceptionDispatchInfo.Throw(e);
            }
        }

        private void onPaintSurface(object? sender, SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs e)
        {
            try
            {
                var frameFrac = Update();
                video.Render(e, doom, frameFrac);
            }
            catch (Exception ex)
            {
                exception = ex;
                Quit(); // Trigger closing here when game crashes
            }
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            try
            {
                var frameFrac = Update();
                video.Render(canvas, doom, frameFrac);
            }
            catch (Exception e)
            {
                exception = e; 
                Quit(); // Trigger closing here when game crashes
            }
        }

        private Fixed Update()
        {
            var delta = frameStart != default ? (int)(DateTime.UtcNow - frameStart).TotalMilliseconds : MIN_FRAME_TIME;
            frameStart = DateTime.UtcNow;
            if (delta < MIN_FRAME_TIME)
            {
                Task.Delay(MIN_FRAME_TIME - delta).Wait();
                delta = MIN_FRAME_TIME;
            }

            var updateCnt = MathF.Round((float)delta / MIN_FRAME_TIME);
            for (int i = 0; i < updateCnt; i++)
            {
                if (++frameCount % fpsScale == 0 && doom.Update() == UpdateResult.Completed)            
                    Quit(); // Trigger closing here when game completes
            }

            return Fixed.FromInt(frameCount % fpsScale + 1) / fpsScale;
        }

        private void Quit()
        {
            // (This could send a message to stop the game loop, etc.)
            Application.Current.Quit();
        }

        public void Dispose()
        {
            gameLoopTimer?.Stop();
            userInput?.Dispose();
            music?.Dispose();
            sound?.Dispose();
            video?.Dispose();
            config?.Save(ConfigUtilities.GetConfigPath());
        }

        public void KeyDown(DoomKey key) => doom.PostEvent(new DoomEvent(EventType.KeyDown, key));
        public void KeyUp(DoomKey key) => doom.PostEvent(new DoomEvent(EventType.KeyUp, key));

        public string QuitMessage => doom.QuitMessage;
        public Exception Exception => exception;
    }
}
