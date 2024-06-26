using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommonwealthCartography.Forms;

namespace CommonwealthCartography
{
	// The main form with map preview and all GUI controls inside it
	public partial class FormMaster : Form
	{
		static List<MapItem> legendItems = new List<MapItem>();
		static List<MapItem> searchResults = new List<MapItem>();

		static List<Space> spaces;

		static FormMaster self;
		static bool isDrawing = false;
		static bool followUpDrawRequested = false; // Somebody requested us to re-draw *again* after the current draw
		static bool followUpDrawBaseLayer = false;

		static CancellationTokenSource mapDrawCancTokenSource;
		static CancellationToken mapDrawCancToken;

		const int searchResultsLargeAmount = 50; // Size of search results at which we need to disable the DataGridView before we populate it
		static bool warnedMISCNotUsed = false; // Flag for if we've displayed this warning, so as to only show once per run
		static bool forceDrawBaseLayer = false; // Force a base layer redraw at the next draw event
		static Point lastMouseDownPos;

		const float designDPI = 96; // The DPI which the form was designed for

		public FormMaster()
		{
			if (self != null)
			{
				throw new InvalidOperationException("Cannot create multiple instances of the master form!");
			}

			self = this;

			InitializeComponent();

			// Hack to prevent the panels inheriting the backcolor of the whole splitter
			splitContainerMain.Panel1.BackColor = splitContainerMain.Panel1.BackColor;
			splitContainerMain.Panel2.BackColor = splitContainerMain.Panel2.BackColor;

			// Makes the splitcontainer divider visible
			splitContainerMain.BackColor = SystemColors.ControlDark;

			UpdateProgressBar(0, "Starting up...");

			float dpiScaling = 1 / (designDPI / CreateGraphics().DpiX);
			splitContainerMain.Panel1.AutoScrollMinSize = new Size(
				(int)(splitContainerMain.Panel1.AutoScrollMinSize.Width * dpiScaling),
				(int)(splitContainerMain.Panel1.AutoScrollMinSize.Height * dpiScaling));

			// Cleanup on launch in case it didn't run last time
			IOManager.Cleanup();

			// Populate UI elements
			SizeMapToFrame();
			PopulateSignatureFilterList();
			SelectRecommendedSignatures();
			PopulateLockTypeFilterList();
			PopulateVariableNPCSpawnList();
			PopulateScrapList();
			ProvideSearchHint();
			ProvideRegionSearchHint();

			// Auto-select text box text and use search button with enter
			textBoxSearch.Select();
			textBoxSearch.SelectAll();
			AcceptButton = buttonSearch;

			// Load settings from the preferences file, if applicable.
			SettingsManager.LoadSettings();

			// Apply min/max values according to current settings
			numericMinZ.Increment = SettingsSpace.GetHeightBinSize();
			numericMaxZ.Increment = numericMinZ.Increment;

			// Apply UI layouts according to current settings
			UpdateResultsLockTypeColumnVisibility();
			UpdateVolumeEnabledState(false);
			UpdateFillRegionsState(false);
			UpdateShowRefFormIDState(false);
			UpdatePlotModeUI();
			UpdateHeatMapColorMode(false);
			UpdateHeatMapResolution(false);
			UpdateTopographColorBands(false);
			UpdateMapBackgroundSettings(false);
			UpdateMapHighlightWater(false);
			UpdateMapGrayscale(false);
			UpdateMapMarker(false);
			UpdateLegendStyle(false);
			UpdateShowFormID();
			UpdateSearchInAllSpaces(false);

			// Check for updates, only notify if update found
			UpdateChecker.CheckForUpdate(false);

			// This ultimately causes the first map draw, as the space list changes index and draws the final selected Space
			PopulateSpaceList();

			tabControlMainSearch.SelectedIndex = 1;

			UpdateProgressBar(0);
		}

		// All Methods not directly responding to UI input
		#region Methods

		// Enable/Disable/Update UI elements based on if a draw is now in progress
		static void SetIsDrawing(bool isDrawing)
		{
			FormMaster.isDrawing = isDrawing;
			self.buttonDrawMap.Text = isDrawing ? "Cancel" : "Update Map";

			if (!isDrawing)
			{
				UpdateProgressBar(0);
			}

			FormsHelper.EnableMenuStrip(self.mapMenuItem, !isDrawing);
			FormsHelper.EnableMenuStrip(self.plotSettingsMenuItem, !isDrawing);

			self.buttonAddToLegend.Enabled = !isDrawing;
			self.buttonRemoveFromLegend.Enabled = !isDrawing;

			mapDrawCancTokenSource = new CancellationTokenSource();
			mapDrawCancToken = mapDrawCancTokenSource.Token;

			if (!isDrawing)
			{
				self.UpdatePlotModeUI();
				self.UpdateCellorWorldExclusiveState();

				// If somebody raised the flag to followup this convening draw with another
				if (followUpDrawRequested)
				{
					DrawMap(followUpDrawBaseLayer);

					// New draw commenced - lower the flag
					followUpDrawRequested = false;
					followUpDrawBaseLayer = false;
				}
			}
		}

		public static bool IsDrawing()
		{
			return isDrawing;
		}

		public static void UpdateUIAsync(Action action)
		{
			if (self.InvokeRequired)
			{
				self.BeginInvoke(action);
			}
			else
			{
				action.Invoke();
			}
		}

		// Updates the progress value and label text of the main form progress bar.
		public static void UpdateProgressBar(double amount, string labelText, bool forceRefresh)
		{
			UpdateProgressBarText(labelText, forceRefresh);

			UpdateUIAsync(() =>
			{
				self.progressBarMain.Value = (int)(self.progressBarMain.Maximum * amount);
			});
		}

		static void UpdateProgressBarText(string labelText, bool forceRefresh)
		{
			UpdateUIAsync(() =>
			{
				self.labelProgressBar.Text = labelText;
				self.labelProgressBar.Location = new Point((self.Width / 2) - (self.labelProgressBar.Width / 2), self.labelProgressBar.Location.Y);

				// Causes the label to repaint, otherwise it won't visually change until we hand back to the UI thead
				if (forceRefresh)
				{
					self.labelProgressBar.Refresh();
				}
			});
		}

		public static void UpdateProgressBar(string labelText, bool forceRefresh)
		{
			UpdateProgressBarText(labelText, forceRefresh);
		}

		public static void UpdateProgressBar(double amount, string labelText)
		{
			UpdateProgressBar(amount, labelText, false);
		}

		public static void UpdateProgressBar(double amount)
		{
			// Ensure the bar doesn't stay at full done, and "empties" when a task is finished
			if (amount == 1)
			{
				amount = 0;
			}

			UpdateProgressBar(amount, amount == 0 ? "Ready" : self.labelProgressBar.Text);
		}

		public static void UpdateProgressBar(string labelText)
		{
			UpdateProgressBarText(labelText, false);
		}

		public static void SetMapImage(Image image)
		{
			UpdateUIAsync(() =>
			{
				self.pictureBoxMapPreview.Image = image;
			});
		}

		void SizeMapToFrame()
		{
			pictureBoxMapPreview.Width = splitContainerMain.Panel2.Width;
			pictureBoxMapPreview.Height = splitContainerMain.Panel2.Height;
			pictureBoxMapPreview.Location = new Point(0, 0);
		}

		// Dynamically fill the Signature filter with every signature present based on the data
		void PopulateSignatureFilterList()
		{
			listViewFilterSignatures.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

			List<string> signatures = Database.GetSignatures();
			List<string> orderedSignatures = new List<string>();

			// First run through the suggested order list. If any of our database signature items are in there, process them FIRST
			// This ensures items are added to the list in order
			foreach (string signature in DataHelper.suggestedSignatureSort)
			{
				// If this item is in the suggested sort - add it to the final items
				if (signatures.Contains(signature))
				{
					orderedSignatures.Add(signature);
				}
			}

			// Now we have processed some suggested sort items, any remaining signatures can be processed next
			foreach (string signature in signatures)
			{
				// This item was NOT picked up when we passed through the suggested sort - so add it onto the end
				if (!orderedSignatures.Contains(signature))
				{
					orderedSignatures.Add(signature);
				}
			}

			// Finally, now we have a list which starts with the suggested order, and ends with any unsorted items
			// We can add them to the ListView on the form
			foreach (string signature in orderedSignatures)
			{
				ListViewItem thisItem = listViewFilterSignatures.Items.Add(signature);
				thisItem.Text = DataHelper.ConvertSignature(thisItem.Text, false);
				thisItem.ToolTipText = DataHelper.GetSignatureDescription(signature);
				thisItem.Checked = true;
			}
		}

