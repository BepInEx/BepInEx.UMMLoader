using System;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		/// <summary>
		///     Allows reloading [0.14.1]
		/// </summary>
		[AttributeUsage(AttributeTargets.Class)]
		public class EnableReloadingAttribute : Attribute { }

		/// <summary>
		///     Copies a value from an old assembly to a new one [0.14.0]
		/// </summary>
		[AttributeUsage(AttributeTargets.Field)]
		public class SaveOnReloadAttribute : Attribute { }
	}
}