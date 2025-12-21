# Sky Models and Astronomical Calculations

Physics-based sky illumination models and astronomical position calculations for Sun, Moon, planets, and stars.

## Sky Models

| Model | Class | Use Case |
|-------|-------|----------|
| CIE Standard General Sky | `CIESky` | Overcast to clear sky conditions (15 standardized types), architectural lighting |
| Hosek-Wilkie | `HosekSky` | High-quality clear sky, alien worlds, spectral rendering |
| Preetham | `PreethamSky` | Fast clear sky approximation, real-time rendering |

## Astronomical Calculations

| Object | Class | Accuracy | Returns |
|--------|-------|----------|---------|
| Sun | `SunPosition` | ~1° | Position, distance, twilight times, solar transit |
| Moon | `MoonPosition` | Low-precision | Position, distance |
| Planets | `Astronomy.PlanetDirectionAndDistance` | 0.03-1.08° | Position, distance (AU) |
| Stars | `Astronomy.ICRFtoCEP`, `Astronomy.CEPtoITRF` | High-precision | Coordinate transformations |

## Usage

### CIE Sky

Standard sky model for architectural lighting with 15 types from overcast to clear.

```csharp
using Aardvark.Physics.Sky;

// Create standard clear sky (type 11)
var sky = new CIESky(
    sunPhi: 0.0,              // South (radians)
    sunTheta: Math.PI / 4,    // 45° from zenith
    CIESkyType.ClearSky2      // Standard clear sky
);

// Get color for viewing direction
var viewDir = new V3d(0, 1, 0).Normalized;
C3f color = sky.GetColor(viewDir);        // Linear sRGB [0,1]
C3f radiance = sky.GetRadiance(viewDir);  // cd/m² in XYZ

// Optional: specify measured illuminance
var sky2 = new CIESky(
    sunPhi: 0.0,
    sunTheta: Math.PI / 4,
    CIESkyType.ClearSky2,
    diffIllu: 15000,    // Diffuse horizontal illuminance (lux)
    globIllu: 80000     // Global illuminance (lux)
);
```

**CIE Sky Types:**
- 0-4: Overcast (uniform to moderate gradation)
- 5-9: Partly cloudy (various solar corona brightness)
- 10-14: Clear sky (standard to turbid atmospheres)

### Hosek-Wilkie Sky

High-quality physically-based sky model supporting spectral, XYZ, and RGB output.

```csharp
// Standard Earth sky
var sky = new HosekSky(
    solarPhi: 0.0,
    solarTheta: Math.PI / 3,
    atmospheric_turbidity: 3.0,      // 1.0 = pristine, 10.0 = heavy pollution
    ground_albedo: new C3f(0.3f),    // RGB ground reflectance
    color_format: Col.Format.CieXYZ  // XYZ, RGB, or spectral
);

C3f radiance = sky.GetRadiance(viewVec);  // cd/m² in XYZ
C3f color = sky.GetColor(viewVec);        // Linear sRGB [0,1]

// Alien world sky
var alienSky = new AlienWorld(
    solarPhi: 0.0,
    solarTheta: Math.PI / 3,
    solar_intensity: 1.5,                      // Relative to Sun
    solar_surface_temperature_kelvin: 6500,    // Star temperature
    atmospheric_turbidity: 2.0,
    ground_albedo: 0.25
);
```

### Preetham Sky

Fast analytical sky model for real-time applications.

```csharp
var sky = new PreethamSky(
    sunPhi: 0.0,
    sunTheta: Math.PI / 6,
    turbidity: 2.5  // 1.9 = clear, 10.0 = hazy
);

C3f radiance = sky.GetRadiance(viewVec);  // cd/m² in XYZ
C3f color = sky.GetColor(viewVec);        // Linear sRGB [0,1]
```

### Sun Position

Calculate sun position, sunrise/sunset, and twilight times.