		// Selects only the recommended signature filters
		void SelectRecommendedSignatures()
		{
			foreach (ListViewItem item in listViewFilterSignatures.Items)
			{
				// check the item if it's in the list of recommended signatures, otherwise uncheck
				item.Checked = DataHelper.recommendedSignatures.Contains(DataHelper.ConvertSignature(item.Text, true));
			}
		}

		// Dynamically fill the Lock Type filter with every lock type present based on the data
		void PopulateLockTypeFilterList()
		{
			List<string> lockLevels = Database.GetLockTypes();
			List<string> orderedLockLevels = new List<string>();

			// First run through the suggested order list. If any of our database lock types are in there, process them FIRST
			// This ensures items are added to the list in order
			foreach (string lockLevel in DataHelper.suggestedLockLevelSort)
			{
				// If this item is in the suggested sort - add it to the final items
				if (lockLevels.Contains(lockLevel))
				{
					orderedLockLevels.Add(lockLevel);
				}
			}

			// Now we have processed some suggested sort items, any remaining lock types can be processed next
			foreach (string lockLevel in lockLevels)
			{
				// This item was NOT picked up when we passed through the suggested sort - so add it onto the end
				if (!orderedLockLevels.Contains(lockLevel))
				{
					orderedLockLevels.Add(lockLevel);
				}
			}

			// Finally, now we have a list which starts with the suggested order, and ends with any unsorted items
			// We can add them to the ListView on the form
			foreach (string lockLevel in orderedLockLevels)
			{
				ListViewItem thisItem = listViewFilterLockTypes.Items.Add(lockLevel);
				thisItem.Text = DataHelper.ConvertLockLevel(thisItem.Text, false);
				thisItem.Checked = true;
			}
		}

		// Fill the list of Variable NPC Spawns that can be chosen to search
		void PopulateVariableNPCSpawnList()
		{
			foreach (string npcSpawn in Database.GetVariableNPCTypes())
			{
				listBoxNPC.Items.Add(npcSpawn);
			}

			listBoxNPC.SelectedIndex = 0;
		}

		// Fill the list of Scrap types that can be chosen to search
		void PopulateScrapList()
		{
			foreach (string scrapType in Database.GetScrapTypes())
			{
				listBoxScrap.Items.Add(scrapType);
			}

			listBoxScrap.SelectedIndex = 0;
		}

		// Populate the Space combo box with all spaces
		void PopulateSpaceList()
		{
			// Return if the list is already populated
			if (comboBoxSpace.Items.Count != 0)
			{
				return;
			}

			spaces = Database.GetAllSpaces();

			foreach (Space space in spaces)
			{
				comboBoxSpace.Items.Add(space.displayName + " (" + space.editorID + ")");
			}

			// Fires indexChanged function which causes a map draw
			comboBoxSpace.SelectedIndex = 0;
		}

		// Fill the search box with a suggested search term.
		void ProvideSearchHint()
		{
			List<string> searchTermHints = new List<string>
			{
				"Nuka Cola",
				"Caps Stash",
				"Thistle",
				"Trunk Boss",
				"Rare",
				"Teddy Bear",
				"Workbench",
				"PowerArmorFurniture",
				"RETrigger",
				"Flora",
				"Pre War Money",
				"Fusion Core",
				"UFO Crash Site",
				"Jet",
				"San Francisco Sunlights",
				"Synth",
				"PerkMag",
			};

			textBoxSearch.Text = searchTermHints[new Random().Next(searchTermHints.Count)];
		}

		// Fill the region search box with a suggested search term.
		void ProvideRegionSearchHint()
		{
			List<string> searchTermHints = new List<string>
			{
				"Border",
				"MapDistrict",
				"Glowing Sea Audio",
			};

			textBoxRegionSearch.Text = searchTermHints[new Random().Next(searchTermHints.Count)];
		}

		// Toggle availability of controls which depend on current Space
		void UpdateCellorWorldExclusiveState()
		{
			groupBoxHeightCropping.Enabled = !SettingsSpace.CurrentSpaceIsWorld();
			mapMarkersMenuItem.Enabled = SettingsSpace.CurrentSpaceIsWorld();
			highlightWaterMenuItem.Enabled = SettingsSpace.CurrentSpaceIsWorld();
			normalBackgroundMenuItem.Enabled = SettingsSpace.CurrentSpaceIsMainWorldspace(); // Only Commonwealth/Far Harbor/NukaWorld has a "normal" UI texture map
			satelliteBackgroundMenuItem.Enabled = SettingsSpace.CurrentSpaceIsWorld();
		}

		// Update the visiblity of the lock type column in the results view, given current settings
		void UpdateResultsLockTypeColumnVisibility()
		{
			// Check if the lock type filter is in use - if it is we probably want to show the column.
			if (tabControlMainSearch.SelectedTab == tabPageStandard)
			{
				foreach (ListViewItem lockType in listViewFilterLockTypes.Items)
				{
					if (!lockType.Checked)
					{
						gridViewSearchResults.Columns["columnSearchLockLevel"].Visible = true;
						return;
					}
				}
			}

			// If they are all checked, or this is not a search where filters apply - hide the column again
			gridViewSearchResults.Columns["columnSearchLockLevel"].Visible = false;
		}

		// Update the map settings > draw volume check, based on current settings
		void UpdateVolumeEnabledState(bool reDraw)
		{
			drawVolumesMenuItem.Checked = SettingsPlot.drawVolumes;

			if (reDraw)
			{
				DrawMap(false);
			}
		}

		// Update the map settings > fill regions check, based on current settings
		void UpdateFillRegionsState(bool reDraw)
		{
			fillRegionsMenuItem.Checked = SettingsPlot.fillRegions;

			if (reDraw)
			{
				DrawMap(false);
			}
		}

		// Update the map settings > Show Reference Form IDs check, based on current settings
		void UpdateShowRefFormIDState(bool reDraw)
		{
			showReferenceFormIDsMenuItem.Checked = SettingsPlot.labelInstanceIDs;

			if (reDraw)
			{
				DrawMap(false);
			}
		}

		// Handle a change in plot mode
		void UpdatePlotMode(bool reDraw)
		{
			PlotIcon.ResetCache(); // Reset the plot icon cache, as we are changing plot modes

			if (reDraw)
			{
				DrawMap(false);
			}

			UpdatePlotModeUI();
		}

		// Refresh the UI enabled state based on the plot mode
		void UpdatePlotModeUI()
		{
			UncheckAllPlotModes();

			switch (SettingsPlot.mode)
			{
				case SettingsPlot.Mode.Icon:
					modeIconMenuItem.Checked = true;
					FormsHelper.EnableMenuStrip(heatmapSettingsMenuItem, false);
					FormsHelper.EnableMenuStrip(TopographColorBandsMenuItem, false);
					FormsHelper.EnableMenuStrip(clusterSettingsMenuItem, false);
					FormsHelper.EnableMenuStrip(drawVolumesMenuItem, true);
					FormsHelper.EnableMenuStrip(showReferenceFormIDsMenuItem, true);
					break;
				case SettingsPlot.Mode.Heatmap:
					modeHeatmapMenuItem.Checked = true;
					FormsHelper.EnableMenuStrip(heatmapSettingsMenuItem, true);
					FormsHelper.EnableMenuStrip(TopographColorBandsMenuItem, false);
					FormsHelper.EnableMenuStrip(clusterSettingsMenuItem, false);
					FormsHelper.EnableMenuStrip(drawVolumesMenuItem, false);
					FormsHelper.EnableMenuStrip(showReferenceFormIDsMenuItem, false);
					break;
				case SettingsPlot.Mode.Topography:
					modeTopographyMenuItem.Checked = true;
					FormsHelper.EnableMenuStrip(heatmapSettingsMenuItem, false);
					FormsHelper.EnableMenuStrip(TopographColorBandsMenuItem, true);
					FormsHelper.EnableMenuStrip(clusterSettingsMenuItem, false);
					FormsHelper.EnableMenuStrip(drawVolumesMenuItem, true);
					FormsHelper.EnableMenuStrip(showReferenceFormIDsMenuItem, true);
					break;
				case SettingsPlot.Mode.Cluster:
					modeClusterMenuItem.Checked = true;
					FormsHelper.EnableMenuStrip(heatmapSettingsMenuItem, false);
					FormsHelper.EnableMenuStrip(TopographColorBandsMenuItem, false);
					FormsHelper.EnableMenuStrip(clusterSettingsMenuItem, true);
					FormsHelper.EnableMenuStrip(drawVolumesMenuItem, false);
					FormsHelper.EnableMenuStrip(showReferenceFormIDsMenuItem, false);
					break;
			}
		}

