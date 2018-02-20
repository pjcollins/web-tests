//
// Program.cs
//
// Author:
//       Martin Baulig <mabaul@microsoft.com>
//
// Copyright (c) 2018 Xamarin, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AsyncTests.Console;
using NDesk.Options;
using Mono.Unix;

namespace AutoProvisionTool
{
	class Program
	{
		enum Command {
			Version,
			Provision
		}

		static TextWriter Output;
		static TextWriter HtmlOutput;

		public static void Main (string[] args)
		{
			var products = new List<Product> ();
			string summaryFile = null;
			string outputFile = null;
			string htmlFile = null;
			var p = new OptionSet ();
			p.Add ("summary=", "Write summary to file", v => summaryFile = v);
			p.Add ("out=", "Write output to file", v => outputFile = v);
			p.Add ("html=", "Write html output to file", v => htmlFile = v);
			p.Add ("mono=", "Mono branch", v => products.Add (new MonoProduct (v)));
			p.Add ("xi=", "Xamarin.iOS branch", v => products.Add (new IOSProduct (v)));
			p.Add ("xm=", "Xamarin.Mac branch", v => products.Add (new MacProduct (v)));
			p.Add ("xa=", "Xamarin.Android branch", v => products.Add (new AndroidProduct (v)));

			List<string> arguments;
			try {
				arguments = p.Parse (args);
			} catch (OptionException ex) {
				Usage (ex.Message);
				return;
			}

			if (args.Length < 1) {
				Usage ("Missing command.");
				return;
			}

			Command command;
			if (!Enum.TryParse (arguments[0], true, out command)) {
				Usage ("Invalid command.");
				return;
			}

			if (outputFile != null) {
				Output = new StreamWriter (outputFile);
				Log ($"Logging output to {outputFile}.");
			}

			if (htmlFile != null) {
				HtmlOutput = new StreamWriter (htmlFile);
				HtmlOutput.WriteLine ("<h3>Provision Summary</h3>");
			}

			try {
				Run (command, products).Wait ();
			} catch {
				LogError ("Provisioning failed.");
			}

			var versionProducts = new List<Product> (products);
			if (versionProducts.Count == 0)
				versionProducts.AddRange (Product.GetDefaultProducts ());

			var shortSummary = GetVersionSummary (products, VersionFormat.Summary).Result;
			var detailedSummary = GetVersionSummary (versionProducts, VersionFormat.Normal).Result;
			var htmlSummary = GetVersionSummary (versionProducts, VersionFormat.Html).Result;

			if (summaryFile != null) {
				Log ($"Writing version summary to {summaryFile}.");
				using (var writer = new StreamWriter (summaryFile)) {
					writer.WriteLine (shortSummary);
					writer.Flush ();
				}
			}

			if (HtmlOutput != null) {
				HtmlOutput.WriteLine (htmlSummary);
			}

			Log ("Short version summary:");
			LogOutput (shortSummary);
			Log ("Detailed version summary:");
			LogOutput (detailedSummary); 

			Log ("Provisioning complete.");

			if (Output != null) {
				Output.Flush ();
				Output.Dispose ();
			}

			if (HtmlOutput != null) {
				HtmlOutput.Flush ();
				HtmlOutput.Dispose ();
			}

			void Usage (string message = null)
			{
				var me = Assembly.GetEntryAssembly ().GetName ().Name;
				using (var sw = new StringWriter ()) {
					if (!string.IsNullOrEmpty (message)) {
						sw.WriteLine ($"Usage error: {message}");
						sw.WriteLine ();
					}
					sw.WriteLine ($"Usage: {me} [options] command [args]");
					sw.WriteLine ();
					sw.WriteLine ("Options:");
					p.WriteOptionDescriptions (sw);
					sw.WriteLine ();
					var options = sw.ToString ();
					LogError (options);
				}
			}
		}

		static Task Run (Command command, IList<Product> products)
		{
			switch (command) {
			case Command.Version:
				return PrintVersions (Output ?? Console.Error, products);
			case Command.Provision:
				return ProvisionProducts (products);
			default:
				throw new InvalidOperationException ();
			}
		}

