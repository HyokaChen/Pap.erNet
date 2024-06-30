using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperList : UserControl
{
	public WallpaperList()
	{
		InitializeComponent();
	}

	private async void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			if (this.DataContext is WallpaperListViewModel vm)
			{
				var offset = scrollViewer.Offset.Length; // 偏移量
				var total = scrollViewer.Extent.Height; // 可滚动内容范围
				var winHeight = scrollViewer.DesiredSize.Height; // 窗体高度
				var computeHeight = total - winHeight * 2; // 计算高度
				var itemCount = (int)(offset / winHeight) * 3;
				if (offset > 0 && offset >= computeHeight)
				{
					// TODO: change load status
					vm.LoadNextWallpapersAsync();
				}
				if (offset > 0)
				{
					Debug.WriteLine(
						$"offset::{offset},extent::{total},winHeight::{winHeight},,,经过了{itemCount}个，总共有: {vm.WallpaperListItems.Count}, 是不是符合最新的url::: {vm.WallpaperListItems[itemCount].ImageSource}"
					);
					vm.LoadNextStatusAsync(itemCount);
				}
				else
				{
					Debug.WriteLine($"初次请求了吗？{vm.WallpaperListItems.Count}");
				}
			}
		}
	}
}
