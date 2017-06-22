﻿using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.Versioning;
using NuGet.Versioning;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Packaging;
using System.Collections.Generic;
using static NuGet.Build.Packaging.Properties.Strings;

namespace NuGet.Build.Packaging.Tasks
{
	public class CreatePackage : Task
	{
		[Required]
		public ITaskItem Manifest { get; set; }

		[Required]
		public ITaskItem[] Contents { get; set; }

		[Required]
		public string TargetPath { get; set; }

		public string NuspecFile { get; set; }

		[Output]
		public ITaskItem OutputPackage { get; set; }

		public override bool Execute()
		{
			try
			{
				using (var stream = File.Create(TargetPath))
				{
					BuildPackage(stream);
				}

				OutputPackage = new TaskItem(TargetPath);
				Manifest.CopyMetadataTo(OutputPackage);
					 
				return !Log.HasLoggedErrors;
			}
			catch (Exception ex)
			{
				Log.LogErrorFromException(ex);
				return false;
			}
		}

		// Implementation for testing to avoid I/O
		public Manifest Execute(Stream output)
		{
			BuildPackage(output);

			output.Seek(0, SeekOrigin.Begin);
			using (var reader = new PackageArchiveReader(output))
			{
				return reader.GetManifest();
			}
		}

		public Manifest CreateManifest()
		{
			var metadata = new ManifestMetadata();

			metadata.Id = Manifest.GetMetadata("Id");
			metadata.Version = NuGetVersion.Parse(Manifest.GetMetadata(MetadataName.Version));
			metadata.DevelopmentDependency = Manifest.GetBoolean("DevelopmentDependency");

			metadata.Title = Manifest.GetMetadata("Title");
			metadata.Description = Manifest.GetMetadata("Description");
			metadata.Summary = Manifest.GetMetadata("Summary");
			metadata.Language = Manifest.GetMetadata("Language");

			metadata.Copyright = Manifest.GetMetadata("Copyright");
			metadata.RequireLicenseAcceptance = Manifest.GetBoolean("RequireLicenseAcceptance");

			if (!string.IsNullOrEmpty(Manifest.GetMetadata("Authors")))
				metadata.Authors = Manifest.GetMetadata("Authors").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			if (!string.IsNullOrEmpty(Manifest.GetMetadata("Owners")))
				metadata.Owners = Manifest.GetMetadata("Owners").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			if (!string.IsNullOrEmpty(Manifest.GetMetadata("LicenseUrl")))
				metadata.SetLicenseUrl(Manifest.GetMetadata("LicenseUrl"));
			if (!string.IsNullOrEmpty(Manifest.GetMetadata("ProjectUrl")))
				metadata.SetProjectUrl(Manifest.GetMetadata("ProjectUrl"));
			if (!string.IsNullOrEmpty(Manifest.GetMetadata("IconUrl")))
				metadata.SetIconUrl(Manifest.GetMetadata("IconUrl"));

			metadata.ReleaseNotes = Manifest.GetMetadata("ReleaseNotes");
			metadata.Tags = Manifest.GetMetadata("Tags");
			metadata.MinClientVersionString = Manifest.GetMetadata("MinClientVersion");

			var manifest = new Manifest(metadata);

			AddDependencies(manifest);
			AddFiles(manifest);
			AddFrameworkAssemblies(manifest);

			return manifest;
		}

		void AddDependencies(Manifest manifest)
		{
			var dependencies = from item in Contents
							   where item.GetMetadata(MetadataName.Kind) == PackageItemKind.Dependency && 
									 !"all".Equals(item.GetMetadata(MetadataName.PrivateAssets), StringComparison.OrdinalIgnoreCase)
							   select new Dependency
							   {
								   Id = item.ItemSpec,
								   Version = VersionRange.Parse(item.GetMetadata(MetadataName.Version)),
								   TargetFramework = item.GetNuGetTargetFramework()
							   };

			var definedDependencyGroups = (from dependency in dependencies
										   group dependency by dependency.TargetFramework into dependenciesByFramework
										   select new PackageDependencyGroup
										   (
											   dependenciesByFramework.Key,
											   (from dependency in dependenciesByFramework
												where dependency.Id != "_._"
												group dependency by dependency.Id into dependenciesById
												select new PackageDependency
												 (
													 dependenciesById.Key,
													 dependenciesById.Select(x => x.Version)
													 .Aggregate(AggregateVersions)
												 )).ToList()
										   )).ToDictionary(p => p.TargetFramework.GetFrameworkString());

			// include frameworks referenced by libraries, but without dependencies..
			foreach (var targetFramework in (from item in Contents
											 where item.GetMetadata(MetadataName.Kind) == PackageItemKind.Lib &&
												   !"all".Equals(item.GetMetadata(MetadataName.PrivateAssets), StringComparison.OrdinalIgnoreCase)
											 select item.GetNuGetTargetFramework()))
				if (!definedDependencyGroups.ContainsKey(targetFramework.GetFrameworkString()))
					definedDependencyGroups.Add(targetFramework.GetFrameworkString(),
												new PackageDependencyGroup(targetFramework, Array.Empty<PackageDependency>()));

			manifest.Metadata.DependencyGroups = definedDependencyGroups.Values;
		}

