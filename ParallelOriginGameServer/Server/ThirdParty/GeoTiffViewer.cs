using System.Runtime.CompilerServices;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;

namespace ParallelOriginGameServer.Server.ThirdParty;

/// <summary>
///     Represents a geo-tiff and contains several methods to manage it.
/// </summary>
public class GeoTiff
{
    /// <summary>
    ///     Constructs a geo tiff
    /// </summary>
    /// <param name="tiffPath">The path to the geo tiff</param>
    public GeoTiff(string tiffPath)
    {
        GeoTiffPath = tiffPath;

        if (!GdalBase.IsConfigured)
            GdalBase.ConfigureAll();

        Dataset = Gdal.Open(GeoTiffPath, Access.GA_ReadOnly);

        SingleValueCache = new byte[1];
        GeoTransform = new double[6];
        InverseGeoTransform = new double[6];
        Dataset.GetGeoTransform(GeoTransform);
        Gdal.InvGeoTransform(GeoTransform, InverseGeoTransform);
    }


    /// <summary>
    ///     The path to the geotiff
    /// </summary>
    public string GeoTiffPath { get; set; }

    /// <summary>
    ///     The dataset
    /// </summary>
    private Dataset Dataset { get; }

    /// <summary>
    ///     The geo transform used to convert pixels to geo coordinates
    /// </summary>
    private double[] GeoTransform { get; }

    /// <summary>
    ///     The geo transform used to convert geo coordinates into pixels
    /// </summary>
    private double[] InverseGeoTransform { get; }

    /// <summary>
    ///     A single cache to <see cref="GetPixelValue" /> operations
    /// </summary>
    private byte[] SingleValueCache { get; }

    // Deconstructor to dispose 
    ~GeoTiff()
    {
        Dataset.Dispose();
    }

    /// <summary>
    ///     Returns the pixel value of a certain geo-coordinate.
    /// </summary>
    /// <param name="latitude"></param>
    /// <param name="longitude"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetPixelValue(int band, double latitude, double longitude)
    {
        // Get raster, transform geo coordinates to pixel
        var bandRef = Dataset.GetRasterBand(band);
        Gdal.ApplyGeoTransform(InverseGeoTransform, longitude, latitude, out var pixelXf, out var pixelYf);
        var pixelX = (int)pixelXf;
        var pixelY = (int)pixelYf;

        // Acess raster and pick the one pixel we are searching for
        bandRef.ReadRaster(pixelX, pixelY, 1, 1, SingleValueCache, 1, 1, 0, 0);
        return SingleValueCache[0];
    }
}