		// Remove any check boxes from the plot mode menu choices
		void UncheckAllPlotModes()
		{
			modeIconMenuItem.Checked = false;
			modeHeatmapMenuItem.Checked = false;
			modeTopographyMenuItem.Checked = false;
			modeClusterMenuItem.Checked = false;
		}

		// Remove any check boxes from the legend style menu choices
		void UncheckAllLegendStyles()
		{
			compactLegendMenuItem.Checked = false;
			extendedLegendMenuItem.Checked = false;
			hiddenLegendMenuItem.Checked = false;
		}

		// Update the UI with the currently selected heatmap color mode
		void UpdateHeatMapColorMode(bool reDraw)
		{
			switch (SettingsPlotHeatmap.colorMode)
			{
				case SettingsPlotHeatmap.ColorMode.Mono:
					monoColorModeMenuItem.Checked = true;
					duoColorModeMenuItem.Checked = false;
					break;
				case SettingsPlotHeatmap.ColorMode.Duo:
					monoColorModeMenuItem.Checked = false;
					duoColorModeMenuItem.Checked = true;
					break;
			}

			if (reDraw && SettingsPlot.IsHeatmap())
			{
				DrawMap(false);
			}
		}

		// Update the UI with the currently selected heatmap resolution
		void UpdateHeatMapResolution(bool reDraw)
		{
			UncheckAllResolutions();
			switch (SettingsPlotHeatmap.resolution)
			{
				case 128:
					resolution128MenuItem.Checked = true;
					break;
				case 256:
					resolution256MenuItem.Checked = true;
					break;
				case 512:
					resolution512MenuItem.Checked = true;
					break;
				case 1024:
					resolution1024MenuItem.Checked = true;
					break;
				default:
					Notify.Error("Unsupported Heatmap resolution " + SettingsPlotHeatmap.resolution);
					break;
			}

			if (reDraw && SettingsPlot.IsHeatmap())
			{
				DrawMap(false);
			}
		}

		// Update the UI checkboxes with the currently selected Topographic color amount
		void UpdateTopographColorBands(bool reDraw)
		{
			UncheckAllColorBands();

			switch (SettingsPlotTopograph.colorBands)
			{
				case 2:
					colorBand2MenuItem.Checked = true;
					break;
				case 3:
					colorBand3MenuItem.Checked = true;
					break;
				case 4:
					colorBand4MenuItem.Checked = true;
					break;
				case 5:
					colorBand5MenuItem.Checked = true;
					break;
				default:
					SettingsPlotTopograph.colorBands = SettingsPlotTopograph.defaultColorBands;
					Notify.Error("Unsupported number of Topograph color bands. Defaulting to " + SettingsPlotTopograph.colorBands);
					UpdateTopographColorBands(reDraw);
					break;
			}

			if (reDraw && SettingsPlot.IsTopographic())
			{
				DrawMap(false);
			}
		}

		// Update check marks in the UI with current MapSettings, and redraw the map if true
		void UpdateMapBackgroundSettings(bool reDraw)
		{
			UncheckAllBackgrounds();

			switch (SettingsMap.background)
			{
				case SettingsMap.Background.Normal:
					normalBackgroundMenuItem.Checked = true;
					break;

				case SettingsMap.Background.Satellite:
					satelliteBackgroundMenuItem.Checked = true;
					break;

				case SettingsMap.Background.None:
					noneBackgroundMenuItem.Checked = true;
					break;

				default:
					throw new NotSupportedException("Unsupported background type " + SettingsMap.background);
			}

			if (reDraw)
			{
				DrawMap(true);
			}
		}

		// Update check mark in the UI with current MapSettings for Highlight Water
		void UpdateMapHighlightWater(bool reDraw)
		{
			highlightWaterMenuItem.Checked = SettingsMap.highlightWater;

			if (reDraw)
			{
				DrawMap(true);
			}
		}

		// Update check mark in the UI with current MapSettings for grayscale
		void UpdateMapGrayscale(bool reDraw)
		{
			grayscaleMenuItem.Checked = SettingsMap.grayScale;

			if (reDraw)
			{
				DrawMap(true);
			}
		}

		// Update check mark in the UI with current MapSettings for Map Markers
		void UpdateMapMarker(bool reDraw)
		{
			showMapLabelsMenuItem.Checked = SettingsMap.showMapLabels;
			showMapIconsMenuItem.Checked = SettingsMap.showMapIcons;

			if (reDraw)
			{
				DrawMap(true);
			}
		}

		// Update check marks for legend style with SettingsMap
		void UpdateLegendStyle(bool reDraw)
		{
			UncheckAllLegendStyles();

			switch (SettingsMap.legendMode)
			{
				case SettingsMap.LegendMode.Compact:
					compactLegendMenuItem.Checked = true;
					break;
				case SettingsMap.LegendMode.Extended:
					extendedLegendMenuItem.Checked = true;
					break;
				case SettingsMap.LegendMode.Hidden:
					hiddenLegendMenuItem.Checked = true;
					break;

				default:
					throw new NotSupportedException("Unknown legend style " + SettingsMap.legendMode);
			}

			if (reDraw)
			{
				DrawMap(true);
			}
		}

		// Update check mark in the UI for Show FormID option
		void UpdateShowFormID()
		{
			showFormIDMenuItem.Checked = SettingsSearch.showFormID;
			gridViewSearchResults.Columns["columnSearchFormID"].Visible = SettingsSearch.showFormID;
		}

		void UpdateSearchInAllSpaces(bool searchAgain)
		{
			searchInAllSpacesMenuItem.Checked = SettingsSearch.searchInAllSpaces;

			if (searchAgain)
			{
				AcceptButton.PerformClick();
			}
		}

		// Unselect all resolution options under heatmap resolution. Used to remove any current selection
		void UncheckAllResolutions()
		{
			resolution128MenuItem.Checked = false;
			resolution256MenuItem.Checked = false;
			resolution512MenuItem.Checked = false;
			resolution1024MenuItem.Checked = false;
		}

		// Unselect all topographic color band amount options
		void UncheckAllColorBands()
		{
			colorBand2MenuItem.Checked = false;
			colorBand3MenuItem.Checked = false;
			colorBand4MenuItem.Checked = false;
			colorBand5MenuItem.Checked = false;
		}

		void UncheckAllBackgrounds()
		{
			normalBackgroundMenuItem.Checked = false;
			satelliteBackgroundMenuItem.Checked = false;
			noneBackgroundMenuItem.Checked = false;
		}

		// Return every MapItem in the items to plot
		public static List<MapItem> GetAllLegendItems()
		{
			return legendItems.OrderBy(l => l.legendGroup).ToList();
		}

		// Return every non-REGN (Type Region) MapItem in the items to plot
		public static List<MapItem> GetNonRegionLegendItems()
		{
			return legendItems.Where(l => !l.IsRegion()).ToList();
		}

		// Return only REGN (Type Region) MapItem in the items to plot
		public static List<MapItem> GetRegionLegendItems()
		{
			return legendItems.Where(l => l.IsRegion()).ToList();
		}

		// Collect the enabled signatures from the UI to a list for use by a query
		List<string> GetEnabledSignatures()
		{
			List<string> filteredSignatures = new List<string>();
			foreach (ListViewItem signature in listViewFilterSignatures.Items)
			{
				if (signature.Checked)
				{
					filteredSignatures.Add(DataHelper.ConvertSignature(signature.Text, true));
				}
			}

			return filteredSignatures;
		}

		// Collect the enabled lock types from the UI to a list for use by a query
		List<string> GetEnabledLockTypes()
		{
			List<string> lockTypes = new List<string>();
			foreach (ListViewItem lockType in listViewFilterLockTypes.Items)
			{
				if (lockType.Checked)
				{
					lockTypes.Add(DataHelper.ConvertLockLevel(lockType.Text, true));
				}
			}

			return lockTypes;
		}

		Regex gravy = new Regex("a(nother)? settlement (that )?((needs?)|(ha((s)|(ve)) ((asked)|(sent word)) for)) y?our help");

		bool Radiant()
		{
			if (gravy.IsMatch(textBoxSearch.Text))
			{
				Notify.Error("I'll mark it on your map.");
				return true;
			}

			return false;
		}

