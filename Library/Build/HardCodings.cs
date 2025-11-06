using System.Text.RegularExpressions;
using System.Xml;
using static Library.Common;

namespace Library
{
	// For multiple reasons, a small subset of data (or expected data) is hardcoded.
	// Naturally hardcoding things comes with risks and may require review after each patch. For that reason all hardcoded items are kept together here.
	// Hardcoding may be done for the following reasons: Datamining is not realistic, or a route is not known. Or the data is apparently server-side.
	// Notably Map Markers are the main offenders of this
	public static partial class BuildTools
	{
		public static Regex SpaceFormIDRegex { get; } = new Regex(@"\[(WRLD|CELL):([0-9A-F]{8})\]");

		public static Regex SignatureFormIDRegex { get; } = new Regex(@"\[[A-Z_]{4}:([0-9A-F]{8})\]");

		public static Regex FormIDRegex { get; } = new Regex(@"[0-9A-F]{8}");

		public static Regex RemoveTrailingReferenceRegex { get; } = new Regex("(.*) " + SignatureFormIDRegex);

		public static Regex QuotedTermRegex { get; } = new Regex(".* :QUOT:(.*):QUOT: " + SignatureFormIDRegex);

		public static Regex TitleCaseAddSpaceRegex { get; } = new Regex("(.*[a-z])([A-Z].*)");

		public static Regex NPCRegex { get; } = new Regex("ESSChance(Main|Sub|Critter[AB])(.*?)s?(LARGE|GIANTONLY)? " + SignatureFormIDRegex);

		public static Regex LockLevelRegex { get; } = new Regex(@"(Novice|Advanced|Expert|Master) \((Level [0-3])\)");

		public static Regex ValidateLockLevel { get; } = new Regex("^(Level[0-3]|Chained|Inaccessible|RequiresKey|RequiresTerminal|Unknown|Barred)$");

		public static Regex ValidatePrimitiveShape { get; } = new Regex("^(Box|Line|Plane|Sphere|Ellipsoid|Cylinder)$");

		public static Regex ValidateSignature { get; } = new Regex("^(ACTI|ALCH|AMMO|ARMO|ASPC|BNDS|BOOK|CNCY|CONT|DOOR|FLOR|FURN|HAZD|IDLM|KEYM|LIGH|LVLI|MISC|MSTT|NOTE|NPC_|PROJ|SCOL|SECH|SOUN|STAT|TACT|TERM|TRAP|TXST|WEAP)$");

		public static Regex ValidateMapMarkerIcon { get; } = new Regex("^(.*Marker)$");

		public static Regex ValidateComponent { get; } = new Regex("^(Acid|Adhesive|Aluminum|Antiseptic|Asbestos|Ballistic Fiber|Black Titanium|Bone|Ceramic|Circuitry|Cloth|Concrete|Copper|Cork|Crystal|Fertilizer|Fiber Optics|Fiberglass|Gear|Glass|Gold|Gunpowder|Lead|Leather|Nuclear Material|Oil|Plastic|Rubber|Screw|Silver|Spring|Steel|Ultracite|Wood)$");

		public static Regex ValidIconFolder { get; } = new Regex("DefineSprite_[0-9]{1,3}_(([A-Z].*Marker))$");

		public static string MapMarkerIconInitialFileName { get; } = "1.svg"; // Frame 1

		public static Dictionary<string, string> MarkerLabelCorrection { get; } = new Dictionary<string, string>()
		{
		};

		public static List<string> MapMarkersToRemove { get; } = new List<string>()
		{
		};

		// Spaces which appear to be copy/pastes or alternates of the same thing, and therefore can and should share scaling/adjustments
		public static List<List<string>> SisterSpaces { get; } = new List<List<string>>()
		{
		};

		// For an unknown reason, some entities in xEdit have this invalid lock level
		public static string CorrectLockLevelQuery { get; } = "UPDATE Position SET lockLevel = 'Novice (Level 0)' WHERE lockLevel = 'Opens Door';";

		// For an unknown reason, some entities in xEdit have this invalid primitive shape
		public static string CorrectPrimitiveShapeQuery { get; } = "UPDATE Position SET primitiveShape = 'Box' WHERE primitiveShape = '7';";

		// Assumes PascalCased names will already have had spaces added
		public static Dictionary<string, string> NPCNameCorrection { get; } = new Dictionary<string, string>()
		{
		};

		// Provides the WHERE clause for a query which defines the rules of which cells we should discard, as they are understood to be cut or otherwise inaccessible.
		public static string DiscardCellsQuery { get; } =
			"spaceDisplayName = '' OR " +
			"spaceDisplayName LIKE '%Test%World%' OR " +
			"spaceDisplayName LIKE '%Test%Cell%' OR " +
			"spaceEditorID LIKE 'zCUT%' OR " +
			"spaceEditorID LIKE '%OLD' OR " +
			"spaceEditorID LIKE 'Warehouse%' OR " +
			"spaceEditorID LIKE 'Test%' OR " +
			"spaceEditorID LIKE '%Debug%' OR " +
			"spaceEditorID LIKE 'zz%' OR " +
			"spaceEditorID LIKE '76%' OR " +
			"spaceEditorID LIKE '%Worldspace' OR " +
			"spaceEditorID LIKE '%Nav%Test%' OR " +
			"spaceEditorID LIKE 'PackIn%' OR " +
			"spaceEditorID LIKE 'COPY%'";

		public static string? GetCorrectedMarkerIcon(string markerName)
		{
			switch (markerName)
			{
				default:
					return null;
			}
		}

		// Values passed with the -xm argument to the render command
		public static List<string> RenderExcludeModels { get; } = new List<string>()
		{
		};

		// The Form ID of NorthMarker
		public static uint NorthMarkerFormID { get; } = HexToInt("00000003");

		// Some spaces have 2 North Markers - we specify which one we want to pick to give the north angle
		// Key = space FormID in Hex, Value = northMarker instance FormID in Hex
		public static Dictionary<string, string> NorthMarkerPreference { get; } = new Dictionary<string, string>()
		{
		};

		// Correct map marker labels by correcting common extraneous/incorrect text in the label
		public static string CorrectCommonBadLabels(string label)
		{
			return label;
		}

		// Returns the known world border(s) of the given space
		// Empty list if not known or doesn't exist
		public static async Task<List<Region>> GetWorldBorders(this Space space)
		{
			if (space.IsCommonwealth())
			{
				return await CommonDatabase.GetRegionsByLikeTerm(GetNewConnection(), space, $"'76Border%'");
			}

			return new List<Region>();
		}

		public static XmlDocument FixMapMarkerSVG(XmlDocument document, MapMarker mapMarker)
		{
			return document;
		}

		// Neatly handles modifying attributes of xml nodes
		static void SetAttributeValue(this XmlNode? node, string attributeName, string value)
		{
			if (node is null)
			{
				throw new NullReferenceException("XML Node is null");
			}

			XmlAttribute? attribute = node.Attributes?[attributeName] ?? throw new NullReferenceException($"XML Node does not have attribute {attributeName}");
			attribute.Value = value;
		}
	}
}
