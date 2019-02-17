using System;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		public class ModSettings
		{
			public virtual void Save(ModEntry modEntry)
			{
				Save(this, modEntry);
			}

			public virtual string GetPath(ModEntry modEntry)
			{
				return Path.Combine(modEntry.Path, "Settings.xml");
			}

			public static void Save<T>(T data, ModEntry modEntry) where T : ModSettings, new()
			{
				string filepath = data.GetPath(modEntry);
				try
				{
					using (var writer = new StreamWriter(filepath))
					{
						var serializer = new XmlSerializer(typeof(T));
						serializer.Serialize(writer, data);
					}
				}
				catch (Exception e)
				{
					modEntry.Logger.Error($"Can't save {filepath}.");
					Debug.LogException(e);
				}
			}

			public static T Load<T>(ModEntry modEntry) where T : ModSettings, new()
			{
				var t = new T();
				string filepath = t.GetPath(modEntry);
				if (File.Exists(filepath))
					try
					{
						using (var stream = File.OpenRead(filepath))
						{
							var serializer = new XmlSerializer(typeof(T));
							var result = (T)serializer.Deserialize(stream);
							return result;
						}
					}
					catch (Exception e)
					{
						modEntry.Logger.Error($"Can't read {filepath}.");
						Debug.LogException(e);
					}

				return t;
			}
		}
	}
}