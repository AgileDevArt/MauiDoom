using System;
using ManagedDoom.Video;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using SkiaSharp;

#if ANDROID
        using Android.Graphics;
#endif

#if WINDOWS
        using Microsoft.UI.Xaml.Media.Imaging;
        using Microsoft.Maui.Graphics;
        using System.Runtime.InteropServices.WindowsRuntime;
        using Windows.Graphics.Imaging;
#endif

namespace ManagedDoom.Maui
{
    public sealed class MauiVideo : IVideo, IDisposable
    {
        private Renderer renderer;

        private byte[] textureData;

        private SKBitmap bitmap;
        //private int textureWidth;
        //private int textureHeight;

        //private int windowWidth;
        //private int windowHeight;

        public MauiVideo(Config config, GameContent content)
        {
            try
            {
                System.Diagnostics.Debug.Write("Initialize video: ");
                this.renderer = new Renderer(config, content);
                this.bitmap = new SKBitmap(renderer.Width, renderer.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

                //textureWidth = renderer.Width;
                //textureHeight = renderer.Height;

                //windowWidth = inputState.ScreenWidth;
                //windowHeight = inputState.ScreenHeight;

                //if (config.video_highresolution)
                //{
                //    textureWidth = 512;
                //    textureHeight = 1024;
                //}
                //else
                //{
                //    textureWidth = 256;
                //    textureHeight = 512;
                //}

                textureData = new byte[4 * renderer.Width * renderer.Height];

                //Resize(window.Size.X, window.Size.Y);

                System.Diagnostics.Debug.WriteLine("OK");
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Failed");
                Dispose();
                throw;
            }
        }

        internal void Render(SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs args, Doom doom, Fixed frameFrac)
        {
            var canvas = args.Surface.Canvas;
            canvas.Clear(SKColors.Black); // Clear the canvas

            // Update texture data with the rendered frame
            renderer.Render(doom, textureData, frameFrac);
            // Transpose the texture data to correct the orientation
            textureData = Transpose(textureData, renderer.Width, renderer.Height);

            // Create an SKBitmap from the RGBA data
            var pixelsAddr = bitmap.GetPixels();
            System.Runtime.InteropServices.Marshal.Copy(textureData, 0, pixelsAddr, textureData.Length);

            // Draw the bitmap to the Skia canvas
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, args.Info.Width, args.Info.Height));
        }

        public void Render(ICanvas canvas, Doom doom, Fixed frameFrac)
        {
            // Update texture data with the rendered frame
            renderer.Render(doom, textureData, frameFrac);
            // Transpose the texture data to correct the orientation
            textureData = Transpose(textureData, renderer.Width, renderer.Height);
            DrawPixelsFast(canvas, textureData, renderer.Width, renderer.Height);
        }

        private byte[] Transpose(byte[] originalData, int width, int height)
        {
            byte[] transposedData = new byte[originalData.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIndex = (x * height + y) * 4; // Original index (RGBA)
                    int destIndex = (y * width + x) * 4; // New index after transposition

                    // Copy the pixel data (4 bytes per pixel)
                    transposedData[destIndex] = originalData[srcIndex];         // R
                    transposedData[destIndex + 1] = originalData[srcIndex + 1]; // G
                    transposedData[destIndex + 2] = originalData[srcIndex + 2]; // B
                    transposedData[destIndex + 3] = originalData[srcIndex + 3]; // A
                }
            }

            return transposedData;
        }

        public void Dispose()
        {
            System.Diagnostics.Debug.WriteLine("Shutdown video.");
        }

        public void InitializeWipe()
        {
            renderer.InitializeWipe();
        }

        public bool HasFocus()
        {
            return true;
        }

        public int WipeBandCount => renderer.WipeBandCount;
        public int WipeHeight => renderer.WipeHeight;
        public int MaxWindowSize => renderer.MaxWindowSize;
        public int WindowSize
        {
            get => renderer.WindowSize;
            set => renderer.WindowSize = value;
        }

        public bool DisplayMessage
        {
            get => renderer.DisplayMessage;
            set => renderer.DisplayMessage = value;
        }

        public int MaxGammaCorrectionLevel => renderer.MaxGammaCorrectionLevel;
        public int GammaCorrectionLevel
        {
            get => renderer.GammaCorrectionLevel;
            set => renderer.GammaCorrectionLevel = value;
        }

        public void DrawPixelsFast(ICanvas canvas, byte[] argbData, int width, int height)
        {
#if ANDROID
            // Create an Android bitmap with ARGB_8888 configuration
            var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);

            // Populate the bitmap with ARGB values
            int index = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int r = argbData[index];
                    int g = argbData[index + 1];
                    int b = argbData[index + 2];
                    int a = argbData[index + 3];

                    // Combine ARGB values into a single int
                    int color = (a << 24) | (r << 16) | (g << 8) | b;
                    bitmap.SetPixel(x, y, new Android.Graphics.Color(color));

                    index += 4; // Move to the next pixel
                }
            }

            // Create a memory stream to hold the bitmap data
            var outputStream = new MemoryStream();
            bitmap.Compress(Bitmap.CompressFormat.Png, 100, outputStream);
            outputStream.Seek(0, SeekOrigin.Begin); // Rewind the stream for reading
            canvas.DrawImage(PlatformImage.FromStream(outputStream, Microsoft.Maui.Graphics.ImageFormat.Png), 0, 0, width, height);
#endif

#if WINDOWS
            using var outputStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();

            var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, outputStream).GetAwaiter().GetResult();
            encoder.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied, (uint)width, (uint)height, 96, 96, argbData);
            encoder.FlushAsync().GetAwaiter().GetResult();

            canvas.DrawImage(PlatformImage.FromStream(outputStream.AsStreamForRead(), ImageFormat.Bmp), 0, 0, width, height);
#endif
        }
    }
}
