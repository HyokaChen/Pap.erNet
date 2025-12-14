using System.Runtime.InteropServices;

namespace Pap.erNet.Utils
{
	/// <summary>
	/// 跨平台单例应用程序检测助手
	/// Windows: 使用 Mutex
	/// Linux/macOS: 使用文件锁
	/// </summary>
	public class SingleInstanceHelper : IDisposable
	{
		private Mutex? _mutex;
		private FileStream? _lockFileStream;
		private readonly string _identifier;
		private bool _isOwned;

		/// <summary>
		/// 创建单例检测助手
		/// </summary>
		/// <param name="identifier">应用程序唯一标识符</param>
		public SingleInstanceHelper(string identifier)
		{
			_identifier = identifier;
		}

		/// <summary>
		/// 尝试获取单例锁
		/// </summary>
		/// <returns>如果成功获取锁返回 true，否则返回 false（表示已有实例在运行）</returns>
		public bool TryAcquireLock()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return TryAcquireLockWindows();
			}
			else
			{
				return TryAcquireLockUnix();
			}
		}

		/// <summary>
		/// Windows 平台使用 Mutex
		/// </summary>
		private bool TryAcquireLockWindows()
		{
			try
			{
				_mutex = new Mutex(true, _identifier, out bool createdNew);
				_isOwned = createdNew;
				return createdNew;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Unix/Linux 平台使用文件锁
		/// </summary>
		private bool TryAcquireLockUnix()
		{
			try
			{
				// 在临时目录创建锁文件
				var tempPath = Path.GetTempPath();
				var lockFilePath = Path.Combine(tempPath, $"{_identifier}.lock");

				// 打开或创建锁文件
				_lockFileStream = new FileStream(
					lockFilePath,
					FileMode.OpenOrCreate,
					FileAccess.ReadWrite,
					FileShare.None, // 独占访问
					1,
					FileOptions.DeleteOnClose // 进程退出时自动删除
				);

				// 写入进程 ID
				var processId = Environment.ProcessId.ToString();
				var bytes = System.Text.Encoding.UTF8.GetBytes(processId);
				_lockFileStream.Write(bytes, 0, bytes.Length);
				_lockFileStream.Flush();

				_isOwned = true;
				return true;
			}
			catch (IOException)
			{
				// 文件被其他进程锁定，说明已有实例在运行
				return false;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// 释放锁
		/// </summary>
		public void Dispose()
		{
			if (!_isOwned)
				return;

			if (_mutex != null)
			{
				try
				{
					_mutex.ReleaseMutex();
					_mutex.Dispose();
				}
				catch { }
				_mutex = null;
			}

			if (_lockFileStream != null)
			{
				try
				{
					_lockFileStream.Close();
					_lockFileStream.Dispose();
				}
				catch { }
				_lockFileStream = null;
			}

			_isOwned = false;
		}
	}
}
