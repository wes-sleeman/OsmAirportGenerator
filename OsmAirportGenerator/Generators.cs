using WSleeman.Osm;

namespace OsmAirportGenerator;

internal static partial class GenerationUtils
{
	/// <summary>Generates a big, ugly poly filling any aerodrome boundaries.</summary>
	/// <param name="filePrefix">The folder & filename to output the generated files into. File extension will be replaced.</param>
	public static async Task GenerateBoundaryAsync(string filePrefix, Overpass data, Config config)
	{
		if (config.Visibility.Boundary is false)
			return;

		// Get the TFL writer and filter the provided data to just the bits we care about.
		string filePath = Path.ChangeExtension(filePrefix, @"tfl");
		var filteredData = data.Filter(static i => i["aeroway"] is "aerodrome");
		using StreamWriter fileWriter = await GetWriterAsync(filePath);

		// Add all the TFLs for all the boundaries.
		foreach (Way boundary in filteredData.AllWays())
			await fileWriter.WriteLineAsync(boundary.ToTfl(config.Colours.Fill.Boundary!, config.Colours.Stroke.Boundary!));
	}

	/// <summary>Generates polys for any provided terminals, hangars, and towers.</summary>
	/// <param name="filePrefix">The folder & filename to output the generated files into. File extension will be replaced.</param>
	public static async Task GenerateBuildingsAsync(string airport, string filePrefix, Overpass data, Config config)
	{
		if (config.Visibility.Building is false)
			return;

		// Get the TFL writer and filter the provided data to just the bits we care about.
		string polyFilePath = Path.ChangeExtension(filePrefix, @"tfl");
		string labelFilePath = Path.ChangeExtension(filePrefix, @"txi");
		var filteredData = data.Filter(static i => i["aeroway"] is "terminal" or "hangar" or "tower");
		using StreamWriter polyWriter = await GetWriterAsync(polyFilePath);
		using StreamWriter labelWriter = await GetWriterAsync(labelFilePath);

		Way[] allWays = [..filteredData.AllWays()];

		// Add all the TFLs for all the buildings.
		foreach (Way building in allWays)
			await polyWriter.WriteLineAsync(building.ToTfl(config.Colours.Fill.Building!, config.Colours.Stroke.Building!, "BUILDING"));

		// Add labels for the buildings which have them.
		foreach (Way building in allWays.Where(static w => (w["ref"] ?? w["name"]) is not null))
			await labelWriter.WriteLineAsync($"{building["ref"] ?? building["name"]};{airport};{building.Nodes.Average(static n => n.Latitude):00.000000};{building.Nodes.Average(static n => n.Longitude):000.000000};");
	}

	/// <summary>Generates polys for any provided aprons.</summary>
	/// <param name="filePrefix">The folder & filename to output the generated files into. File extension will be replaced.</param>
	public static async Task GenerateApronsAsync(string filePrefix, Overpass data, Config config)
	{
		if (config.Visibility.Apron is false)
			return;

		// Get the TFL writer and filter the provided data to just the bits we care about.
		string filePath = Path.ChangeExtension(filePrefix, @"tfl");
		var filteredData = data.Filter(static i => i["aeroway"] is "apron");
		using StreamWriter fileWriter = await GetWriterAsync(filePath);

		foreach (Way apronArea in filteredData.AllWays())
			await fileWriter.WriteLineAsync(apronArea.ToTfl(config.Colours.Fill.Apron!, config.Colours.Stroke.Apron!, "APRON"));
	}

	/// <summary>Generates centrelines for any provided taxilanes.</summary>
	/// <param name="airport">The ICAO code of the airport being generated.</param>
	/// <param name="filePrefix">The folder & filename to output the generated files into. File extension will be replaced.</param>
	public static async Task GenerateTaxilanesAsync(string filePrefix, Overpass data, Config config)
	{
		if (config.Visibility.Taxilane is false)
			return;

		// Get the GEO writer and filter the provided data to just the bits we care about.
		string filePath = Path.ChangeExtension(filePrefix, @"geo");
		var filteredData = data.Filter(static i => i["aeroway"] is "taxilane");
		using StreamWriter fileWriter = await GetWriterAsync(filePath);

		// NOTE: We're not unpacking relations here.
		// That would imply someone went and surrounded the taxilane instead of just drawing its centreline.
		foreach (Way taxilane in filteredData.Ways.Values)
			await fileWriter.WriteLineAsync(taxilane.ToGeo(config.Colours.Stroke.Taxilane!));
	}

