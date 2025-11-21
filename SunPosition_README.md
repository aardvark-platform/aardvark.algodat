# Sun Position Calculation - F# Implementation

## Overview

This directory contains a minimal F# implementation for calculating the sun position for Earth, based on the C# code from the Aardvark.Physics.Sky library:
https://github.com/aardvark-platform/aardvark.algodat/blob/8930ed876b4bc36fc5feeaff4d0034723b01c8c1/src/Aardvark.Physics.Sky/SunPosition.cs#L54

The implementation follows the methodology from **Astronomy Answers** by Dr Louis Strous:
https://www.aa.quae.nl/en/reken/zonpositie.html

**Accuracy**: Approximately ±1°

## Files

- **`SunPosition.fs`** - Standalone F# module with the sun position calculation function
- **`SunPositionCalculation.md`** - Detailed documentation with code examples
- **`TestSunPosition.fsx`** - F# script with test cases demonstrating the function usage
- **`SunPosition_README.md`** - This file

## Quick Start

### Using the F# Module

```fsharp
#load "SunPosition.fs"

// Calculate sun position for Vienna, Austria on June 21, 2024 at noon UTC
let longitude = 16.3738f  // degrees east
let latitude = 48.2082f   // degrees north
let year = 2024
let secondsSinceStartOfYear = float32 (172 * 86400 + 12 * 3600) // Day 173, 12:00 UTC

let (theta, phi, distance) = SunPosition.compute longitude latitude secondsSinceStartOfYear year

printfn "Sun position: theta=%.2f°, phi=%.2f°, distance=%.2e m" 
    (theta * 180.0f / 3.14159265f) 
    (phi * 180.0f / 3.14159265f) 
    distance
```

### Running the Tests

```bash
dotnet fsi TestSunPosition.fsx
```

This will run four test cases covering different locations and dates:
1. Vienna, Austria - Summer Solstice
2. New York City - Winter Solstice
3. Equator - Spring Equinox
4. Sydney, Australia - New Year's Day

## Function Signature

```fsharp
val compute : 
    longitudeInDegrees:float32 -> 
    latitudeInDegrees:float32 -> 
    secondsSinceStartOfYear:float32 -> 
    year:int -> 
    float32 * float32 * float32
```

### Parameters

- **`longitudeInDegrees`**: GPS longitude coordinate in degrees
  - Positive values for East, negative for West
  - Range: -180° to 180°
  - Example: Vienna = 16.3738°, New York = -74.006°

- **`latitudeInDegrees`**: GPS latitude coordinate in degrees
  - Positive values for North, negative for South
  - Range: -90° to 90°
  - Example: Vienna = 48.2082°, Sydney = -33.8688°

- **`secondsSinceStartOfYear`**: Seconds elapsed since January 1st, 00:00:00 UTC
  - For January 1st, 12:00 UTC: `12 * 3600` (43200 seconds)
  - For June 21st, 12:00 UTC: `172 * 86400 + 12 * 3600` (14,904,000 seconds)
  - Calculate as: `dayOfYear * 86400 + hour * 3600 + minute * 60 + second`

- **`year`**: Absolute year
  - Example: 2024, 2025, etc.

### Return Value

Returns a tuple `(theta, phi, distance)`:

- **`theta`**: Angle from zenith in radians
  - 0 = zenith (directly overhead)
  - π/2 = horizon
  - π = nadir (directly below)
  - To convert to height above horizon: `height = π/2 - theta` or `90° - theta_degrees`

- **`phi`**: Azimuth angle in radians (measured from south, clockwise)
  - 0 or 2π = south
  - π/2 = west
  - π = north
  - 3π/2 = east

- **`distance`**: Distance to the sun in meters
  - Typical range: 1.47e11 to 1.52e11 meters (0.983 to 1.017 AU)

## Implementation Details

### Key Features

1. **Float32 Precision**: All calculations use `float32` (single precision) as requested
2. **Inlined Functions**: All astronomical calculations are inlined into a single function
3. **No External Dependencies**: Only standard F# mathematical functions are used
4. **Self-Contained**: All orbital parameters and constants are embedded in the code

