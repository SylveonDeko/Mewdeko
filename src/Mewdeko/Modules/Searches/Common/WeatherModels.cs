using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents the coordinates of a location.
/// </summary>
public class Coord
{
    /// <summary>
    ///     Gets or sets the longitude coordinate.
    /// </summary>
    public double Lon { get; set; }

    /// <summary>
    ///     Gets or sets the latitude coordinate.
    /// </summary>
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
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the main weather group.
    /// </summary>
    public string Main { get; set; }

    /// <summary>
    ///     Gets or sets the weather condition within the group.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    ///     Gets or sets the weather icon ID.
    /// </summary>
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
    public double Temp { get; set; }

    /// <summary>
    ///     Gets or sets the atmospheric pressure.
    /// </summary>
    public float Pressure { get; set; }

    /// <summary>
    ///     Gets or sets the humidity.
    /// </summary>
    public float Humidity { get; set; }

    /// <summary>
    ///     Gets or sets the minimum temperature at the moment.
    /// </summary>
    [JsonProperty("temp_min")]
    public double TempMin { get; set; }

    /// <summary>
    ///     Gets or sets the maximum temperature at the moment.
    /// </summary>
    [JsonProperty("temp_max")]
    public double TempMax { get; set; }
}

/// <summary>
///     Represents wind information.
/// </summary>
public class Wind
{
    /// <summary>
    ///     Gets or sets the wind speed.
    /// </summary>
    public double Speed { get; set; }

    /// <summary>
    ///     Gets or sets the wind direction, degrees (meteorological).
    /// </summary>
    public double Deg { get; set; }
}

/// <summary>
///     Represents cloud information.
/// </summary>
public class Clouds
{
    /// <summary>
    ///     Gets or sets the cloudiness percentage.
    /// </summary>
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
    ///     Gets or sets the internal parameter.
    /// </summary>
    public double Message { get; set; }

    /// <summary>
    ///     Gets or sets the country code (GB, JP etc.).
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    ///     Gets or sets the sunrise time.
    /// </summary>
    public double Sunrise { get; set; }

    /// <summary>
    ///     Gets or sets the sunset time.
    /// </summary>
    public double Sunset { get; set; }
}

/// <summary>
///     Represents weather data for a location.
/// </summary>
public class WeatherData
{
    /// <summary>
    ///     Gets or sets the coordinates of the location.
    /// </summary>
    public Coord Coord { get; set; }

    /// <summary>
    ///     Gets or sets the weather conditions.
    /// </summary>
    public List<Weather> Weather { get; set; }

    /// <summary>
    ///     Gets or sets the main weather parameters.
    /// </summary>
    public Main Main { get; set; }

    /// <summary>
    ///     Gets or sets the visibility, meter.
    /// </summary>
    public int Visibility { get; set; }

    /// <summary>
    ///     Gets or sets the wind parameters.
    /// </summary>
    public Wind Wind { get; set; }

    /// <summary>
    ///     Gets or sets the cloud parameters.
    /// </summary>
    public Clouds Clouds { get; set; }

    /// <summary>
    ///     Gets or sets the time of data calculation, unix, UTC.
    /// </summary>
    public int Dt { get; set; }

    /// <summary>
    ///     Gets or sets the system parameters.
    /// </summary>
    public Sys Sys { get; set; }

    /// <summary>
    ///     Gets or sets the city ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the city name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the internal parameter.
    /// </summary>
    public int Cod { get; set; }
}