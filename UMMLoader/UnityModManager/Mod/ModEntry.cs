using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Harmony12;
using Mono.Cecil;
using Debug = UnityEngine.Debug;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		public partial class ModEntry
		{
			private static readonly Regex RequirementPattern = new Regex(@"(.*)-(\d\.\d\.\d).*", RegexOptions.Compiled);

			/// <summary>
			///     Required game version [0.15.0]
			/// </summary>
			public readonly Version GameVersion;

			public readonly ModInfo Info;

			public readonly ModLogger Logger;

			/// <summary>
			///     Required UMM version
			/// </summary>
			public readonly Version ManagerVersion;

			private readonly Dictionary<long, MethodInfo> mCache = new Dictionary<long, MethodInfo>();

			/// <summary>
			///     Path to mod folder
			/// </summary>
			public readonly string Path;

			/// <summary>
			///     Required mods
			/// </summary>
			public readonly Dictionary<string, Version> Requirements = new Dictionary<string, Version>();

			/// <summary>
			///     Version of a mod
			/// </summary>
			public readonly Version Version;

			/// <summary>
			///     Displayed in UMM UI. Add <color></color> tag to change colors. Can be used when custom verification game version
			///     [0.15.0]
			/// </summary>
			public string CustomRequirements = string.Empty;

			/// <summary>
			///     UI checkbox
			/// </summary>
			public bool Enabled = true;

			/// <summary>
			///     Not used
			/// </summary>
			public bool HasUpdate = false;

			private bool mActive;

			private bool mFirstLoading = true;

			/// <summary>
			///     Not used
			/// </summary>
			public Version NewestVersion;

			/// <summary>
			///     Called by MonoBehaviour.FixedUpdate [0.13.0]
			/// </summary>
			public Action<ModEntry, float> OnFixedUpdate;

			/// <summary>
			///     Called by MonoBehaviour.OnGUI
			/// </summary>
			public Action<ModEntry> OnGUI;

			/// <summary>
			///     Called when closing mod GUI [0.16.0]
			/// </summary>
			public Action<ModEntry> OnHideGUI;

			/// <summary>
			///     Called by MonoBehaviour.LateUpdate [0.13.0]
			/// </summary>
			public Action<ModEntry, float> OnLateUpdate;

			/// <summary>
			///     Called when the UMM UI closes.
			/// </summary>
			public Action<ModEntry> OnSaveGUI;

			/// <summary>
			///     Called when opening mod GUI [0.16.0]
			/// </summary>
			public Action<ModEntry> OnShowGUI;

			/// <summary>
			///     Called to activate / deactivate the mod.
			/// </summary>
			public Func<ModEntry, bool, bool> OnToggle;

			/// <summary>
			///     Called to unload old data for reloading mod [0.14.0]
			/// </summary>
			public Func<ModEntry, bool> OnUnload;

			/// <summary>
			///     Called by MonoBehaviour.Update [0.13.0]
			/// </summary>
			public Action<ModEntry, float> OnUpdate;

			/// <summary>
			///     Show button to reload the mod [0.14.0]
			/// </summary>
			public bool CanReload { get; private set; }

			public Assembly Assembly { get; private set; }

			public bool Started { get; private set; }

			public bool ErrorOnLoading { get; private set; }

			/// <summary>
			///     If OnToggle exists
			/// </summary>
			public bool Toggleable => OnToggle != null;

			/// <summary>
			///     If Assembly is loaded [0.13.1]
			/// </summary>
			public bool Loaded => Assembly != null;

			public bool Active
			{
				get => mActive;
				set
				{
					if (!Started || ErrorOnLoading)
						return;

					try
					{
						if (value == mActive)
							return;

						if (value && !Loaded)
						{
							var stopwatch = Stopwatch.StartNew();
							Load();
							Logger.NativeLog($"Loading time {stopwatch.ElapsedMilliseconds / 1000f:f2} s.");
							return;
						}

						var toggled = OnToggle?.Invoke(this, value);

						if (toggled ?? true)
						{
							mActive = value;
							Logger.Log(value ? "Active." : "Inactive.");
						}
						else
							Logger.Log("Unsuccessfully.");
					}
					catch (Exception e)
					{
						Logger.Error($"OnToggle: {e.GetType().Name} - {e.Message}");
						Debug.LogException(e);
					}
				}
			}

			public ModEntry(ModInfo info, string path)
			{
				Info = info;
				Path = path;
				Logger = new ModLogger(Info.Id);
				Version = ParseVersion(info.Version);
				ManagerVersion = !string.IsNullOrEmpty(info.ManagerVersion) ? ParseVersion(info.ManagerVersion) : new Version();
				GameVersion = !string.IsNullOrEmpty(info.GameVersion) ? ParseVersion(info.GameVersion) : new Version();

				if (info.Requirements == null || info.Requirements.Length <= 0)
					return;

				foreach (string id in info.Requirements)
				{
					var match = RequirementPattern.Match(id);
					if (match.Success)
					{
						Requirements.Add(match.Groups[1].Value, ParseVersion(match.Groups[2].Value));
						continue;
					}

					if (!Requirements.ContainsKey(id))
						Requirements.Add(id, null);
				}
			}

			public bool Load()
			{
				if (Loaded)
					return !ErrorOnLoading;

				ErrorOnLoading = false;

				Logger.Log($"Version '{Info.Version}'. Loading.");
				if (string.IsNullOrEmpty(Info.AssemblyName))
				{
					ErrorOnLoading = true;
					Logger.Error($"{nameof(Info.AssemblyName)} is null.");
				}

				if (string.IsNullOrEmpty(Info.EntryMethod))
				{
					ErrorOnLoading = true;
					Logger.Error($"{nameof(Info.EntryMethod)} is null.");
				}

				if (!string.IsNullOrEmpty(Info.ManagerVersion))
					if (ManagerVersion > GetVersion())
					{
						ErrorOnLoading = true;
						Logger.Error($"Mod Manager must be version '{Info.ManagerVersion}' or higher.");
					}

				if (Requirements.Count > 0)
					foreach (var item in Requirements)
					{
						string id = item.Key;
						var mod = FindMod(id);
						if (mod == null)
						{
							ErrorOnLoading = true;
							Logger.Error($"Required mod '{id}' missing.");
						}
						else if (!mod.Active)
						{
							mod.Enabled = true;
							mod.Active = true;
							if (!mod.Active)
								Logger.Log($"Required mod '{id}' inactive.");
						}
						else if (item.Value != null && item.Value > mod.Version)
						{
							ErrorOnLoading = true;
							Logger.Error($"Required mod '{id}' must be version '{item.Value}' or higher.");
						}
					}

				if (ErrorOnLoading)
					return false;

				string assemblyPath = System.IO.Path.Combine(Path, Info.AssemblyName);

				if (File.Exists(assemblyPath))
				{
					try
					{
						string assemblyCachePath = assemblyPath;
						var cacheExists = false;

						if (mFirstLoading)
						{
							var fi = new FileInfo(assemblyPath);
							var hash = (ushort)((long)fi.LastWriteTimeUtc.GetHashCode() + version.GetHashCode() + ManagerVersion.GetHashCode()).GetHashCode();
							assemblyCachePath = assemblyPath + $".{hash}.cache";
							cacheExists = File.Exists(assemblyCachePath);

							if (!cacheExists)
								foreach (string filepath in Directory.GetFiles(Path, "*.cache"))
									try
									{
										File.Delete(filepath);
									}
									catch (Exception) { }
						}

						if (ManagerVersion >= VER_0_13)
						{
							if (mFirstLoading)
							{
								if (!cacheExists)
									File.Copy(assemblyPath, assemblyCachePath, true);
								Assembly = Assembly.LoadFile(assemblyCachePath);

								foreach (var type in Assembly.GetTypes())
									if (type.GetCustomAttributes(typeof(EnableReloadingAttribute), true).Any())
									{
										CanReload = true;
										break;
									}
							}
							else
								Assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
						}
						else
						{
							using (var asm = AssemblyDefinition.ReadAssembly(assemblyPath))
							{
								var asmName = AssemblyNameReference.Parse(Assembly.GetExecutingAssembly().FullName);
								asm.MainModule.AssemblyReferences.Add(asmName);

								foreach (var typeReference in asm.MainModule.GetTypeReferences())
									if (typeReference.FullName == "UnityModManagerNet.UnityModManager")
										typeReference.Scope = asmName;

								asm.Write(assemblyCachePath);
							}

							Assembly.LoadFile(assemblyCachePath);
						}

						mFirstLoading = false;
					}
					catch (Exception exception)
					{
						ErrorOnLoading = true;
						Logger.Error($"Error loading file '{assemblyPath}'.");
						Debug.LogException(exception);
						return false;
					}

					try
					{
						object[] param = { this };
						Type[] types = { typeof(ModEntry) };
						if (FindMethod(Info.EntryMethod, types, false) == null)
						{
							param = null;
							types = null;
						}

						if (!Invoke(Info.EntryMethod, out var result, param, types) || result != null && (bool)result == false)
						{
							ErrorOnLoading = true;
							Logger.Log("Not loaded.");
						}
					}
					catch (Exception e)
					{
						ErrorOnLoading = true;
						Logger.Log(e.ToString());
						return false;
					}

					Started = true;

					if (!ErrorOnLoading)
					{
						Active = true;
						return true;
					}
				}
				else
				{
					ErrorOnLoading = true;
					Logger.Error($"File '{assemblyPath}' not found.");
				}

				return false;
			}

			internal void Reload()
			{
				if (!Started || !CanReload)
					return;

				try
				{
					string assemblyPath = System.IO.Path.Combine(Path, Info.AssemblyName);
					var reflAssembly = Assembly.ReflectionOnlyLoad(File.ReadAllBytes(assemblyPath));
					if (reflAssembly.GetName().Version == Assembly.GetName().Version)
					{
						Logger.Log("Reload is not needed. The version is exactly the same as the previous one.");
						return;
					}
				}
				catch (Exception e)
				{
					Logger.Error(e.ToString());
					return;
				}

				if (OnSaveGUI != null)
					OnSaveGUI.Invoke(this);

				Logger.Log("Reloading...");

				if (Toggleable)
					Active = false;
				else
					mActive = false;

				try
				{
					if (!Active && (OnUnload == null || OnUnload.Invoke(this)))
					{
						mCache.Clear();
						typeof(Traverse).GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, new AccessCache());
						typeof(Harmony.Traverse).GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, new Harmony.AccessCache());

						var oldAssembly = Assembly;
						Assembly = null;
						Started = false;
						ErrorOnLoading = false;

						OnToggle = null;
						OnGUI = null;
						OnSaveGUI = null;
						OnUnload = null;
						OnUpdate = null;
						OnShowGUI = null;
						OnHideGUI = null;
						OnFixedUpdate = null;
						OnLateUpdate = null;
						CustomRequirements = null;

						if (!Load())
							return;

						foreach (var type in oldAssembly.GetTypes())
						{
							var t = Assembly.GetType(type.FullName);
							if (t == null)
								continue;
							foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(f => f.GetCustomAttributes(typeof(SaveOnReloadAttribute), true).Any()))
							{
								var f = t.GetField(field.Name);
								if (f == null)
									continue;
								Logger.Log($"Copying field '{field.DeclaringType.Name}.{field.Name}'");
								try
								{
									if (field.FieldType != f.FieldType)
									{
										if (field.FieldType.IsEnum && f.FieldType.IsEnum)
											f.SetValue(null, Convert.ToInt32(field.GetValue(null)));
									}
									else
										f.SetValue(null, field.GetValue(null));
								}
								catch (Exception ex)
								{
									Logger.Error(ex.ToString());
								}
							}
						}

						return;
					}

					if (Active)
						Logger.Log("Must be deactivated.");
				}
				catch (Exception e)
				{
					Logger.Error(e.ToString());
				}

				Logger.Log("Reloading canceled.");
			}

			public bool Invoke(string namespaceClassnameMethodname, out object result, object[] param = null, Type[] types = null)
			{
				result = null;
				try
				{
					var methodInfo = FindMethod(namespaceClassnameMethodname, types);
					if (methodInfo != null)
					{
						result = methodInfo.Invoke(null, param);
						return true;
					}
				}
				catch (Exception exception)
				{
					Logger.Error($"Error trying to call '{namespaceClassnameMethodname}'.");
					Logger.Error($"{exception.GetType().Name} - {exception.Message}");
					Debug.LogException(exception);
				}

				return false;
			}

			private MethodInfo FindMethod(string namespaceClassnameMethodname, Type[] types, bool showLog = true)
			{
				long key = namespaceClassnameMethodname.GetHashCode();
				if (types != null)
					key = types.Aggregate(key, (current, val) => current + val.GetHashCode());

				if (mCache.TryGetValue(key, out var methodInfo))
					return methodInfo;

				if (Assembly != null)
				{
					string classString = null;
					string methodString = null;
					int pos = namespaceClassnameMethodname.LastIndexOf('.');
					if (pos != -1)
					{
						classString = namespaceClassnameMethodname.Substring(0, pos);
						methodString = namespaceClassnameMethodname.Substring(pos + 1);
					}
					else
					{
						if (showLog)
							Logger.Error($"Function name error '{namespaceClassnameMethodname}'.");

						goto Exit;
					}

					var type = Assembly.GetType(classString);
					if (type != null)
					{
						if (types == null)
							types = new Type[0];

						methodInfo = type.GetMethod(methodString, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, types, new ParameterModifier[0]);
						if (methodInfo == null)
							if (showLog)
								Logger.Log(types.Length > 0 ? $"Method '{namespaceClassnameMethodname}[{string.Join(", ", types.Select(x => x.Name).ToArray())}]' not found." : $"Method '{namespaceClassnameMethodname}' not found.");
					}
					else
					{
						if (showLog)
							Logger.Error($"Class '{classString}' not found.");
					}
				}
				else
				{
					if (showLog)
						UnityModManager.Logger.Error($"Can't find method '{namespaceClassnameMethodname}'. Mod '{Info.Id}' is not loaded.");
				}

				Exit:

				mCache[key] = methodInfo;

				return methodInfo;
			}
		}
	}
}