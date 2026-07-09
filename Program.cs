//-------------------
// TheVersionator
// Reachable Games
//-------------------
using CommandLine;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Diagnostics.CodeAnalysis;

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

			[Option('s', "suffix", Required = false, HelpText = "If specified, tags are only considered if the suffix matches.  If not specified, tags with suffixes are ignored.  (major.minor.patch-suffix)")]
			public string suffix { get; set; }

			[Option("minor", Required = false, HelpText = "Bumps the minor revision. (major.minor.patch-suffix)")]
			public bool minor { get; set; }

			[Option("major", Required = false, HelpText = "Bumps the major revision. (major.minor.patch-suffix)")]
			public bool major { get; set; }

			[Option("patch", Required = false, HelpText = "Bumps the patch revision. (major.minor.patch-suffix)")]
			public bool patch { get; set; }
		}

		static int resultCode = 0;
		[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Options))]  // CommandLineParser fills Options via reflection, keep it intact under AOT trimming
		static int Main(string[] args)
		{
			Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(Do).WithNotParsed(errors => resultCode = 2);
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
			if (File.Exists(o.config)==false)
			{
				Console.Error.WriteLine($"Error: Config file not found: {o.config}");
				resultCode = 1;
				return;
			}
			string[] configFile = File.ReadAllLines(o.config);
			if (configFile.Length<2)
			{
				Console.Error.WriteLine($"Error: Config file must contain two lines: username then password.  {o.config}");
				resultCode = 1;
				return;
			}
			string username = configFile[0].Trim();
			string password = configFile[1].Trim();

			using (HttpClient c = new HttpClient())
			{
				c.Timeout = TimeSpan.FromSeconds(15);
				Uri baseUri = new Uri(o.url.TrimEnd('/') + "/");  // registries without a trailing slash would otherwise lose their last path segment when combined below
				string base64UserPass = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
				c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64UserPass);

				// Fetch ALL the tags.  Some registries paginate this endpoint (sorted lexically, not by version),
				// so we must follow the Link headers or risk missing the highest version and re-issuing an old one.
				List<string> allTags = new List<string>();
				Uri tagsUri = new Uri(baseUri, $"v2/{o.image}/tags/list?n=1000");
				while (tagsUri!=null)
				{
					HttpResponseMessage response;
					try
					{
						response = c.GetAsync(tagsUri, HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult();
					}
					catch (Exception e)
					{
						Console.Error.WriteLine($"Error: Connection failure. {e.Message}");
						resultCode = 1;
						return;
					}

					if (response.IsSuccessStatusCode==false)
					{
						Console.Error.WriteLine($"Error: Registry returned {(int)response.StatusCode} {response.ReasonPhrase} for {tagsUri}");
						resultCode = 1;
						return;
					}

					string msg = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					ImageRegistry ir = JsonSerializer.Deserialize(msg, SerializationContext.Default.ImageRegistry);  // returns null if there was no image pushed there yet
					if (ir?.tags != null)
					{
						allTags.AddRange(ir.tags);
					}
					tagsUri = GetNextPageUri(baseUri, response);
				}

				// Filter down to only matching tags
				List<string> matchingTags = new List<string>();
				foreach (string t in allTags)
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

		// Registry v2 pagination: Link: </v2/name/tags/list?last=xyz&n=1000>; rel="next"
		static private Uri GetNextPageUri(Uri baseUri, HttpResponseMessage response)
		{
			if (response.Headers.TryGetValues("Link", out IEnumerable<string> links))
			{
				foreach (string link in links)
				{
					if (link.Contains("rel=\"next\""))
					{
						int start = link.IndexOf('<');
						int end = link.IndexOf('>');
						if (start>=0 && end>start+1)
						{
							return new Uri(baseUri, link.Substring(start+1, end-start-1));
						}
					}
				}
			}
			return null;
		}
	}

	// Compile-time JSON serialization so no reflection is needed under AOT.
	[JsonSerializable(typeof(Program.ImageRegistry))]
	internal partial class SerializationContext : JsonSerializerContext
	{
	}
}
