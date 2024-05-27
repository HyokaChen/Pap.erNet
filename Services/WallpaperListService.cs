using System;
using System.Collections.Generic;
using Pap.erNet.Models;
using Pap.erNet.Utils;

namespace Pap.erNet.Services;

public class WallpaperListService
{
    public IEnumerable<Wallpaper> GetItems() => new[]
    {
        new Wallpaper()
        {
            Url = "https://images.unsplash.com/photo-1716619222059-62e8670293e6?q=80&w=2787&auto=format&fit=crop&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            Author = "@ZHENYU LUO",
            ResolutionRatio = "4K"
        },
        new Wallpaper()
        {
            Url = "https://images.unsplash.com/photo-1716369967339-e40e46c00c05?q=80&w=2304&auto=format&fit=crop&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            Author = "@Victória Duarte",
            ResolutionRatio = "5K"
        },
        new Wallpaper()
        {
            Url = "https://images.unsplash.com/photo-1715521801502-42db7b79573b?w=800&auto=format&fit=crop&q=60&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxlZGl0b3JpYWwtZmVlZHwyMDZ8fHxlbnwwfHx8fHw%3D",
            Author = "@Ahmed",
            ResolutionRatio = "2K"
        }
    };
}