```csharp
using System;

// Sun position for location and time
var (sunCoord, distance) = SunPosition.Compute(
    time: DateTime.UtcNow,
    timeZone: 1,                  // UTC+1
    longitudeInDegrees: 16.37,    // East-positive (Vienna)
    latitudeInDegrees: 48.21      // North-positive
);

// sunCoord: SphericalCoordinate
//   Phi: [0..2π] azimuth (0=south, π/2=west, π=north, 3π/2=east)
//   Theta: [0..π] zenith angle (0=zenith, π/2=horizon)
// distance: meters to sun

// Using Julian days directly
double jd = DateTime.UtcNow.ComputeJulianDay();
var (coord, dist) = SunPosition.Compute(jd, 16.37, 48.21);

// Sunrise/sunset
var (sunrise, transit, sunset) = SunPosition.SunRiseAndSet(jd, 16.37, 48.21);
// Returns Julian days; double.NaN if sun doesn't cross horizon

// Twilight times
var (civilDawn, _, civilDusk) = SunPosition.CivilDuskAndDawn(jd, 16.37, 48.21);       // -6°
var (nautDawn, _, nautDusk) = SunPosition.NauticalDuskAndDawn(jd, 16.37, 48.21);     // -12°
var (astroDawn, _, astroDusk) = SunPosition.AstronomicalDuskAndDawn(jd, 16.37, 48.21); // -18°

// Complete twilight data
var times = SunPosition.GetTwilightTimes(jd, 16.37, 48.21);
DateTime sunriseTime = times.ToDateTime(timeZone: 1).SunRise;
```

### Moon Position

```csharp
var (moonCoord, distance) = MoonPosition.Compute(
    time: DateTime.UtcNow,
    timeZone: 1,
    longitudeInDegrees: 16.37,
    latitudeInDegrees: 48.21
);
// distance in meters
```

### Planet Positions

```csharp
// Calculate planet position
var (phi, theta, distanceAU) = Astronomy.PlanetDirectionAndDistance(
    planet: Planet.Mars,
    time: DateTime.UtcNow,
    timeZone: 1,
    longitudeInDegrees: 16.37,
    latitudeInDegrees: 48.21
);

// Available planets
Planet.Mercury, Planet.Venus, Planet.Earth, Planet.Mars,
Planet.Jupiter, Planet.Saturn, Planet.Uranus, Planet.Neptune, Planet.Pluto

// Angular diameter
double angularDiameterRad = Astronomy.AngularDiameter(Planet.Jupiter, distanceAU);

// Planet properties
double diameterMeters = Astronomy.GetPlanetDiameter(Planet.Jupiter);
```

### Star Positions (Advanced)

Transform star catalog coordinates to observer frame.

```csharp
// ICRF (celestial) to local observer coordinates
double jd = DateTime.UtcNow.ComputeJulianDay();

// ICRF to Celestial Ephemeris Pole (accounts for precession/nutation)
M33d icrf2cep = Astronomy.ICRFtoCEP(jd);

// CEP to International Terrestrial Reference Frame
M33d cep2itrf = Astronomy.CEPtoITRF(
    jd: jd,
    xp: 0.0,  // Earth orientation parameters (arcsec)
    yp: 0.0
);

// ITRF to local observer coordinates
M33d itrf2local = Astronomy.ITRFtoLocal(
    longitudeInDegrees: 16.37,
    latitudeInDegrees: 48.21
);

// Combined transformation
M33d icrf2local = itrf2local * cep2itrf * icrf2cep;

// Transform star position from ICRF catalog
V3d starICRF = new V3d(1, 0, 0);  // From star catalog
V3d starLocal = icrf2local * starICRF;
```

### Coordinate Transformations (Geodetics)

Transform between coordinate systems (WGS84, UTM, custom projections).

