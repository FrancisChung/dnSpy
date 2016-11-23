﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using dnSpy.Contracts.Command;
using dnSpy.Contracts.Hex;
using dnSpy.Contracts.Hex.Classification;
using dnSpy.Contracts.Hex.Editor;
using dnSpy.Contracts.Hex.Formatting;
using dnSpy.Contracts.Hex.Operations;
using dnSpy.Contracts.Menus;
using dnSpy.Hex.Formatting;
using dnSpy.Hex.MEF;
using TE = dnSpy.Text.Editor;
using VSTC = Microsoft.VisualStudio.Text.Classification;
using VSTE = Microsoft.VisualStudio.Text.Editor;

namespace dnSpy.Hex.Editor {
	[Export(typeof(HexEditorFactoryService))]
	sealed class HexEditorFactoryServiceImpl : HexEditorFactoryService {
		public override event EventHandler<HexViewCreatedEventArgs> HexViewCreated;
		readonly HexEditorOptionsFactoryService hexEditorOptionsFactoryService;
		readonly ICommandService commandService;
		readonly Lazy<WpfHexViewCreationListener, IDeferrableTextViewRoleMetadata>[] wpfHexViewCreationListeners;
		readonly FormattedHexSourceFactoryService formattedHexSourceFactoryService;
		readonly HexViewClassifierAggregatorService hexViewClassifierAggregatorService;
		readonly HexAndAdornmentSequencerFactoryService hexAndAdornmentSequencerFactoryService;
		readonly HexClassificationFormatMapService classificationFormatMapService;
		readonly HexEditorFormatMapService editorFormatMapService;
		readonly HexAdornmentLayerDefinitionService adornmentLayerDefinitionService;
		readonly HexLineTransformProviderService lineTransformProviderService;
		readonly WpfHexViewMarginProviderCollectionProvider wpfHexViewMarginProviderCollectionProvider;
		readonly IMenuService menuService;
		readonly HexEditorOperationsFactoryService editorOperationsFactoryService;
		readonly HexSpaceReservationStackProvider spaceReservationStackProvider;
		readonly HexBufferLineProviderFactoryService hexBufferLineProviderFactoryService;
		readonly VSTC.IClassificationTypeRegistryService classificationTypeRegistryService;

		public override VSTE.ITextViewRoleSet AllPredefinedRoles => new TE.TextViewRoleSet(allPredefinedRolesList);
		public override VSTE.ITextViewRoleSet DefaultRoles => new TE.TextViewRoleSet(defaultRolesList);
		public override VSTE.ITextViewRoleSet NoRoles => new TE.TextViewRoleSet(Array.Empty<string>());
		static readonly string[] allPredefinedRolesList = new string[] {
			PredefinedHexViewRoles.Analyzable,
			PredefinedHexViewRoles.Debuggable,
			PredefinedHexViewRoles.Document,
			PredefinedHexViewRoles.Editable,
			PredefinedHexViewRoles.Interactive,
			PredefinedHexViewRoles.PrimaryDocument,
			PredefinedHexViewRoles.Structured,
			PredefinedHexViewRoles.Zoomable,
			PredefinedHexViewRoles.CanHaveBackgroundImage,
			PredefinedHexViewRoles.CanHaveCurrentLineHighlighter,
			PredefinedHexViewRoles.CanHaveColumnLineSeparator,
		};
		static readonly string[] defaultRolesList = new string[] {
			PredefinedHexViewRoles.Analyzable,
			PredefinedHexViewRoles.Document,
			PredefinedHexViewRoles.Editable,
			PredefinedHexViewRoles.Interactive,
			PredefinedHexViewRoles.Structured,
			PredefinedHexViewRoles.Zoomable,
			PredefinedHexViewRoles.CanHaveBackgroundImage,
			PredefinedHexViewRoles.CanHaveCurrentLineHighlighter,
			PredefinedHexViewRoles.CanHaveColumnLineSeparator,
		};

		sealed class GuidObjectsProvider : IGuidObjectsProvider {
			readonly WpfHexView wpfHexView;
			readonly Func<GuidObjectsProviderArgs, IEnumerable<GuidObject>> createGuidObjects;

			public GuidObjectsProvider(WpfHexView wpfHexView, Func<GuidObjectsProviderArgs, IEnumerable<GuidObject>> createGuidObjects) {
				this.wpfHexView = wpfHexView;
				this.createGuidObjects = createGuidObjects;
			}

			public IEnumerable<GuidObject> GetGuidObjects(GuidObjectsProviderArgs args) {
				yield return new GuidObject(MenuConstants.GUIDOBJ_WPF_HEXVIEW_GUID, wpfHexView);

				//TODO: Return hex position

				if (createGuidObjects != null) {
					foreach (var guidObject in createGuidObjects(args))
						yield return guidObject;
				}
			}
		}

