﻿using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
namespace Issyn2
{
	/// <summary>
	/// This class parses the robots.txt and determines, if a website is allowed to be parsed.
	/// Also, the class triggers processing of the Sitemaps.xml-File, if existing.
	/// The crawler support Crawl-Delay
	/// </summary>
	public class Robotstxt
	{	
		/// <summary>
		/// Determines whether this instance can be parsed the specified uri.
		/// </summary>
		/// <returns><c>true</c> if this instance can be parsed the specified uri; otherwise, <c>false</c>.</returns>
		/// <param name="uri">URI.</param>
		public bool CanBeParsed(Uri uri){
					
			if (RunParameters.Robotstxt == String.Empty)
				RunParameters.Robotstxt = new Downloader ().DownloadSite (new Uri (string.Format ("{0}://{1}/{2}", uri.Scheme, uri.Authority, "robots.txt")));
			//If the file could not be downloaded, shit happens		
			if (RunParameters.Robotstxt == string.Empty)
				return true;
			else {
				if (!RunParameters.WasSiteMapParsed)
					ParseSitemap (RunParameters.Robotstxt);
				if (Index.SitemapSeed == null) {
					//try to download a sitemap
					string sm = new Downloader ().DownloadSite (new Uri (string.Format ("{0}://{1}/{2}", uri.Scheme, uri.Authority, "sitemap.xml")));
					if (sm != string.Empty) {
						Output.Print ("[I]: Guessed a sitemap. Found one.", false);
						if (RunParameters.WasSiteMapParsed == false) {
							List<Uri> seed = new Sitemapxml ().Parse (new Downloader ().DownloadSite (new Uri (string.Format ("{0}://{1}/{2}", uri.Scheme, uri.Authority, "sitemap.xml"))));
							if (RunParameters.WasSiteMapParsed == false) {
								Output.Print ("[I]: Found a sitemap. Parsing elements...", false);
								if (Index.SitemapSeed == null)
									Index.SitemapSeed = new List<string> ();
								foreach (Uri sitemapLink in seed) {
									if (!Index.SitemapSeed.Contains (sitemapLink.ToString ())) {
										Index.SitemapSeed.Add (sitemapLink.ToString ());
									}
								}
								Output.Print (string.Format("[I]: Sitemap has {0} entries",Index.SitemapSeed.Count), false);
							}
							RunParameters.WasSiteMapParsed = true;
						}
					}
				}
				string localPath = uri.LocalPath;
				string[] matches = GetImportantRobotsPart (RunParameters.Robotstxt);
				if (matches == null) {
					Output.Print (string.Format("[E]: Malformed robots file. Aborting."), true);
					return false;
				}
				//TODO: Verbosity
				/*if (matches.Length == 0) {
					Output.Print (string.Format("[E]: Malformed robots file. No user-agent directives found. Assuming everything is allowed."), true);
				}*/
				bool isAllowed = true;
				foreach(string element in matches){
					if (element.Contains("*")){
						//TODO: Wildcard support

					}
					if (element == uri.LocalPath || uri.LocalPath.ToString().StartsWith(element)){
							//The path is not allowed
						isAllowed = false;
						break;
					}
				}
				if ( matches.Count(c => c.ToLower ().Trim ().StartsWith ("sitemap")) > 0) {
					//Sitemap

				}
				//if no rule applies, the site can be processed
				return isAllowed;
			}
		}