		// Warn the user if they appear to be trying to search for something that might actually be in a MISC, but they have unselected it
		void WarnWhenMISCNotSelected()
		{
			// Already warned - we only do this once per session
			if (warnedMISCNotUsed)
			{
				return;
			}

			List<string> enabledSignatures = GetEnabledSignatures();
			if (!enabledSignatures.Contains("MISC"))
			{
				// A list of signatures that seem to typically be represented by MISC
				foreach (string signatureToWarn in DataHelper.typicalMISCItems)
				{
					if (enabledSignatures.Contains(signatureToWarn))
					{
						Notify.Info(
							$"Your search results may be restricted because you have '{DataHelper.ConvertSignature("MISC", false)}' unchecked under the categories filter.\n" +
							$"Many desirable items in Fallout 4 are categorized generically as '{DataHelper.ConvertSignature("MISC", false)}'.\n" +
							"If you can't find what you're looking for, make sure to enable it and search again.");
						warnedMISCNotUsed = true;
						return;
					}
				}
			}
		}

		// Warn the user if all filters of a category are blank and therefore no results would match
		bool WarnWhenAllFiltersBlank()
		{
			if (GetEnabledSignatures().Count == 0 || GetEnabledLockTypes().Count == 0)
			{
				Notify.Info(
					"No search results were found because you have disabled every filter for a category.\n" +
					"You must enable at least one filter per category to find anything.");
				return true;
			}

			return false;
		}

		// Totally clears all search results
		void ClearSearchResults()
		{
			searchResults = new List<MapItem>();
			UpdateSearchResultsGrid();
		}

		// Wipe and re-populate the search results UI element with the items in "List<MapItem> searchResults"
		void UpdateSearchResultsGrid()
		{
			FormsHelper.ClearDataGridViewSort(gridViewSearchResults);

			bool labelColumnShouldBeShown = false;

			int count = searchResults.Count;
			if (count > searchResultsLargeAmount)
			{
				// When adding multiple items, it is much more efficient to disable the grid
				// Although disabling has an overhead so we only disable for large result sets
				gridViewSearchResults.Enabled = false;
			}

			gridViewSearchResults.Rows.Clear();
			int index = 0;

			foreach (MapItem mapItem in searchResults)
			{
				gridViewSearchResults.Rows.Add(
					mapItem.uniqueIdentifier,
					mapItem.label,
					mapItem.editorID,
					mapItem.displayName,
					DataHelper.ConvertSignature(mapItem.signature, false),
					string.Join(", ", DataHelper.ConvertLockLevel(mapItem.filteredLockTypes, false)),
					mapItem.weight == -1 ? "Unknown" : mapItem.weight.ToString(),
					mapItem.count,
					mapItem.spaceName,
					mapItem.spaceEditorID,
					index); // Index relates the UI row to the List<MapItem>, even if the UI is sorted

				// If the label column is not already visible, but this item has a label, set it to visible
				// (Does not act until end of grid fill to avoid flashing)
				if (!labelColumnShouldBeShown && !string.IsNullOrEmpty(mapItem.label))
				{
					labelColumnShouldBeShown = true;
				}

				index++;
			}

			if (!gridViewSearchResults.Enabled)
			{
				gridViewSearchResults.Enabled = true;
			}

			gridViewSearchResults.Columns["columnLabel"].Visible = labelColumnShouldBeShown;

			GC.Collect();
		}

		void NotifyIfNoResults()
		{
			if (searchResults.Count == 0)
			{
				Notify.Info("No results found for that search.");
			}
		}

		// Wipe and re-populate the Legend Grid View with items contained in "List<MapItem> legendItems"
		void UpdateLegendGrid(MapItem lastSelectedItem)
		{
			int count = legendItems.Count;
			if (count > searchResultsLargeAmount)
			{
				// When adding multiple items, it is much more efficient to disable the grid
				// Although disabling has an overhead so we only disable for large result sets
				gridViewLegend.Enabled = false;
			}

			gridViewLegend.Rows.Clear();

			foreach (MapItem legend in legendItems)
			{
				gridViewLegend.Rows.Add(legend.legendGroup, legend.GetLegendText(false));
			}

			if (!gridViewLegend.Enabled)
			{
				gridViewLegend.Enabled = true;
			}

			// If the list is populated we can select an index
			if (gridViewLegend.RowCount >= 1)
			{
				// Provide a default at the end of the list
				int indexToSelect = gridViewLegend.RowCount - 1;

				// If provided, find and re-select the last selected item
				if (lastSelectedItem != null)
				{
					indexToSelect = legendItems.IndexOf(legendItems.Where(m => m.editorID == lastSelectedItem.editorID).First());
				}

				gridViewLegend.ClearSelection();

				// Select and scroll to the best new index
				gridViewLegend.Rows[indexToSelect].Selected = true;
				gridViewLegend.FirstDisplayedScrollingRowIndex = indexToSelect;
			}
		}

		// Finds the unique instances of legend groups with overridden legend texts, in order for them to be merged together
		public static Dictionary<int, string> GatherOverriddenLegendTexts()
		{
			Dictionary<int, string> overriddenTexts = new Dictionary<int, string>();

			foreach (MapItem mapItem in legendItems)
			{
				if (overriddenTexts.ContainsKey(mapItem.legendGroup) || string.IsNullOrWhiteSpace(mapItem.overridingLegendText))
				{
					continue; // Either this isn't overridden, or we already have this one - skip
				}

				// This must be a new MapItem with overridden text - add it to the Dictionary
				else if (!string.IsNullOrWhiteSpace(mapItem.overridingLegendText))
				{
					overriddenTexts.Add(mapItem.legendGroup, mapItem.overridingLegendText);
				}
			}

			return overriddenTexts;
		}

		// Wipe away the legend items and update the UI. Doesn't re-draw the map
		void ClearLegend()
		{
			foreach (MapItem item in legendItems)
			{
				item.overridingLegendText = string.Empty;
			}

			legendItems = new List<MapItem>();
			UpdateLegendGrid(null);
		}

		// Find the next-lowest free legend group value in LegendItems
		int FindLowestAvailableLegendGroupValue()
		{
			int n = legendItems.Count;
			bool[] takenIndices = new bool[n];

			foreach (MapItem item in legendItems)
			{
				if (item.legendGroup < n)
				{
					takenIndices[item.legendGroup] = true;
				}
			}

			int earliestIndex = Array.IndexOf(takenIndices, false);
			return earliestIndex < 0 ? n : earliestIndex;
		}

		// Draws the map, and if a draw is in progress, queues the job up to run immediately after the current draw job
		// Will only stack up to one additional draw job
		public static void QueueDraw(bool drawBaseLayer)
		{
			if (isDrawing)
			{
				followUpDrawRequested = true;
				followUpDrawBaseLayer = drawBaseLayer;
			}
			else
			{
				DrawMap(drawBaseLayer);
			}
		}

		// User-activated draw. Draw the plot points onto the map, if there is anything to plot
		public static async void DrawMap(bool drawBaseLayer)
		{
			if (isDrawing)
			{
				return;
			}

			SetIsDrawing(true);

			try
			{
				if (drawBaseLayer || forceDrawBaseLayer)
				{
					await Task.Run(() => Map.DrawBaseLayer(mapDrawCancToken));
					forceDrawBaseLayer = false;
				}
				else
				{
					await Task.Run(() => Map.Draw(mapDrawCancToken));
				}
			}
			catch (Exception e)
			{
				Notify.Error("Error during map draw.\n\n" + e);
			}

			SetIsDrawing(false);
		}

		static void PreviewMap()
		{
			Map.Open();
		}

		// Render a crude text graph to visualize height distribution in the currently selected space
		static void ShowHeightDistribution()
		{
			string textVisualization = string.Empty;
			int i = 0;
			foreach (double value in SettingsSpace.GetSpace().GetHeightDistribution())
			{
				textVisualization = (i * SettingsSpace.GetHeightBinSize()).ToString().PadLeft(2, '0') + "%:" + new string('#', (int)Math.Round(value)) + "\n" + textVisualization;
				i++;
			}

			MessageBox.Show(textVisualization, "Height distribution for " + SettingsSpace.GetSpace().editorID);
		}

		static void OpenDiscord()
		{
			IOManager.LaunchURL("https://discord.gg/Z2GMpm6rad");
		}

		#endregion

		// All Methods which represent responses to UI input
		#region UI Controls

		// Map > Update Map - analogous to update map button - draw the map
		void Map_UpdateMap(object sender, EventArgs e)
		{
			DrawMap(false);
		}

		// Map > View - Write the map image to disk and launch in default program
		void Map_View(object sender, EventArgs e)
		{
			PreviewMap();
		}

		// Map > Set Title... - Open the Set Map Title form
		void Map_SetTitle(object sender, EventArgs e)
		{
			FormSetTitle formSetTitle = new FormSetTitle();
			formSetTitle.ShowDialog();
		}

