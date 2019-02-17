using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		public partial class ModEntry
		{
			private static readonly Regex RequirementPattern = new Regex(@"(.*)-(\d\.\d\.\d).*", RegexOptions.Compiled);
			public readonly ModInfo Info;

			public readonly ModLogger Logger;

			public readonly Version ManagerVersion;

			private readonly Dictionary<long, MethodInfo> mCache = new Dictionary<long, MethodInfo>();

			public readonly string Path;

			public readonly Dictionary<string, Version> Requirements = new Dictionary<string, Version>();

			public readonly Version Version;

			public bool Enabled = true;

			public bool HasUpdate = false;

			private bool mActive;

			public Version NewestVersion;

			/// <summary>
			///     Called by MonoBehaviour.FixedUpdate
			///     Added in 0.13.0
			/// </summary>
			public Action<ModEntry, float> OnFixedUpdate = null;

			/// <summary>
			///     Called by MonoBehaviour.OnGUI
			/// </summary>
			public Action<ModEntry> OnGUI = null;

			/// <summary>
			///     Called by MonoBehaviour.LateUpdate
			///     Added in 0.13.0
			/// </summary>
			public Action<ModEntry, float> OnLateUpdate = null;

			/// <summary>
			///     Called when the UMM UI closes.
			/// </summary>
			public Action<ModEntry> OnSaveGUI = null;

			/// <summary>
			///     Called to activate / deactivate the mod.
			/// </summary>
			public Func<ModEntry, bool, bool> OnToggle = null;

			/// <summary>
			///     Called by MonoBehaviour.Update
			///     Added in 0.13.0
			/// </summary>
			public Action<ModEntry, float> OnUpdate = null;

			public Assembly Assembly { get; private set; }

			public bool Started { get; private set; }

			public bool ErrorOnLoading { get; private set; }

			public bool Toggleable => OnToggle != null;

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

						var toggled = OnToggle?.Invoke(this, value);

						if (toggled ?? true)
						{
							mActive = value;
							Logger.Log(value ? "Active." : "Inactive.");
						}
						else
						{
							Logger.Log("Unsuccessfully.");
						}
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
				ManagerVersion = !string.IsNullOrEmpty(info.ManagerVersion)
					? ParseVersion(info.ManagerVersion)
					: new Version();

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
				if (Started)
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
						if (mod?.Assembly == null)
						{
							ErrorOnLoading = true;
							Logger.Error($"Required mod '{id}' not loaded.");
						}
						else if (!mod.Active)
						{
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
						// If the supported version of the manager is new, load as-is for now
						Assembly = ManagerVersion >= VER_0_13 ? Assembly.LoadFile(assemblyPath) : Upgrades.UpgradeToVersion13(assemblyPath, version);
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

						if (!Invoke(Info.EntryMethod, out var result, param, types) ||
							result != null && (bool)result == false)
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

					if (!ErrorOnLoading && Enabled)
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

			public bool Invoke(string namespaceClassnameMethodname, out object result, object[] param = null,
				Type[] types = null)
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

						methodInfo = type.GetMethod(methodString,
							BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, types,
							new ParameterModifier[0]);
						if (methodInfo == null)
							if (showLog)
								Logger.Log(
									types.Length > 0
										? $"Method '{namespaceClassnameMethodname}[{string.Join(", ", types.Select(x => x.Name).ToArray())}]' not found."
										: $"Method '{namespaceClassnameMethodname}' not found.");
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
						UnityModManager.Logger.Error(
							$"Can't find method '{namespaceClassnameMethodname}'. Mod '{Info.Id}' is not loaded.");
				}

				Exit:

				mCache[key] = methodInfo;

				return methodInfo;
			}
		}
	}
}