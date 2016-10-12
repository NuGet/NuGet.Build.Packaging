﻿using NuGet.Build.Packaging;

namespace NuGet.Packaging.VisualStudio
{
	class Constants
	{
		public const string PortableClassLibraryCapability = "MultiTarget";

		/// <summary>
		/// The file extension of this project type.  No preceding period.
		/// </summary>
		public const string ProjectExtension = "nuproj";
		public const string ProjectFileExtension = "." + ProjectExtension;

		public const string Language = "NuGet.Packaging";
		public const string NuGetAuthoringPathPropertyName = "NuGetAuthoringPath";

		public class NuGet
		{
			// TODO: We need to generate these values automatically
			public const string RepositoryId = "NuGet.Packaging";
			public const string BuildPackagingId = "NuGet.Build.Packaging";
			public const string BuildPackagingVersion = ThisAssembly.PackageVersion;
		}

		public class Platforms
		{
			public const string IOS = "Xamarin.iOS";
			public const string Android = "Xamarin.Android";
		}

		public class Suffixes
		{
			public static readonly string IOS = Resources.IOS_Suffix;
			public static readonly string Android = Resources.Android_Suffix;
			public static readonly string NuGetPackage = "NuGet";
			public static readonly string SharedProject = "Shared";

			public static string GetSuffixForPlatform(string platformId)
			{
				if (string.Equals(Platforms.IOS, platformId, System.StringComparison.OrdinalIgnoreCase))
					return IOS;
				if (string.Equals(Platforms.Android, platformId, System.StringComparison.OrdinalIgnoreCase))
					return Android;

				return null;
			}
		}

		public class Templates
		{
			public const string IOS = "Xamarin.iOS.Library";
			public const string Android = "Xamarin.Android.ClassLibrary";
			public const string NuGetPackage = "NuGet.Packaging.VisualStudio.Package";
			public const string SharedProject = "Microsoft.CS.SharedProject";
			public const string PortableClassLibrary = "NuGet.Packaging.VisualStudio.Portable";

			public static string GetPlatformTemplate(string platformId)
			{
				switch (platformId)
				{
					case Platforms.IOS: return IOS;
					case Platforms.Android: return Android;
					default: return null;
				}
			}
		}
	}
}