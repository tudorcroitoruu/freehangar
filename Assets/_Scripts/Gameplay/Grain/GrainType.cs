/// <summary>
/// Harvested grains, pulses and seeds supported by the game's crop palette.
/// Numeric IDs are reserved for serialization and future shader selection.
/// Keep existing values stable and append new types with new IDs.
/// </summary>
public enum GrainType
{
    Corn = 0,          // Golden-yellow, rounded wedge kernels.
    Wheat = 1,         // Tan oval kernels with a lengthwise crease.
    Barley = 2,        // Pale-gold, tapered kernels.
    Oats = 3,          // Light beige, long and narrow kernels.
    Rye = 4,           // Gray-brown, slender kernels.
    Rice = 5,          // Ivory, elongated rounded grains (milled).
    Peas = 6,          // Green spheres.
    Soybeans = 7,      // Cream-yellow spheres with a dark hilum.
    SunflowerSeeds = 8, // Dark, flattened teardrops with pale stripes (in shell).
    Buckwheat = 9      // Brown, triangular kernels (in hull).
}
