// Sun Position Calculation for Earth
// Based on https://www.aa.quae.nl/en/reken/zonpositie.html by Dr Louis Strous
// Ported from: https://github.com/aardvark-platform/aardvark.algodat/blob/8930ed876b4bc36fc5feeaff4d0034723b01c8c1/src/Aardvark.Physics.Sky/SunPosition.cs#L54
// Accuracy: ~1°

module SunPosition

/// <summary>
/// Computes the spherical coordinates (theta, phi) of the sun and its distance in meters.
/// Based on https://www.aa.quae.nl/en/reken/zonpositie.html by Dr Louis Strous
/// Accuracy: ~1°
/// </summary>
/// <param name="longitudeInDegrees">Longitude GPS coordinate in degrees east</param>
/// <param name="latitudeInDegrees">Latitude GPS coordinate in degrees north</param>
/// <param name="secondsSinceStartOfYear">Seconds elapsed since start of the year (Jan 1, 00:00:00 UTC)</param>
/// <param name="year">Absolute year (e.g., 2024)</param>
/// <returns>Tuple of (theta, phi, distance) where theta and phi are in radians, distance in meters</returns>
let compute (longitudeInDegrees: float32) (latitudeInDegrees: float32) (secondsSinceStartOfYear: float32) (year: int) : float32 * float32 * float32 =
    
    // Constants
    let pi = 3.14159265358979323846f
    let piHalf = pi / 2.0f
    let piTimesTwo = pi * 2.0f
    let radiansPerDegree = pi / 180.0f
    let au = 149597870700.0f // Astronomical Unit in meters
    let j2000 = 2451545.0f // Julian day of 1st January 2000 12:00 (J2000)
    let julianCenturiesPerDay = 1.0f / 36525.0f
    
    // Convert year/seconds to Julian day
    // Calculate Julian day using standard formula for Gregorian calendar
    // JD for Jan 1 of given year at 00:00:00 UTC
    let year64 = int64 year
    let a = (14L - 1L) / 12L
    let y = year64 + 4800L - a
    let m = 1L + 12L * a - 3L
    let jdJan1 = 1L + (153L * m + 2L) / 5L + 365L * y + y / 4L - y / 100L + y / 400L - 32045L
    let jdJan1Float = float32 jdJan1 - 0.5f // Julian day starts at noon, so Jan 1 00:00 is JD - 0.5
    
    // Add the elapsed time
    let daysSinceStartOfYear = secondsSinceStartOfYear / 86400.0f
    let jd = jdJan1Float + daysSinceStartOfYear
    
    // Earth orbital parameters
    let earthEccentricity = 0.01671f
    let earthPerihelion = 102.9373f // degrees
    let earthMeanAnomalyAtJ2000 = 357.5291f * radiansPerDegree
    let earthMeanMotion = 0.985608f * radiansPerDegree // degrees per day
    let earthSemiLatusRectum = 0.99972f // a(1 - e²) in AU
    
    // Earth anomaly approximation coefficients (degrees)
    let earthAnomalyCoeffs = [| 1.9148f; 0.0200f; 0.0003f |]
    
    // 1. Calculate Mean Anomaly
    let meanAnomaly = earthMeanAnomalyAtJ2000 + earthMeanMotion * (jd - j2000)
    
    // 2. Approximate True Anomaly (Equation of Center)
    let mutable mi = meanAnomaly
    let mutable equationOfCenter = 0.0f
    for c in earthAnomalyCoeffs do
        equationOfCenter <- equationOfCenter + c * sin(mi)
        mi <- mi + meanAnomaly
    let trueAnomaly = meanAnomaly + equationOfCenter * radiansPerDegree
    
    // 3. Calculate ecliptic longitude (lambda)
    let lambda = trueAnomaly + (earthPerihelion + 180.0f) * radiansPerDegree
    
    // 4. Calculate Earth's obliquity (epsilon) using Astronomical Almanac 2010 approximation
    let t = (jd - j2000) * julianCenturiesPerDay
    let t2 = t * t
    let t3 = t2 * t
    let t4 = t2 * t2
    let t5 = t3 * t2
    let epsilonJ2000Arcsec = 23.0f * 3600.0f + 26.0f * 60.0f + 21.406f
    let epsilonArcsec = epsilonJ2000Arcsec - 46.836769f * t - 0.0001831f * t2 + 0.00200340f * t3 - 5.76e-7f * t4 - 4.34e-8f * t5
    let epsilon = epsilonArcsec / 3600.0f * radiansPerDegree
    
    // 5. Transform geocentric ecliptic to equatorial coordinates
    // Declination (delta)
    let beta = 0.0f // For the sun, beta (ecliptic latitude) is approximately 0
    let sinBeta = sin(beta)
    let cosBeta = cos(beta)
    let sinLambda = sin(lambda)
    let cosLambda = cos(lambda)
    let sinEpsilon = sin(epsilon)
    let cosEpsilon = cos(epsilon)
    
    let delta = asin(sinBeta * cosEpsilon + cosBeta * sinEpsilon * sinLambda)
    
    // Right ascension (alpha)
    let alpha = atan2 (sinLambda * cosEpsilon - tan(beta) * sinEpsilon) cosLambda
    
    // 6. Calculate sidereal time
    let theta = piTimesTwo * (0.7790572732640f + 1.002737811911354f * (jd - j2000))
    
    // 7. Convert to observer coordinates
    let phi = latitudeInDegrees * radiansPerDegree
    let lw = -longitudeInDegrees * radiansPerDegree // convert East to West
    
    let thetaLocal = theta - lw
    let hourAngle = thetaLocal - alpha
    
    // 8. Calculate height and azimuth
    let sinPhi = sin(phi)
    let cosPhi = cos(phi)
    let sinDelta = sin(delta)
    let cosDelta = cos(delta)
    let sinH = sin(hourAngle)
    let cosH = cos(hourAngle)
    
    let height = asin(sinPhi * sinDelta + cosPhi * cosDelta * cosH)
    let azimuth = atan2 sinH (cosH * sinPhi - tan(delta) * cosPhi)
    
    // 9. Convert to spherical coordinates (theta, phi)
    let thetaSphere = piHalf - height
    let phiSphere = azimuth
    
    // 10. Calculate distance to the sun
    let distance = earthSemiLatusRectum / (1.0f + earthEccentricity * cos(trueAnomaly)) * au
    
    (thetaSphere, phiSphere, distance)


// Example usage
let example () =
    // Example: Calculate sun position for Vienna, Austria on June 21, 2024 at noon UTC
    let longitude = 16.3738f  // degrees east
    let latitude = 48.2082f   // degrees north
    let year = 2024
    // June 21 is day 173 of 2024 (leap year), at 12:00:00 UTC
    let secondsSinceStartOfYear = float32 (172 * 86400 + 12 * 3600) // 172 full days + 12 hours
    
    let (theta, phi, distance) = compute longitude latitude secondsSinceStartOfYear year
    
    printfn "Sun Position:"
    printfn "  Theta (from zenith): %.4f radians (%.2f degrees)" theta (theta * 180.0f / 3.14159265f)
    printfn "  Phi (azimuth): %.4f radians (%.2f degrees)" phi (phi * 180.0f / 3.14159265f)
    printfn "  Distance: %.2e meters (%.6f AU)" distance (distance / 149597870700.0f)