```fsharp
open Aardvark.Geodetics

// Create coordinate systems
let wgs84 = CoordinateSystem.epsg 4326        // WGS84 lat/lon
let utm33n = CoordinateSystem.epsg 32633      // UTM Zone 33N
let webMercator = CoordinateSystem.epsg 3857

// Transform points
let wgsPoint = V3d(16.37, 48.21, 100.0)  // lon, lat, height
let utmPoint = CoordinateSystem.transform wgs84 utm33n wgsPoint

// Batch transformation
let wgsPoints = [| V3d(16.37, 48.21, 100.0); V3d(16.38, 48.22, 105.0) |]
let utmPoints = CoordinateSystem.transform wgs84 utm33n wgsPoints
```

```csharp
// C# usage
using Aardvark.Geodetics;

var wgs84 = CoordinateSystem.FromEPSGCode(4326);
var utm33n = CoordinateSystem.FromEPSGCode(32633);

var wgsPoint = new V3d(16.37, 48.21, 100.0);
var utmPoint = CoordinateSystem.Transform(wgs84, utm33n, wgsPoint);

// Custom projection (Proj4 string)
var custom = CoordinateSystem.FromProj4(
    "+proj=tmerc +lat_0=0 +lon_0=15 +k=0.9996 +x_0=500000 +y_0=0 +datum=WGS84"
);
```

## Gotchas

### Coordinate Systems

**Sky Models:**
- **Phi (azimuth):** [0..2π] radians, 0=south, π/2=west, π=north, 3π/2=east (NOT compass bearing)
- **Theta (zenith angle):** [0..π] radians, 0=zenith, π/2=horizon, π=nadir
- View vectors must be normalized

**Astronomy:**
- GPS longitude: **east-positive** (opposite of some conventions)
- GPS latitude: **north-positive**
- Times: UTC in Julian days; subtract timezone offset (hours/24) for local time
- Angles: radians unless specified otherwise

### Julian Day Calculations

```csharp
// DateTime to Julian Day
double jd = dateTime.ComputeJulianDay();

// Subtract timezone for UTC
double jdUtc = dateTime.ComputeJulianDay() - (timeZoneHours / 24.0);

// Julian Day to DateTime
DateTime dt = DateTimeExtensions.ComputeDateFromJulianDay(jd);

// J2000 epoch
double daysSinceJ2000 = jd - Astronomy.J2000;  // J2000 = 2451545.0
```

### Color Space

- `GetRadiance()` returns **XYZ color space** in cd/m² (physical units)
- `GetColor()` returns **linear sRGB [0,1]** (display-ready)
- CIE sky: turbidity affects color temperature (higher = warmer/whiter)
- Hosek/Preetham: physical spectral distribution

### Edge Cases

- Sun/planet below horizon: valid calculations, theta > π/2
- Polar regions: sunrise/sunset may return `double.NaN` (polar day/night)
- CIE sky: clamped to prevent invalid sun positions near horizon (±10°)
- Coordinate transforms: ensure correct EPSG codes and units (degrees vs. meters)

## Performance

- **Preetham:** Fastest, suitable for real-time (few trig operations)
- **CIE:** Moderate, lookup tables + evaluation
- **Hosek:** Slowest, high-quality (polynomial evaluation + optional spectral conversion)
- **Astronomy:** Sun/Moon ~microseconds, planets ~10 microseconds, star transforms sub-microsecond
- **Geodetics:** Batch transformations much faster than individual calls

## See Also

- Aardvark.Base - V3d, C3f, color space conversions
- [EPSG Registry](https://epsg.io/) - Coordinate system codes
- [Astronomy Answers](https://www.aa.quae.nl/en/reken/) - Sun/planet calculation reference
- [CIE Standard](http://mathinfo.univ-reims.fr/IMG/pdf/other2.pdf) - Sky model specification
- Hosek-Wilkie (2012) - "An Analytic Model for Full Spectral Sky-Dome Radiance"
- Preetham (1999) - "A Practical Analytic Model for Daylight"
- [Julian Day](https://en.wikipedia.org/wiki/Julian_day) - Time system reference
- [Celestial Coordinates](https://en.wikipedia.org/wiki/Celestial_coordinate_system) - Coordinate system reference
- DotSpatial.Projections - Underlying library for geodetic transformations
