using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml.Linq;

namespace JollyBit.Tools
{
	public class MergeCsSource
	{
		protected static string namespaceRegexString = @"namespace \w+(?:\.\w+)*\s*\{";
		protected readonly Regex namespaceRegex = new Regex(namespaceRegexString, RegexOptions.Singleline);
		protected static string usingRegexString = string.Format(@"(?<!{0}.*)using \w+(?:\.\w+)*;", namespaceRegexString);
		protected readonly Regex usingRegex = new Regex(usingRegexString, RegexOptions.Singleline);

		#region DoMerge
		public virtual void DoMerge(IEnumerable<string> inputFileContent, StreamWriter outputStream)
		{
			foreach (string currentFile in inputFileContent)
			{
				StringBuilder currentBuilder = new StringBuilder(currentFile);
				var usingStatements = usingRegex
					.Matches(currentFile)
					.Cast<Match>()
					.Reverse();

				//Insert using statements inside of namespace
				{
					bool foundANamespace = false;
					var usingMatchValues = usingStatements.Select(i => i.Value).ToArray();
					foreach (Match namespaceMatch in namespaceRegex.Matches(currentFile))
					{
						int insertIndex = namespaceMatch.Index + namespaceMatch.Length;
						foundANamespace = true;
						foreach (string usingMatch in usingMatchValues)
						{
							currentBuilder.Insert(insertIndex, string.Format("\r\t{0}", usingMatch));
						}
					}
					if (!foundANamespace)
					{
						throw new System.Exception(string.Format("A namespace could not be found in the file: \n{0}", currentFile));
					}
				}

				//Remove using statements outside of namespaces
				foreach (Match usingMatch in usingStatements.ToArray())
				{
					currentBuilder.Remove(usingMatch.Index, usingMatch.Length);
				}

				outputStream.Write(currentBuilder.ToString());
			}
		}
		public virtual void DoMerge(string cSharpProjectFilePath, StreamWriter outputStream)
		{
			XDocument doc = XDocument.Load(cSharpProjectFilePath);
			XNamespace ns = doc.Root.Name.Namespace;
			var includeNodes = doc
				.Descendants(ns + "ItemGroup")
				.Elements(ns + "Compile")
				.Attributes("Include")
				.Select(i => i.Value)
				.Where(i => !i.ToLower().Contains("assemblyinfo.cs"))
				.Select(i => Path.Combine(Path.GetDirectoryName(cSharpProjectFilePath), i))
				.Select(i => File.ReadAllText(i));
			DoMerge(includeNodes, outputStream);
		}
		public virtual void DoMerge(string cSharpProjectFilePath, string outputFilePath)
		{
			using (var stream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
			using (var streamWriter = new StreamWriter(stream))
			{
				DoMerge(cSharpProjectFilePath, streamWriter);
			}
		}
		#endregion

		#region ModifyCSharpProjectFile
		public void ModifyCSharpProjectFile(string oldCSharpProjectFilePath, string newCSharpProjectFilePath)
		{
			File.Copy(oldCSharpProjectFilePath, newCSharpProjectFilePath, true);
			ModifyCSharpProjectFile(newCSharpProjectFilePath);
		}
		public void ModifyCSharpProjectFile(string cSharpProjectFilePath)
		{
			var doc = XDocument.Load(cSharpProjectFilePath);
			XNamespace ns = doc.Root.Name.Namespace;
			var itemGroups = doc
				.Descendants(ns + "ItemGroup")
				.Where(i => i.Descendants(ns + "Compile").Count() > 0);
			var firstItemGroup = itemGroups.First();
			//remove compile nodes
			itemGroups.Descendants(ns + "Compile").Remove();
			//insert new compile node
			firstItemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", Path.GetFileName(Path.ChangeExtension(cSharpProjectFilePath, ".cs")))));
			//remove empty ItemGroups
			doc.Descendants(ns + "ItemGroup")
				.Where(i => i.Descendants().Count() == 0)
				.Remove();
			//del old project file and save new one!!
			File.Delete(cSharpProjectFilePath);
			File.WriteAllText(cSharpProjectFilePath, doc.ToString());
		}
		#endregion

		public virtual void ModifyCSharpProjectFileAndDoMerge(string oldCSharpProjectFilePath, string newCSharpProjectFilePath)
		{
			DoMerge(oldCSharpProjectFilePath, Path.ChangeExtension(newCSharpProjectFilePath, ".cs"));
			ModifyCSharpProjectFile(oldCSharpProjectFilePath, newCSharpProjectFilePath);
		}

		private static void Main(string[] args)
		{
			if (args.Length != 2 || string.IsNullOrEmpty(args[0]) || string.IsNullOrEmpty(args[1]))
			{
				Console.WriteLine("MergeCsSource takes a visual studio project file and merges all the referenced C# source files into a single C# source file. A modified project file is also outputted.");
				Console.WriteLine();
				Console.WriteLine("Usage: 'MergeCsSource [InputProjectFilePath] [OutputProjectFilePath]'");
				return;
			}
			#if !DEBUG
			try
			{
			#endif
				var merger = new MergeCsSource();
				merger.ModifyCSharpProjectFileAndDoMerge(args[0], args[1]);
			#if !DEBUG
			}
			catch (Exception e)
			{
				Console.WriteLine("An Exception occurred:");
				Console.WriteLine(e.ToString());
			}
			#endif
			Console.WriteLine("C# souce merged successfully!!");
		}
	}
}