### Inlined Components

The function includes inline implementations of:
- Julian day calculation from year and seconds
- Earth's mean anomaly calculation
- True anomaly approximation (Equation of Center)
- Earth's obliquity calculation (5th order polynomial)
- Ecliptic to equatorial coordinate transformation
- Sidereal time calculation
- Observer coordinate transformation (height and azimuth)
- Distance to the sun calculation

### Constants Included

- Earth orbital parameters (eccentricity, perihelion, mean motion)
- True anomaly approximation coefficients
- Earth obliquity coefficients (Astronomical Almanac 2010)
- Astronomical unit (AU) in meters
- J2000 epoch (Julian day 2451545.0)

## Example Calculations

### Example 1: Summer Solstice at Noon in Vienna

```fsharp
let longitude = 16.3738f
let latitude = 48.2082f
let year = 2024
let secondsSinceStartOfYear = float32 (172 * 86400 + 12 * 3600) // June 21, 12:00 UTC

let (theta, phi, distance) = SunPosition.compute longitude latitude secondsSinceStartOfYear year
// Result: theta ≈ 27.83°, height above horizon ≈ 62.17°
```

### Example 2: Equinox at the Equator

```fsharp
let longitude = 0.0f
let latitude = 0.0f
let year = 2024
let secondsSinceStartOfYear = float32 (79 * 86400 + 12 * 3600) // March 20, 12:00 UTC

let (theta, phi, distance) = SunPosition.compute longitude latitude secondsSinceStartOfYear year
// Result: theta ≈ 1.69°, height above horizon ≈ 88.31° (nearly overhead)
```

## Coordinate System

The function uses the **Aardvark coordinate system** for spherical coordinates:

- **Theta (θ)**: Polar angle measured from the zenith (top)
  - θ = 0: Zenith (directly overhead)
  - θ = π/2: Horizon
  - θ = π: Nadir (directly below)

- **Phi (φ)**: Azimuthal angle measured from south, rotating clockwise (when viewed from above)
  - φ = 0: South
  - φ = π/2: West
  - φ = π: North
  - φ = 3π/2: East
  - φ = 2π: South again

## Conversion Formulas

### From Theta/Phi to Altitude/Azimuth

```fsharp
let altitude = 90.0f - theta * 180.0f / pi  // degrees above horizon
let azimuthFromNorth = (phi * 180.0f / pi + 180.0f) % 360.0f  // degrees clockwise from north
```

### Calculate Day of Year

```fsharp
// For a specific date in 2024 (leap year)
let daysInMonth = [| 31; 29; 31; 30; 31; 30; 31; 31; 30; 31; 30; 31 |]
let dayOfYear month day = 
    let previousMonths = daysInMonth.[0..month-2] |> Array.sum
    previousMonths + day - 1  // -1 because Jan 1 is day 0

// June 21, 2024
let day = dayOfYear 6 21  // = 172
let seconds = float32 (day * 86400 + 12 * 3600)  // at 12:00 UTC
```

## Accuracy and Limitations

- **Accuracy**: ±1° as specified in the original implementation
- **Valid Range**: Most accurate for years close to 2000 (±100 years)
- **Simplifications**: 
  - Earth's orbit is approximated with low-order polynomials
  - Atmospheric refraction is not included
  - Nutation and aberration are not accounted for
  - Sun's ecliptic latitude (β) is assumed to be 0

## References

1. **Astronomy Answers** by Dr Louis Strous  
   https://www.aa.quae.nl/en/reken/zonpositie.html

2. **Original C# Implementation**  
   https://github.com/aardvark-platform/aardvark.algodat/blob/8930ed876b4bc36fc5feeaff4d0034723b01c8c1/src/Aardvark.Physics.Sky/SunPosition.cs

3. **Astronomical Almanac 2010**  
   Used for Earth's obliquity calculation

## License

This code is based on the Aardvark Platform, which is licensed under the Apache License 2.0.

Copyright (C) 2006-2025 Aardvark Platform Team  
https://aardvark.graphics
