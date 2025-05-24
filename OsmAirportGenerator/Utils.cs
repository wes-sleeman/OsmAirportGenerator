using Clipper2Lib;

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text;

using WSleeman.Osm;

namespace OsmAirportGenerator;

internal static partial class GenerationUtils
{
	/// <summary>Converts a given <see cref="Way"/> to an Aurora-readable TFL block with a given colour and, optionally, filter.</summary>
	/// <param name="way">The <see cref="Way"/> to render.</param>
	/// <param name="colour">The colour which should be put (verbatim) into the sectorfile.</param>
	/// <param name="filter">Optionally, one of	COAST, RUNWAY, GATES, PIER, TAXIWAY, APRON, BUILDING.</param>
	/// <returns>A string containing the whole TFL block, ending with a newline.</returns>
	private static string ToTfl(this Way way, string colour, string? filter = null)
	{
		StringBuilder builder = new($"STATIC;{colour};1;{colour};0;");
		builder.AppendLine(filter is null ? "" : filter);

		foreach (Node node in way.Nodes)
			builder.AppendLine($"{node.Latitude:00.000000};{node.Longitude:000.000000};");

		return builder.ToString();
	}

	/// <summary>Generates the GEOs representing a given <see cref="Way"/>.</summary>
	/// <param name="way">The <see cref="Way"/> to generate.</param>
	private static string ToGeo(this Way way, string colour)
	{
		if (way.Nodes.Length < 2)
			return "";

		StringBuilder builder = new();

		foreach ((Node from, Node to) in way.Nodes.SkipLast(1).Zip(way.Nodes.Skip(1)))
			builder.AppendLine($"{from.Latitude:00.000000};{from.Longitude:000.000000};{to.Latitude:00.000000};{to.Longitude:000.000000};{colour};");

		return builder.ToString();
	}

	/// <summary>Extracts all ways (including those nested within relations) from a given <see cref="OsmPbfFile"/>.</summary>
	private static IEnumerable<Way> AllWays(this OsmPbfFile data) => data.Ways.Values.Concat(data.Relations.Values.Unpack());

	/// <summary>Recursively retrieves all <see cref="Way"/> members from a <see cref="Relation"/> array.</summary>
	private static IEnumerable<Way> Unpack(this ImmutableArray<Relation> relations, HashSet<long>? ignoreIds = null)
	{
		List<Relation> subrelations = [];

		foreach (OsmItem item in relations.SelectMany(static r => r.Members))
			if (item is Way w)
				// Way is directly a member. Yield it.
				yield return w;
			else if (item is Relation r && (ignoreIds is null || !ignoreIds.Contains(r.Id)))
				// Sub relation. Remember it for later.
				subrelations.Add(r);
		// Ignore any member nodes; not really much help to us here.

		if (subrelations.Count == 0)
			yield break;

		// Add any discovered subrelations to the list of relations to ignore on the recursive search.
		// This helps prevent infinite recursion.
		ignoreIds ??= [];
		ignoreIds?.UnionWith(subrelations.Select(static sr => sr.Id));

		// Now recursively unpack all the discovered subrelations.
		foreach (Way w in subrelations.ToImmutableArray().Unpack(ignoreIds))
			yield return w;
	}

	/// <summary>Gets an appending writer for the given file, inserting a header or spacer line as necessary.</summary>
	/// <param name="filePath">The path of the file to write to.</param>
	private static async Task<StreamWriter> GetWriterAsync(string filePath)
	{
		bool fileExists = File.Exists(filePath);

		StreamWriter fileWriter = new(File.Open(filePath, FileMode.Append, FileAccess.Write));

		if (fileExists)
			await fileWriter.WriteLineAsync();
		else
		{
			await fileWriter.WriteLineAsync($"// Automatically generated {DateTime.Now:yyyy-MM-dd} for IVAO ATC Ops.");
			await fileWriter.WriteLineAsync("// Data ⓒ OpenStreetMap Contributors (https://www.openstreetmap.org/copyright)");
		}

		return fileWriter;
	}

