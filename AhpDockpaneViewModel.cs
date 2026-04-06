using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace AhpFahpAnalyzer
{
    internal class AhpDockpaneViewModel : DockPane
    {
        private const string _dockPaneID = "AhpFahpAnalyzer_AhpDockpane";

        public AhpDockpaneViewModel()
        {
            AvailableRasters = new ObservableCollection<string>();
            
            SelectedCriteria = new ObservableCollection<string> { "- Goal" };

            NumSimulationsOptions = new ObservableCollection<int> { 100, 500, 1000, 5000 };
            SelectedNumSimulations = 1000;

            FahpApproaches = new ObservableCollection<string> { "Chang's Extent Analysis", "Geometric Mean" };
            SelectedFahpApproach = "Chang's Extent Analysis";

            TargetScales = new ObservableCollection<string> { "1 to 3", "1 to 5", "1 to 7", "1 to 9" };
            SelectedTargetScale = "1 to 9";

            IsAhpMethod = true;
            IsPreClassified = true;
            IsManualMatrixEntry = true;
            
            GetRastersFromMapAsync();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;
            pane.Activate();
        }

        #region Properties

        public ObservableCollection<string> AvailableRasters { get; }
        public ObservableCollection<string> SelectedCriteria { get; }
        public ObservableCollection<int> NumSimulationsOptions { get; }
        public ObservableCollection<string> FahpApproaches { get; }

        private string _selectedFahpApproach;
        public string SelectedFahpApproach
        {
            get => _selectedFahpApproach;
            set => SetProperty(ref _selectedFahpApproach, value);
        }
        
        public ObservableCollection<string> TargetScales { get; }
        
        private string _selectedTargetScale;
        public string SelectedTargetScale
        {
            get => _selectedTargetScale;
            set => SetProperty(ref _selectedTargetScale, value);
        }

        private bool _isPreClassified;
        public bool IsPreClassified
        {
            get => _isPreClassified;
            set 
            { 
                SetProperty(ref _isPreClassified, value);
                if (value) IsUnclassified = false;
            }
        }

        private bool _isUnclassified;
        public bool IsUnclassified
        {
            get => _isUnclassified;
            set 
            { 
                SetProperty(ref _isUnclassified, value);
                if (value) IsPreClassified = false;
            }
        }

        private bool _isManualMatrixEntry;
        public bool IsManualMatrixEntry
        {
            get => _isManualMatrixEntry;
            set 
            { 
                SetProperty(ref _isManualMatrixEntry, value);
                if (value) IsImportCsvMatrixEntry = false;
            }
        }

        private bool _isImportCsvMatrixEntry;
        public bool IsImportCsvMatrixEntry
        {
            get => _isImportCsvMatrixEntry;
            set 
            { 
                SetProperty(ref _isImportCsvMatrixEntry, value);
                if (value) IsManualMatrixEntry = false;
            }
        }

        private string _selectedAvailableRaster;
        public string SelectedAvailableRaster
        {
            get => _selectedAvailableRaster;
            set => SetProperty(ref _selectedAvailableRaster, value);
        }

        private string _selectedCriterion;
        public string SelectedCriterion
        {
            get => _selectedCriterion;
            set => SetProperty(ref _selectedCriterion, value);
        }

        private DataView _ahpMatrix;
        public DataView AhpMatrix
        {
            get => _ahpMatrix;
            set => SetProperty(ref _ahpMatrix, value);
        }
        
        private bool _isUpdatingMatrix = false;

        private bool _isAhpMethod;
        public bool IsAhpMethod
        {
            get => _isAhpMethod;
            set
            {
                SetProperty(ref _isAhpMethod, value);
                if (value) IsFahpMethod = false;
            }
        }

        private bool _isFahpMethod;
        public bool IsFahpMethod
        {
            get => _isFahpMethod;
            set
            {
                SetProperty(ref _isFahpMethod, value);
                if (value) IsAhpMethod = false;
            }
        }

        private string _consistencyRatioText;
        public string ConsistencyRatioText
        {
            get => _consistencyRatioText;
            set => SetProperty(ref _consistencyRatioText, value);
        }

        private string _selectedMainCriterion;
        public string SelectedMainCriterion
        {
            get => _selectedMainCriterion;
            set => SetProperty(ref _selectedMainCriterion, value);
        }

        private int _selectedNumSimulations;
        public int SelectedNumSimulations
        {
            get => _selectedNumSimulations;
            set => SetProperty(ref _selectedNumSimulations, value);
        }

        private string _geoprocessingStatus = "Geoprocessing status will be displayed here !!";
        public string GeoprocessingStatus
        {
            get => _geoprocessingStatus;
            set => SetProperty(ref _geoprocessingStatus, value);
        }

        /// <summary>
        /// Stores the last computed weights from CalculateWeightsCommand for use by 
        /// CreateSuitabilityMapCommand and PerformSACommand.
        /// </summary>
        private double[] _computedWeights;

        /// <summary>
        /// Stores the criteria names corresponding to the computed weights.
        /// </summary>
        private string[] _computedCriteriaNames;

        #endregion

        #region Commands

        public ICommand ShowAboutCommand => new RelayCommand(() =>
        {
            string aboutText =
                "AHP / FAHP Spatial Analysis Add-in\n" +
                "Version 1.0\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                "A Multi-Criteria Decision Analysis (MCDA) tool for ArcGIS Pro " +
                "that implements the Analytic Hierarchy Process (AHP) and " +
                "Fuzzy AHP (FAHP) methods for spatial suitability mapping.\n\n" +
                "Features:\n" +
                "  • AHP eigenvector-based weight calculation\n" +
                "  • FAHP (Chang's Extent & Geometric Mean)\n" +
                "  • Consistency Ratio validation\n" +
                "  • Reclassification Manager with raster statistics\n" +
                "  • Monte Carlo Sensitivity Analysis\n" +
                "  • Weighted overlay suitability mapping\n\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                "Author: Taimoor Ashfaq\n" +
                "Institution: IGIS, NUST\n" +
                "Contact: taimoorcantbenormal@gmail.com\n\n" +
                "For questions, bug reports, or collaboration\n" +
                "inquiries, feel free to reach out via email.";

            MessageBox.Show(aboutText, "About — AHP/FAHP Analyzer",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        });

        public ICommand OpenReclassManagerCommand => new RelayCommand(() =>
        {
            var actualCriteria = SelectedCriteria.Where(c => c != "- Goal").ToList();
            if (actualCriteria.Count == 0)
            {
                MessageBox.Show("Please select unclassified rasters via 'Available Rasters' first to open the manager.", "Notice");
                return;
            }

            var vm = new ReclassManagerViewModel(SelectedCriteria, SelectedTargetScale);
            
            if (ReclassificationVM != null)
            {
                vm.OutputDirectory = ReclassificationVM.OutputDirectory;
            }
            
            var view = new ReclassManagerView { DataContext = vm };
            
            if (view.ShowDialog() == true)
            {
                GeoprocessingStatus = "Reclassification parameters saved successfully.";
                ReclassificationVM = vm;
            }
        });

        public ReclassManagerViewModel ReclassificationVM { get; set; }

        public ICommand ImportMatrixCommand => new RelayCommand(() =>
        {
            ImportCsvMatrix();
        });

        private void ImportCsvMatrix()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "CSV Files (*.csv)|*.csv";
            
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var lines = System.IO.File.ReadAllLines(dlg.FileName).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                    var actualCriteria = SelectedCriteria.Where(c => c != "- Goal").ToList();
                    
                    if (actualCriteria.Count < 2)
                    {
                        MessageBox.Show("Please select at least 2 criteria (plus Goal) before importing a matrix.", "Warning");
                        return;
                    }
                    
                    int startIdx = 0;
                    if (lines.Count > 0)
                    {
                        var testCols = lines[0].Split(',');
                        if (testCols.Length > 0 && !double.TryParse(testCols[testCols.Length - 1].Trim(), out _))
                            startIdx = 1;
                    }

                    int expectedRows = startIdx == 1 ? actualCriteria.Count + 1 : actualCriteria.Count;
                    if (lines.Count != expectedRows && lines.Count < expectedRows)
                    {
                        MessageBox.Show($"CSV mismatch. You have {actualCriteria.Count} criteria selected, but CSV has {lines.Count - startIdx} valid data rows.", "Error");
                        return;
                    }

                    var table = new DataTable();
                    var critCol = new DataColumn("Criteria", typeof(string));
                    critCol.Caption = "Criteria";
                    table.Columns.Add(critCol);

                    for (int k = 0; k < actualCriteria.Count; k++)
                    {
                        var col = new DataColumn("C_" + k, typeof(double));
                        col.Caption = actualCriteria[k];
                        table.Columns.Add(col);
                    }
                    
                    for (int i = 0; i < actualCriteria.Count; i++)
                    {
                        var row = table.NewRow();
                        row["Criteria"] = actualCriteria[i];
                        
                        var cols = lines[i + startIdx].Split(',');
                        int colOffset = (cols.Length > actualCriteria.Count) ? 1 : 0;
                        
                        if ((cols.Length - colOffset) < actualCriteria.Count)
                        {
                            MessageBox.Show($"CSV mismatch on row {i+1}. Need {actualCriteria.Count} columns.", "Error");
                            return;
                        }

                        for (int j = 0; j < actualCriteria.Count; j++)
                        {
                            if (double.TryParse(cols[j + colOffset].Trim(), out double val))
                            {
                                row["C_" + j] = val;
                            }
                            else
                            {
                                row["C_" + j] = 0.0;
                            }
                        }
                        table.Rows.Add(row);
                    }

                    AhpMatrix = table.DefaultView;
                    _computedWeights = null; // Reset computed weights
                    GeoprocessingStatus = "CSV Matrix imported successfully.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing CSV: {ex.Message}", "Import Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        public ICommand RefreshRastersCommand => new RelayCommand(() => GetRastersFromMapAsync());

        private async void GetRastersFromMapAsync()
        {
            if (MapView.Active?.Map == null)
            {
                GeoprocessingStatus = "No active map found.";
                return;
            }

            try
            {
                var layerNames = await QueuedTask.Run(() =>
                {
                    return MapView.Active.Map.GetLayersAsFlattenedList()
                        .OfType<Layer>()
                        .Where(l => !(l is GroupLayer))
                        .Select(l => l.Name)
                        .ToList();
                });

                AvailableRasters.Clear();
                foreach (var name in layerNames)
                {
                    AvailableRasters.Add(name);
                }

                GeoprocessingStatus = $"Loaded {layerNames.Count} layers from active map.";
            }
            catch (Exception ex)
            {
                GeoprocessingStatus = $"Failed to load layers: {ex.Message}";
            }
        }

        public ICommand AddSingleRasterCommand => new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(SelectedAvailableRaster) && !SelectedCriteria.Contains(SelectedAvailableRaster))
            {
                SelectedCriteria.Add(SelectedAvailableRaster);
            }
        });

        public ICommand AddMultipleRastersCommand => new RelayCommand((object param) =>
        {
            if (param is IList selectedItems)
            {
                var itemsToAdd = selectedItems.Cast<string>().ToList();
                foreach (var item in itemsToAdd)
                {
                    if (!SelectedCriteria.Contains(item))
                    {
                        SelectedCriteria.Add(item);
                    }
                }
            }
            return Task.CompletedTask;
        });

        public ICommand RemoveCriterionCommand => new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(SelectedCriterion) && SelectedCriterion != "- Goal")
            {
                SelectedCriteria.Remove(SelectedCriterion);
            }
        });

        public ICommand MoveCriterionUpCommand => new RelayCommand(() =>
        {
            if (string.IsNullOrEmpty(SelectedCriterion) || SelectedCriterion == "- Goal") return;
            int idx = SelectedCriteria.IndexOf(SelectedCriterion);
            // Can't move above index 1 (index 0 is "- Goal")
            if (idx > 1)
            {
                SelectedCriteria.Move(idx, idx - 1);
            }
        });

        public ICommand MoveCriterionDownCommand => new RelayCommand(() =>
        {
            if (string.IsNullOrEmpty(SelectedCriterion) || SelectedCriterion == "- Goal") return;
            int idx = SelectedCriteria.IndexOf(SelectedCriterion);
            if (idx > 0 && idx < SelectedCriteria.Count - 1)
            {
                SelectedCriteria.Move(idx, idx + 1);
            }
        });

        public ICommand GenerateMatrixCommand => new RelayCommand(() =>
        {
            var actualCriteria = SelectedCriteria.Where(c => c != "- Goal").ToList();
            if (actualCriteria.Count < 2)
            {
                MessageBox.Show("Please select at least 2 criteria (plus Goal).", "Warning");
                return;
            }

            var table = new DataTable();
            var critCol = new DataColumn("Criteria", typeof(string));
            critCol.Caption = "Criteria";
            table.Columns.Add(critCol);

            for (int k = 0; k < actualCriteria.Count; k++)
            {
                var col = new DataColumn("C_" + k, typeof(double));
                col.Caption = actualCriteria[k];
                table.Columns.Add(col);
            }

            for (int i = 0; i < actualCriteria.Count; i++)
            {
                var row = table.NewRow();
                row["Criteria"] = actualCriteria[i];
                for (int j = 0; j < actualCriteria.Count; j++)
                {
                    row["C_" + j] = i == j ? 1.0 : 0.0;
                }
                table.Rows.Add(row);
            }

            table.ColumnChanged += (s, e) =>
            {
                if (_isUpdatingMatrix) return;
                
                try
                {
                    _isUpdatingMatrix = true;
                    if (e.Column.ColumnName == "Criteria" || e.Row[e.Column] == DBNull.Value) return;
                    
                    double val = Convert.ToDouble(e.Row[e.Column]);
                    if (val == 0) return;
                    
                    int rowIndex = table.Rows.IndexOf(e.Row);
                    int colIndex = table.Columns.IndexOf(e.Column);
                    int critIndex = colIndex - 1;
                    
                    if (rowIndex == critIndex && val != 1.0)
                    {
                        e.Row[e.Column] = 1.0;
                        return;
                    }
                    
                    if (rowIndex != critIndex)
                    {
                        if (critIndex >= 0 && critIndex < table.Rows.Count && rowIndex + 1 < table.Columns.Count)
                        {
                            table.Rows[critIndex][rowIndex + 1] = Math.Round(1.0 / val, 4);
                        }
                    }
                }
                finally
                {
                    _isUpdatingMatrix = false;
                }
            };

            AhpMatrix = table.DefaultView;
            _computedWeights = null;
            ConsistencyRatioText = string.Empty;
            GeoprocessingStatus = "Matrix generated. Please enter pairwise comparisons.";
        });

        public ICommand ClearMatrixCommand => new RelayCommand(() =>
        {
            AhpMatrix = null;
            ConsistencyRatioText = string.Empty;
            _computedWeights = null;
            _computedCriteriaNames = null;
            GeoprocessingStatus = "Matrix cleared.";
        });

        /// <summary>
        /// Calculates AHP or FAHP weights using the real mathematical engine.
        /// </summary>
        public ICommand CalculateWeightsCommand => new RelayCommand(() =>
        {
            if (AhpMatrix == null || AhpMatrix.Count == 0)
            {
                MessageBox.Show("Please generate and fill the pairwise comparison matrix first.", "Warning");
                return;
            }

            try
            {
                // Extract matrix values from the DataView
                var actualCriteria = SelectedCriteria.Where(c => c != "- Goal").ToList();
                int n = actualCriteria.Count;
                var matrixValues = ExtractMatrixFromDataView(n);
                _computedCriteriaNames = actualCriteria.ToArray();

                // Validate the matrix
                double[,] matrix = AhpMathEngine.ToMatrix(matrixValues);
                string validationError = AhpMathEngine.ValidateMatrix(matrix);
                if (validationError != null)
                {
                    MessageBox.Show($"Matrix validation failed:\n\n{validationError}\n\nPlease correct the matrix values.", 
                        "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var sb = new StringBuilder();

                if (IsAhpMethod)
                {
                    // --- AHP Calculation ---
                    var result = AhpMathEngine.ComputeAhpWeights(matrix, _computedCriteriaNames);
                    _computedWeights = result.Weights;

                    sb.AppendLine("═══ AHP RESULTS ═══");
                    sb.AppendLine();
                    sb.AppendLine($"λmax = {result.LambdaMax:F4}");
                    sb.AppendLine($"CI   = {result.ConsistencyIndex:F4}");
                    sb.AppendLine($"RI   = {result.RandomIndex:F4}");
                    sb.AppendLine($"CR   = {result.ConsistencyRatio:F4}");
                    sb.AppendLine();

                    if (result.IsConsistent)
                        sb.AppendLine("✓ CR < 0.10 → Matrix is CONSISTENT");
                    else
                        sb.AppendLine("✗ CR ≥ 0.10 → Matrix is INCONSISTENT! Revise judgments.");

                    sb.AppendLine();
                    sb.AppendLine("─── Priority Weights ───");
                    for (int i = 0; i < n; i++)
                    {
                        sb.AppendLine($"  {actualCriteria[i]}: {result.Weights[i]:F4} ({result.Weights[i] * 100:F1}%)");
                    }

                    GeoprocessingStatus = result.IsConsistent 
                        ? "AHP weights calculated successfully. Matrix is consistent." 
                        : "⚠ AHP weights calculated but matrix is INCONSISTENT (CR ≥ 0.10). Please revise.";
                }
                else
                {
                    // --- FAHP Calculation ---
                    FahpResult fahpResult;
                    if (SelectedFahpApproach == "Chang's Extent Analysis")
                        fahpResult = AhpMathEngine.ComputeFahpChangExtent(matrix, _computedCriteriaNames);
                    else
                        fahpResult = AhpMathEngine.ComputeFahpGeometricMean(matrix, _computedCriteriaNames);

                    _computedWeights = fahpResult.Weights;

                    // Also compute AHP CR for reference
                    var ahpRef = AhpMathEngine.ComputeAhpWeights(matrix, _computedCriteriaNames);

                    sb.AppendLine($"═══ FAHP RESULTS ({fahpResult.Approach}) ═══");
                    sb.AppendLine();
                    sb.AppendLine("─── Fuzzy Priority Weights ───");
                    for (int i = 0; i < n; i++)
                    {
                        sb.AppendLine($"  {actualCriteria[i]}: {fahpResult.Weights[i]:F4} ({fahpResult.Weights[i] * 100:F1}%)");
                    }
                    sb.AppendLine();
                    sb.AppendLine("─── AHP Consistency Check ───");
                    sb.AppendLine($"CR = {ahpRef.ConsistencyRatio:F4}");
                    if (ahpRef.IsConsistent)
                        sb.AppendLine("✓ Underlying crisp matrix is consistent.");
                    else
                        sb.AppendLine("✗ Underlying crisp matrix is INCONSISTENT!");

                    GeoprocessingStatus = "FAHP weights calculated successfully.";
                }

                ConsistencyRatioText = sb.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error computing weights:\n{ex.Message}", "Calculation Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                GeoprocessingStatus = "Error during weight calculation.";
            }
        });

        /// <summary>
        /// Creates the suitability map by calling the Python geoprocessing toolbox.
        /// Passes the computed weights (not raw matrix) to the Python tool.
        /// </summary>
        public ICommand CreateSuitabilityMapCommand => new RelayCommand(async () =>
        {
            if (AhpMatrix == null || AhpMatrix.Count == 0)
            {
                MessageBox.Show("Please generate and fill the matrix first.", "Warning");
                return;
            }

            if (_computedWeights == null || _computedWeights.Length == 0)
            {
                MessageBox.Show("Please click 'Calculate Weights' first before creating the suitability map.", "Warning");
                return;
            }

            try
            {
                GeoprocessingStatus = "Running spatial analysis...";

                var actualCriteria = SelectedCriteria.Where(c => c != "- Goal").ToList();
                int count = actualCriteria.Count;

                // Build the weights JSON
                string weightsJson = JsonSerializer.Serialize(_computedWeights);
                string method = IsAhpMethod ? "AHP" : "FAHP";
                string fahpApproach = IsFahpMethod ? SelectedFahpApproach : "None";
                string criteriaJson = JsonSerializer.Serialize(actualCriteria);
                
                string reclassJson = "None";
                string outDir = "None";
                if (IsUnclassified && ReclassificationVM != null)
                {
                    // Build reclassification definition JSON
                    var reclassData = new List<object>();
                    foreach (var rDef in ReclassificationVM.Rasters)
                    {
                        var intervals = rDef.Intervals.Select(iv => new
                        {
                            min = iv.MinVal,
                            max = iv.MaxVal,
                            newVal = iv.NewVal
                        }).ToList();

                        reclassData.Add(new
                        {
                            rasterName = rDef.RasterName,
                            method = rDef.ReclassMethod,
                            intervals = intervals
                        });
                    }
                    reclassJson = JsonSerializer.Serialize(reclassData);
                    outDir = ReclassificationVM.OutputDirectory ?? "None";
                }

                // Resolve Python toolbox path (bundled with the add-in)
                string addinAssemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string addinDir = System.IO.Path.GetDirectoryName(addinAssemblyPath);
                string toolboxPath = System.IO.Path.Combine(addinDir, "AhpSpatialEngine.pyt");

                string toolName = $@"{toolboxPath}\RunAHP";
                var args = Geoprocessing.MakeValueArray(
                    weightsJson,
                    criteriaJson, 
                    method, 
                    fahpApproach, 
                    reclassJson, 
                    outDir,
                    IsPreClassified ? "true" : "false"
                );

                var result = await Geoprocessing.ExecuteToolAsync(toolName, args);

                if (result.IsFailed)
                {
                    var errorMsg = string.Join(Environment.NewLine, result.Messages.Select(m => m.Text));
                    MessageBox.Show($"Geoprocessing Tool Failed.\n\n{errorMsg}", "Analysis Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    GeoprocessingStatus = "Geoprocessing Failed.";
                }
                else
                {
                    MessageBox.Show($"{method} Analysis complete and suitability raster generated!", "Analysis Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    GeoprocessingStatus = "Suitability Map Created Successfully!";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred:\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                GeoprocessingStatus = "Error during execution.";
            }
        });

        /// <summary>
        /// Performs Monte Carlo sensitivity analysis on the computed weights.
        /// </summary>
        public ICommand PerformSACommand => new RelayCommand(() =>
        {
            if (_computedWeights == null || _computedWeights.Length == 0)
            {
                MessageBox.Show("Please calculate weights first before performing sensitivity analysis.", "Warning");
                return;
            }

            if (string.IsNullOrEmpty(SelectedMainCriterion) || SelectedMainCriterion == "- Goal")
            {
                MessageBox.Show("Please select a main criterion (the one to perturb) from the Sensitivity Analysis panel.", "Warning");
                return;
            }

            try
            {
                var actualCriteria = SelectedCriteria.Where(c => c != "- Goal").ToList();
                int perturbedIndex = actualCriteria.IndexOf(SelectedMainCriterion);

                if (perturbedIndex < 0)
                {
                    MessageBox.Show($"Criterion '{SelectedMainCriterion}' not found in the selected criteria list.", "Error");
                    return;
                }

                GeoprocessingStatus = $"Running Monte Carlo SA ({SelectedNumSimulations} simulations)...";

                var saResult = AhpMathEngine.PerformSensitivityAnalysis(
                    _computedWeights,
                    actualCriteria.ToArray(),
                    perturbedIndex,
                    SelectedNumSimulations,
                    0.5  // ±50% perturbation range
                );

                // Build the results display
                var sb = new StringBuilder();
                sb.AppendLine("═══ SENSITIVITY ANALYSIS RESULTS ═══");
                sb.AppendLine();
                sb.AppendLine($"Perturbed Criterion: {saResult.PerturbedCriterion}");
                sb.AppendLine($"Simulations: {saResult.TotalSimulations:N0}");
                sb.AppendLine($"Perturbation Range: ±50%");
                sb.AppendLine();
                sb.AppendLine("─── Rank Reversal ───");
                sb.AppendLine($"  Reversals: {saResult.RankReversalCount} / {saResult.TotalSimulations}");
                sb.AppendLine($"  Percentage: {saResult.RankReversalPercentage:F1}%");
                sb.AppendLine();
                sb.AppendLine("─── Original Ranking ───");
                for (int i = 0; i < actualCriteria.Count; i++)
                {
                    sb.AppendLine($"  Rank {saResult.OriginalRanking[i]}: {actualCriteria[i]} (w = {saResult.OriginalWeights[i]:F4})");
                }
                sb.AppendLine();
                sb.AppendLine("─── Weight Ranges Observed ───");
                for (int i = 0; i < actualCriteria.Count; i++)
                {
                    sb.AppendLine($"  {actualCriteria[i]}: [{saResult.WeightRanges[i][0]:F4} – {saResult.WeightRanges[i][1]:F4}]");
                }

                if (saResult.RankReversalPercentage < 5)
                    sb.AppendLine("\n✓ Results are ROBUST — ranking is stable under perturbation.");
                else if (saResult.RankReversalPercentage < 20)
                    sb.AppendLine("\n⚠ Results are MODERATELY SENSITIVE — some rank reversals detected.");
                else
                    sb.AppendLine("\n✗ Results are HIGHLY SENSITIVE — significant rank reversals. Consider revising judgments.");

                ConsistencyRatioText = sb.ToString();
                GeoprocessingStatus = $"Sensitivity analysis complete. {saResult.RankReversalPercentage:F1}% rank reversals out of {SelectedNumSimulations} simulations.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during sensitivity analysis:\n{ex.Message}", "SA Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                GeoprocessingStatus = "Error during sensitivity analysis.";
            }
        });

        #endregion

        #region Helpers

        /// <summary>
        /// Extracts the pairwise comparison matrix values from the AhpMatrix DataView.
        /// </summary>
        private List<List<double>> ExtractMatrixFromDataView(int count)
        {
            var matrixValues = new List<List<double>>();
            foreach (DataRowView rowView in AhpMatrix)
            {
                DataRow row = rowView.Row;
                var rowList = new List<double>();
                for (int i = 0; i < count; i++)
                {
                    var cellValue = row["C_" + i];
                    double value = cellValue != DBNull.Value ? Convert.ToDouble(cellValue) : 0.0;
                    rowList.Add(value);
                }
                matrixValues.Add(rowList);
            }
            return matrixValues;
        }

        #endregion
    }

    internal class AhpDockpane_ShowButton : Button
    {
        protected override void OnClick()
        {
            AhpDockpaneViewModel.Show();
        }
    }
}
