using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		public sealed class Param
		{
			private static readonly string filepath = Path.Combine(UMMLoader.UMMLoader.PluginPath, "Params.xml");

			public int CheckUpdates = 1;

			public List<Mod> ModParams = new List<Mod>();

			public int ShortcutKeyId = 0;
			public int ShowOnStart = 1;
			public float WindowHeight;
			public float WindowWidth;

			public void Save()
			{
				try
				{
					ModParams.Clear();
					foreach (var mod in modEntries)
						ModParams.Add(new Mod { Id = mod.Info.Id, Enabled = mod.Enabled });
					using (var writer = new StreamWriter(filepath))
					{
						var serializer = new XmlSerializer(typeof(Param));
						serializer.Serialize(writer, this);
					}
				}
				catch (Exception e)
				{
					Logger.Error($"Can't write file '{filepath}'.");
					Debug.LogException(e);
				}
			}

			public static Param Load()
			{
				if (File.Exists(filepath))
					try
					{
						using (var stream = File.OpenRead(filepath))
						{
							var serializer = new XmlSerializer(typeof(Param));
							var result = serializer.Deserialize(stream) as Param;
							foreach (var item in result.ModParams)
							{
								var mod = FindMod(item.Id);
								if (mod != null)
									mod.Enabled = item.Enabled;
							}

							return result;
						}
					}
					catch (Exception e)
					{
						Logger.Error($"Can't read file '{filepath}'.");
						Debug.LogException(e);
					}

				return new Param();
			}

			[Serializable]
			public class Mod
			{
				[XmlAttribute]
				public bool Enabled = true;

				[XmlAttribute]
				public string Id;
			}
		}
	}
}