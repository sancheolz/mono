//
// Parameters.cs: Class that contains information about command line parameters
//
// Author:
//   Marek Sieradzki (marek.sieradzki@gmail.com)
//
// (C) 2005 Marek Sieradzki
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using System.Collections;
using System.Text;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Mono.XBuild.CommandLine {
	public class Parameters {
	
		string			consoleLoggerParameters;
		bool			displayHelp;
		bool			displayVersion;
		IList			flatArguments;
		IList			loggers;
		LoggerVerbosity		loggerVerbosity;
		bool			noConsoleLogger;
		bool			noLogo;
		string			projectFile;
		BuildPropertyGroup	properties;
		IList			remainingArguments;
		Hashtable		responseFiles;
		string[]		targets;
		bool			validate;
		string			validationSchema;
		
		string			responseFile;
	
		public Parameters (MainClass mc)
		{
			consoleLoggerParameters = "";
			displayHelp = false;
			displayVersion = true;
			loggers = new ArrayList ();
			loggerVerbosity = LoggerVerbosity.Normal;
			noConsoleLogger = false;
			noLogo = false;
			properties = new BuildPropertyGroup ();
			targets = new string [0];
			
			responseFile = Path.Combine (mc.BinPath, "xbuild.rsp");
		}
		
		public void ParseArguments (string[] args)
		{
			bool autoResponse = true;
			flatArguments = new ArrayList ();
			remainingArguments = new ArrayList ();
			responseFiles = new Hashtable ();
			foreach (string s in args) {
				if (s.StartsWith ("/noautoresponse") || s.StartsWith ("/noautorsp")) {
					autoResponse = false;
					continue;
				}
				if (s [0] != '@') {
					flatArguments.Add (s);
					continue;
				}
				string responseFilename = Path.GetFullPath (s.Substring (1));
				if (responseFiles.ContainsKey (responseFilename))
					throw new CommandLineException ("We already have " + responseFilename + "file.", 0001);
				responseFiles [responseFilename] = responseFilename;
				LoadResponseFile (responseFilename);
			}
			if (autoResponse == true) {
				// FIXME: we do not allow nested auto response file
				LoadResponseFile (responseFile);
			}
			foreach (string s in flatArguments) {
				if (s [0] == '/') {
					ParseFlatArgument (s);
				} else
					remainingArguments.Add (s);
			}
			if (remainingArguments.Count == 0) {
				string[] files = Directory.GetFiles (Directory.GetCurrentDirectory (), "*.??proj");
				if (files.Length > 0)
					projectFile = files [0];
				else
					throw new CommandLineException ("No .proj file specified and no found in current directory.", 0003);
			} else if (remainingArguments.Count == 1) {
				projectFile = (string) remainingArguments [0];
			} else {
				throw new CommandLineException ("Too many project files specified.", 0004);
			}
		}
		
		private void LoadResponseFile (string filename)
		{
			StreamReader sr = null;
			string line;
			try {
				sr = new StreamReader (filename);
                                StringBuilder sb = new StringBuilder ();

                                while ((line = sr.ReadLine ()) != null) {
                                        int t = line.Length;

                                        for (int i = 0; i < t; i++) {
                                                char c = line [i];

                                                if (c == '"' || c == '\'') {
                                                        char end = c;

                                                        for (i++; i < t; i++) {
                                                                c = line [i];

                                                                if (c == end)
                                                                        break;
                                                                sb.Append (c);
                                                        }
                                                } else if (c == ' ') {
                                                        if (sb.Length > 0) {
                                                                flatArguments.Add (sb.ToString ());
                                                                sb.Length = 0;
                                                        }
                                                } else
                                                        sb.Append (c);
                                        }
                                        if (sb.Length > 0){
                                                flatArguments.Add (sb.ToString ());
                                                sb.Length = 0;
                                        }
                                }
                        } catch (Exception ex) {
                                throw new CommandLineException ("Error during loading response file.", ex, 0002);
                        } finally {
                                if (sr != null)
                                        sr.Close ();
                        }
		}
		
		private void ParseFlatArgument (string s)
		{
			switch (s) {
			case "/help":
			case "/h":
			case "/?":
				throw new CommandLineException ("Show usage", 0006);
			case "/nologo":
				noLogo = true;
				break;
			case "/version":
			case "/ver":
				throw new CommandLineException ("Show version", 0005);
			case "/noconsolelogger":
			case "/noconlog":
				noConsoleLogger = true;
				break;
			case "/validate":
			case "/val":
				validate = true;
				break;
			default:
				if (s.StartsWith ("/target:") || s.StartsWith ("/t:")) {
					ProcessTarget (s);
				}
				if (s.StartsWith ("/property:") || s.StartsWith ("/p:")) {
					ProcessProperty (s);
				}
				if (s.StartsWith ("/logger:") || s.StartsWith ("/l:")) {
					ProcessLogger (s);
				}
				if (s.StartsWith ("/verbosity:") || s.StartsWith ("/v:")) {
					ProcessVerbosity (s);
				}
				if (s.StartsWith ("/consoleloggerparameters:") || s.StartsWith ("/clp:")) {
					ProcessConsoleLoggerParameters (s);
				}
				if (s.StartsWith ("/validate:") || s.StartsWith ("/val:")) {
					ProcessValidate (s);
				}
				break;
			}
		}
		
		internal void ProcessTarget (string s)
		{
			string[] temp = s.Split (':');
			targets = temp [1].Split (';');
		}
		
		internal void ProcessProperty (string s)
		{
			string[] parameter, splittedProperties, property;
			parameter = s.Split (':');
			splittedProperties = parameter [1].Split (';');
			foreach (string st in splittedProperties) {
				property = st.Split ('=');
				properties.AddNewProperty (property [0], property [1]);
			}
		}
		
		internal void ProcessLogger (string s)
		{
			loggers.Add (new LoggerInfo (s));
		}
		
		internal void ProcessVerbosity (string s)
		{
			string[] temp = s.Split (':');
			switch (temp [1]) {
			case "q":
			case "quiet":
				loggerVerbosity = LoggerVerbosity.Quiet;
				break;
			case "m":
			case "minimal":
				loggerVerbosity = LoggerVerbosity.Minimal;
				break;
			case "n":
			case "normal":
				loggerVerbosity = LoggerVerbosity.Normal;
				break;
			case "d":
			case "detailed":
				loggerVerbosity = LoggerVerbosity.Detailed;
				break;
			case "diag":
			case "diagnostic":
				loggerVerbosity = LoggerVerbosity.Diagnostic;
				break;
			}
		}
		
		internal void ProcessConsoleLoggerParameters (string s)
		{
			consoleLoggerParameters = s; 
		}
		
		internal void ProcessValidate (string s)
		{
			string[] temp;
			validate = true;
			temp = s.Split (':');
			validationSchema = temp [1];
		}
		public bool DisplayHelp {
			get { return displayHelp; }
		}
		
		public bool NoLogo {
			get { return noLogo; }
		}
		
		public bool DisplayVersion {
			get { return displayVersion; }
		}
		
		public string ProjectFile {
			get { return projectFile; }
		}
		
		public string[] Targets {
			get { return targets; }
		}
		
		public BuildPropertyGroup Properties {
			get { return properties; }
		}
		
		public IList Loggers {
			get { return loggers; }
		}
		
		public LoggerVerbosity LoggerVerbosity {
			get { return loggerVerbosity; }
		}
		
		public string ConsoleLoggerParameters {
			get { return consoleLoggerParameters; }
		}
		
		public bool NoConsoleLogger {
			get { return noConsoleLogger; }
		}
		
		public bool Validate {
			get { return validate; }
		}
		
		public string ValidationSchema {
			get { return validationSchema; }
		}
		
	}
}