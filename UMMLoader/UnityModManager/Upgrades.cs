using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace UnityModManagerNet
{
	public static class Upgrades
	{
		public static Assembly UpgradeToVersion13(string assemblyPath, Version version)
		{
			var fi = new FileInfo(assemblyPath);
			var hash = (uint)((long)fi.LastWriteTimeUtc.GetHashCode() + version.GetHashCode()).GetHashCode();
			string assemblyDir = Path.GetDirectoryName(assemblyPath);
			string assemblyCachePath = Path.Combine(assemblyDir, $"{Path.GetFileNameWithoutExtension(assemblyPath)}.{hash}.upgraded");

			if (File.Exists(assemblyCachePath))
				return Assembly.LoadFile(assemblyCachePath);

			foreach (string file in Directory.GetFiles(assemblyDir, "*.upgraded"))
				try
				{
					File.Delete(file);
				}
				catch { }

			using (var asm = AssemblyDefinition.ReadAssembly(assemblyPath))
			{
				var asmName = AssemblyNameReference.Parse(Assembly.GetExecutingAssembly().FullName);
				asm.MainModule.AssemblyReferences.Add(asmName);

				foreach (var typeReference in asm.MainModule.GetTypeReferences())
					if (typeReference.FullName == "UnityModManagerNet.UnityModManager")
						typeReference.Scope = asmName;

				asm.Write(assemblyCachePath);
			}

			return Assembly.LoadFile(assemblyCachePath);
		}
	}
}