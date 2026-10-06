# Sky Models and Astronomy

## Sky Models

| Model | Use |
|-------|-----|
| `CIESky` | CIE Standard General Sky: 15 overcast, partly cloudy, and clear types |
| `HosekSky` | Hosek-Wilkie sky with turbidity and ground albedo |
| `PreethamSky` | Analytical clear-sky approximation |
| `AlienWorld` | Experimental spectral model; source notes unresolved intensity and spectrum-to-XYZ issues |

```csharp
using System;
using Aardvark.Base;
using Aardvark.Physics.Sky;

var cie = new CIESky(0.0, Math.PI / 4, CIESkyType.ClearSky2,
    diffIllu: 15000, globIllu: 80000);
var hosek = new HosekSky(0.0, Math.PI / 4, atmospheric_turbidity: 3.0,
    ground_albedo: new C3f(0.3f), color_format: Col.Format.CieXYZ);
var preetham = new PreethamSky(0.0, Math.PI / 4, turbidity: 2.5);

C3f radiance = cie.GetRadiance(V3d.OOI);
C3f color = cie.GetColor(V3d.OOI);
```

- Supply normalized view vectors. `Phi` is azimuth in radians (0 south, π/2 west); `Theta` is the zenith angle (0 zenith, π/2 horizon). These are not compass bearings.
- CIE `diffIllu` and `globIllu` are diffuse horizontal and global illuminance in lux; omit them to use the model's estimates.
- CIE, Preetham, and the **XYZ-configured** Hosek model return XYZ values from `GetRadiance()`, with luminance Y in cd/m². Do not generalize that color space to every Hosek configuration.
- `GetColor(V3d)` maps brightness into [0,1] and applies the sRGB transfer function. It is not linear RGB or a physical radiance output; use `GetRadiance(V3d)` for your own exposure/color pipeline. Hosek's color conversion assumes XYZ input, so use `Col.Format.CieXYZ` with it.

See [CIESky](../src/Aardvark.Physics.Sky/CIESky.cs), [HosekSky](../src/Aardvark.Physics.Sky/HosekSky.cs), and [PreethamSky](../src/Aardvark.Physics.Sky/PreethamSky.cs) for model-specific parameters and limits.

## Time and Observer Coordinates

Longitude is east-positive and latitude north-positive, both in degrees. Julian-day overloads take **UTC**. DateTime overloads subtract the supplied timezone offset; pass **zero with `DateTime.UtcNow`**. They do not infer a civil timezone or daylight-saving rules.

```csharp
var utc = DateTime.UtcNow;
var (sun, sunDistanceMeters) = SunPosition.Compute(utc, 0, 16.37, 48.21);
var (moon, moonDistanceMeters) = MoonPosition.Compute(utc, 0, 16.37, 48.21);

double jd = utc.ComputeJulianDay();
var (phi, theta, distanceAU) = Astronomy.PlanetDirectionAndDistance(
    Planet.Mars, jd, 16.37, 48.21);
```

Sun and Moon return `SphericalCoordinate` and distance in meters; planets return azimuth, zenith angle, and distance in astronomical units. Below-horizon directions are valid astronomy results, not necessarily valid inputs to every daylight sky model. Accuracy limits for planet calculations are documented in [Astronomy](../src/Aardvark.Physics.Sky/Astronomy.cs).

`ComputeJulianDay()` converts the supplied clock fields. For local time, subtract `offsetHours / 24.0` once. `DateTimeExtensions.ComputeDateFromJulianDay(jd)` performs the reverse conversion; `Astronomy.J2000` is Julian day 2451545.0.

## Sunrise, Sunset, and Twilight

Continuing with the UTC Julian day above:

```csharp
var (sunrise, transit, sunset) = SunPosition.SunRiseAndSet(jd, 16.37, 48.21);
var times = SunPosition.GetTwilightTimes(jd, 16.37, 48.21);
var localTimes = times.ToDateTime(timeZone: 1);
```

Transitions are returned as Julian days. Nonexistent transitions (for example polar day/night) are `double.NaN`; `ToDateTime` converts those to `DateTime.MinValue`. Its timezone argument is a fixed output offset in hours.

[SunPosition](../src/Aardvark.Physics.Sky/SunPosition.cs) uses an approximate solar
model, not a precision ephemeris. `HorizonTransit(jd, longitude, latitude, elevation)`
returns rising crossing, solar transit, and setting crossing. The elevation is the
solar center's angle above the local horizon in degrees (negative below it), **not
solar declination**. Its initial estimate uses declination at transit; two refinements
use the declination at each event. Agreement with roots of this model's altitude
equation does not guarantee real-world timing accuracy.

`SunRiseAndSet` uses −0.83°, approximating the solar disk radius and standard sea-level
refraction. `CivilDuskAndDawn`, `NauticalDuskAndDawn`, and `AstronomicalDuskAndDawn`
use −6°, −12°, and −18°. `GetTwilightTimes` uses those same levels, plus −0.3° for
sunrise end/sunset start and +6° for golden-hour end/start. Missing crossings remain
NaN while solar transit is retained. The legacy three-argument `HorizonTransit`
overload estimates geometric-horizon (0°) crossings without these two refinements;
it is not the −0.83° sunrise/sunset wrapper.

## Star-Catalog Transformations

For a catalog direction `V3d starICRF` and UTC Julian day `jd`:

```csharp
M33d icrf2cep = Astronomy.ICRFtoCEP(jd);
M33d cep2itrf = Astronomy.CEPtoITRF(jd, xp: 0.0, yp: 0.0);
M33d itrf2local = Astronomy.ITRFtoLocal(16.37, 48.21);
V3d starLocal = (itrf2local * cep2itrf * icrf2cep) * starICRF;
```

`xp` and `yp` are polar-motion inputs; zero omits that correction. This is an astronomical frame transformation, not an EPSG projection.

## Related

- [Geodetics](GEOMETRY.md#geodetics): WGS84, UTM, and custom map projections
- [Astronomy Answers](https://www.aa.quae.nl/en/reken/): astronomical calculation reference
- [CIE Standard](http://mathinfo.univ-reims.fr/IMG/pdf/other2.pdf)
- Hosek-Wilkie (2012), *An Analytic Model for Full Spectral Sky-Dome Radiance*
- Preetham (1999), *A Practical Analytic Model for Daylight*
