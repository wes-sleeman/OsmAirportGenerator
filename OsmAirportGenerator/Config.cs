﻿using System.Text.Json.Serialization;

namespace OsmAirportGenerator;

internal record Config(
	[property: JsonPropertyName("termsAccepted")] bool TermsAccepted,
	[property: JsonPropertyName("colours")] ColourschemeSet Colours,
	[property: JsonPropertyName("visibility")] Visibility Visibility,
	[property: JsonPropertyName("inflation")] Inflation Inflation
)
{
	public static Config Default { get; } = new(
		false,
		ColourschemeSet.Default,
		Visibility.Empty.Normalise(),
		new Inflation(null, null).Normalise()
	); 

	public Config Normalise() => new(
		TermsAccepted,
		Colours.Normalise(),
		Visibility.Normalise(),
		Inflation.Normalise()
	);
}

internal record ColourschemeSet(
	[property: JsonPropertyName("fill")] Colourscheme Fill,
	[property: JsonPropertyName("stroke")] Colourscheme Stroke
)
{
	public static ColourschemeSet Default { get; } = new(
		Colourscheme.Empty.Normalise(),
		Colourscheme.Empty.Normalise()
	);
	
	public ColourschemeSet Normalise() => new(
		Fill.Normalise(),
		Stroke.Normalise()
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
	public static Colourscheme Empty { get; } = new(null, null, null, null, null, null, null);

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
	public static Visibility Empty { get; } = new(null, null, null, null, null, null, null);

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

internal record Inflation(
	[property: JsonPropertyName("taxiway")] double? Taxiway,
	[property: JsonPropertyName("runway")] double? Runway
)
{
	public Inflation Normalise() => new(
		Taxiway ?? 0.0001,
		Runway ?? 0.0002
	);
}