		void AddFiles(Manifest manifest)
		{
			var contents = Contents.Where(item => 
				!string.IsNullOrEmpty(item.GetMetadata(MetadataName.PackagePath)));

			var duplicates = contents.GroupBy(item => item.GetMetadata(MetadataName.PackagePath))
				.Where(x => x.Count() > 1)
				.Select(x => x.Key);

			foreach (var duplicate in duplicates)
			{
				Log.LogErrorCode(nameof(ErrorCode.NG0012), ErrorCode.NG0012(duplicate));
			}

			// All files need to be added so they are included in the nupkg
			manifest.Files.AddRange(contents
				.Select(item => new ManifestFile
				{
					Source = item.GetMetadata("FullPath"),
					Target = item.GetMetadata(MetadataName.PackagePath),
				}));

			// Additional metadata for the content files must be added separately
			manifest.Metadata.ContentFiles = contents
				.Where(item => item.GetMetadata(MetadataName.PackageFolder) == PackagingConstants.Folders.ContentFiles)
				.Select(item => new ManifestContentFiles
				{
					Include = item.GetMetadata(MetadataName.PackagePath),
					BuildAction = item.GetNullableMetadata(MetadataName.ContentFile.BuildAction),
					CopyToOutput = item.GetNullableMetadata(MetadataName.ContentFile.CopyToOutput),
					Flatten = item.GetNullableMetadata(MetadataName.ContentFile.Flatten),
				}).ToArray();
		}

		void AddFrameworkAssemblies(Manifest manifest)
		{
			var frameworkReferences = (from item in Contents
			 						   where item.GetMetadata(MetadataName.Kind) == PackageItemKind.FrameworkReference
			 						   select new FrameworkAssemblyReference
									   (
										   item.ItemSpec,
										   new[] { NuGetFramework.Parse(item.GetTargetFrameworkMoniker().FullName) }
									   )).Distinct(FrameworkAssemblyReferenceComparer.Default);

			manifest.Metadata.FrameworkReferences = frameworkReferences;
		}

		void BuildPackage(Stream output)
		{
			var builder = new PackageBuilder();
			var manifest = CreateManifest();

			builder.Populate(manifest.Metadata);
			// We don't use PopulateFiles because that performs search expansion, base path 
			// extraction and the like, which messes with our determined files to include.
			// TBD: do we support wilcard-based include/exclude?
			builder.Files.AddRange(manifest.Files.Select(file => 
				new PhysicalPackageFile { SourcePath = file.Source, TargetPath = file.Target }));
			
			builder.Save(output);

			if (!string.IsNullOrEmpty(NuspecFile))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(NuspecFile));
				using (var stream = File.Create(NuspecFile))
				{
					manifest.Save(stream, true);
				}
			}
		}

		static VersionRange AggregateVersions(VersionRange aggregate, VersionRange next)
		{
			var versionSpec = new VersionSpec();
			SetMinVersion(versionSpec, aggregate);
			SetMinVersion(versionSpec, next);
			SetMaxVersion(versionSpec, aggregate);
			SetMaxVersion(versionSpec, next);

			if (versionSpec.MinVersion == null && versionSpec.MaxVersion == null)
				return null;

			return versionSpec.ToVersionRange();
		}

		static void SetMinVersion(VersionSpec target, VersionRange source)
		{
			if (source == null || source.MinVersion == null)
				return;

			if (target.MinVersion == null)
			{
				target.MinVersion = source.MinVersion;
				target.IsMinInclusive = source.IsMinInclusive;
			}

			if (target.MinVersion < source.MinVersion)
			{
				target.MinVersion = source.MinVersion;
				target.IsMinInclusive = source.IsMinInclusive;
			}

			if (target.MinVersion == source.MinVersion)
				target.IsMinInclusive = target.IsMinInclusive && source.IsMinInclusive;
		}

		static void SetMaxVersion(VersionSpec target, VersionRange source)
		{
			if (source == null || source.MaxVersion == null)
				return;

			if (target.MaxVersion == null)
			{
				target.MaxVersion = source.MaxVersion;
				target.IsMaxInclusive = source.IsMaxInclusive;
			}

			if (target.MaxVersion > source.MaxVersion)
			{
				target.MaxVersion = source.MaxVersion;
				target.IsMaxInclusive = source.IsMaxInclusive;
			}

			if (target.MaxVersion == source.MaxVersion)
				target.IsMaxInclusive = target.IsMaxInclusive && source.IsMaxInclusive;
		}


		class Dependency
		{
			public string Id { get; set; }

			public NuGetFramework TargetFramework { get; set; }

			public VersionRange Version { get; set; }
		}

		class VersionSpec
		{
			public bool IsMinInclusive { get; set; }
			public NuGetVersion MinVersion { get; set; }
			public bool IsMaxInclusive { get; set; }
			public NuGetVersion MaxVersion { get; set; }

			public VersionRange ToVersionRange()
			{
				return new VersionRange(MinVersion, IsMinInclusive, MaxVersion, IsMaxInclusive);
			}
		}
	}
}
