using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		public partial class UI : MonoBehaviour
		{
			public static GUIStyle window;
			public static GUIStyle h1;
			public static GUIStyle h2;
			public static GUIStyle bold;
			private static GUIStyle settings;
			private static GUIStyle status;
			private static GUIStyle www;
			private static GUIStyle updates;

			/// <summary>
			///     [0.13.1]
			/// </summary>
			public static GUIStyle button = null;

			private static readonly string[] mCheckUpdateStrings = { "Never", "Automatic" };

			private static readonly string[] mHotkeyNames = { "CTRL+F10", "ScrollLock", "Num *", "~" };

			private static readonly string[] mShowOnStartStrings = { "No", "Yes" };

			private static int mLastWindowId;

			private readonly List<Column> mColumns = new List<Column>
			{
				new Column { name = "Name", width = 200, expand = true },
				new Column { name = "Version", width = 60 },
				new Column { name = "Requirements", width = 150, expand = true },
				new Column { name = "On/Off", width = 50 },
				new Column { name = "Status", width = 50 }
			};

			private readonly List<Column> mOriginColumns = new List<Column>
			{
				new Column { name = "Name", width = 200, expand = true },
				new Column { name = "Version", width = 60 },
				new Column { name = "Requirements", width = 150, expand = true },
				new Column { name = "On/Off", width = 50 },
				new Column { name = "Status", width = 50 }
			};

			public int globalFontSize = 13;

			private GameObject mCanvas;
			private Resolution mCurrentResolution;
			private float mExpectedUIScale = 1f;
			private Vector2 mExpectedWindowSize = Vector2.zero;

			private bool mFirstLaunched;
			private bool mInit;

			private int mPreviousShowModSettings = -1;

			private Vector2[] mScrollPosition = new Vector2[0];

			private int mShowModSettings = -1;

			private float mUIScale = 1f;
			private bool mUIScaleChanged;

			private Rect mWindowRect = new Rect(0, 0, 0, 0);
			private Vector2 mWindowSize = Vector2.zero;

			public int tabId;
			private DrawTabDelegate[] tabRenderers;
			public string[] tabs = { "Mods", "Logs", "Settings" };

			private int ShowModSettings
			{
				get => mShowModSettings;
				set
				{
					mShowModSettings = value;
					if (mShowModSettings == mPreviousShowModSettings)
						return;
					if (mShowModSettings == -1)
						HideMod(modEntries[mPreviousShowModSettings]);
					else if (mPreviousShowModSettings == -1)
						HideMod(modEntries[mShowModSettings]);
					else
					{
						HideMod(modEntries[mPreviousShowModSettings]);
						ShowMod(modEntries[mShowModSettings]);
					}

					mPreviousShowModSettings = mShowModSettings;
				}
			}

			public static UI Instance { get; private set; }

			public bool Opened { get; private set; }

			internal bool GameCursorLocked { get; set; }

			private void HideMod(ModEntry mod)
			{
				if (mod.Active && mod.OnHideGUI != null && mod.OnGUI != null)
					try
					{
						mod.OnHideGUI(mod);
					}
					catch (Exception ex)
					{
						mod.Logger.Error("OnHideGUI: " + ex.GetType().Name + " - " + ex.Message);
						Debug.LogException(ex);
					}
			}

			private void ShowMod(ModEntry mod)
			{
				if (mod.Active && mod.OnShowGUI != null && mod.OnGUI != null)
					try
					{
						mod.OnShowGUI(mod);
					}
					catch (Exception ex)
					{
						mod.Logger.Error("OnShowGUI: " + ex.GetType().Name + " - " + ex.Message);
						Debug.LogException(ex);
					}
			}

			internal static bool Load()
			{
				try
				{
					new GameObject(typeof(UI).FullName, typeof(UI));

					return true;
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}

				return false;
			}

			private void Awake()
			{
				tabRenderers = new DrawTabDelegate[] { DrawModsTab, DrawLogsTab, DrawSettingsTab };

				Instance = this;
				DontDestroyOnLoad(this);
				mWindowSize = ClampWindowSize(new Vector2(Params.WindowWidth, Params.WindowHeight));
				mExpectedWindowSize = mWindowSize;
				mUIScale = Mathf.Clamp(Params.UIScale, 0.5f, 2f);
				mExpectedUIScale = mUIScale;
				Textures.Init();
				var harmony = HarmonyInstance.Create("UnityModManager.UI");
				var original = typeof(Screen).GetMethod("set_lockCursor");
				var prefix = typeof(Screen_lockCursor_Patch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
				harmony.Patch(original, new HarmonyMethod(prefix));
			}

			private void Start()
			{
				CalculateWindowPos();
				if (string.IsNullOrEmpty(Config.UIStartingPoint))
					FirstLaunch();
				if (Params.CheckUpdates == 1)
					CheckModUpdates();
			}

			private void OnDestroy() { SaveSettingsAndParams(); }

			private void Update()
			{
				if (Opened)
				{
					Cursor.lockState = CursorLockMode.None;
					Cursor.visible = true;
				}

				float deltaTime = Time.deltaTime;
				foreach (var mod in modEntries)
					if (mod.Active && mod.OnUpdate != null)
						try
						{
							mod.OnUpdate.Invoke(mod, deltaTime);
						}
						catch (Exception e)
						{
							mod.Logger.Error($"OnUpdate: {e.GetType().Name} - {e.Message}");
							Debug.LogException(e);
						}

				var toggle = false;

				switch (Params.ShortcutKeyId)
				{
					default:
						if (Input.GetKeyUp(KeyCode.F10) && (Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftControl)))
							toggle = true;

						break;
					case 1:
						if (Input.GetKeyUp(KeyCode.ScrollLock))
							toggle = true;

						break;
					case 2:
						if (Input.GetKeyUp(KeyCode.KeypadMultiply))
							toggle = true;

						break;
					case 3:
						if (Input.GetKeyUp(KeyCode.BackQuote))
							toggle = true;

						break;
				}

				if (toggle)
					ToggleWindow();

				if (Opened && Input.GetKey(KeyCode.Escape))
					ToggleWindow();
			}

			private void FixedUpdate()
			{
				float deltaTime = Time.fixedDeltaTime;
				foreach (var mod in modEntries)
					if (mod.Active && mod.OnFixedUpdate != null)
						try
						{
							mod.OnFixedUpdate.Invoke(mod, deltaTime);
						}
						catch (Exception e)
						{
							mod.Logger.Error($"OnFixedUpdate: {e.GetType().Name} - {e.Message}");
							Debug.LogException(e);
						}
			}

			private void LateUpdate()
			{
				float deltaTime = Time.deltaTime;
				foreach (var mod in modEntries)
					if (mod.Active && mod.OnLateUpdate != null)
						try
						{
							mod.OnLateUpdate.Invoke(mod, deltaTime);
						}
						catch (Exception e)
						{
							mod.Logger.Error($"OnLateUpdate: {e.GetType().Name} - {e.Message}");
							Debug.LogException(e);
						}
			}

			private void PrepareGUI()
			{
				window = new GUIStyle { name = "umm window", normal = { background = Textures.Window } };
				window.normal.background.wrapMode = TextureWrapMode.Repeat;

				h1 = new GUIStyle { name = "umm h1", normal = { textColor = Color.white }, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

				h2 = new GUIStyle { name = "umm h2", normal = { textColor = new Color(0.6f, 0.91f, 1f) }, fontStyle = FontStyle.Bold };

				bold = new GUIStyle(GUI.skin.label) { name = "umm bold", normal = { textColor = Color.white }, fontStyle = FontStyle.Bold };

				settings = new GUIStyle { alignment = TextAnchor.MiddleCenter, stretchHeight = true };

				status = new GUIStyle { alignment = TextAnchor.MiddleCenter, stretchHeight = true };

				www = new GUIStyle { alignment = TextAnchor.MiddleCenter, stretchHeight = true };

				updates = new GUIStyle { alignment = TextAnchor.MiddleCenter, stretchHeight = true };
			}

			private void ScaleGUI()
			{
				GUI.skin.font = Font.CreateDynamicFontFromOSFont(new[] { "Arial" }, Scale(globalFontSize));
				GUI.skin.button.padding = new RectOffset(Scale(10), Scale(10), Scale(3), Scale(3));
				//GUI.skin.button.margin = RectOffset(Scale(4), Scale(2));

				GUI.skin.horizontalSlider.fixedHeight = Scale(12);
				GUI.skin.horizontalSlider.border = RectOffset(3, 0);
				GUI.skin.horizontalSlider.padding = RectOffset(Scale(-1), 0);
				GUI.skin.horizontalSlider.margin = RectOffset(Scale(4), Scale(8));

				GUI.skin.horizontalSliderThumb.fixedHeight = Scale(12);
				GUI.skin.horizontalSliderThumb.border = RectOffset(4, 0);
				GUI.skin.horizontalSliderThumb.padding = RectOffset(Scale(7), 0);
				GUI.skin.horizontalSliderThumb.margin = RectOffset(0);

				GUI.skin.toggle.margin.left = Scale(10);

				window.padding = RectOffset(Scale(5));
				h1.fontSize = Scale(16);
				h1.margin = RectOffset(Scale(0), Scale(5));
				h2.fontSize = Scale(13);
				h2.margin = RectOffset(Scale(0), Scale(3));
				button.fontSize = Scale(13);
				button.padding = RectOffset(Scale(30), Scale(5));

				var iconHeight = 28;
				settings.fixedWidth = Scale(24);
				settings.fixedHeight = Scale(iconHeight);
				status.fixedWidth = Scale(12);
				status.fixedHeight = Scale(iconHeight);
				www.fixedWidth = Scale(24);
				www.fixedHeight = Scale(iconHeight);
				updates.fixedWidth = Scale(26);
				updates.fixedHeight = Scale(iconHeight);

				mColumns.Clear();
				foreach (var column in mOriginColumns)
					mColumns.Add(new Column { name = column.name, width = Scale(column.width), expand = column.expand, skip = column.skip });
			}

			private void OnGUI()
			{
				if (!mInit)
				{
					mInit = true;
					PrepareGUI();
					ScaleGUI();
				}

				var toRemove = new List<PopupToggleGroup_GUI>(0);
				var anyRendered = false;
				foreach (var item in PopupToggleGroup_GUI.mList)
				{
					item.mDestroyCounter.Add(Time.frameCount);
					if (item.mDestroyCounter.Count > 1)
					{
						toRemove.Add(item);
						continue;
					}

					if (item.Opened && !anyRendered)
					{
						item.Render();
						anyRendered = true;
					}
				}

				foreach (var item in toRemove)
					PopupToggleGroup_GUI.mList.Remove(item);

				if (Opened)
				{
					if (mCurrentResolution.width != Screen.currentResolution.width || mCurrentResolution.height != Screen.currentResolution.height)
					{
						mCurrentResolution = Screen.currentResolution;
						CalculateWindowPos();
					}

					if (mUIScaleChanged)
					{
						mUIScaleChanged = false;
						ScaleGUI();
					}

					var backgroundColor = GUI.backgroundColor;
					var color = GUI.color;
					GUI.backgroundColor = Color.white;
					GUI.color = Color.white;
					mWindowRect = GUILayout.Window(0, mWindowRect, WindowFunction, "", window, GUILayout.Height(mWindowSize.y));
					GUI.backgroundColor = backgroundColor;
					GUI.color = color;
				}
			}

			public static int Scale(int value)
			{
				if (!Instance)
					return value;
				return (int)(value * Instance.mUIScale);
			}

			private float Scale(float value)
			{
				if (!Instance)
					return value;
				return value * mUIScale;
			}

			private void CalculateWindowPos()
			{
				mWindowSize = ClampWindowSize(mWindowSize);
				mWindowRect = new Rect((Screen.width - mWindowSize.x) / 2f, (Screen.height - mWindowSize.y) / 2f, 0, 0);
			}

			private Vector2 ClampWindowSize(Vector2 orig) { return new Vector2(Mathf.Clamp(orig.x, Mathf.Min(960, Screen.width), Screen.width), Mathf.Clamp(orig.y, Mathf.Min(720, Screen.height), Screen.height)); }

			private void WindowFunction(int windowId)
			{
				if (Input.GetKey(KeyCode.LeftControl))
					GUI.DragWindow(mWindowRect);

				UnityAction buttons = () => { };

				GUILayout.Label("Mod Manager " + version, h1);

				GUILayout.Space(3);

				tabId = GUILayout.Toolbar(tabId, tabs, button, GUILayout.ExpandWidth(false));

				GUILayout.Space(5);

				if (mScrollPosition.Length != tabs.Length)
					mScrollPosition = new Vector2[tabs.Length];

				tabRenderers[tabId](ref buttons);

				GUILayout.FlexibleSpace();
				GUILayout.Space(5);
				GUILayout.BeginHorizontal();
				if (GUILayout.Button("Close", button, GUILayout.ExpandWidth(false)))
					ToggleWindow();

				if (GUILayout.Button("Save", button, GUILayout.ExpandWidth(false)))
					SaveSettingsAndParams();

				buttons();
				GUILayout.EndHorizontal();
			}

			private void DrawModsTab(ref UnityAction buttons)
			{
				var minWidth = GUILayout.MinWidth(mWindowSize.x);

				mScrollPosition[tabId] = GUILayout.BeginScrollView(mScrollPosition[tabId], minWidth, GUILayout.ExpandHeight(false));

				float amountWidth = mColumns.Where(x => !x.skip).Sum(x => x.width);
				float expandWidth = mColumns.Where(x => x.expand && !x.skip).Sum(x => x.width);

				var mods = modEntries;
				var colWidth = mColumns.Select(x => x.expand ? GUILayout.Width(x.width / expandWidth * (mWindowSize.x - 60 + expandWidth - amountWidth)) : GUILayout.Width(x.width)).ToArray();

				GUILayout.BeginVertical("box");

				GUILayout.BeginHorizontal("box");
				for (var i = 0; i < mColumns.Count; i++)
				{
					if (mColumns[i].skip)
						continue;
					GUILayout.Label(mColumns[i].name, colWidth[i]);
				}

				GUILayout.EndHorizontal();

				for (int i = 0, c = mods.Count; i < c; i++)
				{
					int col = -1;
					GUILayout.BeginVertical("box");
					GUILayout.BeginHorizontal();

					GUILayout.BeginHorizontal(colWidth[++col]);
					if (mods[i].OnGUI != null || mods[i].CanReload)
					{
						if (GUILayout.Button(mods[i].Info.DisplayName, GUI.skin.label, GUILayout.ExpandWidth(true)))
							ShowModSettings = ShowModSettings == i ? -1 : i;

						if (GUILayout.Button(mShowModSettings == i ? Textures.SettingsActive : Textures.SettingsNormal, settings))
							ShowModSettings = ShowModSettings == i ? -1 : i;
					}
					else
						GUILayout.Label(mods[i].Info.DisplayName);

					if (!string.IsNullOrEmpty(mods[i].Info.HomePage))
					{
						GUILayout.Space(10);
						if (GUILayout.Button(Textures.WWW, www))
							Application.OpenURL(mods[i].Info.HomePage);
					}

					if (mods[i].NewestVersion != null)
					{
						GUILayout.Space(10);
						GUILayout.Box(Textures.Updates, updates);
					}

					GUILayout.Space(20);

					GUILayout.EndHorizontal();

					GUILayout.BeginHorizontal(colWidth[++col]);
					GUILayout.Label(mods[i].Info.Version, GUILayout.ExpandWidth(false));
					GUILayout.EndHorizontal();

					if (mods[i].ManagerVersion > GetVersion())
						GUILayout.Label("<color=\"#CD5C5C\">Manager-" + mods[i].Info.ManagerVersion + "</color>", colWidth[++col]);
					else if (mods[i].Requirements.Count > 0)
						foreach (var item in mods[i].Requirements)
						{
							string id = item.Key;
							var mod = FindMod(id);
							GUILayout.Label((mod == null || item.Value != null && item.Value > mod.Version || !mod.Active) && mods[i].Active ? "<color=\"#CD5C5C\">" + id + "</color>" : id, colWidth[++col]);
						}
					else if (gameVersion != VER_0 && mods[i].GameVersion > gameVersion)
						GUILayout.Label("<color=\"#CD5C5C\">Game-" + mods[i].Info.GameVersion + "</color>", colWidth[++col]);
					else if (!string.IsNullOrEmpty(mods[i].CustomRequirements))
						GUILayout.Label(mods[i].CustomRequirements, colWidth[++col]);
					else
						GUILayout.Label("-", colWidth[++col]);

					bool action = mods[i].Enabled;
					action = GUILayout.Toggle(action, "", colWidth[++col]);
					if (action != mods[i].Enabled)
					{
						mods[i].Enabled = action;
						if (mods[i].Toggleable)
							mods[i].Active = action;
						else if (action && !mods[i].Loaded)
							mods[i].Active = true;
					}

					if (mods[i].Active)
						GUILayout.Box(mods[i].Enabled ? Textures.StatusActive : Textures.StatusNeedRestart, status);
					else
						GUILayout.Box(!mods[i].Enabled ? Textures.StatusInactive : Textures.StatusNeedRestart, status);

					if (mods[i].ErrorOnLoading)
						GUILayout.Label("!!!");

					GUILayout.EndHorizontal();

					if (ShowModSettings == i)
					{
						if (mods[i].CanReload)
						{
							GUILayout.Label("Debug", h2);
							if (GUILayout.Button("Reload", button, GUILayout.ExpandWidth(false)))
								mods[i].Reload();
							GUILayout.Space(5);
						}

						if (mods[i].Active && mods[i].OnGUI != null)
						{
							GUILayout.Label("Options", h2);
							try
							{
								mods[i].OnGUI(mods[i]);
							}
							catch (Exception e)
							{
								ShowModSettings = -1;
								mods[i].Logger.Error("OnGUI: " + e.GetType().Name + " - " + e.Message);
								Debug.LogException(e);
							}
						}
					}

					GUILayout.EndVertical();
				}

				GUILayout.EndVertical();

				GUILayout.EndScrollView();

				GUILayout.Space(10);

				GUILayout.BeginHorizontal();
				GUILayout.Space(10);
				GUILayout.Box(Textures.SettingsNormal, settings);
				GUILayout.Space(3);
				GUILayout.Label("Options", GUILayout.ExpandWidth(false));
				GUILayout.Space(15);
				GUILayout.Box(Textures.WWW, www);
				GUILayout.Space(3);
				GUILayout.Label("Home page", GUILayout.ExpandWidth(false));
				GUILayout.Space(15);
				GUILayout.Box(Textures.Updates, updates);
				GUILayout.Space(3);
				GUILayout.Label("Available update", GUILayout.ExpandWidth(false));
				GUILayout.Space(15);
				GUILayout.Box(Textures.StatusActive, status);
				GUILayout.Space(3);
				GUILayout.Label("Active", GUILayout.ExpandWidth(false));
				GUILayout.Space(10);
				GUILayout.Box(Textures.StatusInactive, status);
				GUILayout.Space(3);
				GUILayout.Label("Inactive", GUILayout.ExpandWidth(false));
				GUILayout.Space(10);
				GUILayout.Box(Textures.StatusNeedRestart, status);
				GUILayout.Space(3);
				GUILayout.Label("Need restart", GUILayout.ExpandWidth(false));
				GUILayout.Space(10);
				GUILayout.Label("!!!", GUILayout.ExpandWidth(false));
				GUILayout.Space(3);
				GUILayout.Label("Errors", GUILayout.ExpandWidth(false));
				GUILayout.Space(10);
				GUILayout.Label("[CTRL + LClick]", bold, GUILayout.ExpandWidth(false));
				GUILayout.Space(3);
				GUILayout.Label("Drag window", GUILayout.ExpandWidth(false));
				GUILayout.EndHorizontal();

				if (GUI.changed) { }
			}

			private void DrawLogsTab(ref UnityAction buttons)
			{
				var minWidth = GUILayout.MinWidth(mWindowSize.x);

				mScrollPosition[tabId] = GUILayout.BeginScrollView(mScrollPosition[tabId], minWidth);

				GUILayout.BeginVertical("box");

				for (int c = Logger.history.Count, i = Mathf.Max(0, c - Logger.historyCapacity); i < c; i++)
					GUILayout.Label(Logger.history[i]);

				GUILayout.EndVertical();
				GUILayout.EndScrollView();

				buttons += delegate
				{
					if (GUILayout.Button("Clear", button, GUILayout.ExpandWidth(false)))
						Logger.Clear();
					if (GUILayout.Button("Open detailed log", button, GUILayout.ExpandWidth(false)))
						OpenUnityFileLog();
				};
			}

			private void DrawSettingsTab(ref UnityAction buttons)
			{
				var minWidth = GUILayout.MinWidth(mWindowSize.x);

				mScrollPosition[tabId] = GUILayout.BeginScrollView(mScrollPosition[tabId], minWidth);

				GUILayout.BeginVertical("box");

				GUILayout.BeginHorizontal();
				GUILayout.Label("Hotkey", GUILayout.ExpandWidth(false));
				ToggleGroup(Params.ShortcutKeyId, mHotkeyNames, i => { Params.ShortcutKeyId = i; }, null, GUILayout.ExpandWidth(false));
				GUILayout.EndHorizontal();

				GUILayout.Space(5);

				GUILayout.BeginHorizontal();
				GUILayout.Label("Check updates", GUILayout.ExpandWidth(false));
				ToggleGroup(Params.CheckUpdates, mCheckUpdateStrings, i => { Params.CheckUpdates = i; }, null, GUILayout.ExpandWidth(false));
				GUILayout.EndHorizontal();

				GUILayout.Space(5);

				GUILayout.BeginHorizontal();
				GUILayout.Label("Show this window on startup", GUILayout.ExpandWidth(false));
				ToggleGroup(Params.ShowOnStart, mShowOnStartStrings, i => { Params.ShowOnStart = i; }, null, GUILayout.ExpandWidth(false));
				GUILayout.EndHorizontal();

				GUILayout.Space(5);

				GUILayout.BeginVertical("box");
				GUILayout.Label("Window size", bold, GUILayout.ExpandWidth(false));
				GUILayout.BeginHorizontal();
				GUILayout.Label("Width ", GUILayout.ExpandWidth(false));
				mExpectedWindowSize.x = GUILayout.HorizontalSlider(mExpectedWindowSize.x, Mathf.Min(Screen.width, 960), Screen.width, GUILayout.Width(200));
				GUILayout.Label(" " + mExpectedWindowSize.x.ToString("f0") + " px ", GUILayout.ExpandWidth(false));
				GUILayout.EndHorizontal();
				GUILayout.BeginHorizontal();
				GUILayout.Label("Height", GUILayout.ExpandWidth(false));
				mExpectedWindowSize.y = GUILayout.HorizontalSlider(mExpectedWindowSize.y, Mathf.Min(Screen.height, 720), Screen.height, GUILayout.Width(200));
				GUILayout.Label(" " + mExpectedWindowSize.y.ToString("f0") + " px ", GUILayout.ExpandWidth(false));
				GUILayout.EndHorizontal();
				if (GUILayout.Button("Apply", button, GUILayout.ExpandWidth(false)))
				{
					mWindowSize.x = Mathf.Floor(mExpectedWindowSize.x) % 2 > 0 ? Mathf.Ceil(mExpectedWindowSize.x) : Mathf.Floor(mExpectedWindowSize.x);
					mWindowSize.y = Mathf.Floor(mExpectedWindowSize.y) % 2 > 0 ? Mathf.Ceil(mExpectedWindowSize.y) : Mathf.Floor(mExpectedWindowSize.y);
					CalculateWindowPos();
					Params.WindowWidth = mWindowSize.x;
					Params.WindowHeight = mWindowSize.y;
				}

				GUILayout.EndVertical();

				GUILayout.Space(5);

				GUILayout.BeginVertical("box");
				GUILayout.Label("UI", bold, GUILayout.ExpandWidth(false));
				GUILayout.BeginHorizontal();
				GUILayout.Label("Scale", GUILayout.ExpandWidth(false), GUILayout.ExpandWidth(false));
				mExpectedUIScale = GUILayout.HorizontalSlider(mExpectedUIScale, 0.5f, 2f, GUILayout.Width(200));
				GUILayout.Label(" " + mExpectedUIScale.ToString("f2"), GUILayout.ExpandWidth(false));
				GUILayout.EndHorizontal();
				if (GUILayout.Button("Apply", button, GUILayout.ExpandWidth(false)))
					if (mUIScale != mExpectedUIScale)
					{
						mUIScaleChanged = true;
						mUIScale = mExpectedUIScale;
						Params.UIScale = mUIScale;
					}

				GUILayout.EndVertical();

				GUILayout.EndVertical();
				GUILayout.EndScrollView();
			}

			public void FirstLaunch()
			{
				if (mFirstLaunched || Params.ShowOnStart == 0 && modEntries.All(x => !x.ErrorOnLoading))
					return;

				ToggleWindow(true);
			}

			public void ToggleWindow() { ToggleWindow(!Opened); }

			public void ToggleWindow(bool open)
			{
				if (open == Opened)
					return;

				if (open)
					mFirstLaunched = true;

				if (!open)
				{
					int i = ShowModSettings;
					ShowModSettings = -1;
					mShowModSettings = i;
				}
				else
					ShowModSettings = mShowModSettings;

				try
				{
					Opened = open;
					BlockGameUI(open);
					if (open)
					{
						GameCursorLocked = Cursor.lockState == CursorLockMode.Locked || !Cursor.visible;
						if (!GameCursorLocked)
							return;
						Cursor.visible = true;
						Cursor.lockState = CursorLockMode.None;
					}
					else
					{
						if (!GameCursorLocked)
							return;
						Cursor.visible = false;
						Cursor.lockState = CursorLockMode.Locked;
					}
				}
				catch (Exception e)
				{
					Logger.Error("ToggleWindow: " + e.GetType().Name + " - " + e.Message);
					Debug.LogException(e);
				}
			}

			private void BlockGameUI(bool value)
			{
				if (value)
				{
					mCanvas = new GameObject("", typeof(Canvas), typeof(GraphicRaycaster));
					mCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
					mCanvas.GetComponent<Canvas>().sortingOrder = short.MaxValue;
					DontDestroyOnLoad(mCanvas);
					var panel = new GameObject("", typeof(Image));
					panel.transform.SetParent(mCanvas.transform);
					panel.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0);
					panel.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
					panel.GetComponent<RectTransform>().offsetMin = Vector2.zero;
					panel.GetComponent<RectTransform>().offsetMax = Vector2.zero;
				}
				else
				{
					if (mCanvas)
						Destroy(mCanvas);
				}
			}

			private static RectOffset RectOffset(int value) { return new RectOffset(value, value, value, value); }

			private static RectOffset RectOffset(int x, int y) { return new RectOffset(x, x, y, y); }

			public static int GetNextWindowId() { return ++mLastWindowId; }

			private delegate void DrawTabDelegate(ref UnityAction buttons);

			private class Column
			{
				public bool expand;
				public string name;
				public bool skip;
				public float width;
			}
		}

		private static class Screen_lockCursor_Patch
		{
			private static bool Prefix(bool value)
			{
				if (UI.Instance == null || !UI.Instance.Opened)
					return true;

				UI.Instance.GameCursorLocked = value;
				Cursor.visible = true;
				Cursor.lockState = CursorLockMode.None;
				return false;
			}
		}
	}
}