		/// <summary>
		/// Get the local paths which are listed in the Robots file to get disallowed
		/// </summary>
		/// <returns>The important robots part.</returns>
		/// <param name="robots">Robots.</param>
		private string[] GetImportantRobotsPart(string robots){
			string regexString = @"user-agent:\s+(?<useragent>.*)+";
			try{
				MatchCollection matches = new Regex (regexString, RegexOptions.IgnoreCase).Matches (robots);
				List<String> disallowed = new List<string>();
				for (int i = 0; i < matches.Count; i++) {
					if (matches [i].Value.ToLower ().Contains (Properties.UserAgent.ToLower ()) || matches [i].Value.Contains ("*") ) {				
						int startIndex = robots.IndexOf (matches [i].Value) + matches[i].Value.Length;
						int endIndex = (i < matches.Count -1 ) ? robots.IndexOf (matches [i + 1].Value) :  robots.Length    ; 
						string importantContent = robots.Substring (startIndex, endIndex - startIndex).Trim();
						string disallowedLocalPaths = Regex.Replace(importantContent,@"Disallow:\s+","");
						disallowed.AddRange(GetDisallowedPathsFromString(importantContent));							
					}
				}
				if (!Regex.IsMatch(robots,regexString))
				{
					disallowed.AddRange(GetDisallowedPathsFromString(robots));
				}
				return disallowed.ToArray();
			}
			catch{
				if (!Regex.IsMatch (robots, regexString))
					return new string[]{ };
				else
					return null;
			}
		}
		/// <summary>
		/// Gets the disallowed paths from string.
		/// Note: If a line contains a crawl-delay-directive, it will be set.
		/// </summary>
		/// <returns>The disallowed paths from string.</returns>
		/// <param name="content">Content.</param>
		public string[] GetDisallowedPathsFromString(string content){
			List<string> disallowed = new List<string> ();
			string disallowedLocalPaths = Regex.Replace(content,@"Disallow:\s+","");
			string[] paths = disallowedLocalPaths.Split ('\n');
			foreach (string s in paths) {
				string Path = s.ToLower ().Trim();
				if (!Path.StartsWith ("#")) {
					if (!Path.StartsWith ("crawl-delay") && !Path.StartsWith ("sitemap") && Path != string.Empty)
						disallowed.Add (s);
					else if (Path.StartsWith ("crawl-delay")) {
						int delay = GetCrawlDelay (Path);
						Properties.CrawlDelay = (delay != 0) ? delay : Properties.CrawlDelay;
						Properties.MaxCrawlDelay = Properties.MaxCrawlDelay + Properties.CrawlDelay;
					}	
				}
			}
			return disallowed.ToArray();
		}
		/// <summary>
		/// Triggers the inital parsing of the sitemap.
		/// </summary>
		/// <param name="robot">The content of the robots file.</param>
		private void ParseSitemap(string robot){
			string regex = @"Sitemap:\s*(?<sitemap>.*)";
			string content = robot;
		
			MatchCollection matches = Regex.Matches (content, regex, RegexOptions.IgnoreCase);
			foreach (Match m in matches) {
				string sitemapUri = m.Groups["sitemap"].Value;
				//Add the result to the seed!
				if (RunParameters.WasSiteMapParsed == false) {
					List<Uri> seed = new Sitemapxml ().Parse (new Downloader ().DownloadSite (new Uri (sitemapUri)));
					if (RunParameters.WasSiteMapParsed == false) {
						Output.Print ("[I]: Found a sitemap. Parsing elements...", false);
						if (Index.SitemapSeed == null)
							Index.SitemapSeed = new List<string> ();
						foreach (Uri sitemapLink in seed) {
							if (!Index.SitemapSeed.Contains (sitemapLink.ToString ())) {
								Index.SitemapSeed.Add (sitemapLink.ToString ());
							}
						}
						Output.Print (string.Format("[I]: Sitemap has {0} entries",Index.SitemapSeed.Count), false);
					}
				}
			}
			//Set the flag only if a Sitemap was found
			if (Regex.IsMatch(content,regex,RegexOptions.IgnoreCase))
				RunParameters.WasSiteMapParsed = true;
		}
		/// <summary>
		/// Get The Crawl-delay value
		/// </summary>
		/// <returns>The crawl delay or -1.</returns>
		/// <param name="robots">Robots.</param>
		public int GetCrawlDelay(string robots){
			string delayRegex = @"Crawl-delay:[^\d]*(?<crawl_delay>\d+)";
			MatchCollection matches = new Regex (delayRegex, RegexOptions.IgnoreCase).Matches (robots);
			if (matches.Count == 0)
				return 0;
			else
				return int.Parse (matches [0].Groups ["crawl_delay"].Value);
		}
	}
}