		static async Task PrintVersions (TextWriter writer, IList<Product> products)
		{
			if (products.Count == 0)
				products = Product.GetDefaultProducts ();
			foreach (var product in products) {
				var version = await product.PrintVersion (VersionFormat.Full).ConfigureAwait (false);
				writer.WriteLine ($"{product.Name}: {version}");
			}
		}

		static async Task<string> GetVersionSummary (IList<Product> products, VersionFormat format)
		{
			if (products.Count == 0)
				return "No products enabled.";
			var sb = new StringBuilder ();
			if (format == VersionFormat.Html)
				sb.AppendLine ("<ul>");
			foreach (var product in products) {
				var version = await product.PrintVersion (VersionFormat.Summary).ConfigureAwait (false);
				if (format == VersionFormat.Summary) {
					if (sb.Length > 0)
						sb.Append ("<br>");
					sb.Append ($"{product.ShortName}: {version}");
				} else if (format == VersionFormat.Html) {
					sb.AppendLine ($"<li>{product.Name}: <i>{version}</i></li>");
				} else {
					sb.AppendLine ($"{product.Name}: {version}");
				}
			}
			if (format == VersionFormat.Html)
				sb.AppendLine ("</ul>");
			return sb.ToString ();
		}

		static async Task ProvisionProducts (IList<Product> products)
		{
			foreach (var product in products) {
				var oldVersion = await product.PrintVersion (VersionFormat.Normal).ConfigureAwait (false);
				Log ($"Provisioning {product.Name} from {product.Branch}.");
				Package package;
				try {
					package = await product.Provision ();
				} catch (Exception ex) {
					LogError ($"Failed to provision {product.Name} from {product.Branch}:\n{ex}");
					throw;
				}
				var newVersion = await product.PrintVersion (VersionFormat.Normal);
				var fullVersion = await product.PrintVersion (VersionFormat.Full);
				Log ($"Old {product.Name} version: {oldVersion}");
				Log ($"New {product.Name} version: {newVersion}");
				LogHtml ($"<p>Provisioned {product.Name} version {newVersion} from " +
				         $"{BranchLink (product)} commit {CommitLink (package)}: {PackageLink (package)}.");
				LogHtml ($"<pre>{fullVersion}</pre>");
			}

			string BranchLink (Product product)
			{
				return $"<a href=\"https://github.com/{product.RepoOwner}/{product.RepoName}/commits/{product.Branch}\">{product.RepoName}/{product.Branch}</a>";
			}

			string CommitLink (Package package)
			{
				return $"<a href=\"https://github.com/{package.Product.RepoOwner}/{package.Product.RepoName}/commit/{package.Commit.Sha}\">{package.Commit.Sha.Substring (0, 7)}</a>";
			}

			string PackageLink (Package package)
			{
				return $"<a href=\"{package.TargetUri}\">{package.TargetUri}</a>";
			}
		}

		internal const string ME = "AutoProvisionTool";

		public static void Debug (string message)
		{
			Log (message);
		}

		public static void Debug (string format, params object[] args)
		{
			Log (string.Format (format, args));
		}

		public static void LogHtml (string message)
		{
			if (HtmlOutput != null)
				HtmlOutput.WriteLine (message);
		}

		public static async Task<string> RunCommandWithOutput (
			string command, string args, CancellationToken cancellationToken)
		{
			var output = await ProcessHelper.RunCommandWithOutput (
				command, args, cancellationToken).ConfigureAwait (false);
			LogOutput (output);
			return output;
		}

		public static void LogOutput (string output)
		{
			using (var reader = new StringReader (output)) {
				string line;
				while ((line = reader.ReadLine ()) != null) {
					Log ($"    {line}");
				}
			}
		}

		public static void Log (string message)
		{
			Console.Error.WriteLine ($"{ME}: {message}");
			if (Output != null)
				Output.WriteLine ($"{ME}: {message}");
		}

		public static void LogError (string message)
		{
			Console.Error.WriteLine ($"{ME} ERROR: {message}");
			if (Output != null) {
				Output.WriteLine ($"{ME} ERROR: {message}");
				Output.Flush ();
				Output.Dispose ();
			}
			if (HtmlOutput != null) {
				HtmlOutput.WriteLine ("<p>{ME} ERROR: {message}</p>");
				HtmlOutput.Flush ();
				HtmlOutput.Dispose ();
			}
			Environment.Exit (1);
		}
	}
}
