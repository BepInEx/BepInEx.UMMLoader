using System;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		[XmlRoot("Config")]
		public class GameInfo
		{
			private static readonly string filepath = Path.Combine(UMMLoader.UMMLoader.PluginPath, "Config.xml");

			public string EntryPoint;
			public string Folder;
			public string GameExe;
			public string ModInfo;
			public string ModsDirectory;

			[XmlAttribute]
			public string Name;

			public string StartingPoint;
			public string UIStartingPoint;

			public static GameInfo Load()
			{
				try
				{
					using (var stream = File.OpenRead(filepath))
					{
						return new XmlSerializer(typeof(GameInfo)).Deserialize(stream) as GameInfo;
					}
				}
				catch (Exception e)
				{
					Logger.Error($"Can't read file '{filepath}'.");
					Debug.LogException(e);
					return null;
				}
			}
		}
	}
}