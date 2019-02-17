using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using Ping = System.Net.NetworkInformation.Ping;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		private static void CheckModUpdates()
		{
			Logger.Log("Checking for updates.");

			if (!HasNetworkConnection())
			{
				Logger.Log("No network connection or firewall blocked.");
				return;
			}

			var urls = new HashSet<string>();

			foreach (var modEntry in modEntries)
				if (!string.IsNullOrEmpty(modEntry.Info.Repository))
					urls.Add(modEntry.Info.Repository);

			if (urls.Count > 0)
				foreach (string url in urls)
					UI.Instance.StartCoroutine(DownloadString(url, ParseRepository));
		}

		private static void ParseRepository(string json, string url)
		{
			if (string.IsNullOrEmpty(json))
				return;

			try
			{
				var repository = JsonUtility.FromJson<Repository>(json);
				if (repository?.Releases == null || repository.Releases.Length <= 0)
					return;
				foreach (var release in repository.Releases)
					if (!string.IsNullOrEmpty(release.Id) && !string.IsNullOrEmpty(release.Version))
					{
						var modEntry = FindMod(release.Id);
						if (modEntry == null)
							continue;
						var ver = ParseVersion(release.Version);
						if (modEntry.Version < ver &&
							(modEntry.NewestVersion == null || modEntry.NewestVersion < ver))
							modEntry.NewestVersion = ver;
					}
			}
			catch (Exception e)
			{
				Logger.Log($"Error checking mod updates on '{url}'.");
				Logger.Log(e.Message);
			}
		}

		public static bool HasNetworkConnection()
		{
			try
			{
				using (var ping = new Ping())
				{
					return ping.Send("www.google.com.mx", 2000).Status == IPStatus.Success;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}

			return false;
		}

		private static IEnumerator DownloadString(string url, UnityAction<string, string> handler)
		{
			var www = UnityWebRequest.Get(url);

			yield return www.Send();

			var ver = ParseVersion(Application.unityVersion);
			var isError = typeof(UnityWebRequest).GetMethod(ver.Major >= 2017 ? "get_isNetworkError" : "get_isError");

			if (isError == null || (bool)isError.Invoke(www, null))
			{
				Logger.Log(www.error);
				Logger.Log($"Error downloading '{url}'.");
				yield break;
			}

			handler(www.downloadHandler.text, url);
		}
	}
}