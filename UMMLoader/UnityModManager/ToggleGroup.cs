using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		public partial class UI
		{
			/// <summary>
			///     [0.16.0]
			/// </summary>
			public static void PopupToggleGroup(int selected, string[] values, Action<int> onChange, GUIStyle style = null, params GUILayoutOption[] option) { PopupToggleGroup(selected, values, onChange, null, style, option); }

			/// <summary>
			///     [0.16.0]
			/// </summary>
			public static void PopupToggleGroup(int selected, string[] values, Action<int> onChange, string title, GUIStyle style = null, params GUILayoutOption[] option)
			{
				if (values == null)
					throw new ArgumentNullException("values");
				if (onChange == null)
					throw new ArgumentNullException("onChange");
				if (values.Length == 0)
					throw new IndexOutOfRangeException();
				var needInvoke = false;
				if (selected >= values.Length)
				{
					selected = values.Length - 1;
					needInvoke = true;
				}
				else if (selected < 0)
				{
					selected = 0;
					needInvoke = true;
				}

				PopupToggleGroup_GUI obj = null;
				foreach (var item in PopupToggleGroup_GUI.mList)
					if (item.values.SequenceEqual(values))
					{
						obj = item;
						break;
					}

				if (obj == null)
					obj = new PopupToggleGroup_GUI(values);
				if (obj.newSelected != null && selected != obj.newSelected.Value && obj.newSelected.Value < values.Length)
				{
					selected = obj.newSelected.Value;
					needInvoke = true;
				}

				obj.selected = selected;
				obj.newSelected = null;
				obj.title = title;
				obj.Button(null, style, option);
				if (needInvoke)
					try
					{
						onChange.Invoke(selected);
					}
					catch (Exception e)
					{
						Logger.Error("PopupToggleGroup: " + e.GetType() + " - " + e.Message);
						Console.WriteLine(e.ToString());
					}
			}

			/// <summary>
			///     [0.16.0]
			/// </summary>
			public static void ToggleGroup(int selected, string[] values, Action<int> onChange, GUIStyle style = null, params GUILayoutOption[] option)
			{
				if (values == null)
					throw new ArgumentNullException("values");
				if (onChange == null)
					throw new ArgumentNullException("onChange");
				if (values.Length == 0)
					throw new IndexOutOfRangeException();
				var needInvoke = false;
				if (selected >= values.Length)
				{
					selected = values.Length - 1;
					needInvoke = true;
				}
				else if (selected < 0)
				{
					selected = 0;
					needInvoke = true;
				}

				var i = 0;
				foreach (string str in values)
				{
					bool prev = selected == i;
					bool value = GUILayout.Toggle(prev, str, style ?? GUI.skin.toggle, option);
					if (value && !prev)
					{
						selected = i;
						needInvoke = true;
					}

					i++;
				}

				if (needInvoke)
					try
					{
						onChange.Invoke(selected);
					}
					catch (Exception e)
					{
						Logger.Error("ToggleGroup: " + e.GetType() + " - " + e.Message);
						Console.WriteLine(e.ToString());
					}
			}

			private class PopupToggleGroup_GUI
			{
				private const int MARGIN = 100;
				internal static readonly List<PopupToggleGroup_GUI> mList = new List<PopupToggleGroup_GUI>();
				internal readonly HashSet<int> mDestroyCounter = new HashSet<int>();

				public readonly string[] values;
				private int mHeight;

				private readonly int mId;

				private bool mOpened;
				private int mRecalculateFrame;
				private Vector2 mScrollPosition;
				private int mWidth;
				private Rect mWindowRect;

				public int? newSelected;
				public int selected;
				public string title;

				private bool Recalculating => mRecalculateFrame == Time.frameCount;

				public bool Opened
				{
					get => mOpened;
					set
					{
						mOpened = value;
						if (value)
							Reset();
					}
				}

				public PopupToggleGroup_GUI(string[] values)
				{
					mId = GetNextWindowId();
					mList.Add(this);
					this.values = values;
				}

				public void Button(string text = null, GUIStyle style = null, params GUILayoutOption[] option)
				{
					mDestroyCounter.Clear();
					if (GUILayout.Button(text ?? values[selected], style ?? GUI.skin.button, option))
					{
						if (!Opened)
						{
							foreach (var popup in mList)
								popup.Opened = false;
							Opened = true;
							return;
						}

						Opened = false;
					}
				}

				public void Render()
				{
					if (Recalculating)
					{
						mWindowRect = GUILayout.Window(mId, mWindowRect, WindowFunction, "", window);
						if (mWindowRect.width > 0)
						{
							mWidth = (int)Math.Min(Math.Max(mWindowRect.width, 150), Screen.width - MARGIN);
							mHeight = (int)Math.Min(mWindowRect.height, Screen.height - MARGIN);
							mWindowRect.x = Math.Max(Screen.width - mWidth, 0) / 2;
							mWindowRect.y = Math.Max(Screen.height - mHeight, 0) / 2;
						}
					}
					else
					{
						mWindowRect = GUILayout.Window(mId, mWindowRect, WindowFunction, "", window, GUILayout.Width(mWidth), GUILayout.Height(mHeight + 10));
						GUI.BringWindowToFront(mId);
					}
				}

				private void WindowFunction(int windowId)
				{
					if (title != null)
						GUILayout.Label(title, h1);
					if (!Recalculating)
						mScrollPosition = GUILayout.BeginScrollView(mScrollPosition);
					if (values != null)
					{
						var i = 0;
						foreach (string option in values)
						{
							if (GUILayout.Button(i == selected ? "<b>" + option + "</b>" : option))
							{
								newSelected = i;
								Opened = false;
							}

							i++;
						}
					}

					if (!Recalculating)
						GUILayout.EndScrollView();
					//if (GUILayout.Button("Close", button))
					//    Opened = false;
				}

				internal void Reset()
				{
					mRecalculateFrame = Time.frameCount;
					mWindowRect = new Rect(-9000, 0, 0, 0);
				}
			}
		}
	}
}