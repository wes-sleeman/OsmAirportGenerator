using OsmAirportGenerator;

using System.IO.Compression;

using WSleeman.Osm;

using static OsmAirportGenerator.GenerationUtils;

// Load the config file if it exists.
Config config = Config.Default;

if (File.Exists("config.json"))
{
	if (System.Text.Json.JsonSerializer.Deserialize<Config>(File.ReadAllText("config.json")) is not Config deserialisedConfig)
	{
		File.Move("config.json", "config.broken.json");
		Console.Error.WriteLine("Invalid configuration file. Moved to config.broken.json. Next time this program is run, a clean file will be generated.");
		Environment.Exit(-1);
		return;
	}

	config = deserialisedConfig;
}

// Check acceptance of terms.
while (!config.TermsAccepted)
{
	Console.WriteLine(@"OSM LAYOUT GENERATOR - TERMS OF USE:
1. This tool is for IVAO use only.
2. If you notice inaccurate data, fix it on OSM's website.
3. Follow OSM contributor guidelines for all your edits. Do not modify data to enhance the appearance of generated layouts.
4. You are solely responsible for any edits that you make.
");
	Console.Write("To confirm your acceptance of the above, type \"I AGREE\" in all capital letters: ");

	if (Console.ReadLine()?.Trim() is "I AGREE")
	{
		config = config with { TermsAccepted = true };
		await File.WriteAllTextAsync("config.json", System.Text.Json.JsonSerializer.Serialize(config));
	}

	Console.Clear();
}

// Main input loop: Process airport codes and generate files.
while (true)
{
	Console.Write("Enter an airport ICAO code: ");

	// Uppercase provided ICAO code by US casing definitions (sorry, Turkey!)
	if (Console.ReadLine()?.ToUpperInvariant() is not string airportIcao)
	{
		Console.Error.WriteLine("Error in reading input.");
		Environment.Exit(-2);
		return;
	}

	// ICAO codes: EXIT and QUIT are keywords.
	if (airportIcao is "EXIT" or "QUIT")
	{
		Console.WriteLine("Goodbye!");
		return;
	}

	// Query Overpass (overpass-api.de) for the airport data. Timeout the query in 5mins and the processing 1min later.
	Console.Write("Downloading data... ");
	await Console.Out.FlushAsync();

	Overpass data = await Overpass.FromQueryAsync(@$"[out:json][timeout:300];
(way[""icao""=""{airportIcao}""];)->.searchArea;
(
	nwr[""aeroway""](area.searchArea);
	>;
);
out;", timeoutMins: 6);

	// Check if it's a closed relation and try the alternate query just in case.
	if (data.Nodes.Count is 0 && data.Ways.Count is 0)
	{
		data = await Overpass.FromQueryAsync(@$"[out:json][timeout:300];
(area[""icao""=""{airportIcao}""];)->.searchArea;
(
	nwr[""aeroway""](area.searchArea);
	>;
);
out;", timeoutMins: 6);

		// Make sure the airport actually pulled correctly.
		if (data.Nodes.Count is 0 && data.Ways.Count is 0)
		{
			// No nodes or ways. Probably not a valid airport.
			Console.WriteLine("ERROR!");
			Console.WriteLine("Not a known airport! You might need to add it in OSM.");
			Console.WriteLine("https://www.openstreetmap.org/edit");
			Console.WriteLine("Don't forget! Follow OSM rules when editing OSM data.");
			continue;
		}
		else
		{
			Console.WriteLine("Done!");
			Console.WriteLine($"The boundary of {airportIcao} is defined as a relation.");
			Console.WriteLine("Boundaries may not generate correctly and some data may be missing.");
			Console.WriteLine("If this doesn't need to be a relation, consider editing OSM to combine the segments of the boundary.");
			Console.WriteLine("https://www.openstreetmap.org/edit");
			Console.WriteLine("Don't forget! Follow OSM rules when editing OSM data.");
		}

	}
	else
		Console.WriteLine("Done!");

	if (data.Relations.Count > 0)
		// There are relations. We'll try to generate "everything", but no guarantees.
		Console.WriteLine($"{airportIcao} contains OSM relations. Generated data may be incomplete!");

	Console.Write("Generating... ");
	await Console.Out.FlushAsync();

	// Data is saved to a temporary directory until it's done.
	string tempFolder = Directory.CreateTempSubdirectory().FullName;
	string filePrefix = Path.Combine(tempFolder, airportIcao);

	var cultureCache = System.Globalization.CultureInfo.CurrentCulture;
	System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

	// Generate the necessary files.
	await GenerateBoundaryAsync(filePrefix, data, config);
	await GenerateApronsAsync(filePrefix, data, config);
	await GenerateBuildingsAsync(airportIcao, filePrefix, data, config);
	await GenerateTaxilanesAsync(filePrefix, data, config);
	await GenerateTaxiwaysAsync(airportIcao, filePrefix, data, config);
	await GenerateHelipadsAsync(filePrefix, data, config);
	await GenerateRunwaysAsync(filePrefix, data, config);

	System.Globalization.CultureInfo.CurrentCulture = cultureCache;
	Console.WriteLine("Done!");

	// ZIP created data and move to executing directory.
	string zipDir = Path.Combine(Environment.CurrentDirectory, $"{airportIcao}.zip");

	if (File.Exists(zipDir))
		File.Delete(zipDir);

	ZipFile.CreateFromDirectory(tempFolder, zipDir, CompressionLevel.Optimal, false);
	Directory.Delete(tempFolder, true);
	Console.WriteLine($"File saved to {zipDir}");
}
