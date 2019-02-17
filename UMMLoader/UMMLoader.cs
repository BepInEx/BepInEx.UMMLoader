using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityModManagerNet;

namespace UMMLoader
{
	[BepInPlugin("org.bepinex.ummloader", "UnityModManagerLoader", "0.13.0.0")]
	public class UMMLoader : BaseUnityPlugin
	{
		internal new static ManualLogSource Logger;

		internal static readonly string PluginPath = Path.GetDirectoryName(typeof(UMMLoader).Assembly.Location);
		internal static readonly string BinariesPath = Path.Combine(PluginPath, "bin");

		public UMMLoader()
		{
			Logger = base.Logger;

			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
		}

		private Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
		{
			var name = new AssemblyName(args.Name);

			if (name.Name.Equals("UnityModManager", StringComparison.InvariantCultureIgnoreCase))
				return Assembly.GetExecutingAssembly();

			string candidate = Path.Combine(BinariesPath, $"{name.Name}.dll");

			if (File.Exists(candidate))
				return Assembly.LoadFile(candidate);

			return null;
		}

		private void Awake()
		{
			DontDestroyOnLoad(this);

			Logger.LogInfo("Initializing UMM");
			if (!UnityModManager.Initialize())
			{
				Logger.LogInfo("Failed to initialize!");
				Destroy(this);
				return;
			}

			Logger.LogInfo("Loading mods");

			UnityModManager.Start();

			if (UnityModManager.Params.ShowOnStart == 1 && UnityModManager.UI.Instance)
			{
				Logger.LogInfo("Opening GUI");
				UnityModManager.UI.Instance.FirstLaunch();
			}
		}
	}
}