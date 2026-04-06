using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace AhpFahpAnalyzer
{
    public class ReclassRow : PropertyChangedBase
    {
        private double _minVal;
        public double MinVal { get => _minVal; set => SetProperty(ref _minVal, value); }

        private double _maxVal;
        public double MaxVal { get => _maxVal; set => SetProperty(ref _maxVal, value); }

        private int _newVal;
        public int NewVal { get => _newVal; set => SetProperty(ref _newVal, value); }
    }

    public class RasterReclassDefinition : PropertyChangedBase
    {
        public string RasterName { get; set; }
        public ObservableCollection<ReclassRow> Intervals { get; set; } = new ObservableCollection<ReclassRow>();

        private string _reclassMethod = "Equal Interval";
        public string ReclassMethod
        {
            get => _reclassMethod;
            set
            {
                SetProperty(ref _reclassMethod, value);
                NotifyPropertyChanged(nameof(IsDefinedInterval));
            }
        }

        /// <summary>
        /// True when "Defined Interval" is selected — used to enable/disable the Interval Width input.
        /// </summary>
        public bool IsDefinedInterval => ReclassMethod == "Defined Interval";

        private double _rasterMin = double.NaN;
        public double RasterMin
        {
            get => _rasterMin;
            set => SetProperty(ref _rasterMin, value);
        }

        private double _rasterMax = double.NaN;
        public double RasterMax
        {
            get => _rasterMax;
            set => SetProperty(ref _rasterMax, value);
        }

        private bool _statsLoaded = false;
        public bool StatsLoaded
        {
            get => _statsLoaded;
            set => SetProperty(ref _statsLoaded, value);
        }

        private string _statsStatusText = "Loading statistics...";
        public string StatsStatusText
        {
            get => _statsStatusText;
            set => SetProperty(ref _statsStatusText, value);
        }

        private int _numberOfClasses = 5;
        public int NumberOfClasses
        {
            get => _numberOfClasses;
            set => SetProperty(ref _numberOfClasses, value);
        }

        private double _definedIntervalWidth = 10;
        public double DefinedIntervalWidth
        {
            get => _definedIntervalWidth;
            set => SetProperty(ref _definedIntervalWidth, value);
        }
    }

    public class ReclassManagerViewModel : PropertyChangedBase
    {
        public ReclassManagerViewModel(ObservableCollection<string> rasters, string targetScale)
        {
            Rasters = new ObservableCollection<RasterReclassDefinition>();
            foreach (var r in rasters)
            {
                if (r != "- Goal")
                {
                    var def = new RasterReclassDefinition { RasterName = r };

                    // Determine default number of classes from target scale
                    int maxScale = ParseMaxScale(targetScale);
                    def.NumberOfClasses = maxScale;

                    Rasters.Add(def);
                }
            }

            ReclassMethods = new ObservableCollection<string> 
            { 
                "Manual Interval", 
                "Equal Interval", 
                "Geometric Interval", 
                "Defined Interval" 
            };
            TargetScale = targetScale;
        }

        public string TargetScale { get; }
        public ObservableCollection<string> ReclassMethods { get; }
        public ObservableCollection<RasterReclassDefinition> Rasters { get; }

        private RasterReclassDefinition _selectedRaster;
        public RasterReclassDefinition SelectedRaster
        {
            get => _selectedRaster;
            set
            {
                SetProperty(ref _selectedRaster, value);
                if (value != null && !value.StatsLoaded)
                {
                    // Load raster statistics asynchronously and then generate intervals
                    LoadRasterStatisticsAndGenerateIntervals(value);
                }
            }
        }

        private string _outputDirectory;
        public string OutputDirectory
        {
            get => _outputDirectory;
            set => SetProperty(ref _outputDirectory, value);
        }

        // Commands
        public ICommand BrowseOutputDirectoryCommand => new RelayCommand(() =>
        {
            ArcGIS.Desktop.Catalog.OpenItemDialog dlg = new ArcGIS.Desktop.Catalog.OpenItemDialog
            {
                Title = "Select Output Directory for Reclassified Rasters",
                MultiSelect = false,
                Filter = ArcGIS.Desktop.Catalog.ItemFilters.Folders
            };

            if (dlg.ShowDialog() == true)
            {
                var item = dlg.Items.FirstOrDefault();
                if (item != null)
                {
                    OutputDirectory = item.Path;
                }
            }
        });

        public ICommand RegenerateIntervalsCommand => new RelayCommand(() =>
        {
            if (SelectedRaster != null && SelectedRaster.StatsLoaded)
            {
                GenerateIntervals(SelectedRaster);
            }
        });

        /// <summary>
        /// Loads raster min/max statistics from the active map layer, then generates default intervals.
        /// Uses a two-phase approach: first reads CIM data on MCT, then calls GP tool on the main thread
        /// to avoid async deadlocks.
        /// </summary>
        private async void LoadRasterStatisticsAndGenerateIntervals(RasterReclassDefinition def)
        {
            try
            {
                def.StatsStatusText = "Loading raster statistics...";

                // Phase 1: Try to read from CIM colorizer on the MCT
                var cimResult = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                        return (valid: false, min: 0.0, max: 0.0, layerFound: false, dataSource: "", msg: "No active map.");

                    // Find the raster layer by name — search all layer types, not just RasterLayer
                    var layer = map.GetLayersAsFlattenedList()
                        .FirstOrDefault(l => l.Name == def.RasterName);

                    if (layer == null)
                        return (valid: false, min: 0.0, max: 0.0, layerFound: false, dataSource: "",
                            msg: $"Layer '{def.RasterName}' not found in map.");

                    // Get the data source path for fallback GP tool usage
                    string ds = "";
                    try
                    {
                        var uri = (layer as RasterLayer)?.GetPath();
                        if (uri != null)
                        {
                            ds = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                        }
                    }
                    catch { }

                    // If it's not a RasterLayer, try using the layer name as-is (works for GP tools)
                    if (string.IsNullOrEmpty(ds))
                        ds = def.RasterName;

                    // Try CIM colorizer approach — works for most single-band rasters with stretch rendering
                    if (layer is RasterLayer rasterLayer)
                    {
                        try
                        {
                            var cimDef = rasterLayer.GetDefinition() as CIMRasterLayer;
                            var colorizer = cimDef?.Colorizer;

                            // Strategy A: Stretch colorizer (most common for continuous rasters)
                            if (colorizer is CIMRasterStretchColorizer stretchCol && stretchCol.StretchStats != null)
                            {
                                double sMin = stretchCol.StretchStats.min ?? double.NaN;
                                double sMax = stretchCol.StretchStats.max ?? double.NaN;
                                if (!double.IsNaN(sMin) && !double.IsNaN(sMax) && sMax > sMin)
                                    return (valid: true, min: sMin, max: sMax, layerFound: true, dataSource: ds, msg: "OK");
                            }

                            // Strategy B: Classify colorizer — read the breaks
                            if (colorizer is CIMRasterClassifyColorizer classifyCol && classifyCol.ClassBreaks != null)
                            {
                                var breaks = classifyCol.ClassBreaks;
                                if (breaks.Length > 0)
                                {
                                    double cMin = breaks.Min(b => b.UpperBound);
                                    double cMax = breaks.Max(b => b.UpperBound);
                                    // The lowest break's lower bound isn't always stored; use the min break as approximation
                                    return (valid: true, min: cMin, max: cMax, layerFound: true, dataSource: ds, msg: "OK (from classify breaks)");
                                }
                            }
                        }
                        catch { /* CIM read failed, will fall through to GP tool */ }
                    }

                    return (valid: false, min: 0.0, max: 0.0, layerFound: true, dataSource: ds, msg: "CIM stats not available.");
                });

                if (cimResult.valid)
                {
                    ApplyStats(def, cimResult.min, cimResult.max, cimResult.msg);
                    return;
                }

                if (!cimResult.layerFound)
                {
                    def.StatsStatusText = $"⚠ {cimResult.msg}";
                    def.StatsLoaded = true;
                    GenerateFallbackRows(def);
                    return;
                }

                // Phase 2: Fall back to GetRasterProperties GP tool (runs outside QueuedTask to avoid deadlock)
                try
                {
                    def.StatsStatusText = "Reading statistics via GP tool...";
                    string layerRef = cimResult.dataSource;

                    var minArgs = Geoprocessing.MakeValueArray(layerRef, "MINIMUM");
                    var minResult = await Geoprocessing.ExecuteToolAsync("management.GetRasterProperties", minArgs,
                        null, null, null, GPExecuteToolFlags.None);

                    var maxArgs = Geoprocessing.MakeValueArray(layerRef, "MAXIMUM");
                    var maxResult = await Geoprocessing.ExecuteToolAsync("management.GetRasterProperties", maxArgs,
                        null, null, null, GPExecuteToolFlags.None);

                    if (!minResult.IsFailed && !maxResult.IsFailed)
                    {
                        double gpMin = Convert.ToDouble(minResult.ReturnValue);
                        double gpMax = Convert.ToDouble(maxResult.ReturnValue);
                        ApplyStats(def, gpMin, gpMax, "OK (from GP tool)");
                        return;
                    }
                }
                catch { /* Fall through */ }

                def.StatsStatusText = "⚠ Could not read statistics. Enter values manually.";
                def.StatsLoaded = true;
                GenerateFallbackRows(def);
            }
            catch (Exception ex)
            {
                def.StatsStatusText = $"⚠ Error: {ex.Message}";
                def.StatsLoaded = true;
                GenerateFallbackRows(def);
            }
        }

        /// <summary>
        /// Applies loaded statistics to the raster definition and generates intervals.
        /// </summary>
        private void ApplyStats(RasterReclassDefinition def, double min, double max, string msg)
        {
            def.RasterMin = min;
            def.RasterMax = max;
            def.StatsLoaded = true;
            def.StatsStatusText = $"Min: {min:F2} | Max: {max:F2}";
            if (def.Intervals.Count == 0)
            {
                GenerateIntervals(def);
            }
        }

        /// <summary>
        /// Generates interval rows based on the selected reclassification method and raster statistics.
        /// </summary>
        private void GenerateIntervals(RasterReclassDefinition def)
        {
            def.Intervals.Clear();
            int numClasses = def.NumberOfClasses;
            double min = def.RasterMin;
            double max = def.RasterMax;

            if (double.IsNaN(min) || double.IsNaN(max) || Math.Abs(max - min) < 1e-10)
            {
                GenerateFallbackRows(def);
                return;
            }

            switch (def.ReclassMethod)
            {
                case "Equal Interval":
                    GenerateEqualIntervals(def, min, max, numClasses);
                    break;

                case "Geometric Interval":
                    GenerateGeometricIntervals(def, min, max, numClasses);
                    break;

                case "Defined Interval":
                    GenerateDefinedIntervals(def, min, max, def.DefinedIntervalWidth);
                    break;

                case "Manual Interval":
                default:
                    // For manual, still pre-fill with equal intervals as a starting point
                    GenerateEqualIntervals(def, min, max, numClasses);
                    break;
            }
        }

        /// <summary>
        /// Divides [min, max] into N equal-width bins.
        /// </summary>
        private void GenerateEqualIntervals(RasterReclassDefinition def, double min, double max, int numClasses)
        {
            double width = (max - min) / numClasses;
            for (int i = 0; i < numClasses; i++)
            {
                double lo = Math.Round(min + i * width, 4);
                double hi = Math.Round(min + (i + 1) * width, 4);
                // Ensure last bin captures the exact max
                if (i == numClasses - 1) hi = max;

                def.Intervals.Add(new ReclassRow
                {
                    MinVal = lo,
                    MaxVal = hi,
                    NewVal = i + 1
                });
            }
        }

        /// <summary>
        /// Divides [min, max] using geometric progression for the bin boundaries.
        /// Handles min <= 0 by shifting the range.
        /// </summary>
        private void GenerateGeometricIntervals(RasterReclassDefinition def, double min, double max, int numClasses)
        {
            // Geometric intervals require positive values; shift if necessary
            double offset = 0;
            if (min <= 0)
            {
                offset = Math.Abs(min) + 1;
                min += offset;
                max += offset;
            }

            double ratio = Math.Pow(max / min, 1.0 / numClasses);
            for (int i = 0; i < numClasses; i++)
            {
                double lo = min * Math.Pow(ratio, i) - offset;
                double hi = min * Math.Pow(ratio, i + 1) - offset;
                if (i == numClasses - 1) hi = max - offset;

                def.Intervals.Add(new ReclassRow
                {
                    MinVal = Math.Round(lo, 4),
                    MaxVal = Math.Round(hi, 4),
                    NewVal = i + 1
                });
            }
        }

        /// <summary>
        /// Creates bins of a fixed user-defined width, starting from min.
        /// </summary>
        private void GenerateDefinedIntervals(RasterReclassDefinition def, double min, double max, double intervalWidth)
        {
            if (intervalWidth <= 0) intervalWidth = (max - min) / 5;

            int score = 1;
            double cursor = min;
            while (cursor < max)
            {
                double hi = Math.Min(cursor + intervalWidth, max);
                def.Intervals.Add(new ReclassRow
                {
                    MinVal = Math.Round(cursor, 4),
                    MaxVal = Math.Round(hi, 4),
                    NewVal = score
                });
                cursor = hi;
                score++;

                // Safety: prevent infinite loop
                if (score > 100) break;
            }
        }

        /// <summary>
        /// Fallback when raster statistics are unavailable: create empty rows for manual entry.
        /// </summary>
        private void GenerateFallbackRows(RasterReclassDefinition def)
        {
            def.Intervals.Clear();
            int maxScale = ParseMaxScale(TargetScale);
            for (int i = 1; i <= maxScale; i++)
            {
                def.Intervals.Add(new ReclassRow { MinVal = 0, MaxVal = 0, NewVal = i });
            }
        }

        private int ParseMaxScale(string targetScale)
        {
            if (targetScale == "1 to 3") return 3;
            if (targetScale == "1 to 5") return 5;
            if (targetScale == "1 to 7") return 7;
            return 9; // Default "1 to 9"
        }
    }
}
