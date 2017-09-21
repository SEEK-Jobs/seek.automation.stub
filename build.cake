var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var projectName = "seek.automation.stub";
var projectDescription = "A library to allow stubbing of services while building integration or pact automated tests.";

var nugetDir = Directory("publish");
var releaseNotes = ParseReleaseNotes("ReleaseNotes.md");
var buildNumber = EnvironmentVariable("BUILD_NUMBER") ?? "0";
var appVeyorBuildNumber = EnvironmentVariable("APPVEYOR_BUILD_NUMBER") ?? "0";
var version = string.Format("{0}.{1}", releaseNotes.Version, appVeyorBuildNumber);

Setup(context =>
			{
				CopyFileToDirectory("tools/nuget.exe", "tools/cake/");

				if(DirectoryExists(nugetDir))
				{
					DeleteDirectory(nugetDir, recursive:true);
				}

				CreateDirectory(nugetDir);

				NuGetInstall("xunit.runner.console", new NuGetInstallSettings {
										ExcludeVersion  = true,
										OutputDirectory = "./tools"
								});
			}
	 );

Teardown(context =>
				{
					Information("Completed!");
				}
		);

Task("Clean")
	.Does(() =>
				{
					CleanDirectories(string.Format("{0}/**/bin", projectName));
					CleanDirectories(string.Format("{0}/**/obj", projectName));
				}
		 );

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
				{
					NuGetRestore(string.Format("{0}.sln", projectName));
				}
		 );

Task("AssemblyInfo")
    .IsDependentOn("Restore-NuGet-Packages")
	.Does(() =>
		{
			CreateAssemblyInfo(string.Format("{0}/Properties/AssemblyInfo.cs", projectName), new AssemblyInfoSettings
			{
				Title = projectName,
				Description = projectDescription,
				Guid = "4dd5b14a-ef02-4ce0-9c33-52a4a4ea05f5",
				Product = projectName,
				Version = version,
				FileVersion = version
			});
		});

Task("Build-Solution")
    .IsDependentOn("AssemblyInfo")
    .Does(() =>
				{
					MSBuild(string.Format("{0}.sln", projectName), new MSBuildSettings()
						.UseToolVersion(MSBuildToolVersion.NET46)
						.SetVerbosity(Verbosity.Minimal)
						.SetConfiguration(configuration)
						);
				}
		 );

Task("Run-Unit-Tests")
    .IsDependentOn("Build-Solution")
    .Does(() =>
				{
					XUnit2(string.Format("{0}.tests/**/bin/{1}/*.Tests.dll", projectName, configuration));
				}
		 );

Task("Create-Nuget-Package")
    .IsDependentOn("Build-Solution")
    .Does(() =>
				{
					var nuGetPackSettings   = new NuGetPackSettings {
								Id							= projectName,
								Version						= version,
								Title						= "SEEK Pact Based Stub Library",
								Authors						= new[] {"Behdad Darougheh"},
								Owners						= new[] {"SEEK", "Behdad Darougheh"},
								Description					= projectDescription,
								Summary						= "Try stubbing the dependent services instead of mocking...you might like it.",
								ProjectUrl					= new Uri("https://github.com/seek-oss/seek.automation.stub"),
								Copyright					= "Copyright 2015",
								ReleaseNotes				= new [] {"Please see below, the notes for the last release:", Environment.NewLine , String.Join(Environment.NewLine, releaseNotes.Notes).ToString()},
								Tags						= new [] {"StubLib", "PactAutomation", "Integration", "Consumer Driven Contract"},
								RequireLicenseAcceptance	= false,
								Files						= new [] {
																		new NuSpecContent {Source = string.Format("bin/release/{0}.dll", projectName), Target = "lib/net45"},
																	 },
								BasePath					= string.Format("{0}/", projectName),
								OutputDirectory				= nugetDir
							};

					NuGetPack(string.Format("{0}.nuspec", projectName), nuGetPackSettings);
				}
			);

Task("Publish-Nuget-Package")
    .IsDependentOn("Create-Nuget-Package")
    .Does(() =>
				{
					Information("Publishing to Nuget.org...");

					var apiKey = EnvironmentVariable("NUGET_ORG_API_KEY");
					if(string.IsNullOrEmpty(apiKey)) {
								throw new InvalidOperationException("Could not resolve NUGET_ORG_API_KEY API key.");
					}

					// Push the package
					NuGetPush(System.IO.Directory.GetFiles("publish")[0], new NuGetPushSettings {
						Source = "https://www.nuget.org/api/v2/package",
						ApiKey = apiKey
					});
				}
			);

Task("Default")
    .IsDependentOn("Run-Unit-Tests");

RunTarget(target);
