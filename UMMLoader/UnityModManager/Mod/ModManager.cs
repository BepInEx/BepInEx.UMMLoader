using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		private static readonly Version VER_0 = new Version();

		private static readonly Version VER_0_13 = new Version(0, 13);

		public static readonly List<ModEntry> modEntries = new List<ModEntry>();

		internal static bool started;
		internal static bool initialized;

		/// <summary>
		///     Contains version of UnityEngine
		/// </summary>
		public static Version unityVersion { get; private set; }

		/// <summary>
		///     Contains version of a game, if configured [0.15.0]
		/// </summary>
		public static Version gameVersion { get; private set; } = new Version();

		public static Version version { get; } = typeof(UnityModManager).Assembly.GetName().Version;
		public static string modsPath { get; private set; }

		internal static Param Params { get; set; } = new Param();
		internal static GameInfo Config { get; set; } = new GameInfo();

		public static bool Initialize()
		{
			if (initialized)
				return true;

			initialized = true;

			Logger.Clear();

			Logger.Log($"Initialize. Version '{version}'.");

			unityVersion = ParseVersion(Application.unityVersion);

			Config = GameInfo.Load();
			if (Config == null)
				return false;

			Params = Param.Load();

			modsPath = Path.Combine(Environment.CurrentDirectory, Config.ModsDirectory);

			if (!Directory.Exists(modsPath))
				Directory.CreateDirectory(modsPath);

			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

			return true;
		}

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
			if (assembly != null)
				return assembly;

			var assName = new AssemblyName(args.Name);

			if (!assName.Name.StartsWith("0Harmony", StringComparison.InvariantCultureIgnoreCase))
				return null;

			string filepath = Path.Combine(UMMLoader.UMMLoader.BinariesPath, $"0Harmony-{assName.Version.Major}.{assName.Version.Minor}.dll");

			if (!File.Exists(filepath))
				return null;

			try
			{
				return Assembly.LoadFile(filepath);
			}
			catch (Exception e)
			{
				Logger.Error(e.ToString());
			}

			return null;
		}

		public static void Start()
		{
			try
			{
				_Start();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				OpenUnityFileLog();
			}
		}

		private static void ParseGameVersion()
		{
			if (string.IsNullOrEmpty(Config.GameVersionPoint))
				return;
			try
			{
				Logger.Log("Start parsing game version.");
				if (!Injector.TryParseEntryPoint(Config.GameVersionPoint, out string assembly, out string className, out string methodName, out _))
					return;
				var asm = Assembly.Load(assembly);
				if (asm == null)
				{
					Logger.Error($"File '{assembly}' not found.");
					return;
				}

				var foundClass = asm.GetType(className);
				if (foundClass == null)
				{
					Logger.Error($"Class '{className}' not found.");
					return;
				}

				var foundMethod = foundClass.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				if (foundMethod == null)
				{
					var foundField = foundClass.GetField(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
					if (foundField != null)
					{
						gameVersion = ParseVersion(foundField.GetValue(null).ToString());
						Logger.Log($"Game version detected as '{gameVersion}'.");
						return;
					}

					Logger.Error($"Method '{methodName}' not found.");
					return;
				}

				gameVersion = ParseVersion(foundMethod.Invoke(null, null).ToString());
				Logger.Log($"Game version detected as '{gameVersion}'.");
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				OpenUnityFileLog();
			}
		}

		private static void _Start()
		{
			if (!Initialize())
			{
				Logger.Log("Cancel start due to an error.");
				OpenUnityFileLog();
				return;
			}

			if (started)
			{
				Logger.Log("Cancel start. Already started.");
				return;
			}

			started = true;

			ParseGameVersion();

			if (Directory.Exists(modsPath))
			{
				Logger.Log("Parsing mods.");

				var mods = new Dictionary<string, ModEntry>();

				var countMods = 0;

				foreach (string dir in Directory.GetDirectories(modsPath))
				{
					string jsonPath = Path.Combine(dir, Config.ModInfo);
					if (!File.Exists(Path.Combine(dir, Config.ModInfo)))
						jsonPath = Path.Combine(dir, Config.ModInfo.ToLower());

					if (!File.Exists(jsonPath))
						continue;

					countMods++;
					Logger.Log($"Reading file '{jsonPath}'.");
					try
					{
						var modInfo = JsonUtility.FromJson<ModInfo>(File.ReadAllText(jsonPath));
						if (string.IsNullOrEmpty(modInfo.Id))
						{
							Logger.Error("Id is null.");
							continue;
						}

						if (mods.ContainsKey(modInfo.Id))
						{
							Logger.Error($"Id '{modInfo.Id}' already uses another mod.");
							continue;
						}

						if (string.IsNullOrEmpty(modInfo.AssemblyName))
							modInfo.AssemblyName = modInfo.Id + ".dll";

						var modEntry = new ModEntry(modInfo, dir + Path.DirectorySeparatorChar);
						mods.Add(modInfo.Id, modEntry);
					}
					catch (Exception exception)
					{
						Logger.Error($"Error parsing file '{jsonPath}'.");
						Debug.LogException(exception);
					}
				}

				if (mods.Count > 0)
				{
					Logger.Log("Sorting mods.");
					TopoSort(mods);

					Params.ReadModParams();

					Logger.Log("Loading mods.");
					foreach (var mod in modEntries)
						if (!mod.Enabled)
							mod.Logger.Log("To skip (disabled).");
						else
							mod.Active = true;
				}

				Logger.Log($"Finish. Found {countMods} mods. Successful loaded {modEntries.Count(x => !x.ErrorOnLoading)} mods.\n\n".ToUpper());
			}

			if (!UI.Load())
				Logger.Error("Can't load UI.");
		}

		private static void DFS(string id, Dictionary<string, ModEntry> mods)
		{
			if (modEntries.Any(m => m.Info.Id == id))
				return;
			foreach (string req in mods[id].Requirements.Keys)
				DFS(req, mods);
			modEntries.Add(mods[id]);
		}

		private static void TopoSort(Dictionary<string, ModEntry> mods)
		{
			foreach (string id in mods.Keys)
				DFS(id, mods);
		}

		public static ModEntry FindMod(string id) { return modEntries.FirstOrDefault(x => x.Info.Id == id); }

		public static Version GetVersion() { return version; }

		public static void SaveSettingsAndParams()
		{
			Params.Save();
			foreach (var mod in modEntries)
				if (mod.Active && mod.OnSaveGUI != null)
					try
					{
						mod.OnSaveGUI(mod);
					}
					catch (Exception e)
					{
						mod.Logger.Error($"OnSaveGUI: {e.GetType().Name} - {e.Message}");
						Debug.LogException(e);
					}
		}
	}
}