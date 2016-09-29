using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using NuGet.Build.Packaging.Tasks;
using static NuGet.Build.Packaging.Properties.Strings;
using Metadata = System.Collections.Generic.Dictionary<string, string>;

namespace NuGet.Build.Packaging
{
	public class AssignPackagePathTests
	{
		static readonly ITaskItem[] kinds;
		ITestOutputHelper output;
		MockBuildEngine engine;

		static AssignPackagePathTests()
		{
			kinds = new Project(Path.Combine(ModuleInitializer.BaseDirectory, "NuGet.Build.Packaging.props"), null, null, new ProjectCollection())
				.GetItems("PackageItemKind")
				.Select(item => new TaskItem(item.UnevaluatedInclude, item.Metadata.ToDictionary(meta => meta.Name, meta => meta.UnevaluatedValue)))
				.ToArray();
		}

		public AssignPackagePathTests(ITestOutputHelper output)
		{
			this.output = output;
			engine = new MockBuildEngine(output);
		}

		[Fact]
		public void when_file_has_no_kind_then_logs_error_code()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" }
					})
				}
			};

			Assert.False(task.Execute());
			Assert.Equal(nameof(ErrorCode.NP0010), engine.LoggedErrorEvents[0].Code);
		}

		[Fact]
		public void when_file_has_no_kind_but_has_package_path_then_assigns_target_framework()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "PackagePath", "workbooks\\library.dll" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal("net45", task.AssignedFiles[0].GetMetadata(MetadataName.TargetFramework));
		}

		[Fact]
		public void assigned_files_contains_all_files()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("a.dll", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "Lib" }
					}),
					new TaskItem("a.pdb", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "Symbols" }
					})
				}
			};

			Assert.True(task.Execute());

			Assert.Equal(task.Files.Length, task.AssignedFiles.Length);
		}

		[Fact]
		public void when_file_has_no_package_id_then_package_path_is_not_specified()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "Lib" }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal("", task.AssignedFiles[0].GetMetadata(MetadataName.PackagePath));
		}

		[Fact]
		public void when_file_has_no_package_id_then_target_framework_is_calculated_anyway()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "Lib" }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal("net45", task.AssignedFiles[0].GetMetadata(MetadataName.TargetFramework));
		}

		[Fact]
		public void when_file_has_no_package_id_then_package_folder_is_calculated_anyway()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "Lib" }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal("lib", task.AssignedFiles[0].GetMetadata(MetadataName.PackageFolder));
		}

		[Fact]
		public void when_file_has_no_tfm_then_assigned_file_contains_no_target_framework()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "PackageId", "A" },
						{ "Kind", "Lib" }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal("lib", task.AssignedFiles[0].GetMetadata(MetadataName.PackageFolder));
			Assert.Equal("lib\\library.dll", task.AssignedFiles[0].GetMetadata(MetadataName.PackagePath));
			Assert.Equal("", task.AssignedFiles[0].GetMetadata(MetadataName.TargetFramework));
		}

		// TODO: these all end up in all lowercase, but MonoAndroid, Xamarin.iOS are usually properly 
		// cased in nupkgs out in the wild (i.e. Rx)
		[InlineData(".NETFramework,Version=v4.5", "net45")]
		[InlineData(".NETPortable,Version=v5.0", "portable50")]
		[InlineData("Xamarin.iOS,Version=v1.0", "xamarinios10")]
		// TODO: should somehow we allow targetting monoandroid without the version suffix?
		[InlineData("MonoAndroid,Version=v2.5", "monoandroid25")]
		[Theory]
		public void when_file_has_tfm_then_assigned_file_contains_target_framework(string targetFrameworkMoniker, string expectedTargetFramework)
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", targetFrameworkMoniker },
						{ "Kind", "Lib" }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal("lib", task.AssignedFiles[0].GetMetadata(MetadataName.PackageFolder));
			Assert.Equal($"lib\\{expectedTargetFramework}\\library.dll", task.AssignedFiles[0].GetMetadata(MetadataName.PackagePath));
			Assert.Equal(expectedTargetFramework, task.AssignedFiles[0].GetMetadata(MetadataName.TargetFramework));
		}

		public static IEnumerable<object[]> GetMappedKnownKinds => kinds
			// Skip unmapped kinds (i.e. None, Dependency, etc.)
			.Where(kind => !string.IsNullOrEmpty(kind.GetMetadata(MetadataName.PackageFolder)) &&
				// Skip contentFiles from this test since they get a special map that includes the codelang
				kind.GetMetadata(MetadataName.PackageFolder) != "contentFiles")
			.Select(kind => new object[] { kind.ItemSpec, kind.GetMetadata("PackageFolder") });

		[MemberData("GetMappedKnownKinds")]
		[Theory]
		public void when_file_has_known_kind_then_assigned_file_contains_mapped_package_folder(string packageFileKind, string mappedPackageFolder)
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", packageFileKind }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal(mappedPackageFolder, task.AssignedFiles[0].GetMetadata(MetadataName.PackageFolder));
			Assert.Equal($"{mappedPackageFolder}\\net45\\library.dll", task.AssignedFiles[0].GetMetadata(MetadataName.PackagePath));
		}

		public static IEnumerable<object[]> GetUnmappedKnownKinds => kinds
			.Where(kind => string.IsNullOrEmpty(kind.GetMetadata(MetadataName.PackageFolder)) && 
				kind.GetMetadata(MetadataName.PackageFolder) != "contentFiles" && 
				kind.ItemSpec != PackageItemKind.None)
			.Select(kind => new object[] { kind.ItemSpec });

		[MemberData("GetUnmappedKnownKinds")]
		[Theory]
		public void when_file_has_known_kind_with_no_package_folder_then_package_path_is_empty(string packageFileKind)
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("Foo", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", packageFileKind }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal("", task.AssignedFiles[0].GetMetadata(MetadataName.PackagePath));
		}


		[Fact]
		public void when_file_has_explicit_package_path_then_calculated_package_folder_is_empty_and_preserves_package_path()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("readme.txt", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "None" },
						{ "PackagePath", "docs\\readme.txt" }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Empty(task.AssignedFiles[0].GetMetadata(MetadataName.PackageFolder));
			Assert.Equal("docs\\readme.txt", task.AssignedFiles[0].GetMetadata("PackagePath"));
		}

		[InlineData("", "vb", "contentFiles\\vb\\any\\")]
		[InlineData("", "", "contentFiles\\any\\any\\")]
		[InlineData(".NETFramework,Version=v4.5", "cs", "contentFiles\\cs\\net45\\")]
		[InlineData(".NETFramework,Version=v4.5", "", "contentFiles\\any\\net45\\")]
		[Theory]
		public void when_assigning_content_file_then_applies_tfm_and_language(string tfm, string lang, string expectedPath)
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("Sample.cs", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", tfm },
						{ "Kind", "Content" },
						{ "CodeLanguage", lang }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal("contentFiles", task.AssignedFiles[0].GetMetadata(MetadataName.PackageFolder));
			Assert.True(task.AssignedFiles[0].GetMetadata("PackagePath").StartsWith(expectedPath),
				$"'{task.AssignedFiles[0].GetMetadata("PackagePath")}' does not start with expected '{expectedPath}'");
		}

		[Fact]
		public void when_assigning_content_file_with_additional_metadata_then_preserves_metadata()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("Sample.cs", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "Content" },
						{ MetadataName.ContentFile.CodeLanguage, "cs" },
						{ MetadataName.ContentFile.BuildAction, "EmbeddedResource" },
						{ MetadataName.ContentFile.CopyToOutput, "true" },
						{ MetadataName.ContentFile.Flatten, "true" },
					})
				}
			};

			Assert.True(task.Execute());

			var item = task.AssignedFiles[0];

			Assert.Equal("cs", item.GetMetadata(MetadataName.ContentFile.CodeLanguage));
			Assert.Equal("EmbeddedResource", item.GetMetadata(MetadataName.ContentFile.BuildAction));
			Assert.Equal("true", item.GetMetadata(MetadataName.ContentFile.CopyToOutput));
			Assert.Equal("true", item.GetMetadata(MetadataName.ContentFile.Flatten));
		}

		[Fact]
		public void when_file_has_none_kind_then_assigned_file_has_empty_package_folder()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("readme.txt", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "None" }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Empty(task.AssignedFiles[0].GetMetadata(MetadataName.PackageFolder));
			Assert.Equal("readme.txt", task.AssignedFiles[0].GetMetadata("PackagePath"));
		}

		[Fact]
		public void when_file_has_none_kind_with_target_path_then_assigned_file_has_empty_package_folder_with_relative_package_path()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "None" },
						{ "TargetPath", "workbook\\library.dll"}
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Empty(task.AssignedFiles[0].GetMetadata(MetadataName.PackageFolder));
			Assert.Equal(@"workbook\library.dll", task.AssignedFiles[0].GetMetadata("PackagePath"));
		}

		[Fact]
		public void when_file_has_none_kind_then_assigned_file_has_no_target_framework_in_package_path()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "None" }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.NotEqual("net45", task.AssignedFiles[0].GetMetadata(MetadataName.PackagePath).Split(Path.DirectorySeparatorChar)[0]);
		}

		[InlineData("Build", "build")]
		[InlineData("Runtimes", "runtimes")]
		[InlineData("Workbook", "workbook")]
		[Theory]
		public void when_file_has_inferred_folder_from_kind_then_assigned_file_contains_inferred_package_folder(string packageFileKind, string inferredPackageFolder)
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("library.dll", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", packageFileKind }
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal(inferredPackageFolder, task.AssignedFiles[0].GetMetadata(MetadataName.PackageFolder));
			Assert.Equal($"{inferredPackageFolder}\\net45\\library.dll", task.AssignedFiles[0].GetMetadata(MetadataName.PackagePath));
		}

		[Fact]
		public void when_file_has_relative_target_path_without_tfm_then_package_path_has_relative_path()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("sdk\\bin\\tool.exe", new Metadata
					{
						{ "PackageId", "A" },
						{ "Kind", "Tool" },
						{ "TargetPath", "sdk\\bin\\tool.exe"}
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal(task.AssignedFiles[0].GetMetadata("PackagePath"), @"tools\sdk\bin\tool.exe");
		}

		[Fact]
		public void when_file_has_relative_target_path_with_tfm_then_package_path_has_relative_path_with_target_framework()
		{
			var task = new AssignPackagePath
			{
				BuildEngine = engine,
				Kinds = kinds,
				Files = new ITaskItem[]
				{
					new TaskItem("sdk\\bin\\tool.exe", new Metadata
					{
						{ "PackageId", "A" },
						{ "TargetFrameworkMoniker", ".NETFramework,Version=v4.5" },
						{ "Kind", "Tool" },
						{ "TargetPath", "sdk\\bin\\tool.exe"}
					})
				}
			};

			Assert.True(task.Execute());
			Assert.Equal(@"tools\net45\sdk\bin\tool.exe", task.AssignedFiles[0].GetMetadata("PackagePath"));
		}
	}
}