	/// <summary>Generates centrelines & polys for any provided taxiways.</summary>
	/// <param name="airport">The ICAO code of the airport being generated.</param>
	/// <param name="filePrefix">The folder & filename to output the generated files into. File extension will be replaced.</param>
	/// <param name="inflation">The amount to inflate the centreline by to make the polygon.</param>
	public static async Task GenerateTaxiwaysAsync(string airport, string filePrefix, Overpass data, Config config)
	{
		if (config.Visibility.Taxiway is false)
			return;

		// Get the writers and filter the provided data to just the bits we care about.
		string centrelineFilePath = Path.ChangeExtension(filePrefix, @"geo");
		string polyFilePath = Path.ChangeExtension(filePrefix, @"tfl");
		string labelFilePath = Path.ChangeExtension(filePrefix, @"txi");
		var filteredData = data.Filter(static i => i["aeroway"] is "taxiway");
		using StreamWriter centrelineWriter = await GetWriterAsync(centrelineFilePath);
		using StreamWriter polyWriter = await GetWriterAsync(polyFilePath);
		using StreamWriter labelWriter = await GetWriterAsync(labelFilePath);

		Way[] allWays = [..filteredData.AllWays()];

		foreach (Way taxiway in allWays)
			await centrelineWriter.WriteLineAsync(taxiway.ToGeo(config.Colours.Stroke.Taxiway!));

		// Inflate the centrelines by the given amount and render them out.
		foreach (Way taxiwayBoundary in allWays.Select(w => w.Inflate(config.Inflation.Taxiway ?? 0.0001)))
			await polyWriter.WriteLineAsync(taxiwayBoundary.ToTfl(config.Colours.Fill.Taxiway!, config.Colours.Stroke.Taxiway!, "TAXIWAY"));

		// Generate the taxiway labels.
		await labelWriter.WriteLineAsync(allWays.LabelCentrelines(airport));
	}

	/// <summary>Generates polys for any provided runways.</summary>
	/// <param name="filePrefix">The folder & filename to output the generated files into. File extension will be replaced.</param>
	/// <param name="inflation">The amount to inflate the centreline by to make the polygon.</param>
	public static async Task GenerateRunwaysAsync(string filePrefix, Overpass data, Config config)
	{
		if (config.Visibility.Runway is false)
			return;

		// Get the TFL writer and filter the provided data to just the bits we care about.
		string filePath = Path.ChangeExtension(filePrefix, @"tfl");
		var filteredData = data.Filter(static i => i["aeroway"] is "runway");
		using StreamWriter fileWriter = await GetWriterAsync(filePath);

		// Inflate the centrelines by the given amount and render them out.
		foreach (Way runwayBoundary in filteredData.AllWays().Select(w => w.Inflate(config.Inflation.Runway ?? 0.0002)))
			await fileWriter.WriteLineAsync(runwayBoundary.ToTfl(config.Colours.Fill.Runway!, config.Colours.Stroke.Runway!, "RUNWAY"));
	}

	/// <summary>Generates polys for any provided helipads.</summary>
	/// <param name="filePrefix">The folder & filename to output the generated files into. File extension will be replaced.</param>
	public static async Task GenerateHelipadsAsync(string filePrefix, Overpass data, Config config)
	{
		if (config.Visibility.Helipad is false)
			return;

		// Get the TFL writer and filter the provided data to just the bits we care about.
		string filePath = Path.ChangeExtension(filePrefix, @"tfl");
		var filteredData = data.Filter(static i => i["aeroway"] is "helipad");
		using StreamWriter fileWriter = await GetWriterAsync(filePath);

		foreach (Way helipad in filteredData.AllWays())
			await fileWriter.WriteLineAsync(helipad.ToTfl(config.Colours.Fill.Helipad!, config.Colours.Stroke.Helipad!, "RUNWAY"));
	}
}