		[ImportingConstructor]
		HexEditorFactoryServiceImpl(HexEditorOptionsFactoryService hexEditorOptionsFactoryService, ICommandService commandService, [ImportMany] IEnumerable<Lazy<WpfHexViewCreationListener, IDeferrableTextViewRoleMetadata>> wpfHexViewCreationListeners, FormattedHexSourceFactoryService formattedHexSourceFactoryService, HexViewClassifierAggregatorService hexViewClassifierAggregatorService, HexAndAdornmentSequencerFactoryService hexAndAdornmentSequencerFactoryService, HexClassificationFormatMapService classificationFormatMapService, HexEditorFormatMapService editorFormatMapService, HexAdornmentLayerDefinitionService adornmentLayerDefinitionService, HexLineTransformProviderService lineTransformProviderService, WpfHexViewMarginProviderCollectionProvider wpfHexViewMarginProviderCollectionProvider, IMenuService menuService, HexEditorOperationsFactoryService editorOperationsFactoryService, HexSpaceReservationStackProvider spaceReservationStackProvider, HexBufferLineProviderFactoryService hexBufferLineProviderFactoryService, VSTC.IClassificationTypeRegistryService classificationTypeRegistryService) {
			this.hexEditorOptionsFactoryService = hexEditorOptionsFactoryService;
			this.commandService = commandService;
			this.wpfHexViewCreationListeners = wpfHexViewCreationListeners.ToArray();
			this.formattedHexSourceFactoryService = formattedHexSourceFactoryService;
			this.hexViewClassifierAggregatorService = hexViewClassifierAggregatorService;
			this.hexAndAdornmentSequencerFactoryService = hexAndAdornmentSequencerFactoryService;
			this.classificationFormatMapService = classificationFormatMapService;
			this.editorFormatMapService = editorFormatMapService;
			this.adornmentLayerDefinitionService = adornmentLayerDefinitionService;
			this.lineTransformProviderService = lineTransformProviderService;
			this.wpfHexViewMarginProviderCollectionProvider = wpfHexViewMarginProviderCollectionProvider;
			this.menuService = menuService;
			this.editorOperationsFactoryService = editorOperationsFactoryService;
			this.spaceReservationStackProvider = spaceReservationStackProvider;
			this.hexBufferLineProviderFactoryService = hexBufferLineProviderFactoryService;
			this.classificationTypeRegistryService = classificationTypeRegistryService;
		}

		public override WpfHexView Create(HexBuffer buffer, HexViewCreatorOptions options) =>
			Create(buffer, DefaultRoles, hexEditorOptionsFactoryService.GlobalOptions, options);

		public override WpfHexView Create(HexBuffer buffer, VSTE.ITextViewRoleSet roles, HexViewCreatorOptions options) =>
			Create(buffer, roles, hexEditorOptionsFactoryService.GlobalOptions, options);

		public override WpfHexView Create(HexBuffer buffer, VSTE.ITextViewRoleSet roles, VSTE.IEditorOptions parentOptions, HexViewCreatorOptions options) {
			if (buffer == null)
				throw new ArgumentNullException(nameof(buffer));
			if (roles == null)
				throw new ArgumentNullException(nameof(roles));
			if (parentOptions == null)
				throw new ArgumentNullException(nameof(parentOptions));

			var wpfHexView = new WpfHexViewImpl(buffer, roles, parentOptions, hexEditorOptionsFactoryService, commandService, formattedHexSourceFactoryService, hexViewClassifierAggregatorService, hexAndAdornmentSequencerFactoryService, hexBufferLineProviderFactoryService, classificationFormatMapService, editorFormatMapService, adornmentLayerDefinitionService, lineTransformProviderService, spaceReservationStackProvider, wpfHexViewCreationListeners, classificationTypeRegistryService);

			if (options?.MenuGuid != null) {
				var guidObjectsProvider = new GuidObjectsProvider(wpfHexView, options?.CreateGuidObjects);
				menuService.InitializeContextMenu(wpfHexView.VisualElement, options.MenuGuid.Value, guidObjectsProvider, null/*TODO: pass in an instance*/);
			}

			HexViewCreated?.Invoke(this, new HexViewCreatedEventArgs(wpfHexView));

			return wpfHexView;
		}

		public override WpfHexViewHost CreateHost(WpfHexView wpfHexView, bool setFocus) {
			if (wpfHexView == null)
				throw new ArgumentNullException(nameof(wpfHexView));
			return new WpfHexViewHostImpl(wpfHexViewMarginProviderCollectionProvider, wpfHexView, editorOperationsFactoryService, setFocus);
		}

		public override VSTE.ITextViewRoleSet CreateTextViewRoleSet(IEnumerable<string> roles) => new TE.TextViewRoleSet(roles);
		public override VSTE.ITextViewRoleSet CreateTextViewRoleSet(params string[] roles) => new TE.TextViewRoleSet(roles);
	}
}
