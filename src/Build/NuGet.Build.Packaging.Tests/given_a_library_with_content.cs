﻿using System.Linq;
using Microsoft.Build.Execution;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Build.Packaging
{
	public class given_a_library_with_content
	{
		ITestOutputHelper output;

		public given_a_library_with_content(ITestOutputHelper output)
		{
			this.output = output;
		}

		[Fact]
		public void when_library_is_not_packable_then_still_contains_content_files()
		{
			var result = Builder.BuildScenario(nameof(given_a_library_with_content));

			Assert.Equal(TargetResultCode.Success, result.ResultCode);

			Assert.Contains(result.Items, item => item.Matches(new
			{
				TargetPath = @"Resources\drawable-hdpi\Icon.png",
			}));
		}

		[Fact]
		public void when_include_content_is_false_then_does_not_contain_content_files()
		{
			var result = Builder.BuildScenario(nameof(given_a_library_with_content), new
			{
				IncludeContent = "false"
			});

			Assert.Equal(TargetResultCode.Success, result.ResultCode);

			Assert.DoesNotContain(result.Items, item => item.Matches(new
			{
				TargetPath = @"Resources\drawable-hdpi\Icon.png",
			}));
		}

		[Fact]
		public void when_library_is_packable_then_contains_content_files_in_anylang_tfm_path()
		{
			var result = Builder.BuildScenario(nameof(given_a_library_with_content), new
			{
				PackageId = "ContentPackage"
			});

			Assert.Equal(TargetResultCode.Success, result.ResultCode);

			Assert.Contains(result.Items, item => item.Matches(new
			{
				PackagePath = @"contentFiles\any\monoandroid51\Resources\drawable-hdpi\Icon.png",
			}));
		}

		[Fact]
		public void when_none_item_has_no_include_in_package_then_it_is_not_included()
		{
			var result = Builder.BuildScenario(nameof(given_a_library_with_content), new
			{
				PackageId = "ContentPackage",
			});

			Assert.Equal(TargetResultCode.Success, result.ResultCode);

			Assert.DoesNotContain(result.Items, item => item.Matches(new
			{
				ItemSpec = "none.txt",
			}));
		}

		[Fact]
		public void when_none_item_has_include_in_package_then_it_is_included_in_specified_target_path()
		{
			var result = Builder.BuildScenario(nameof(given_a_library_with_content), new
			{
				PackageId = "ContentPackage",
			});

			Assert.Equal(TargetResultCode.Success, result.ResultCode);

			Assert.Contains(result.Items, item => item.Matches(new
			{
				Filename = "sample",
				Extension = ".cs",
				Kind = PackageItemKind.None,
				PackagePath = @"contentFiles\cs\monoandroid\sample.cs", 
				TargetPath = @"contentFiles\cs\monoandroid\sample.cs",
			}));
		}

	}
}
