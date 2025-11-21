# Sun Position Calculation - F# Function

## Quick Access

You now have access to a minimal F# function for calculating sun position for Earth!

### Files Available

1. **`SunPosition.fs`** (5.9 KB)
   - Standalone F# module with the `compute` function
   - Ready to use in your F# projects

2. **`SunPositionCalculation.md`** (7.1 KB)
   - Technical documentation with inline code
   - Includes detailed implementation notes

3. **`TestSunPosition.fsx`** (3.6 KB)
   - F# script with 4 test cases
   - Run with: `dotnet fsi TestSunPosition.fsx`

4. **`SunPosition_README.md`** (7.0 KB)
   - Comprehensive user guide
   - Coordinate system explanations
   - Conversion formulas

## Quick Start

```fsharp
#load "SunPosition.fs"

// Calculate sun position
let (theta, phi, distance) = SunPosition.compute 
    16.3738f    // longitude in degrees (east positive)
    48.2082f    // latitude in degrees (north positive)
    14904000.0f // seconds since start of year
    2024        // year

// Convert to degrees
let thetaDeg = theta * 180.0f / 3.14159265f
let phiDeg = phi * 180.0f / 3.14159265f
let heightAboveHorizon = 90.0f - thetaDeg

printfn "Height above horizon: %.2f degrees" heightAboveHorizon
```

## Function Signature

```fsharp
val compute : 
    longitudeInDegrees:float32 -> 
    latitudeInDegrees:float32 -> 
    secondsSinceStartOfYear:float32 -> 
    year:int -> 
    float32 * float32 * float32
```

Returns: `(theta, phi, distance)`
- `theta`: Angle from zenith in radians (0 = overhead, π/2 = horizon)
- `phi`: Azimuth in radians (0 = south, π/2 = west, π = north, 3π/2 = east)
- `distance`: Distance to sun in meters

## Key Features

✅ **Float32 precision** - All calculations use single precision floats  
✅ **Fully inlined** - No external dependencies except standard math functions  
✅ **Simple parameters** - Just lon, lat, seconds, and year  
✅ **±1° accuracy** - Same as the original C# implementation  
✅ **Well tested** - Includes test cases for multiple locations and dates  

## Example Results

**Vienna, Austria - June 21, 2024, 12:00 UTC (Summer Solstice)**
```
Height above horizon: 62.17 degrees
Distance: 1.52e+11 meters (1.016 AU)
```

**Equator - March 20, 2024, 12:00 UTC (Spring Equinox)**
```
Height above horizon: 88.31 degrees (nearly overhead)
Distance: 1.49e+11 meters (0.996 AU)
```

## Based On

- **Original C# Code**: https://github.com/aardvark-platform/aardvark.algodat/blob/8930ed876b4bc36fc5feeaff4d0034723b01c8c1/src/Aardvark.Physics.Sky/SunPosition.cs#L54
- **Methodology**: https://www.aa.quae.nl/en/reken/zonpositie.html by Dr Louis Strous
- **License**: Apache License 2.0

## Documentation

For detailed information, see:
- **Technical details**: `SunPositionCalculation.md`
- **User guide**: `SunPosition_README.md`
- **Test examples**: `TestSunPosition.fsx`

---

All files are in the root directory of this repository and ready to use!
