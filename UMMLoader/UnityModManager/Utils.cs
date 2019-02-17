using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		private static readonly char[] VersionSeparators = { '.', ',' };

		public static void OpenUnityFileLog()
		{
			var folders = new[] { Application.persistentDataPath, Application.dataPath };
			foreach (string folder in folders)
			{
				string filepath = Path.Combine(folder, "output_log.txt");
				if (!File.Exists(filepath))
					continue;
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
			if (array.Length >= 3)
				return new Version(int.Parse(array[0]), int.Parse(array[1]), int.Parse(array[2]));

			Logger.Error($"Error parsing version {str}");
			return new Version();
		}

		public static bool IsUnixPlatform()
		{
			var p = (int)Environment.OSVersion.Platform;
			return p == 4 || p == 6 || p == 128;
		}
	}
}