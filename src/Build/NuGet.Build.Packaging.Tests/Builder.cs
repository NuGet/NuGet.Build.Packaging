using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using NuGet.Build.Packaging;

/// <summary>
/// General-purpose MSBuild builder with support for 
/// automatic solution-configuration generation for 
/// P2P references.
/// </summary>
public static partial class Builder
{
	static Builder()
	{
		var dotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
		if (!File.Exists(dotnet))
		{
			var where = Environment.OSVersion.Platform == PlatformID.Win32NT ? "where" : "which";
			Process
				.Start(new ProcessStartInfo("cmd.exe", $"/c {where} dotnet > dotnet.txt")
				{
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden
				})
				.WaitForExit();

			dotnet = File.ReadAllText("dotnet.txt").Trim();
		}

		if (string.IsNullOrEmpty(dotnet) || !File.Exists(dotnet))
			throw new InvalidOperationException("Failed to locate dotnet.");

		var sdkPath = Directory.EnumerateDirectories(Path.Combine(
				Path.GetDirectoryName(dotnet), "sdk")).FirstOrDefault();

		if (string.IsNullOrEmpty(sdkPath))
			throw new InvalidOperationException("Failed to locate an MSBuild SDK.");

		var sdkVersion = sdkPath.Substring(sdkPath.LastIndexOf(Path.DirectorySeparatorChar) + 1);

		// This envvar allows the local copy of MSBuild from the Microsoft.Build.Runtime package to 
		// properly locate the Microsoft.NET.Sdk we use in our projects.
		Environment.SetEnvironmentVariable("MSBuildSDKsPath", Path.Combine(sdkPath, "Sdks"), EnvironmentVariableTarget.Process);

		// Ensure we use the version we found.
		File.WriteAllText(".\\global.json", $@"{{
  ""sdk"": {{
    ""version"": ""{sdkVersion}""
  }}
 }}");
	}

	public static Dictionary<string, string> DefaultGlobalProperties
		=> new Dictionary<string, string>
		{
			{ "VsInstallRoot", ThisAssembly.Project.Properties.VsInstallRoot },
			{ "VSINSTALLDIR", ThisAssembly.Project.Properties.VsInstallRoot + "\\" },
			{ "NuGetRestoreTargets", Path.Combine(ThisAssembly.Project.Properties.VsInstallRoot, @"Common7\IDE\CommonExtensions\Microsoft\NuGet\NuGet.targets") },
			{ "CSharpDesignTimeTargetsPath", Path.Combine(ThisAssembly.Project.Properties.VsInstallRoot, @"MSBuild\Microsoft\VisualStudio\Managed\Microsoft.CSharp.DesignTime.targets") },
			{ "VisualBasicDesignTimeTargetsPath", Path.Combine(ThisAssembly.Project.Properties.VsInstallRoot, @"MSBuild\Microsoft\VisualStudio\Managed\Microsoft.VisualBasic.DesignTime.targets") },
			{ "RoslynTargetsPath", Path.Combine(ThisAssembly.Project.Properties.VsInstallRoot, @"MSBuild\15.0\Bin\Roslyn") }
		};

	public static BuildResult Build(ProjectInstance project, string targets, Dictionary<string, string> properties = null, ILogger[] loggers = null)
	{
		if (properties == null)
		{
			properties = DefaultGlobalProperties;
		}
		else
		{
			foreach (var prop in DefaultGlobalProperties)
			{
				properties[prop.Key] = prop.Value;
			}
		}

		// Without this, builds end up running in process and colliding with each other, 
		// especially around the current directory used to resolve relative paths in projects.
		//Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1", EnvironmentVariableTarget.Process);
		using (var manager = new BuildManager(Guid.NewGuid().ToString()))
		{
			var request = new BuildRequestData(project, targets.Split(','));
			var parameters = new BuildParameters
			{
				GlobalProperties = properties,
				DisableInProcNode = !Debugger.IsAttached,
				EnableNodeReuse = false,
				ShutdownInProcNodeOnBuildFinish = !Debugger.IsAttached,
				// Without this, builds end up running in process and colliding with each other, 
				// especially around the current directory used to resolve relative paths in projects.
				LogInitialPropertiesAndItems = true,
				LogTaskInputs = true,
			};

			if (loggers != null)
				parameters.Loggers = loggers;

			var result = manager.Build(parameters, request);
			//manager.ShutdownAllNodes();
			return result;
		}
	}

	public static BuildResult Build(string projectOrSolution, string targets, Dictionary<string, string> properties = null, ILogger[] loggers = null)
	{
		if (!Path.IsPathRooted(projectOrSolution))
			projectOrSolution = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, projectOrSolution);

		if (!File.Exists(projectOrSolution))
			throw new FileNotFoundException($"Project or solution to build {projectOrSolution} was not found.", projectOrSolution);

		if (properties == null)
		{
			properties = DefaultGlobalProperties;
		}
		else
		{
			foreach (var prop in DefaultGlobalProperties)
			{
				properties[prop.Key] = prop.Value;
			}
		}

		// If file is not a solution, build up a fake solution configuration so P2P references just work
		if (Path.GetExtension(projectOrSolution) != ".sln")
			AddSolutionConfiguration(projectOrSolution, properties);

		var projectInstance = new ProjectInstance(projectOrSolution, properties, "15.0");

		return Build(projectInstance, targets, properties, loggers);
	}

	static void AddSolutionConfiguration(string projectFile, Dictionary<string, string> properties)
	{
		var collection = new ProjectCollection(properties);
		var toolsVersion = default(string);
		var project = new Project(projectFile, properties, toolsVersion, collection);
		var config = project.GetPropertyValue("Configuration");
		var platform = project.GetPropertyValue("Platform");

		var xml = new XElement("SolutionConfiguration");
		xml.Add(new XElement("ProjectConfiguration",
			new XAttribute("Project", project.GetPropertyValue("ProjectGuid")),
			new XAttribute("AbsolutePath", project.FullPath),
			new XAttribute("BuildProjectInSolution", "true"))
		{
			Value = $"{config}|{platform}"
		});

		foreach (var reference in GetProjectReferences(project))
		{
			xml.Add(new XElement("ProjectConfiguration",
				new XAttribute("Project", reference.GetPropertyValue("ProjectGuid")),
				new XAttribute("AbsolutePath", reference.FullPath),
				new XAttribute("BuildProjectInSolution", "true"))
			{
				Value = $"{config}|{platform}"
			});
		}

		properties["CurrentSolutionConfigurationContents"] = xml.ToString(SaveOptions.None);
	}

	static IEnumerable<Project> GetProjectReferences(Project project)
	{
		foreach (var reference in project.GetItems("ProjectReference"))
		{
			var referenced = new Project(
					Path.Combine(Path.GetDirectoryName(project.FullPath), reference.EvaluatedInclude),
					project.GlobalProperties,
					project.ToolsVersion,
					new ProjectCollection(project.GlobalProperties));

			yield return referenced;
			foreach (var transitive in GetProjectReferences(referenced))
			{
				yield return transitive;
			}
		}
	}
}