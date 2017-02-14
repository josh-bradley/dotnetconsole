using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace LightAnchor.Extensions.RunWeb
{
    public class Program
    { 
        const string DefaultURIEnvironmentVariable = "ASPNETCORE_URLS";
        const string AspDotNetCoreDefaultPort = "5000";
        const string NowListeningOn = "Now listening on:";
        public static void Main(string[] args)
        {                        
            var options = new Options(args);
            if(options.Help) 
            {
                options.GetHelpTextLines().ForEach(Console.WriteLine);
                return;
            }
            options.OptionErrors.ForEach(Console.Error.WriteLine);            
   
            var uri = options.PortNumberProvided ? BuildUrl(options.PortNumber) : null;
            
            var process = StartDotNetRun(options.UnknownArgs, uri);
            var actualUri = string.Empty;
            var reader = process.StandardOutput;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                actualUri = GetUriFromConsoleLine(line);
                if (actualUri.Length > 0 && options.ShouldOpenBrowser)
                {
                    var launchBrowser = GetLaunchBrowserAction(); 
                    if(launchBrowser == null)
                        Console.Out.WriteLine("Unable to detect platform, cannot launch browser.");
                    else
                        launchBrowser(actualUri);
                }
                Console.Out.WriteLine(line);
            }
        }

        private static Process StartDotNetRun(IEnumerable<string> argumentsForDotNetRun, string uri)
        {
            var process = new Process();
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.Arguments = $"run {String.Join(" ", argumentsForDotNetRun)}";
            if(uri != null)
            {
                if(process.StartInfo.Environment.ContainsKey(DefaultURIEnvironmentVariable))
                    process.StartInfo.Environment[DefaultURIEnvironmentVariable] = uri;
                else
                    process.StartInfo.Environment.Add(DefaultURIEnvironmentVariable, uri);
            }

            process.Start();

            return process;
        }

        private static Action<string> GetLaunchBrowserAction()
        {
            Action<string> openCommand = null;
            if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                openCommand = (uri) => Browser.WindowsPlatformLaunch(uri);
            else if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                openCommand = (uri) => Browser.CommandLaunch(uri, "xdg-open");
            else if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                openCommand = (uri) => Browser.CommandLaunch(uri, "open");

            return openCommand;
        }

        private static string BuildUrl(string portNumber) => $"http://localhost:{portNumber}";

        private static string GetUriFromConsoleLine(string line) 
        {
            if(line.Contains(NowListeningOn))
                return line.Substring(line.IndexOf(NowListeningOn) + NowListeningOn.Length + 1);

            return string.Empty;
        }
    }

    public class Browser
    {
        public static void CommandLaunch(string uri, string command)
        {
            var process = new Process();
            process.StartInfo.FileName = command;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Arguments = uri;
            process.Start();
        }

        public static void WindowsPlatformLaunch(string uri)
        {        
        }
    }

    public class Options
    {
        string[] OpenBrowserOptionAlias = { "-o", "--open" };
        string[] PortNumberOptionAlias = { "-r", "--port" };
        string[] HelpOptionsAlias = { "-h", "--help" };

        public Options(string[] args)
        {
            ShouldOpenBrowser = FindOptionIndex(args, OpenBrowserOptionAlias) >= 0;
            SetPortNumberFromOption(args);
            FindUnknownOptions(args);
            Help = FindOptionIndex(args, HelpOptionsAlias) >= 0;
        }

        public List<string> GetHelpTextLines()
        {
            var help = new List<string> {
                ".Net Run Web Command",
                " ",
                "Usage: dotnet web [options]",
                " ",
                "Options:",
                "-h|--help\tShow help information",
                "-o|--open\tOpen in default browser",
                "-r|--port\tPort number to use when running the app",
                "As well as any dotnet run options"
            };
            return help;
        }

        private void SetPortNumberFromOption(string[] args)
        {
            var indexPortNumberArg = FindOptionIndex(args, PortNumberOptionAlias);

            if (indexPortNumberArg >= 0)
            {
                if (indexPortNumberArg + 1 == args.Length)
                    OptionErrors.Add("No port number provided");
                else
                {
                    int portNumber;
                    if (Int32.TryParse(args[indexPortNumberArg + 1], out portNumber) 
                        && portNumber < 65535
                        && portNumber != 0)
                    {
                        PortNumber = Math.Abs(portNumber).ToString();
                        PortNumberProvided = true;
                    }
                    else
                        OptionErrors.Add("Invalid port number format");
                }

            }
        }

        private void FindUnknownOptions(string[] args)
        {
            bool lastArgWasPortNumber = false;
            var knownArguments = OpenBrowserOptionAlias.Concat(PortNumberOptionAlias);
            foreach (var arg in args)
            {
                if (!knownArguments.Contains(arg) && (!lastArgWasPortNumber || arg.StartsWith("-")))
                {
                    UnknownArgs.Add(arg);
                }

                lastArgWasPortNumber = PortNumberOptionAlias.Contains(arg);
            }
        }

        private static int FindOptionIndex(string[] array, string[] values)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (Array.IndexOf(values, array[i]) >= 0)
                    return i;
            }

            return -1;
        }

        public bool Help { get; private set; }

        public bool ShouldOpenBrowser { get; private set; }

        public string PortNumber { get; private set; }

        public bool PortNumberProvided { get; private set; } = false;

        public List<string> UnknownArgs { get; set; } = new List<string>();

        public List<string> OptionErrors { get; private set; } = new List<string>();
    }
}
