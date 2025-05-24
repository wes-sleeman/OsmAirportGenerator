using System.Text.Json.Serialization;

namespace OsmAirportGenerator;

internal record Config(
	[property: JsonPropertyName("termsAccepted")] bool TermsAccepted,
	[property: JsonPropertyName("colours")] Colourscheme Colours,
	[property: JsonPropertyName("visibility")] Visibility Visibility
)
{
	public static Config Default { get; } = new(
		false,
		new Colourscheme(null, null, null, null, null, null, null).Normalise(),
		new Visibility(null, null, null, null, null, null, null).Normalise()
	); 

	public Config Normalise() => new(
		TermsAccepted,
		Colours.Normalise(),
		Visibility.Normalise()
	);
}

internal record Colourscheme(
	[property: JsonPropertyName("boundary")] string? Boundary,
	[property: JsonPropertyName("apron")] string? Apron,
	[property: JsonPropertyName("building")] string? Building,
	[property: JsonPropertyName("taxilane")] string? Taxilane,
	[property: JsonPropertyName("taxiway")] string? Taxiway,
	[property: JsonPropertyName("helipad")] string? Helipad,
	[property: JsonPropertyName("runway")] string? Runway
)
{
	public Colourscheme Normalise() => new(
		Boundary ?? "BOUNDARY",
		Apron ?? "APRON",
		Building ?? "BUILDING",
		Taxilane ?? "TAXILANE",
		Taxiway ?? "TAXIWAY",
		Helipad ?? "HELIPAD",
		Runway ?? "RUNWAY"
	);
}

internal record Visibility(
	[property: JsonPropertyName("boundary")] bool? Boundary,
	[property: JsonPropertyName("apron")] bool? Apron,
	[property: JsonPropertyName("building")] bool? Building,
	[property: JsonPropertyName("taxilane")] bool? Taxilane,
	[property: JsonPropertyName("taxiway")] bool? Taxiway,
	[property: JsonPropertyName("helipad")] bool? Helipad,
	[property: JsonPropertyName("runway")] bool? Runway
)
{
	public Visibility Normalise() => new(
		Boundary ?? false,
		Apron ?? true,
		Building ?? true,
		Taxilane ?? true,
		Taxiway ?? true,
		Helipad ?? true,
		Runway ?? true
	);
}
