namespace Particular.TeamCity.TestAide
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
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
                    return 0;
                }

                if (netCoreMajorSupport.ParsedValue == gitVersionMajor.ParsedValue)
                {
                    if (netCoreMinorSupport.ParsedValue > gitVersionMinor.ParsedValue)
                    {
                        Console.WriteLine("This Minor doesn't support netcore. No netcore tests required");
                        return 0;
                    }
                }

                var testingDirs = Directory.GetDirectories(currentProjectDirectory.ParsedValue, "*Tests");

                var exitCode = 0;

                foreach (var testingDir in testingDirs)
                {
                    // validate artifacts adn get target framework
                    var targetDirectories = Directory.GetDirectories(Path.Combine(testingDir, "bin", "Release"), "netcoreapp*")
                                                .Concat(Directory.GetDirectories(Path.Combine(testingDir, "bin", "Release"), "net?.*")
                                                .Concat(Directory.GetDirectories(Path.Combine(testingDir, "bin", "Release"), "netstandard*"));

                    if (targetDirectories.Any())
                    {
                        var dirInfo = new DirectoryInfo(testingDir);

                        foreach (var targetDirectory in targetDirectories)
                        {
                            var targetDirectoryInfo = new DirectoryInfo(targetDirectory);
                            var targetName = targetDirectoryInfo.Name;

                            var restoreExitCode = UpdateUnixDependencies(doUnixDependencies.ParsedValue, dirInfo, targetName);
                            if (exitCode == 0)
                            {
                                exitCode = restoreExitCode;
                            }

                            //run the tests
                            Console.WriteLine($"Running tests in {testingDir} for {targetName}");
                            var trxFile = Path.Combine(testingDir, "TestResults", $"testoutput-{targetName}.trx");

                            var testProcess = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = "dotnet",
                                    Arguments = $"test -c Release -f {targetName} --no-build --logger \"trx;LogFileName={trxFile}\"",
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

                            testProcess.WaitForExit();

                            if (exitCode == 0)
                            {
                                exitCode = testProcess.ExitCode;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unable to find artifacts in {testingDir}");
                        exitCode = 1;
                    }
                }

                return exitCode;
            });

            return app.Execute(args);
        }

        static int UpdateUnixDependencies(string doUnixDependencies, DirectoryInfo projectDirectoryInfo, string targetName)
        {
            if (doUnixDependencies == "true")
            {
                Console.WriteLine($"Creating {projectDirectoryInfo.Name}.runtimeconfig.dev.json for resolving Unix-specific dependencies from NuGet packages");
                var artifactsDir = Path.Combine(projectDirectoryInfo.FullName, "bin", "Release", targetName);
                var homeDir = Environment.GetEnvironmentVariable("HOME");
                using (var stream = File.CreateText(Path.Combine(artifactsDir, $"{projectDirectoryInfo.Name}.runtimeconfig.dev.json")))
                {
                    stream.Write($"{{\"runtimeOptions\":{{\"additionalProbingPaths\":[\"{homeDir}/.dotnet/store/|arch|/|tfm|\",\"{homeDir}/.nuget/packages\",\"/usr/share/dotnet/sdk/NuGetFallbackFolder\"]}}}}");
                    stream.Flush();
                    stream.Close();
                }

                var restoreProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "restore",
                        WorkingDirectory = projectDirectoryInfo.FullName,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                restoreProcess.Start();

                Console.WriteLine(restoreProcess.StandardOutput.ReadToEnd());
                Console.WriteLine(restoreProcess.StandardError.ReadToEnd());

                restoreProcess.WaitForExit();
                return restoreProcess.ExitCode;
            }

            return 0;
        }
    }
}
