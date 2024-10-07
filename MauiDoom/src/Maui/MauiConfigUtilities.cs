// Copyright (C) 1993-1996 Id Software, Inc.
// Copyright (C) 2019-2020 Nobuaki Tanaka
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

using CommunityToolkit.Maui.Views;
using System;
using System.IO;
using System.Reflection;

namespace ManagedDoom.Maui
{
    public static class MauiConfigUtilities
    {
        public static Config GetConfig()
        {
            var config = new Config(ConfigUtilities.GetConfigPath());

            if (!config.IsRestoredFromFile)
            {
                var vm = GetDefaultVideoMode();
                config.video_screenwidth = vm.Width;
                config.video_screenheight = vm.Height;
            }

            return config;
        }

        public static VideoMode GetDefaultVideoMode()
        {
            //TODO: get from view
            int viewWidth = 1920;
            int viewHeight = 1080;

            var baseWidth = 640;
            var baseHeight = 400;

            var currentWidth = baseWidth;
            var currentHeight = baseHeight;

            while (true)
            {
                var nextWidth = currentWidth + baseWidth;
                var nextHeight = currentHeight + baseHeight;

                if (nextWidth >= 0.9 * viewWidth || nextHeight >= 0.9 * viewHeight)
                {
                    break;
                }

                currentWidth = nextWidth;
                currentHeight = nextHeight;
            }

            return new VideoMode(currentWidth, currentHeight);
        }

        public static MauiMusic GetMusicInstance(Config config, GameContent content, MediaElement channel)
        {
            var sfPath = Path.Combine(ConfigUtilities.GetAppDataDirectory(), config.audio_soundfont);
            if (File.Exists(sfPath))
            {
                return new MauiMusic(config, content, channel, sfPath);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("SoundFont '" + config.audio_soundfont + "' was not found!");
                return null;
            }
        }

        public class VideoMode
        {
            public int Width { get; }
            public int Height { get; }

            public VideoMode(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }
    }
}