	/// <summary>"Inflates" a way to expand it out into a polygon.</summary>
	/// <param name="way">The <see cref="Way"/> to inflate around.</param>
	/// <param name="radius">The amount to inflate the <see cref="Way"/> by.</param>
	private static Way Inflate(this Way way, double radius)
	{
		if (way.Nodes.Length < 2)
			// Ways with 1 or 0 nodes should just stay the same. Copy-construct it to be safe.
			return way with { Nodes = way.Nodes };

		return way with {
			Nodes = [..Clipper.InflatePaths(
				[[.. way.Nodes.Select(n => new PointD(n.Longitude, n.Latitude))]],
				radius, JoinType.Round, EndType.Butt,
				precision: 6
			)[0].Select(p => new Node(0, p.y, p.x, FrozenDictionary<string, string>.Empty))]
		};
	}

	/// <summary>Attempts to place labels roughly equitably along a group of centrelines.</summary>
	/// <param name="ways">The full set of taxiways/taxilanes/runways whatever.</param>
	/// <param name="airport">The ICAO code of the airport.</param>
	private static string LabelCentrelines(this IEnumerable<Way> ways, string airport)
	{
		const float SPACING_SHORT = 0.0025f, SPACING_LONG = 0.005f;

		Way[] centrelines = [.. ways.Where(static w => (w["ref"] ?? w["name"]) is not null)];
		StringBuilder labels = new();
		HashSet<string> neededLabels = [], placedLabels = [];

		foreach (Way centreline in centrelines)
		{
			string label = centreline["ref"] ?? centreline["name"]!;
			neededLabels.Add(label);

			if (centreline.Nodes.Length <= 1)
				continue;

			float spacing = label.Any(char.IsDigit) ? SPACING_SHORT : SPACING_LONG;

			float distSinceLast = spacing;
			float totalDistance = 0;

			var (iLastX, iLastY) = (centreline.Nodes[0].Longitude, centreline.Nodes[0].Latitude);

			foreach (var node in centreline.Nodes[1..])
			{
				var (x, y) = (node.Longitude, node.Latitude);
				float dx = (float)(x - iLastX), dy = (float)(y - iLastY);
				totalDistance += MathF.Sqrt(dx * dx + dy * dy);
				(iLastX, iLastY) = (x, y);
			}

			if (totalDistance * 2 < spacing)
				continue;

			distSinceLast = (totalDistance % spacing) / 2;

			var (lastX, lastY) = (centreline.Nodes[0].Longitude, centreline.Nodes[0].Latitude);
			int labelsPlaced = 0;

			foreach (var node in centreline.Nodes[1..])
			{
				var (x, y) = (node.Longitude, node.Latitude);
				while (lastX != x || lastY != y)
				{
					float dx = (float)(x - lastX), dy = (float)(y - lastY);
					float distRemaining = MathF.Sqrt(dx * dx + dy * dy);

					float stepLength = Math.Min(distRemaining, spacing - distSinceLast);
					float norm = stepLength / distRemaining;
					(lastX, lastY) = (lastX + dx * norm, lastY + dy * norm);
					distSinceLast += stepLength;

					if (distSinceLast >= spacing)
					{
						labels.AppendLine($"{label};{airport};{y:00.000000};{x:000.000000};");
						++labelsPlaced;
						distSinceLast = 0;
					}
				}
			}

			if (labelsPlaced > 0)
				placedLabels.Add(label);
		}

		foreach (var label in neededLabels.Except(placedLabels))
		{
			// Find the taxiways that were too short and give them their own labels in the middle so we didn't miss any letters.
			Node[]? nodes = centrelines.Where(tw => (tw["ref"] ?? tw["name"]) == label).MaxBy(tw => tw.Nodes.Length)?.Nodes;

			if (nodes is null || nodes.Length < 1)
				continue;

			Node median = nodes[nodes.Length / 2];
			labels.AppendLine($"{label};{airport};{median.Latitude:00.000000};{median.Longitude:000.000000};");
		}

		return labels.ToString();
	}
}
