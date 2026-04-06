# AHP / FAHP Spatial Analyzer for ArcGIS Pro

![ArcGIS Pro](https://img.shields.io/badge/ArcGIS%20Pro-3.5%2B-blue.svg)
![License](https://img.shields.io/badge/License-MIT-green.svg)

A professional-grade Multi-Criteria Decision Analysis (MCDA) Add-in for ArcGIS Pro. This tool implements both the **Analytic Hierarchy Process (AHP)** and the **Fuzzy Analytic Hierarchy Process (FAHP)** to generate spatial suitability maps through a streamlined WPF interface.

## 🚀 Key Features

*   **AHP Weight Calculation**: Computes priority weights using the eigenvector (power) method.
*   **Fuzzy AHP (FAHP)**: Implements both **Chang's Extent Analysis** and the **Geometric Mean (Buckley)** methods for handling uncertainty in decision-making.
*   **Consistency Validation**: Automatic calculation of the Consistency Ratio (CR) to validate pairwise comparison matrices.
*   **Reclassification Manager**: A built-in UI to handle unclassified rasters. It automatically fetches min/max statistics from your map layers and supports:
    *   Equal Interval
    *   Geometric Interval
    *   Defined Interval
    *   Manual Interval
*   **Monte Carlo Sensitivity Analysis**: Robustly test the stability of your rankings by perturbing weights across thousands of simulations.
*   **Integrated Geoprocessing**: Seamlessly triggers a Python-based spatial engine to perform weighted overlays and add the results directly to your active map.

## 🛠️ Installation

1.  Download the latest release (`.esriAddinX` file) from the [Releases](#) page.
2.  Ensure ArcGIS Pro is closed.
3.  Double-click the `AhpFahpAnalyzer.esriAddinX` file to install the Add-in.
4.  Open ArcGIS Pro, navigate to the **MCDA Tools** tab, and click **AHP Analysis**.

## 📖 How to Use

1.  **Select Criteria**: Choose rasters from your active map to include in the analysis.
2.  **Initialize Matrix**: Generate the pairwise comparison matrix.
3.  **Enter Judgments**: Fill in the matrix values (1-9) based on relative importance. The tool automatically computes recripocals.
4.  **Calculate Weights**: Click "Calculate Weights" to see the priority vector and consistency check.
5.  **Reclassify (Optional)**: If using unclassified data, use the Reclassification Manager to define your scoring intervals.
6.  **Run Analysis**: Click "Create Suitability Map" to generate your final weighted overlay raster.

## 🔧 Technical Details

*   **Framework**: .NET 8.0 (WPF)
*   **ArcGIS Pro SDK**: Targeted for 3.5.0 (Forward compatible with 3.6+)
*   **Backend**: Python (ArcPy) via a bundled Python Toolbox (`.pyt`)
*   **Dependencies**: Requires the **Spatial Analyst** extension.

## 👨‍💻 Author

**Taimoor Ashfaq**  
IGIS, National University of Sciences and Technology (NUST)  
📧 [taimoorcantbenormal@gmail.com](mailto:taimoorcantbenormal@gmail.com)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
