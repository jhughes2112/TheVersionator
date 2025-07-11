//-------------------
// TheVersionator
// Reachable Games
//-------------------
using CommandLine;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace TheVersionator
{
	class Program
	{
		public class Options
		{
			[Option('r', "registry", Required = true, HelpText = "Full url to the docker registry, such as https://some.registry.com/")]
			public string url { get; set; }

			[Option('c', "config", Required = true, HelpText = "Path to a config file that is simply two lines long that define the user/pass to be used when connecting to the registry.  The first line is the username, the second line is the password.")]
			public string config { get; set; }

			[Option('i', "image", Required = true, HelpText = "Image that will be used to request for version tags.")]
			public string image { get; set; }

			[Option('s', "suffix", Required = false, HelpText = "If specified, tags are only considered if the suffix matches.  If not specified, tags with suffixes are ignored.  (minor.major.patch-suffix)")]
			public string suffix { get; set; }

			[Option("minor", Required = false, HelpText = "Bumps the minor revision. (major.minor.patch-suffix)")]
			public bool minor { get; set; }

			[Option("major", Required = false, HelpText = "Bumps the major revision. (major.minor.patch-suffix)")]
			public bool major { get; set; }

			[Option("patch", Required = false, HelpText = "Bumps the patch revision. (major.minor.patch-suffix)")]
			public bool patch { get; set; }
		}

		static int resultCode = 0;
		static int Main(string[] args)
		{
			Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(Do);
			return resultCode;
		}

		// We deserialize from the docker registry into this structure.
		public class ImageRegistry
		{
			public string name { get; set; }
			public string[] tags { get; set; }
		}

		static private void Do(Options o)
		{
			string[] configFile = File.ReadAllLines(o.config);
			string username = configFile[0].Trim();
			string password = configFile[1].Trim();

			using (HttpClient c = new HttpClient())
			{
				Uri baseUri = new Uri(o.url);
				string base64UserPass = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes($"{username}:{password}"));
				c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64UserPass);
				Uri tagsUri = new Uri(baseUri, $"v2/{o.image}/tags/list");

				Task<HttpResponseMessage> response = c.GetAsync(tagsUri, HttpCompletionOption.ResponseContentRead);
				response.Wait(15000);  // 15 seconds timeout is pretty long

				if (!response.IsCompletedSuccessfully)
				{
					Console.WriteLine("Error: Connection failure");
					resultCode = 1;
					return;
				}

				string msg = response.Result.Content.ReadAsStringAsync().Result;
				ImageRegistry ir = JsonSerializer.Deserialize<ImageRegistry>(msg);  // returns null if there was no image pushed there yet

				// Filter down to only matching tags
				List<string> matchingTags = new List<string>();
				if (ir.tags!=null)
				{
					foreach (string t in ir.tags)
					{
						if (string.IsNullOrEmpty(o.suffix) && t.Contains('-')==false)  // no suffix supplied means ignore any tags with a suffix
						{
							matchingTags.Add(t);
						}
						else if (string.IsNullOrEmpty(o.suffix)==false && t.EndsWith($"-{o.suffix}"))  // suffix supplied requires the suffix in the tags
						{
							matchingTags.Add(t);
						}
					}
				}

				// See which is the highest current tag.
				int major = 0;
				int minor = 0;
				int patch = 0;
				foreach (string t in matchingTags)
				{
					int tMajor = 0;
					int tMinor = 0;
					int tPatch = 0;
					string[] tagChunks = t.Split('-', '.');
					if (tagChunks.Length>=1)
					{
						int.TryParse(tagChunks[0], out tMajor);
					}
					if (tagChunks.Length>=2)
					{
						int.TryParse(tagChunks[1], out tMinor);
					}
					if (tagChunks.Length>=3)
					{
						int.TryParse(tagChunks[2], out tPatch);
					}

					// Take only the highest semantic version
					if (tMajor>major || (tMajor==major && (tMinor>minor || (tMinor==minor && tPatch>patch))))
					{
						major = tMajor;
						minor = tMinor;
						patch = tPatch;
					}
				}

				// bump the version as directed
				if (o.major) 
				{
					major++;
					minor = 0;
					patch = 0;
				}
				else if (o.minor)
				{
					minor++;
					patch = 0;
				}
				else if (o.patch)
				{
					patch++;
				}

				string newTag = $"{major}.{minor}.{patch}" + (string.IsNullOrEmpty(o.suffix) ? "" : $"-{o.suffix}");
				Console.WriteLine(newTag);
			}
		}
	}
}
