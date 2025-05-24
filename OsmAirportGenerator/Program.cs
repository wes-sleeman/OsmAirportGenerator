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
		Console.Error.WriteLine("Invalid configuration file.");
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

	Console.WriteLine("Done!");

	// Make sure the airport actually pulled correctly.
	if (data.Nodes.Count is 0 && data.Ways.Count is 0)
	{
		// No nodes or ways. Probably not a valid airport.
		Console.WriteLine(@"Not a known airport! You might need to add it in OSM.");
		Console.WriteLine(@"<link to OSM edit page>");
		Console.WriteLine(@"Don't forget! Follow OSM rules when editing OSM data.");
		continue;
	}

	if (data.Relations.Count > 0)
		// There are relations. We'll try to generate "everything", but no guarantees.
		Console.WriteLine($"{airportIcao} contains OSM relations. Generated data may be incomplete!");

	Console.Write("Generating... ");
	await Console.Out.FlushAsync();

	// Data is saved to a temporary directory until it's done.
	string tempFolder = Directory.CreateTempSubdirectory().FullName;
	string filePrefix = Path.Combine(tempFolder, airportIcao);

	// Generate the necessary files.
	if (config.Visibility.Boundary!.Value)
		await GenerateBoundaryAsync(filePrefix, data, config.Colours.Boundary!);

	if (config.Visibility.Apron!.Value)
		await GenerateApronsAsync(filePrefix, data, config.Colours.Apron!);

	if (config.Visibility.Building!.Value)
		await GenerateBuildingsAsync(airportIcao, filePrefix, data, config.Colours.Building!);

	if (config.Visibility.Taxilane!.Value)
		await GenerateTaxilanesAsync(filePrefix, data, config.Colours.Taxilane!);

	if (config.Visibility.Taxiway!.Value)
		await GenerateTaxiwaysAsync(airportIcao, filePrefix, data, config.Colours.Taxiway!, config.Colours.Taxiway!);

	if (config.Visibility.Helipad!.Value)
		await GenerateHelipadsAsync(filePrefix, data, config.Colours.Helipad!);

	if (config.Visibility.Runway!.Value)
		await GenerateRunwaysAsync(filePrefix, data, config.Colours.Runway!);

	Console.WriteLine("Done!");

	// ZIP created data and move to executing directory.
	string zipDir = Path.Combine(Environment.CurrentDirectory, $"{airportIcao}.zip");

	if (File.Exists(zipDir))
		File.Delete(zipDir);

	ZipFile.CreateFromDirectory(tempFolder, zipDir, CompressionLevel.Optimal, false);
	Directory.Delete(tempFolder, true);
	Console.WriteLine($"File saved to {zipDir}");
}
