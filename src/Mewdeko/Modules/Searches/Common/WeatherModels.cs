using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents the coordinates of a location.
/// </summary>
public class Coord
{
    /// <summary>
    ///     Gets or sets the longitude coordinate.
    /// </summary>
    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    /// <summary>
    ///     Gets or sets the latitude coordinate.
    /// </summary>
    [JsonPropertyName("lat")]
    public double Lat { get; set; }
}

/// <summary>
///     Represents weather information.
/// </summary>
public class Weather
{
    /// <summary>
    ///     Gets or sets the weather condition ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the main weather group.
    /// </summary>
    [JsonPropertyName("main")]
    public string Main { get; set; }

    /// <summary>
    ///     Gets or sets the weather condition within the group.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    ///     Gets or sets the weather icon ID.
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon { get; set; }
}

/// <summary>
///     Represents main weather parameters.
/// </summary>
public class Main
{
    /// <summary>
    ///     Gets or sets the temperature.
    /// </summary>
    [JsonPropertyName("temp")]
    public double Temp { get; set; }

    /// <summary>
    ///     Gets or sets the temperature that it feels like.
    /// </summary>
    [JsonPropertyName("feels_like")]
    public double Feels_Like { get; set; }

    /// <summary>
    ///     Gets or sets the minimum temperature at the moment.
    /// </summary>
    [JsonPropertyName("temp_min")]
    public double TempMin { get; set; }

    /// <summary>
    ///     Gets or sets the maximum temperature at the moment.
    /// </summary>
    [JsonPropertyName("temp_max")]
    public double TempMax { get; set; }

    /// <summary>
    ///     Gets or sets the atmospheric pressure.
    /// </summary>
    [JsonPropertyName("pressure")]
    public float Pressure { get; set; }

    /// <summary>
    ///     Gets or sets the humidity.
    /// </summary>
    [JsonPropertyName("humidity")]
    public float Humidity { get; set; }

    /// <summary>
    ///     Gets or sets the sea level pressure.
    /// </summary>
    [JsonPropertyName("sea_level")]
    public float? Sea_Level { get; set; }

    /// <summary>
    ///     Gets or sets the ground level pressure.
    /// </summary>
    [JsonPropertyName("grnd_level")]
    public float? Grnd_Level { get; set; }
}

/// <summary>
///     Represents wind information.
/// </summary>
public class Wind
{
    /// <summary>
    ///     Gets or sets the wind speed.
    /// </summary>
    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    /// <summary>
    ///     Gets or sets the wind direction, degrees (meteorological).
    /// </summary>
    [JsonPropertyName("deg")]
    public double Deg { get; set; }

    /// <summary>
    ///     Gets or sets the wind gust speed.
    /// </summary>
    [JsonPropertyName("gust")]
    public double? Gust { get; set; }
}

/// <summary>
///     Represents cloud information.
/// </summary>
public class Clouds
{
    /// <summary>
    ///     Gets or sets the cloudiness percentage.
    /// </summary>
    [JsonPropertyName("all")]
    public int All { get; set; }
}

/// <summary>
///     Represents system information.
/// </summary>
public class Sys
{
    /// <summary>
    ///     Gets or sets the internal parameter.
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    ///     Gets or sets the internal parameter.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the country code (GB, JP etc.).
    /// </summary>
    [JsonPropertyName("country")]
    public string Country { get; set; }

    /// <summary>
    ///     Gets or sets the sunrise time.
    /// </summary>
    [JsonPropertyName("sunrise")]
    public double Sunrise { get; set; }

    /// <summary>
    ///     Gets or sets the sunset time.
    /// </summary>
    [JsonPropertyName("sunset")]
    public double Sunset { get; set; }

    /// <summary>
    ///     Gets or sets the internal parameter.
    /// </summary>
    public double? Message { get; set; }
}

/// <summary>
///     Represents weather data for a location.
/// </summary>
public class WeatherData
{
    /// <summary>
    ///     Gets or sets the coordinates of the location.
    /// </summary>
    [JsonPropertyName("coord")]
    public Coord Coord { get; set; }

    /// <summary>
    ///     Gets or sets the weather conditions.
    /// </summary>
    [JsonPropertyName("weather")]
    public List<Weather> Weather { get; set; }

    /// <summary>
    ///     Gets or sets the base station.
    /// </summary>
    [JsonPropertyName("base")]
    public string Base { get; set; }

    /// <summary>
    ///     Gets or sets the main weather parameters.
    /// </summary>
    [JsonPropertyName("main")]
    public Main Main { get; set; }

    /// <summary>
    ///     Gets or sets the visibility, meter.
    /// </summary>
    [JsonPropertyName("visibility")]
    public int Visibility { get; set; }

    /// <summary>
    ///     Gets or sets the wind parameters.
    /// </summary>
    [JsonPropertyName("wind")]
    public Wind Wind { get; set; }

    /// <summary>
    ///     Gets or sets the cloud parameters.
    /// </summary>
    [JsonPropertyName("clouds")]
    public Clouds Clouds { get; set; }

    /// <summary>
    ///     Gets or sets the time of data calculation, unix, UTC.
    /// </summary>
    [JsonPropertyName("dt")]
    public int Dt { get; set; }

    /// <summary>
    ///     Gets or sets the system parameters.
    /// </summary>
    [JsonPropertyName("sys")]
    public Sys Sys { get; set; }

    /// <summary>
    ///     Gets or sets the timezone.
    /// </summary>
    [JsonPropertyName("timezone")]
    public int Timezone { get; set; }

    /// <summary>
    ///     Gets or sets the city ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the city name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the internal parameter.
    /// </summary>
    [JsonPropertyName("cod")]
    public int Cod { get; set; }
}