		// Map > Background Image > Normal - Toggle the map background to the in-game menu version
		void Map_Image_Normal(object sender, EventArgs e)
		{
			SettingsMap.background = SettingsMap.Background.Normal;
			UpdateMapBackgroundSettings(SettingsSpace.CurrentSpaceIsWorld());
		}

		// Map > Background Image > Satellite - Toggle the map background to the top-down render
		void Map_Image_Satellite(object sender, EventArgs e)
		{
			SettingsMap.background = SettingsMap.Background.Satellite;
			UpdateMapBackgroundSettings(SettingsSpace.CurrentSpaceIsWorld());
		}

		// Map > Background Image > None - Toggle the map background off (transparent)
		void Map_Image_None(object sender, EventArgs e)
		{
			// If we're in a cell and None is already selected (Other options are disabled so user has clicked this to attempt to turn it off)
			if (!SettingsSpace.CurrentSpaceIsWorld() && SettingsMap.background == SettingsMap.Background.None)
			{
				SettingsMap.background = SettingsMap.Background.Normal;
			}
			else
			{
				SettingsMap.background = SettingsMap.Background.None;
			}

			UpdateMapBackgroundSettings(true);
		}

		// Map > Highlight Water - Toggle rendering of water mask overlay on background
		void Map_HighlightWater(object sender, EventArgs e)
		{
			SettingsMap.highlightWater = !SettingsMap.highlightWater;
			UpdateMapHighlightWater(SettingsSpace.CurrentSpaceIsWorld());
		}

		// Map > Brightness... - Open the brightness adjust form
		void Map_Brightness(object sender, EventArgs e)
		{
			FormSetBrightness formSetBrightness = new FormSetBrightness();
			formSetBrightness.ShowDialog();
		}

		// Map > Grayscale - Toggle grayscale drawing of map base layer
		void Map_Grayscale(object sender, EventArgs e)
		{
			SettingsMap.grayScale = !SettingsMap.grayScale;
			UpdateMapGrayscale(true);
		}

		// Map > Map Markers > Labels - toggle rendering map marker labels on map draw
		void Map_MapMarkers_Labels(object sender, EventArgs e)
		{
			SettingsMap.showMapLabels = !SettingsMap.showMapLabels;
			UpdateMapMarker(SettingsSpace.CurrentSpaceIsWorld());
		}

		// Map > Map Markers > Icons - toggle rendering map marker icons on map draw
		void Map_MapMarkers_Icons(object sender, EventArgs e)
		{
			SettingsMap.showMapIcons = !SettingsMap.showMapIcons;
			UpdateMapMarker(SettingsSpace.CurrentSpaceIsWorld());
		}

		// Map > Legend Style > Compact
		void Map_Legend_Compact(object sender, EventArgs e)
		{
			SettingsMap.legendMode = SettingsMap.LegendMode.Compact;
			UpdateLegendStyle(legendItems.Count > 0);
		}

		// Map > Legend Style > Extended
		void Map_Legend_Extended(object sender, EventArgs e)
		{
			SettingsMap.legendMode = SettingsMap.LegendMode.Extended;
			UpdateLegendStyle(legendItems.Count > 0);
		}

		// Map > Legend Style > Hidden
		void Map_Legend_Hidden(object sender, EventArgs e)
		{
			SettingsMap.legendMode = SettingsMap.LegendMode.Hidden;
			UpdateLegendStyle(legendItems.Count > 0);
		}

		// Map > Export To File - Open the export to file dialog
		void Map_Export(object sender, EventArgs e)
		{
			FormExportToFile formExportToFile = new FormExportToFile();
			formExportToFile.ShowDialog();
		}

		// Map > Quick Save
		// Simulates a normal file export operation, but assumes default settings in all cases to quickly get the file written
		void Map_QuickSave(object sender, EventArgs e)
		{
			IOManager.QuickSaveImage(IOManager.OpenImageMode.QuickSaveInExplorer);
		}

		// Map > Clear - Remove legend items and remove plotted layers from the map
		void Map_Clear(object sender, EventArgs e)
		{
			ClearLegend();
			DrawMap(false);
		}

		// Map > Reset - Hard reset the map, all layers, plots, legend items and pan/zoom
		void Map_Reset(object sender, EventArgs e)
		{
			ClearLegend();

			SettingsMap.brightness = SettingsMap.brightnessDefault;
			SettingsMap.background = SettingsMap.backgroundDefault;
			SettingsMap.highlightWater = SettingsMap.highlightWaterDefault;
			SettingsMap.grayScale = SettingsMap.grayScaleDefault;
			SettingsMap.showMapLabels = SettingsMap.showMapLabelsDefault;
			SettingsMap.showMapIcons = SettingsMap.showMapIconsDefault;
			SettingsMap.legendMode = SettingsMap.legendModeDefault;
			SettingsMap.title = SettingsMap.titleDefault;

			comboBoxSpace.SelectedIndex = 0;

			SettingsSpace.minHeightPerc = 0;
			SettingsSpace.maxHeightPerc = 100;
			numericMinZ.Value = 0;
			numericMaxZ.Value = 100;

			UpdateMapBackgroundSettings(false);
			UpdateMapGrayscale(false);
			UpdateMapHighlightWater(false);
			UpdateMapMarker(false);
			UpdateLegendStyle(false);

			SizeMapToFrame();

			DrawMap(true);
		}

		// Search Settings > Show FormID - Toggle visibility of FormID column
		void Search_FormID(object sender, EventArgs e)
		{
			SettingsSearch.showFormID = !SettingsSearch.showFormID;
			UpdateShowFormID();
		}

		// Search Settings > Search in all Spaces - Toggles search results being returned for all spaces
		void Search_SearchInAllSpaces(object sender, EventArgs e)
		{
			SettingsSearch.searchInAllSpaces = !SettingsSearch.searchInAllSpaces;
			UpdateSearchInAllSpaces(true);
		}

		// Plot Settings > Mode > Icon - Change plot mode to icon
		void Plot_Mode_Icon(object sender, EventArgs e)
		{
			SettingsPlot.mode = SettingsPlot.Mode.Icon;
			UpdatePlotMode(true);
		}

		// Plot Settings > Mode > Heatmap - Change plot mode to heatmap
		void Plot_Mode_Heatmap(object sender, EventArgs e)
		{
			SettingsPlot.mode = SettingsPlot.Mode.Heatmap;
			UpdatePlotMode(true);
		}

		// Plot Settings > Mode > Topography - Change plot mode to Topography
		void Plot_Mode_Topography(object sender, EventArgs e)
		{
			SettingsPlot.mode = SettingsPlot.Mode.Topography;
			UpdatePlotMode(true);
		}

		// Plot Settings > Mode > Cluster - Change plot mode to Cluster
		void Plot_Mode_Cluster(object sender, EventArgs e)
		{
			SettingsPlot.mode = SettingsPlot.Mode.Cluster;
			UpdatePlotMode(true);
		}

		// Plot Settings > Plot Icon Settings - Open plot settings form
		void Plot_PlotIconSettings(object sender, EventArgs e)
		{
			FormPlotStyleSettings formPlotSettings = new FormPlotStyleSettings();
			formPlotSettings.ShowDialog();
		}

		// Plot Settings > Heatmap Settings > Color Mode > Mono - Change color mode to mono
		void Plot_HeatMap_ColorMode_Mono(object sender, EventArgs e)
		{
			SettingsPlotHeatmap.colorMode = SettingsPlotHeatmap.ColorMode.Mono;
			UpdateHeatMapColorMode(true);
		}

		// Plot Settings > Heatmap Settings > Color Mode > Duo - Change color mode to duo
		void Plot_HeatMap_ColorMode_Duo(object sender, EventArgs e)
		{
			SettingsPlotHeatmap.colorMode = SettingsPlotHeatmap.ColorMode.Duo;
			UpdateHeatMapColorMode(true);
		}

		// Plot Settings > Heatmap Settings > Resolution > 128 - Change resolution to 128
		void Plot_HeatMap_Resolution_128(object sender, EventArgs e)
		{
			SettingsPlotHeatmap.resolution = 128;
			UpdateHeatMapResolution(true);
		}

		// Plot Settings > Heatmap Settings > Resolution > 256 - Change resolution to 256
		void Plot_HeatMap_Resolution_256(object sender, EventArgs e)
		{
			SettingsPlotHeatmap.resolution = 256;
			UpdateHeatMapResolution(true);
		}

