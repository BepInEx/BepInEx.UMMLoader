using System.Text.RegularExpressions;

namespace UnityModManagerNet
{
	public static class Injector
	{
		private static readonly Regex EntryPointPattern = new Regex(@"(?:(?<=\[)(?'assembly'.+(?>\.dll))(?=\]))|(?:(?'class'[\w|\.]+)(?=\.))|(?:(?<=\.)(?'func'\w+))|(?:(?<=\:)(?'mod'\w+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		internal static bool TryParseEntryPoint(string str, out string assembly, out string @class, out string method, out string insertionPlace)
		{
			assembly = string.Empty;
			@class = string.Empty;
			method = string.Empty;
			insertionPlace = string.Empty;

			var matches = EntryPointPattern.Matches(str);
			var groupNames = EntryPointPattern.GetGroupNames();

			if (matches.Count > 0)
				foreach (Match match in matches)
				{
					foreach (string group in groupNames)
						if (match.Groups[group].Success)
							switch (group)
							{
								case "assembly":
									assembly = match.Groups[group].Value;
									break;
								case "class":
									@class = match.Groups[group].Value;
									break;
								case "func":
									method = match.Groups[group].Value;
									if (method == "ctor" || method == "cctor")
										method = $".{method}";
									break;
								case "mod":
									insertionPlace = match.Groups[group].Value.ToLower();
									break;
							}
				}

			var hasError = false;

			if (string.IsNullOrEmpty(assembly))
			{
				hasError = true;
				UnityModManager.Logger.Error("Assembly name not found.");
			}

			if (string.IsNullOrEmpty(@class))
			{
				hasError = true;
				UnityModManager.Logger.Error("Class name not found.");
			}

			if (string.IsNullOrEmpty(method))
			{
				hasError = true;
				UnityModManager.Logger.Error("Method name not found.");
			}

			if (hasError)
			{
				UnityModManager.Logger.Error($"Error parsing EntryPoint '{str}'.");
				return false;
			}

			return true;
		}
	}
}