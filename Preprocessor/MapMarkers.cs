﻿using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mappalachia
{
	internal static class MapMarkers
	{
		static readonly string bloodEagleMarker = "BloodEagleMarker";
		static readonly string cultistMarker = "CultistMarker";
		static readonly string fastTravelBadString = "Fast Travel Point: ";
		static readonly string fissureSite = "Fissure Site";
		static readonly Regex validMapMarkerName = new Regex("^(([A-Z].*Marker)|WhitespringResort|NukaColaQuantumPlant|TrainTrackMark)$");
		static readonly Regex badMapMarkerNames = new Regex("^(Door|Quest|PowerArmorLoc|PlayerLoc)Marker$");
		static readonly Regex biomeNames = new Regex("^(The )?(Mire|Cranberry Bog|Forest|Toxic Valley|Savage Divide|Mountain)( Region)?$");

		static readonly Dictionary<string, string> locationMarkerCorrection = new Dictionary<string, string>()
		{
			{ "Ammo Dump", bloodEagleMarker },
			{ "Bloody Frank's", bloodEagleMarker },
			{ "Cliffwatch", bloodEagleMarker },
			{ "Crimson Prospect", bloodEagleMarker },
			{ "Hunter's Ridge", bloodEagleMarker },
			{ "Ripper Alley", bloodEagleMarker },
			{ "Rollins Labor Camp", bloodEagleMarker },
			{ "Seneca Gang Camp", bloodEagleMarker },
			{ "Skullbone Vantage", bloodEagleMarker },
			{ "South Cutthroat Camp", bloodEagleMarker },
			{ "The Bounty", bloodEagleMarker },
			{ "The Crosshair", bloodEagleMarker },
			{ "The Kill Box", bloodEagleMarker },
			{ "The Pigsty", bloodEagleMarker },
			{ "The Sludge Works", bloodEagleMarker },
			{ "The Vantage", bloodEagleMarker },
			{ "Twin Pine Cabins", bloodEagleMarker },
			{ "Widow's Perch", bloodEagleMarker },
			{ "Blakes Offering", cultistMarker },
			{ "Clancy Manor", cultistMarker },
			{ "Ingram Mansion", cultistMarker },
			{ "Johnson's Acre", cultistMarker },
			{ "Kanawha County Cemetery", cultistMarker },
			{ "Lucky Hole Mine", cultistMarker },
			{ "Moth-Home", cultistMarker },
			{ "Sacrament", cultistMarker },
			{ "Foundation", "HammerWingMarker" },
			{ "Hawke's Refuge", "CaveMarker" },
			{ "Ohio River Adventures", "SkullRingMarker" },
			{ "The Crater", "SpaceStationMarker" },
			{ "The Rusty Pick", "LegendaryPurveyorMarker" },
			{ "Vault 51", "Vault51Marker" },
			{ "Vault 79", "Vault79Marker" },
		};

		static readonly Dictionary<string, string> wrongLabelNames = new Dictionary<string, string>()
		{
			{ "Animal Cave", "Hopewell Cave" },
			{ "Bleeding Kate's Grinder", "Bleeding Kate's Grindhouse" },
			{ "Cranberry Bog Region", "Quarry X3" },
			{ "Garrahan Excavations Headquarters", "Garrahan Mining Headquarters" },
			{ "Hornwright Air Cleanser Site #01", "Hornwright Air Purifier Site #01" },
			{ "Hornwright Air Cleanser Site #02", "Hornwright Air Purifier Site #02" },
			{ "Hornwright Air Cleanser Site #03", "Hornwright Air Purifier Site #03" },
			{ "Hornwright Air Cleanser Site #04", "Hornwright Air Purifier Site #04" },
			{ "Lumber Camp", "Sylvie & Sons Logging Camp" },
			{ "Maybell Pond", "Beckwith Farm" },
			{ "Mine Shaft No. 9", "AMS Testing Site" },
			{ "Mountain Region", "Colonel Kelly Monument" },
			{ "Relay Tower 2", "Relay Tower HN-B1-12" },
			{ "Relay Tower 3", "Relay Tower DP-B5-21" },
			{ "Relay Tower 4", "Relay Tower LW-B1-22" },
			{ "Relay Tower 5", "Relay Tower HG-B7-09" },
			{ "Relay Tower 6", "Relay Tower EM-B1-27" },
			{ "Sundew Grove 02", "Veiled Sundew Grove" },
			{ "Sundew Grove 03", "Creekside Sundew Grove" },
			{ "The Savage Divide", "Monorail Elevator" },
			{ "World's Largest Teapot", "The Giant Teapot" },
		};

		// Pull the MapMarker display text from position data and store it in a new file
		public static CSVFile ProcessMapMarkers(CSVFile positionData)
		{
			List<string> newFileHeader = new List<string> { "spaceFormID", "label", "mapMarkerName", "x", "y" };
			List<CSVRow> newFileRows = new List<CSVRow>();

			// Monongah workshop (003D4B48) does not have its 'Map Marker/FULL - Name' record assigned so the export scripts don't find it - manually add back
			newFileRows.Add(new CSVRow("0025DA15,Monongah Power Plant Yard,IndustrialStacksMarker,44675.304687,73761.358125", newFileHeader));

			foreach (CSVRow row in positionData.rows)
			{
				// Uniquely for map markers, xEdit scripts return the map marker label under what is normally the reference column
				string label = row.GetCellFromColumn("referenceFormID");

				if (Validation.matchFormID.IsMatch(label))
				{
					continue; // This is a normal entity which came without an actual display name so we assume it's not a map marker
				}

				string spaceFormID = row.GetCellFromColumn("spaceFormID");
				string iconName = row.GetCellFromColumn("mapMarkerName");

				/* Fix incorrect Map Marker Icons.
				There must be something basic data mining misses, or an XEdit bug
				or the game changes them on the fly, but some labels and icons are different in-game.
				This appears to largely but not exclusively affect content changed or added with Wastelanders */

				// Swap or amend some entirely wrong names
				if (wrongLabelNames.ContainsKey(label))
                {
					label = wrongLabelNames[label];
                }

				// This marker is in the data but not in-game and does not represent anything particular, so we drop it
				if (label == "Mire")
                {
					continue;
				}

				// Large collection of incorrect Wastelanders icons - suspect xedit bug?
				if (locationMarkerCorrection.ContainsKey(label))
				{
					iconName = locationMarkerCorrection[label];
                }

				// Misnamed workshop
				if (label == "Hemlock Holes" && iconName == "FactoryMarker")
				{
					label = "Hemlock Holes Maintenance";
				}

				// Removes "Fast Travel Point: " from some (typically station) names
				if (label.StartsWith(fastTravelBadString))
                {
					label = label.Replace(fastTravelBadString, string.Empty);
                }

				// Fix fissure site naming - Rename Zeta to Alpha, drop names from all others
				if (label.StartsWith(fissureSite))
				{
					if (label == "Fissure Site Zeta")
					{
						label = "Fissure Site Alpha";
					}
					else
					{
						label = fissureSite;
					}
				}

				// Perform our own specialized validation
				if (badMapMarkerNames.IsMatch(iconName) ||
					!validMapMarkerName.IsMatch(iconName) ||
					biomeNames.IsMatch(label) ||
					wrongLabelNames.ContainsKey(label))
				{
					throw new System.Exception("Map Marker failed internal validation: " + label + ", " + iconName);
				}

				// ... Finally, we assume this is a (corrected) map marker
				string newRow =
					spaceFormID + "," +
					label + "," +
					iconName + "," +
					row.GetCellFromColumn("x") + "," +
					row.GetCellFromColumn("y");

				newFileRows.Add(new CSVRow(newRow, newFileHeader));
			}

			return new CSVFile("Map_Markers.csv", newFileHeader, newFileRows);
		}
	}
}
