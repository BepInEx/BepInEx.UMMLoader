using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		private static readonly char[] VersionSeparators = { '.' };

		public static void OpenUnityFileLog()
		{
			var folders = new[] { Application.persistentDataPath, Application.dataPath };
			foreach (string folder in folders)
			{
				string filepath = Path.Combine(folder, "output_log.txt");
				if (!File.Exists(filepath))
					continue;
				Thread.Sleep(500);
				Application.OpenURL(filepath);
				return;
			}
		}

		private static string EscapeVersion(string versionString)
		{
			var sb = new StringBuilder();

			foreach (char c in versionString)
				if (c == '.' || c == ',' || char.IsDigit(c))
					sb.Append(c);

			return sb.ToString();
		}

		public static Version ParseVersion(string str)
		{
			var array = EscapeVersion(str).Split(VersionSeparators, StringSplitOptions.RemoveEmptyEntries);

			switch (array.Length)
			{
				case 3: return new Version(int.Parse(array[0]), int.Parse(array[1]), int.Parse(array[2]));
				case 2: return new Version(int.Parse(array[0]), int.Parse(array[1]));
				case 1: return new Version(int.Parse(array[0]), 0);
				default:
					Logger.Error($"Error parsing version {str}");
					return new Version();
			}
		}

		public static bool IsUnixPlatform()
		{
			var p = (int)Environment.OSVersion.Platform;
			return p == 4 || p == 6 || p == 128;
		}
	}

	public static class Extensions
	{
		/// <summary>
		///     Only works on ARGB32, RGB24 and Alpha8 textures that are marked readable
		/// </summary>
		/// <param name="tex"></param>
		/// <param name="size"></param>
		public static void ResizeToIfLess(this Texture2D tex, int size)
		{
			float target = size;
			float t = Mathf.Max(tex.width / target, tex.height / target);
			if (t < 1f)
				TextureScale.Bilinear(tex, (int)(tex.width / t), (int)(tex.height / t));
		}
	}

	public static class TextureScale
	{
		private static Color[] texColors;
		private static Color[] newColors;
		private static int w;
		private static float ratioX;
		private static float ratioY;
		private static int w2;
		private static int finishCount;
		private static Mutex mutex;

		public static void Point(Texture2D tex, int newWidth, int newHeight) { ThreadedScale(tex, newWidth, newHeight, false); }

		public static void Bilinear(Texture2D tex, int newWidth, int newHeight) { ThreadedScale(tex, newWidth, newHeight, true); }

		private static void ThreadedScale(Texture2D tex, int newWidth, int newHeight, bool useBilinear)
		{
			texColors = tex.GetPixels();
			newColors = new Color[newWidth * newHeight];
			if (useBilinear)
			{
				ratioX = 1.0f / ((float)newWidth / (tex.width - 1));
				ratioY = 1.0f / ((float)newHeight / (tex.height - 1));
			}
			else
			{
				ratioX = (float)tex.width / newWidth;
				ratioY = (float)tex.height / newHeight;
			}

			w = tex.width;
			w2 = newWidth;
			int cores = Mathf.Min(SystemInfo.processorCount, newHeight);
			int slice = newHeight / cores;

			finishCount = 0;
			if (mutex == null)
				mutex = new Mutex(false);
			if (cores > 1)
			{
				var i = 0;
				ThreadData threadData;
				for (i = 0; i < cores - 1; i++)
				{
					threadData = new ThreadData(slice * i, slice * (i + 1));
					var ts = useBilinear ? BilinearScale : new ParameterizedThreadStart(PointScale);
					var thread = new Thread(ts);
					thread.Start(threadData);
				}

				threadData = new ThreadData(slice * i, newHeight);
				if (useBilinear)
					BilinearScale(threadData);
				else
					PointScale(threadData);
				while (finishCount < cores)
					Thread.Sleep(1);
			}
			else
			{
				var threadData = new ThreadData(0, newHeight);
				if (useBilinear)
					BilinearScale(threadData);
				else
					PointScale(threadData);
			}

			tex.Resize(newWidth, newHeight);
			tex.SetPixels(newColors);
			tex.Apply();

			texColors = null;
			newColors = null;
		}

		public static void BilinearScale(object obj)
		{
			var threadData = (ThreadData)obj;
			for (int y = threadData.start; y < threadData.end; y++)
			{
				var yFloor = (int)Mathf.Floor(y * ratioY);
				int y1 = yFloor * w;
				int y2 = (yFloor + 1) * w;
				int yw = y * w2;

				for (var x = 0; x < w2; x++)
				{
					var xFloor = (int)Mathf.Floor(x * ratioX);
					float xLerp = x * ratioX - xFloor;
					newColors[yw + x] = ColorLerpUnclamped(ColorLerpUnclamped(texColors[y1 + xFloor], texColors[y1 + xFloor + 1], xLerp), ColorLerpUnclamped(texColors[y2 + xFloor], texColors[y2 + xFloor + 1], xLerp), y * ratioY - yFloor);
				}
			}

			mutex.WaitOne();
			finishCount++;
			mutex.ReleaseMutex();
		}

		public static void PointScale(object obj)
		{
			var threadData = (ThreadData)obj;
			for (int y = threadData.start; y < threadData.end; y++)
			{
				int thisY = (int)(ratioY * y) * w;
				int yw = y * w2;
				for (var x = 0; x < w2; x++)
					newColors[yw + x] = texColors[(int)(thisY + ratioX * x)];
			}

			mutex.WaitOne();
			finishCount++;
			mutex.ReleaseMutex();
		}

		private static Color ColorLerpUnclamped(Color c1, Color c2, float value) { return new Color(c1.r + (c2.r - c1.r) * value, c1.g + (c2.g - c1.g) * value, c1.b + (c2.b - c1.b) * value, c1.a + (c2.a - c1.a) * value); }

		public class ThreadData
		{
			public int end;
			public int start;

			public ThreadData(int s, int e)
			{
				start = s;
				end = e;
			}
		}
	}
}