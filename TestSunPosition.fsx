// Test script for Sun Position calculation

#load "SunPosition.fs"

open System

// Test 1: Vienna, Austria on June 21, 2024 at noon UTC (summer solstice)
printfn "Test 1: Vienna, Austria - June 21, 2024, 12:00 UTC (Summer Solstice)"
let longitude1 = 16.3738f  // degrees east
let latitude1 = 48.2082f   // degrees north
let year1 = 2024
// June 21 is day 173 of 2024 (leap year), at 12:00:00 UTC
let secondsSinceStartOfYear1 = float32 (172 * 86400 + 12 * 3600) // 172 full days + 12 hours

let (theta1, phi1, distance1) = SunPosition.compute longitude1 latitude1 secondsSinceStartOfYear1 year1

printfn "  Theta (from zenith): %.4f radians (%.2f degrees)" theta1 (theta1 * 180.0f / 3.14159265f)
printfn "  Phi (azimuth): %.4f radians (%.2f degrees)" phi1 (phi1 * 180.0f / 3.14159265f)
printfn "  Distance: %.2e meters (%.6f AU)" distance1 (distance1 / 149597870700.0f)
printfn "  Height above horizon: %.2f degrees" ((90.0f - theta1 * 180.0f / 3.14159265f))
printfn ""

// Test 2: New York City on December 21, 2024 at noon local time (EST = UTC-5) (winter solstice)
printfn "Test 2: New York City - December 21, 2024, 12:00 EST (17:00 UTC, Winter Solstice)"
let longitude2 = -74.006f  // degrees west
let latitude2 = 40.7128f   // degrees north
let year2 = 2024
// December 21 is day 356 of 2024 (leap year), at 17:00:00 UTC
let secondsSinceStartOfYear2 = float32 (355 * 86400 + 17 * 3600) // 355 full days + 17 hours

let (theta2, phi2, distance2) = SunPosition.compute longitude2 latitude2 secondsSinceStartOfYear2 year2

printfn "  Theta (from zenith): %.4f radians (%.2f degrees)" theta2 (theta2 * 180.0f / 3.14159265f)
printfn "  Phi (azimuth): %.4f radians (%.2f degrees)" phi2 (phi2 * 180.0f / 3.14159265f)
printfn "  Distance: %.2e meters (%.6f AU)" distance2 (distance2 / 149597870700.0f)
printfn "  Height above horizon: %.2f degrees" ((90.0f - theta2 * 180.0f / 3.14159265f))
printfn ""

// Test 3: Equator on March 20, 2024 at noon UTC (spring equinox)
printfn "Test 3: Equator (0°, 0°) - March 20, 2024, 12:00 UTC (Spring Equinox)"
let longitude3 = 0.0f
let latitude3 = 0.0f
let year3 = 2024
// March 20 is day 80 of 2024 (leap year), at 12:00:00 UTC
let secondsSinceStartOfYear3 = float32 (79 * 86400 + 12 * 3600) // 79 full days + 12 hours

let (theta3, phi3, distance3) = SunPosition.compute longitude3 latitude3 secondsSinceStartOfYear3 year3

printfn "  Theta (from zenith): %.4f radians (%.2f degrees)" theta3 (theta3 * 180.0f / 3.14159265f)
printfn "  Phi (azimuth): %.4f radians (%.2f degrees)" phi3 (phi3 * 180.0f / 3.14159265f)
printfn "  Distance: %.2e meters (%.6f AU)" distance3 (distance3 / 149597870700.0f)
printfn "  Height above horizon: %.2f degrees" ((90.0f - theta3 * 180.0f / 3.14159265f))
printfn ""

// Test 4: Sydney, Australia on January 1, 2024 at noon local time (AEDT = UTC+11)
printfn "Test 4: Sydney, Australia - January 1, 2024, 12:00 AEDT (01:00 UTC)"
let longitude4 = 151.2093f  // degrees east
let latitude4 = -33.8688f   // degrees south
let year4 = 2024
// January 1 is day 1, at 01:00:00 UTC
let secondsSinceStartOfYear4 = float32 (0 * 86400 + 1 * 3600) // 0 full days + 1 hour

let (theta4, phi4, distance4) = SunPosition.compute longitude4 latitude4 secondsSinceStartOfYear4 year4

printfn "  Theta (from zenith): %.4f radians (%.2f degrees)" theta4 (theta4 * 180.0f / 3.14159265f)
printfn "  Phi (azimuth): %.4f radians (%.2f degrees)" phi4 (phi4 * 180.0f / 3.14159265f)
printfn "  Distance: %.2e meters (%.6f AU)" distance4 (distance4 / 149597870700.0f)
printfn "  Height above horizon: %.2f degrees" ((90.0f - theta4 * 180.0f / 3.14159265f))
printfn ""

printfn "All tests completed!"