		// Plot Settings > Heatmap Settings > Resolution > 512 - Change resolution to 512
		void Plot_HeatMap_Resolution_512(object sender, EventArgs e)
		{
			SettingsPlotHeatmap.resolution = 512;
			UpdateHeatMapResolution(true);
		}

		// Plot Settings > Heatmap Settings > Resolution > 1024 - Change resolution to 1024
		void Plot_HeatMap_Resolution_1024(object sender, EventArgs e)
		{
			SettingsPlotHeatmap.resolution = 1024;
			UpdateHeatMapResolution(true);
		}

		// Plot Settings > Topograph color bands > 2
		void Plot_TopographBands_2(object sender, EventArgs e)
		{
			SettingsPlotTopograph.colorBands = 2;
			UpdateTopographColorBands(true);
		}

		// Plot Settings > Topograph color bands > 3
		void Plot_TopographBands_3(object sender, EventArgs e)
		{
			SettingsPlotTopograph.colorBands = 3;
			UpdateTopographColorBands(true);
		}

		// Plot Settings > Topograph color bands > 4
		void Plot_TopographBands_4(object sender, EventArgs e)
		{
			SettingsPlotTopograph.colorBands = 4;
			UpdateTopographColorBands(true);
		}

		// Plot Settings > Topograph color bands > 5
		void Plot_TopographBands_5(object sender, EventArgs e)
		{
			SettingsPlotTopograph.colorBands = 5;
			UpdateTopographColorBands(true);
		}

		// Plot Settings > Cluster Settings...
		void Plot_ClusterSettings(object sender, EventArgs e)
		{
			FormSetClusterRange formSetCluster = new FormSetClusterRange();
			formSetCluster.ShowDialog();
		}

		// Plot Settings > Draw Volumes - Toggle drawing volumes
		void Plot_DrawVolumes(object sender, EventArgs e)
		{
			SettingsPlot.drawVolumes = !SettingsPlot.drawVolumes;
			UpdateVolumeEnabledState(true);
		}

		// Plot Settings > Fill Regions - toggle fill of region draws
		void Plot_FillRegions(object sender, EventArgs e)
		{
			SettingsPlot.fillRegions = !SettingsPlot.fillRegions;
			UpdateFillRegionsState(true);
		}

		// Plot Settings > Show Reference Form IDs - toggle label of instance IDs
		void Plot_ShowRefFormIDs(object sender, EventArgs e)
		{
			SettingsPlot.labelInstanceIDs = !SettingsPlot.labelInstanceIDs;
			UpdateShowRefFormIDState(SettingsPlot.IsIconOrTopographic());
		}

		// Help > About - Show the About box
		void Help_About(object sender, EventArgs e)
		{
			FormAbout aboutBox = new FormAbout();
			aboutBox.ShowDialog();
		}

		// Help > User Guides - Open help guides at github master
		void Help_UserGuides(object sender, EventArgs e)
		{
			IOManager.LaunchURL("https://github.com/Mappalachia/Commonwealth_Cartography#getting-started---user-guides");
		}

		// Help > Check for Updates - Notify the user if there is an update available. Reports back if there were errors.
		void Help_CheckForUpdates(object sender, EventArgs e)
		{
			UpdateChecker.CheckForUpdate(true);
		}

		// Help > Via Discord - Launch the discord invite
		private void Help_ViaDiscord(object sender, EventArgs e)
		{
			OpenDiscord();
		}

		// Donate > Patreon
		void Donate_ViaPatreon(object sender, EventArgs e)
		{
			IOManager.LaunchURL("https://www.patreon.com/user?u=73036527");
		}

		// Donate > PayPal
		void Donate_ViaPayPal(object sender, EventArgs e)
		{
			IOManager.LaunchURL("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=TDVKFJ97TFFVC&source=url");
		}

		// Donate - Via Ko-Fi
		void Donate_ViaKoFi(object sender, EventArgs e)
		{
			IOManager.LaunchURL("https://ko-fi.com/aheroicllama");
		}

		// "Join the Discord" top-level menu item
		private void JoinDiscord(object sender, EventArgs e)
		{
			OpenDiscord();
		}

		// Signature select all
		void ButtonSelectAllSignature(object sender, EventArgs e)
		{
			foreach (ListViewItem item in listViewFilterSignatures.Items)
			{
				item.Checked = true;
			}
		}

		// Signature select recommended
		void ButtonSelectRecommendedSignature(object sender, EventArgs e)
		{
			SelectRecommendedSignatures();
		}

		// Signature deselect all
		void ButtonDeselectAllSignature(object sender, EventArgs e)
		{
			foreach (ListViewItem item in listViewFilterSignatures.Items)
			{
				item.Checked = false;
			}
		}

		// Lock select all
		void ButtonSelectAllLock(object sender, EventArgs e)
		{
			foreach (ListViewItem item in listViewFilterLockTypes.Items)
			{
				item.Checked = true;
			}
		}

		// Lock deselect all
		void ButtonDeselectAllLock(object sender, EventArgs e)
		{
			foreach (ListViewItem item in listViewFilterLockTypes.Items)
			{
				item.Checked = false;
			}
		}

		// User changed select Space - Update in Settings Class and wipe search results and legend
		void ComboBoxSpace_SelectedIndexChanged(object sender, EventArgs e)
		{
			// If we're searching in all spaces we don't need to reset search results
			if (!SettingsSearch.searchInAllSpaces)
			{
				ClearSearchResults();
			}

			ClearLegend();
			numericMinZ.Value = numericMinZ.Minimum;
			numericMaxZ.Value = numericMaxZ.Maximum;
			SettingsSpace.SetSpace(spaces[comboBoxSpace.SelectedIndex]);
			UpdateCellorWorldExclusiveState();
			SizeMapToFrame();
			SettingsFileExport.UpdateRecommendation();
		}

		void ButtonSpaceHeightDistribution_Click(object sender, EventArgs e)
		{
			ShowHeightDistribution();
		}

		// Height range changed - maintain the min safely below the max
		void NumericMinZ_ValueChanged(object sender, EventArgs e)
		{
			if (numericMaxZ.Value <= numericMinZ.Value)
			{
				numericMaxZ.Value = Math.Min(numericMaxZ.Value + numericMaxZ.Increment, numericMaxZ.Maximum);

				// It's likely the user just pressed tab to cross to the max value
				// We just highlighted it, but adjusting the value will un-highlight it again
				// So, re-highlight it
				NumericMaxZ_Enter(sender, e);
			}

			// Special case where the max could not go higher, so this must stay an increment lower
			if (numericMinZ.Value == numericMaxZ.Maximum)
			{
				numericMinZ.Value -= numericMinZ.Increment;
			}

			SettingsSpace.minHeightPerc = (int)numericMinZ.Value;
			forceDrawBaseLayer = true;
		}

		// Height range changed - maintain the max safely above the min
		void NumericMaxZ_ValueChanged(object sender, EventArgs e)
		{
			if (numericMinZ.Value >= numericMaxZ.Value)
			{
				numericMinZ.Value = Math.Max(numericMinZ.Value - numericMinZ.Increment, numericMinZ.Minimum);

				// It's likely the user just pressed shift-tab to cross to the min value
				// We just highlighted it, but adjusting the value will un-highlight it again
				// So, re-highlight it
				NumericMinZ_Enter(sender, e);
			}

			// Special case where the min could not go lower, so this must stay an increment higher
			if (numericMaxZ.Value == numericMinZ.Minimum)
			{
				numericMaxZ.Value += numericMaxZ.Increment;
			}

			SettingsSpace.maxHeightPerc = (int)numericMaxZ.Value;
			forceDrawBaseLayer = true;
		}

		// Select the value to overwrite when entered
		void NumericMinZ_Enter(object sender, EventArgs e)
		{
			numericMinZ.Select(0, numericMinZ.Text.Length);
		}

		void NumericMaxZ_Enter(object sender, EventArgs e)
		{
			numericMaxZ.Select(0, numericMaxZ.Text.Length);
		}

		// Clicked on - pass to enter event
		void NumericMinZ_MouseDown(object sender, MouseEventArgs e)
		{
			NumericMinZ_Enter(sender, e);
		}

		void NumericMaxZ_MouseDown(object sender, MouseEventArgs e)
		{
			NumericMaxZ_Enter(sender, e);
		}

