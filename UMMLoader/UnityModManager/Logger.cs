using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		public static class Logger
		{
			private const string Prefix = "[Manager]";
			private const string PrefixError = "[Manager] [Error]";

			internal static int historyCapacity = 200;
			internal static List<string> history = new List<string>(historyCapacity * 2);

			public static void NativeLog(string str)
			{
				NativeLog(str, Prefix);
			}

			public static void NativeLog(string str, string prefix)
			{
				Write($"{prefix} {str}");
			}

			public static void Log(string str)
			{
				Log(str, Prefix);
			}

			public static void Log(string str, string prefix)
			{
				Write($"{prefix} {str}");
			}

			public static void Error(string str)
			{
				Error(str, PrefixError);
			}

			public static void Error(string str, string prefix)
			{
				Write(prefix + str, false, true);
			}

			private static void Write(string str, bool onlyNative = false, bool error = false)
			{
				if (str == null)
					return;

				UMMLoader.UMMLoader.Logger.Log(error ? LogLevel.Error : LogLevel.Info, str);

				if (onlyNative)
					return;

				history.Add(str);

				if (history.Count >= historyCapacity * 2)
				{
					var result = history.Skip(historyCapacity);
					history.Clear();
					history.AddRange(result);
				}
			}

			public static void Clear()
			{
				history.Clear();
			}
		}
	}
}