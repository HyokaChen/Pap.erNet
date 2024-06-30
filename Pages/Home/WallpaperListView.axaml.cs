using System.Diagnostics;
using Avalonia.Controls;
using Pap.erNet.ViewModels;

namespace Pap.erNet.Pages.Home;

public partial class WallpaperList : UserControl
{
	public WallpaperList()
	{
		InitializeComponent();
	}

	private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			if (this.DataContext is WallpaperListViewModel vm)
			{
				var offset = scrollViewer.Offset.Length; // 偏移量
				var total = scrollViewer.Extent.Height; // 可滚动内容范围
				var winHeight = scrollViewer.DesiredSize.Height; // 窗体高度
				var computeHeight = total - winHeight * 2; // 计算高度
				if (offset > 0 && offset >= computeHeight)
				{
					// TODO: change load status
				}
			}
		}
	}
}