		// Search Button - Gather parameters, execute query and populate results
		void ButtonSearchStandard(object sender, EventArgs e)
		{
			// Disable the button to reduce stacking search operations and reset the progress bar
			buttonSearch.Enabled = false;
			UpdateProgressBar(0.25, "Searching...", true);

			// Check for and show warnings
			WarnWhenMISCNotSelected();

			// If there are no filters selected, inform and cancel the search
			if (WarnWhenAllFiltersBlank() || Radiant())
			{
				buttonSearch.Enabled = true;
				UpdateProgressBar(1);
				return;
			}

			// Execute the search
			searchResults = Database.SearchStandard(textBoxSearch.Text, GetEnabledSignatures(), GetEnabledLockTypes(), SettingsSpace.GetCurrentFormID(), SettingsSearch.searchInAllSpaces);

			UpdateProgressBar(0.50, "Populating UI...", true);

			// Perform UI update
			UpdateResultsLockTypeColumnVisibility();
			textBoxSearch.Select();
			textBoxSearch.SelectAll();

			UpdateSearchResultsGrid();
			NotifyIfNoResults();

			// Search complete - re-enable UI and set progress bar to full
			buttonSearch.Enabled = true;
			UpdateProgressBar(1);
		}

		// Scrap search
		void ButtonSearchScrap(object sender, EventArgs e)
		{
			UpdateProgressBar(0.25, "Searching...", true);

			// Perform UI update
			UpdateResultsLockTypeColumnVisibility();

			searchResults = Database.SearchScrap(listBoxScrap.SelectedItem.ToString(), SettingsSpace.GetCurrentFormID(), SettingsSearch.searchInAllSpaces);

			UpdateProgressBar(0.50, "Populating UI...", true);

			UpdateSearchResultsGrid();
			NotifyIfNoResults();

			UpdateProgressBar(1);
		}

		// NPC Search
		void ButtonSearchNPC(object sender, EventArgs e)
		{
			UpdateProgressBar(0.25, "Searching...", true);

			// Perform UI update
			UpdateResultsLockTypeColumnVisibility();

			searchResults = Database.SearchNPC(
				listBoxNPC.SelectedItem.ToString(),
				SettingsSpace.GetCurrentFormID(),
				SettingsSearch.searchInAllSpaces);

			UpdateProgressBar(0.50, "Populating UI...", true);

			UpdateSearchResultsGrid();
			NotifyIfNoResults();

			UpdateProgressBar(1);
		}

		// Region search
		void ButtonSearchRegion(object sender, EventArgs e)
		{
			UpdateProgressBar(0.25, "Searching...", true);

			// Perform UI update
			UpdateResultsLockTypeColumnVisibility();

			searchResults = Database.SearchRegion(textBoxRegionSearch.Text, SettingsSpace.GetCurrentFormID(), SettingsSearch.searchInAllSpaces);

			UpdateProgressBar(0.50, "Populating UI...", true);

			UpdateSearchResultsGrid();
			NotifyIfNoResults();

			UpdateProgressBar(1);
		}

		// Add selected valid items from the search results to the legend.
		void ButtonAddToLegend(object sender, EventArgs e)
		{
			double totalItems = gridViewSearchResults.SelectedRows.Count;

			UpdateProgressBar($"Adding {totalItems} items...", true);

			if (totalItems == 0)
			{
				Notify.Info("Please select items from the search results first.");
				return;
			}

			List<string> rejectedItemsDuplicate = new List<string>(); // Items rejected because they're already present
			List<string> rejectedItemsOtherSpace = new List<string>(); // Items rejected because they belong to another space
			int legendGroup = -1;

			// Get a single legend group for this group of item(s)
			if (checkBoxAddAsGroup.Checked)
			{
				legendGroup = FindLowestAvailableLegendGroupValue();
			}

			// Record the contents of the legend before we start adding to it
			List<MapItem> legendItemsBeforeAdd = new List<MapItem>(legendItems);

			int progress = 0;

			foreach (DataGridViewRow row in gridViewSearchResults.SelectedRows.Cast<DataGridViewRow>().Reverse())
			{
				progress++;

				UpdateProgressBar(progress / totalItems);

				MapItem selectedItem = searchResults[Convert.ToInt32(row.Cells["columnSearchIndex"].Value)];

				// Warn if a selected item is another space item or already on the legend list - otherwise add it.
				if (selectedItem.spaceEditorID != SettingsSpace.GetSpace().editorID)
				{
					rejectedItemsOtherSpace.Add(selectedItem.editorID);
				}
				else if (legendItemsBeforeAdd.Contains(selectedItem))
				{
					rejectedItemsDuplicate.Add(selectedItem.editorID);
				}

				// Item is fine - add it
				else
				{
					// If the legend group is already fixed (added as group) use that, otherwise use a new legend group
					selectedItem.legendGroup = (legendGroup == -1) ?
						FindLowestAvailableLegendGroupValue() :
						legendGroup;

					legendItems.Add(selectedItem);
				}
			}

			UpdateProgressBar("Validating...");

			int totalRejectedItems = rejectedItemsOtherSpace.Count + rejectedItemsDuplicate.Count;

			// Update the legend grid, as long as there's at least one item we didn't have to reject
			if (totalRejectedItems < gridViewSearchResults.SelectedRows.Count)
			{
				UpdateLegendGrid(null);
			}

			// If we dropped items, let the user know why.
			if (totalRejectedItems > 0)
			{
				// Cap the list of items we warn about to prevent a huge error box
				int maxItemsToShow = 8;
				int truncatedItems = totalRejectedItems - maxItemsToShow;

				string message = "The following items were not added to the legend because ";
				if (rejectedItemsDuplicate.Count > 0 && rejectedItemsOtherSpace.Count > 0)
				{
					message += "some were already present, and others were from spaces other than the selected space.";
				}
				else if (rejectedItemsDuplicate.Count > 0)
				{
					message += "some were already present.";
				}
				else if (rejectedItemsOtherSpace.Count > 0)
				{
					message += "some were from spaces other than the selected space.";
				}

				if (rejectedItemsOtherSpace.Count > 0)
				{
					message += "\n\nItems from search results can only be mapped if they belong to the currently selected space. " +
						"To find results only in the selected space, ensure 'Search Settings > Search in all Spaces' is toggled off.\n" +
						"Alternatively double-click the name of the space in the Location column to quick-swap to that space.";
				}

				// Add a line to say that a further x items (not shown) were not added
				message += "\n\n" + string.Join("\n", rejectedItemsOtherSpace.Concat(rejectedItemsDuplicate).Take(maxItemsToShow)) +
					(truncatedItems > 0 ? "\n(+ " + truncatedItems + " more...)" : string.Empty);

				Notify.Info(message);
			}

			UpdateProgressBar(1);
		}

		// Remove selected rows from legend DataGridView
		void ButtonRemoveFromLegend(object sender, EventArgs e)
		{
			int selectedRowsCount = gridViewLegend.SelectedRows.Count;

			if (selectedRowsCount == 0)
			{
				Notify.Info("Please select items from the 'items to plot' list first.");
				return;
			}

			// If they are removing *everything*, then we can skip the processing below and just wipe the list
			if (selectedRowsCount == legendItems.Count)
			{
				ClearLegend();
				return;
			}

			UpdateProgressBar($"Removing {selectedRowsCount} items...", true);

			List<MapItem> remainingLegendItems = new List<MapItem>();
			List<int> selectedIndexes = new List<int>();

			// Removing multiple list items by index breaks, as the indices keep changing
			// So we find which items *weren't* selected, then make a new list of remaining items from that
			foreach (DataGridViewRow row in gridViewLegend.SelectedRows)
			{
				selectedIndexes.Add(row.Index);
			}

			for (int i = 0; i < legendItems.Count; i++)
			{
				if (!selectedIndexes.Contains(i))
				{
					remainingLegendItems.Add(legendItems[i]);
				}
				else
				{
					legendItems[i].overridingLegendText = string.Empty;
				}
			}

			legendItems = remainingLegendItems;

			UpdateLegendGrid(null);
			UpdateProgressBar(1);
		}

