using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using System.ComponentModel.Composition;
using Clide;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

namespace NuGet.Packaging.VisualStudio
{
	[Export(typeof(IBuildService))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	public class BuildService : IBuildService, IVsUpdateSolutionEvents
	{
		const string PackOnBuildPropertyName = "PackOnBuild";

		readonly uint updateSolutionEventsCookie;
		readonly IVsSolutionBuildManager2 buildManager;

		Action resetPropertyCallback;

		[ImportingConstructor]
		public BuildService([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
		{
			buildManager = serviceProvider.GetService<SVsSolutionBuildManager, IVsSolutionBuildManager2>();
			buildManager.AdviseUpdateSolutionEvents(this, out updateSolutionEventsCookie);
		}

		public bool IsBusy
		{
			get
			{
				int buildManagerBusy;
				return ErrorHandler.Succeeded(buildManager.QueryBuildManagerBusy(out buildManagerBusy)) &&
					buildManagerBusy != 0;
			}
		}

		public void Pack(IProjectNode project)
		{
			var hierarchy = project.AsVsHierarchy();
			var storage = hierarchy as IVsBuildPropertyStorage;

			if (hierarchy != null && storage != null)
			{
				resetPropertyCallback = () => storage.RemoveProperty(PackOnBuildPropertyName, null, (uint)_PersistStorageType.PST_USER_FILE);
				storage.SetPropertyValue(PackOnBuildPropertyName, null, (uint)_PersistStorageType.PST_USER_FILE, "true");

				buildManager.StartSimpleUpdateProjectConfiguration(hierarchy, null, null,
					(uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD, 0, 0);
			}
		}

		int IVsUpdateSolutionEvents.OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy) => VSConstants.S_OK;

		int IVsUpdateSolutionEvents.UpdateSolution_Begin(ref int pfCancelUpdate) => VSConstants.S_OK;

		int IVsUpdateSolutionEvents.UpdateSolution_Cancel() => VSConstants.S_OK;

		int IVsUpdateSolutionEvents.UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
		{
			if (resetPropertyCallback != null)
			{
				try
				{
					resetPropertyCallback();
				}
				finally
				{
					resetPropertyCallback = null;
				}
			}

			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents.UpdateSolution_StartUpdate(ref int pfCancelUpdate) => VSConstants.S_OK;
	}
}