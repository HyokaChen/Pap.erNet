using System;
using System.Collections.Generic;
using Pap.erNet.Models;
using Pap.erNet.Utils;

namespace Pap.erNet.Services;

public class WallpaperListService
{
    public IEnumerable<Wallpaper> GetItems() =>
        new[]
        {
            new Wallpaper()
            {
                Url =
                    "https://images.unsplash.com/photo-1716619222059-62e8670293e6?q=80&w=2787&auto=format&fit=crop&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                Author = "@mrnuclear / Unsplash",
                Thumbnail =
                    "data:image/bmp;base64,Qk32BAAAAAAAADYAAAAoAAAACAAAAAgAAAABABgAAAAAAMAAAAATCwAAEwsAAAAAAAAAAAAAU0Q3WUtAZFdNaVxRZlZJW0s5UEEvSD4wVUxFWlBJYlhRZ1tTZVhMX1FCV0s8Ukk8WFJPWlRQYFdSZFpSZVlOYlZJXlRFWlNEWFNQWVRQXVVPYVZNY1dLY1dJYVdHXlZFVE1IVk5HW09EX1FCYVJBYVNAXlI/XFE9TkE1UUM0WEcyXkowX0svXEktWEcsVUQrSDQVTTgXV0AZXUUZXUQVWD8OUTkITTYGRi0ATDIAVj0AXUIFXEEAVjoATjMASi4A",
                ResolutionRatio = "4K"
            },
            new Wallpaper()
            {
                Url =
                    "https://images.unsplash.com/photo-1716369967339-e40e46c00c05?q=80&w=2304&auto=format&fit=crop&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                Author = "@vicduarte / Unsplash",
                Thumbnail =
                    "data:image/bmp;base64,Qk32BAAAAAAAADYAAAAoAAAACAAAAAgAAAABABgAAAAAAMAAAAATCwAAEwsAAAAAAAAAAAAAlKCZlJ+WkZuSgpaVZJGfOY6oFIqqJoilurGTt66QrqaMnp6QiZmadZeiapShaZCb2MCJ1LyHyLCDuKaHqKGRm5+Xk5yVjpaM5MV+4cF72LZ4yqx8u6aFrqOLpJ6Hnph83L9y3Lxw2rZt0a5wwaZ4rZ99oJh5m5Nvw65pyrBm07Fj0a1kvaNrn5Zwio1uh4pmpZxis6FgyapczatctZ5ijIxnbIBnbYBjlZNgqJtdxadZy6pZspxegohjXHtkX3ti",
                ResolutionRatio = "5K"
            },
            new Wallpaper()
            {
                Url =
                    "https://images.unsplash.com/photo-1715521801502-42db7b79573b?w=800&auto=format&fit=crop&q=60&ixlib=rb-4.0.3&ixid=M3wxMjA3fDB8MHxlZGl0b3JpYWwtZmVlZHwyMDZ8fHxlbnwwfHx8fHw%3D",
                Author = "@mutecevvil / Unsplash",
                Thumbnail =
                    "data:image/bmp;base64,Qk32BAAAAAAAADYAAAAoAAAACAAAAAgAAAABABgAAAAAAMAAAAATCwAAEwsAAAAAAAAAAAAAx83Kw8nIvMPEucLEu8fJu8jKs7/Cpa+0wsvKvsbGtb7BsL3Bs8TJtMjNrb/GoK+4vsrLuMTGrLq+pbi/qcHJrMfQp8DLmq+8u8vPtMTJpbi/nLa/ocHLp8nUosLPlK/Au8/Ws8fPobrDmLjCn8PPpsvZoMPTj6/BvdTetM3Xor/Jmb3IocfUqc/docXVjK7BwNnkt9HdpMTPnMHMpcvYrdHgo8bXi67AwdvnuNPfpcbRncPOp83Zr9LhpMfXi67A",
                ResolutionRatio = "2K"
            }
        };
}