		// Handle the entry and assignment of new values to the Legend Group column or Overriding legend text
		void GridViewLegend_CellEndEdit(object sender, DataGridViewCellEventArgs e)
		{
			DataGridViewRow editedRow = gridViewLegend.Rows[e.RowIndex];
			DataGridViewCell editedCell = editedRow.Cells[e.ColumnIndex];
			object cellValue = editedCell.Value;
			MapItem editedItem = legendItems[e.RowIndex];

			// Legend group
			if (e.ColumnIndex == 0)
			{
				// First verify the value entered into Legend Group cell is a non-null positive int
				if (cellValue == null ||
					!uint.TryParse(cellValue.ToString(), out _) ||
					!int.TryParse(cellValue.ToString(), out _))
				{
					Notify.Warn("\"" + editedCell.Value + "\" must be a positive whole number.");
					editedCell.Value = 0; // Overwrite the edit by assigning 0, but continue the method so that the value of 0 is reflected in the MapItem too.
				}

				// Record the overridden legend texts, *before* we edit the legend group, in case the edit conflicts
				Dictionary<int, string> overriddenLegendTexts = GatherOverriddenLegendTexts();

				int legendGroup = int.Parse(editedCell.Value.ToString());
				editedItem.legendGroup = legendGroup;

				// We just edited into a legend group which is already overridden
				// Inherit the overriding text of the one which came first
				if (overriddenLegendTexts.ContainsKey(legendGroup))
				{
					editedItem.overridingLegendText = overriddenLegendTexts[legendGroup];
					BeginInvoke((MethodInvoker)delegate { UpdateLegendGrid(editedItem); });
				}
			}

			// Override legend text
			else if (e.ColumnIndex == 1)
			{
				int targetLegendGroup = int.Parse(editedRow.Cells[0].Value.ToString());

				/*Loop over all items of the same legend group (including of the edited row),
				assigning the overriding text, or reverting to default if blank*/
				foreach (MapItem mapItem in legendItems.FindAll(m => m.legendGroup == targetLegendGroup))
				{
					// If the value entered was blank/deleted, reset to default
					mapItem.overridingLegendText = (cellValue == null || string.IsNullOrWhiteSpace(cellValue.ToString())) ?
						string.Empty :
						cellValue.ToString();
				}

				BeginInvoke((MethodInvoker)delegate { UpdateLegendGrid(editedItem); });
			}
		}

		void ButtonDrawMap(object sender, EventArgs e)
		{
			if (isDrawing)
			{
				buttonDrawMap.Text = "Cancelling...";
				mapDrawCancTokenSource.Cancel();
				return;
			}

			DrawMap(false);
		}

		// Double click map for preview
		void MapPreview_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			PreviewMap();
		}

		// Record left click location on map in order to pan with mouse
		void MapPreview_MouseDown(object sender, MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				lastMouseDownPos = mouseEvent.Location;
			}
		}

		// Pan the map while left mouse is held
		void MapPreview_MouseMove(object sender, MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				int deltaX = mouseEvent.Location.X - lastMouseDownPos.X;
				int deltaY = mouseEvent.Location.Y - lastMouseDownPos.Y;

				int newX = pictureBoxMapPreview.Location.X + deltaX;
				int newY = pictureBoxMapPreview.Location.Y + deltaY;

				pictureBoxMapPreview.Location = new Point(newX, newY);
			}
		}

		// Pick up scroll wheel events and apply them to zoom the map preview
		protected override void OnMouseWheel(MouseEventArgs e)
		{
			double minZoom = Map.mapDimension * Map.minZoomRatio;
			double maxZoom = Map.mapDimension * Map.maxZoomRatio;

			// Record the center coord of the 'viewport' (The container of the PictureBox)
			int viewPortCenterX = splitContainerMain.Panel2.Width / 2;
			int viewPortCenterY = splitContainerMain.Panel2.Height / 2;

			// Record initial positions
			Point location = pictureBoxMapPreview.Location;
			int width = pictureBoxMapPreview.Size.Width;
			int height = pictureBoxMapPreview.Size.Height;
			Point apparentCenter = new Point(
				(int)((viewPortCenterX - location.X) * (Map.mapDimension / (float)width)),
				(int)((viewPortCenterY - location.Y) * (Map.mapDimension / (float)height)));

			// Calculate how much to zoom
			float zoomRatio = 1 + (e.Delta / 1500f);

			// Calculate the new dimensions once zoomed, given zoom limits
			int newWidth = (int)Math.Max(Math.Min(maxZoom, width * zoomRatio), minZoom);
			int newHeight = (int)Math.Max(Math.Min(maxZoom, height * zoomRatio), minZoom);

			// Calculate the required position offset while also factoring the offset to keep the apparent center pixel the same
			int widthOffset = (width - newWidth) * apparentCenter.X / Map.mapDimension;
			int heightOffset = (height - newHeight) * apparentCenter.Y / Map.mapDimension;

			// Apply the new zoom and position
			pictureBoxMapPreview.Size = new Size(newWidth, newHeight);
			pictureBoxMapPreview.Location = new Point(location.X + widthOffset, location.Y + heightOffset);
		}

		// Select items in the filters just by clicking their label
		void ListViewFilterSignatures_SelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			if (e.IsSelected)
			{
				e.Item.Checked = !e.Item.Checked;
			}
		}

		// Select items in the filters just by clicking their label
		void ListViewFilterLockTypes_SelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			if (e.IsSelected)
			{
				e.Item.Checked = !e.Item.Checked;
			}
		}

		// Show dynamic tooltips on certain columns
		void GridViewSearchResults_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
		{
			// Ignore the header
			if (e.RowIndex == -1)
			{
				return;
			}

			// Indentify which cell was hovered
			DataGridViewCell hoveredCell = gridViewSearchResults.Rows[e.RowIndex].Cells[e.ColumnIndex];

			// Explain the signature AKA category
			if (gridViewSearchResults.Columns[e.ColumnIndex].Name == "columnSearchCategory")
			{
				hoveredCell.ToolTipText = DataHelper.GetSignatureDescription(hoveredCell.Value.ToString());
			}

			// Explain what is meant by "unknown" under spawn chance
			else if (gridViewSearchResults.Columns[e.ColumnIndex].Name == "columnSearchChance")
			{
				if (hoveredCell.Value.ToString() == "Unknown")
				{
					hoveredCell.ToolTipText = "For a number of possible reasons, Commonwealth Cartography is unable to provide a single, static figure to describe the spawn chances of this entity.";
				}
			}

			// Give the technical name of a cell display name for those curious
			else if (gridViewSearchResults.Columns[e.ColumnIndex].Name == "columnSearchLocation")
			{
				hoveredCell.ToolTipText = gridViewSearchResults.Rows[e.RowIndex].Cells["columnSearchLocationID"].Value.ToString();
			}
		}

		// Set the current Space when double clicking on another Space in search results
		void GridViewSearchResults_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			int mouseOverRow = gridViewSearchResults.HitTest(e.X, e.Y).RowIndex;
			int mouseOverColumn = gridViewSearchResults.HitTest(e.X, e.Y).ColumnIndex;
			if (mouseOverRow == -1 || mouseOverColumn == -1)
			{
				return;
			}

			DataGridViewRow selectedRow = gridViewSearchResults.Rows[mouseOverRow];
			DataGridViewColumn selectedColumn = gridViewSearchResults.Columns[mouseOverColumn];

			if (selectedColumn.Name != "columnSearchLocation")
			{
				return;
			}

			string selectedEditorID = selectedRow.Cells["columnSearchLocationID"].Value.ToString();
			comboBoxSpace.SelectedIndex = spaces.FindIndex(space => space.editorID == selectedEditorID);
		}

		// Explain Legend Group on mouseover with tooltip
		void GridViewLegend_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
		{
			// Only apply to Legend Group column
			DataGridViewColumn column = gridViewLegend.Columns[e.ColumnIndex];
			if (column.HeaderText != "Legend Group")
			{
				return;
			}

			string legendGroupTooltip = "The legend group allows you to group items together, so they are represented by the same color and/or shape.";

			// Assign the tooltip to the column header
			column.ToolTipText = legendGroupTooltip;

			// Also assign to any cell (not the header)
			if (e.RowIndex != -1)
			{
				gridViewLegend.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = legendGroupTooltip;
			}
		}

		// Change the default enter action depending on the currently selected control
		void SetAcceptButtonScrap(object sender, EventArgs e)
		{
			AcceptButton = buttonSearchScrap;
		}

		// Change the default enter action depending on the currently selected control
		void SetAcceptButtonNPC(object sender, EventArgs e)
		{
			AcceptButton = buttonSearchNPC;
		}

		void SetAcceptButtonRegion(object sender, EventArgs e)
		{
			AcceptButton = buttonRegionSearch;
		}

		// Change the default enter action depending on the currently selected control
		void TabControlMain_SelectedIndexChanged(object sender, EventArgs e)
		{
			AcceptButton = tabControlMainSearch.SelectedTab == tabPageStandard ? buttonSearch : buttonSearchNPC;
		}

		#endregion

		void FormMain_FormClosed(object sender, FormClosedEventArgs e)
		{
			IOManager.Cleanup();
			SettingsManager.SaveSettings();

			try
			{
				// Ensures any potentially long-running map building task is stopped too
				Environment.Exit(0);
			}
			catch (Exception)
			{ } // If this fails, we're already exiting and there is no action to take
		}
	}
}
