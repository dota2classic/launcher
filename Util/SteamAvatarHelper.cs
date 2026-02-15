using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using d2c_launcher.Models;

namespace d2c_launcher.Util;

public static class SteamAvatarHelper
{
    /// <summary>Creates an Avalonia bitmap from Steam RGBA avatar data, or null if invalid.</summary>
    public static Bitmap? FromUser(User? user)
    {
        if (user?.AvatarRgba == null || user.AvatarWidth <= 0 || user.AvatarHeight <= 0)
            return null;
        return FromRgba(user.AvatarRgba, user.AvatarWidth, user.AvatarHeight);
    }

    public static Bitmap? FromRgba(byte[] rgba, int width, int height)
    {
        if (rgba.Length < 4 * width * height)
            return null;
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);
        using (var fb = bitmap.Lock())
        {
            Marshal.Copy(rgba, 0, fb.Address, Math.Min(rgba.Length, 4 * width * height));
        }
        return bitmap;
    }
}
