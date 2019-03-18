using BepInEx.Logging;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		public partial class ModEntry
		{
			public class ModLogger
			{
				private readonly ManualLogSource logSource;

				public ModLogger(string Id)
				{
					// No need to unload, since UMM has no support for mod unloading
					logSource = BepInEx.Logging.Logger.CreateLogSource($"UMM_{Id}");
				}

				public void Log(string str) { logSource.LogInfo(str); }

				public void Error(string str) { logSource.LogError(str); }

				public void Critical(string str) { logSource.LogFatal(str); }

				public void Warning(string str) { logSource.LogWarning(str); }

				public void NativeLog(string str) { logSource.LogMessage(str); }
			}
		}
	}
}