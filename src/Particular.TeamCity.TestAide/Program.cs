using System;

namespace Particular.TeamCity.TestAide
{
    using System.Diagnostics;
    using System.IO;
    using McMaster.Extensions.CommandLineUtils;

    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "tctestaide"
            };

            var gitVersionMajor = app.Option<int>(
                    "-gvmaj | --gitversionmajor",
                    "Value pulled from GitVersion.Major parameter in TeamCity",
                    CommandOptionType.SingleValue);
            gitVersionMajor.IsRequired();

            var gitVersionMinor = app.Option<int>(
                    "-gvmin | --gitversionminor",
                    "Value pulled from GitVersion.Minor parameter in TeamCity",
                    CommandOptionType.SingleValue);
            gitVersionMinor.IsRequired();

            var netCoreMajorSupport = app.Option<int>(
                    "-ncmaj | --netcoreversionmajor",
                    "Value pulled from VersionThatStartedNetCoreSupport parameter in TeamCity",
                    CommandOptionType.SingleValue);
            netCoreMajorSupport.IsRequired();

            var netCoreMinorSupport = app.Option<int>(
                    "-ncmin | --netcoreversionminor",
                    "Value pulled from MinorVersionThatStartedNetCoreSupport parameter in TeamCity",
                    CommandOptionType.SingleValue);
            netCoreMinorSupport.IsRequired();

            var currentProjectDirectory = app.Option<string>(
                    "-curdir | --currentdirectory",
                    "The working directory that the current project is being run in.",
                    CommandOptionType.SingleValue);
            currentProjectDirectory.IsRequired();

            var doUnixDependencies = app.Option<string>(
                    "-udep | --unixdependencies",
                    "Should the process create a runtimeconfig.dev.json file and restore the dependencies. Usually only used for Linux test runs",
                    CommandOptionType.SingleValue);
            doUnixDependencies.IsRequired();
            doUnixDependencies.Accepts().Values("true", "false");

            app.OnExecute(() =>
            {
                Console.WriteLine($"gitVersionMajor: {gitVersionMajor.ParsedValue}");
                Console.WriteLine($"gitVersionMinor: {gitVersionMinor.ParsedValue}");
                Console.WriteLine($"netCoreMajorSupport: {netCoreMajorSupport.ParsedValue}");
                Console.WriteLine($"netCoreMinorSupport: {netCoreMinorSupport.ParsedValue}");
                Console.WriteLine($"currentDirectory: {currentProjectDirectory.ParsedValue}");
                Console.WriteLine($"unixDependencies: {doUnixDependencies.ParsedValue}");

                if (netCoreMajorSupport.ParsedValue > gitVersionMajor.ParsedValue)
                {
                    Console.WriteLine("This Major doesn't support netcore. No netcore tests required");
                    return 1;
                }

                if (netCoreMajorSupport.ParsedValue == gitVersionMajor.ParsedValue)
                {
                    if (netCoreMinorSupport.ParsedValue > gitVersionMinor.ParsedValue)
                    {
                        Console.WriteLine("This Minor doesn't support netcore. No netcore tests required");
                        return 1;
                    }
                }

                var testingDirs = Directory.GetDirectories(currentProjectDirectory.ParsedValue, "*Tests");

                //validate artifacts
                var missingArtifacts = false;
                foreach (var testingDir in testingDirs)
                {
                    //check for artifacts directory
                    var artifactsDir = Path.Combine(testingDir, "bin", "Release", "netcoreapp2.0");
                    if (!Directory.Exists(artifactsDir))
                    {
                        Console.WriteLine($"Missing artifacts in {artifactsDir}");
                        missingArtifacts = true;
                    }
                }

                if (missingArtifacts)
                {
                    return 1;
                }

                var exitCode = 0;

                foreach (var testingDir in testingDirs)
                {
                    var dirInfo = new DirectoryInfo(testingDir);
                    if (doUnixDependencies.ParsedValue == "true")
                    {
                        Console.WriteLine($"Creating {dirInfo.Name}.runtimeconfig.dev.json for resolving Unix-specific dependencies from NuGet packages");
                        var artifactsDir = Path.Combine(testingDir, "bin","Release", "netcoreapp2.0");
                        using (var stream = File.CreateText(Path.Combine(artifactsDir, $"{dirInfo.Name}.runtimeconfig.dev.json")))
                        {
                            stream.Write("{\"runtimeOptions\":{\"additionalProbingPaths\":[\"$HOME/.dotnet/store/|arch|/|tfm|\",\"$HOME/.nuget/packages\",\"/usr/share/dotnet/sdk/NuGetFallbackFolder\"]}}");
                            stream.Flush();
                            stream.Close();
                        }

                        var restoreProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "dotnet",
                                Arguments = "restore",
                                WorkingDirectory = testingDir,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };

                        restoreProcess.Start();
                    }

                    //run the tests
                    Console.WriteLine($"Running tests in {testingDir}");
                    var trxFile = Path.Combine(testingDir, "TestResults", "testoutput.trx");

                    var testProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"test -c Release -f netcoreapp2.0 --no-restore --no-build --logger \"trx;LogFileName={trxFile}\"",
                            WorkingDirectory = testingDir,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    testProcess.Start();
                    Console.WriteLine(testProcess.StandardOutput.ReadToEnd());
                    Console.WriteLine(testProcess.StandardError.ReadToEnd());

                    if (exitCode == 0)
                    {
                        exitCode = testProcess.ExitCode;
                    }
                }

                return exitCode;
            });

            return app.Execute(args);
        }
    }
}
