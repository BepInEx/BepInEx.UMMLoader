using System;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		public class ModInfo : IEquatable<ModInfo>
		{
			public string AssemblyName;

			public string Author;

			public string DisplayName;

			public string EntryMethod;
			public string GameVersion;

			public string HomePage;
			public string Id;

			public string ManagerVersion;

			public string Repository;

			public string[] Requirements;

			public string Version;

			public bool Equals(ModInfo other) { return Id.Equals(other.Id); }

			public static implicit operator bool(ModInfo exists) { return exists != null; }

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj))
					return false;
				return obj is ModInfo modInfo && Equals(modInfo);
			}

			public override int GetHashCode() { return Id.GetHashCode(); }
		}
	}
}