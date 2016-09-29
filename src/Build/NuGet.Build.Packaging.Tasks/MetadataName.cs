﻿namespace NuGet.Build.Packaging.Tasks
{
	public static class MetadataName
	{
		public const string FileSource = "FullPath";

		public const string Kind = nameof(Kind);

		public const string Version = nameof(Version);

		/// <summary>
		/// One of https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/PackagingConstants.cs#L27
		/// </summary>
		public const string PackageFolder = nameof(PackageFolder);

		/// <summary>
		/// The package that declares the given package file.
		/// </summary>
		public const string PackageId = nameof(PackageId);

		/// <summary>
		/// Concatenation of <see cref="PackageFolder"/> and <see cref="TargetFramework"/>. 
		/// For <c>contentFiles</c>, also includes the <see cref="CodeLanguage"/> or <c>any</c> if 
		/// none was provided.
		/// </summary>
		public const string PackagePath = nameof(PackagePath);

		/// <summary>
		/// Marks a @(PackageReference) as a development dependency when set to 'true'.
		/// </summary>
		public const string IsDevelopmentDependency = nameof(IsDevelopmentDependency);

		/// <summary>
		/// Whether the project can be packed as a .nupkg.
		/// </summary>
		public const string IsPackable = nameof(IsPackable);

		public const string TargetFramework = nameof(TargetFramework);

		public const string TargetFrameworkMoniker = nameof(TargetFrameworkMoniker);

		/// <summary>
		/// Available optional metadata values of contentFiles.
		/// </summary>
		public static class ContentFile
		{
			public const string CodeLanguage = nameof(CodeLanguage);
			public const string BuildAction = nameof(BuildAction);
			public const string CopyToOutput = nameof(CopyToOutput);
			public const string Flatten = nameof(Flatten);
		}
	}
}