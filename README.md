<h1 align="center">
  AHP / FAHP Spatial Analyzer for ArcGIS Pro
</h1>

<p align="center">
  <strong>A professional-grade Multi-Criteria Decision Analysis (MCDA) Add-in for ArcGIS Pro</strong>
</p>

<p align="center">
  <a href="#"><img src="https://img.shields.io/badge/ArcGIS%20Pro-3.5%2B-0079C1?style=for-the-badge&logo=esri&logoColor=white" alt="ArcGIS Pro 3.5+"></a>
  <a href="#"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8.0"></a>
  <a href="#"><img src="https://img.shields.io/badge/WPF-UI-68217A?style=for-the-badge&logo=windows&logoColor=white" alt="WPF"></a>
  <a href="#"><img src="https://img.shields.io/badge/Python-ArcPy-3776AB?style=for-the-badge&logo=python&logoColor=white" alt="Python"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-green?style=for-the-badge" alt="License MIT"></a>
</p>

<p align="center">
  Implements both the <strong>Analytic Hierarchy Process (AHP)</strong> and <strong>Fuzzy AHP (FAHP)</strong> — including Chang's Extent Analysis and Buckley's Geometric Mean — to generate spatial suitability maps through a streamlined WPF dockpane interface, complete with Monte Carlo Sensitivity Analysis.
</p>

---

## 📑 Table of Contents

- [Key Features](#-key-features)
- [Screenshots](#-screenshots)
- [Installation](#-installation)
- [Requirements](#-requirements)
- [Usage Workflow](#-usage-workflow)
- [Technical Architecture](#-technical-architecture)
- [Project Structure](#-project-structure)
- [Methodology](#-methodology)
- [How to Adapt for Your Own Study](#-how-to-adapt-for-your-own-study)
- [Documentation](#-documentation)
- [Citation](#-citation)
- [Contributing](#-contributing)
- [Author](#-author)
- [License](#-license)

---

## 🚀 Key Features

### AHP Weight Calculation
Computes priority weights using the **eigenvector (power) method** with automatic convergence detection. Supports pairwise comparison matrices from 3×3 up to arbitrary sizes.

### Fuzzy AHP (FAHP)
Goes beyond classical AHP by modeling uncertainty with **Triangular Fuzzy Numbers (TFNs)**:
- **Chang's Extent Analysis** — Synthetic extent values with degree-of-possibility comparisons
- **Geometric Mean (Buckley)** — Fuzzy geometric mean with centroid defuzzification

### Consistency Validation
Real-time calculation of the **Consistency Ratio (CR)** using Saaty's Random Index. Results are flagged if CR exceeds the 0.10 threshold — ensuring your judgments are logically sound.

### Reclassification Manager
A built-in WPF dialog for handling **unclassified rasters**. It automatically reads min/max statistics from your map layers and supports:
- Equal Interval
- Geometric Interval
- Defined Interval (fixed step size)
- Manual Interval (user-defined breakpoints)

### Monte Carlo Sensitivity Analysis
Assess ranking robustness by perturbing the weight of a selected main criterion across *N* simulations. Remaining weights are proportionally adjusted to maintain a sum of 1.

### Flexible Matrix Input
- **Manual Entry** — Fill in the pairwise comparison matrix interactively; reciprocals are auto-computed
- **CSV Import** — Load a pre-prepared comparison matrix from a CSV file

### Integrated Geoprocessing
Seamlessly triggers a bundled **Python Toolbox** (`.pyt`) to perform the weighted overlay using ArcPy's Spatial Analyst `WeightedSum`, and adds the resulting suitability raster directly to your active map.

---

## 📸 Screenshots

### General Interface
![General Interface](docs/images/General%20Interface.png)

### Pairwise Comparison Matrix
![Pairwise Comparison Matrix](docs/images/Pairwise%20Comparison%20Matrix.png)

### AHP Weights
![AHP Weights](docs/images/AHP%20Weights.png)

### FAHP — Chang's Extent Analysis
![FAHP Chang's Extent Method](docs/images/FAHP%20Chang's%20Extent%20Method.png)

### FAHP — Geometric Mean (Buckley)
![FAHP Geometric Mean](docs/images/FAHP%20Geometric%20Mean.png)

### Sensitivity Analysis
![Sensitivity Analysis](docs/images/Sensitivity%20Analysis.png)

### Suitability Map Output
![Suitability Map](docs/images/Suitability%20Map.png)

---

## 🛠️ Installation

### Quick Install (Pre-built Add-in)

1. Download **`AhpFahpAnalyzer.esriAddinX`** from the [repository root](AhpFahpAnalyzer.esriAddinX) or the [Releases](https://github.com/tcantbenormal/Ahp-Fahp-Analyzer-For-ArcGIS-Pro/releases) page
2. **Close ArcGIS Pro** if it is running
3. Double-click the `.esriAddinX` file — the ArcGIS Add-in Installation Utility will open
4. Click **Install Add-in**
5. Open ArcGIS Pro → navigate to the **MCDA Tools** tab → click **AHP Analysis**

### Build from Source

```bash
git clone https://github.com/tcantbenormal/Ahp-Fahp-Analyzer-For-ArcGIS-Pro.git
cd Ahp-Fahp-Analyzer-For-ArcGIS-Pro
```

Open `AhpFahpAnalyzer.sln` in Visual Studio 2022 and build the project. The `.esriAddinX` file will be generated in `bin\Debug\net8.0-windows\`.

---

## 📋 Requirements

| Requirement | Version |
|-------------|---------|
| ArcGIS Pro | 3.5 or later |
| .NET Runtime | 8.0 (Windows Desktop) |
| Visual Studio | 2022 (for building from source) |
| ArcGIS Extension | **Spatial Analyst** (required) |
| ArcGIS Pro SDK NuGet | `Esri.ArcGISPro.Extensions30` v3.5.0+ |

---

## 📖 Usage Workflow

The analysis follows a structured, step-by-step process:

### Step 1 — Select Criteria
Click **Refresh** to load raster layers from your active map. Add the desired layers to the **Selected Criteria** list (minimum 3). Use the ▲▼ buttons to reorder.

### Step 2 — Initialize Matrix
Click **Add Selected Criteria for AHP Analysis Matrix** to generate the pairwise comparison matrix.

### Step 3 — Enter Judgments
Fill in the upper triangle of the matrix with values from **1** (equal preference) to **9** (extreme preference). Reciprocal values are auto-calculated.
- Alternatively, import a pre-prepared matrix via **Import CSV**.

### Step 4 — Choose Method
Select your analysis method:
- **AHP** — Classical eigenvector approach
- **FAHP** — Fuzzy approach with a choice of *Chang's Extent Analysis* or *Geometric Mean (Buckley)*

### Step 5 — Calculate Weights
Click **Calculate Weights** to compute the priority vector:
- View the **Consistency Ratio (CR)** — must be ≤ 0.10
- View individual criterion weights

### Step 6 — Reclassify (if needed)
If your rasters are unclassified, open the **Reclassification Manager** to define scoring intervals and a target scale (e.g., 1–5 or 1–10).

### Step 7 — Create Suitability Map
Click **Create Suitability Map** to trigger the Python-based spatial engine. The weighted overlay result is automatically added to your active map.

### Step 8 — Sensitivity Analysis (Optional)
Select a **main criterion**, choose the number of simulations, and run the **Monte Carlo Sensitivity Analysis** to test ranking stability.

---

## 🏗️ Technical Architecture

The add-in uses a **hybrid C# / Python architecture**:

```
┌─────────────────────────────────────────────────┐
│                ArcGIS Pro Host                  │
│                                                 │
│  ┌──────────────────────────────────────────┐   │
│  │          WPF Dockpane (C# / XAML)        │   │
│  │  ┌──────────┐  ┌─────────────────────┐   │   │
│  │  │  UI Layer │  │  AhpMathEngine.cs   │   │   │
│  │  │  (Views)  │  │  - AHP Eigenvector  │   │   │
│  │  │           │  │  - FAHP Chang       │   │   │
│  │  │           │  │  - FAHP Buckley     │   │   │
│  │  │           │  │  - CR Validation    │   │   │
│  │  │           │  │  - Monte Carlo SA   │   │   │
│  │  └──────────┘  └─────────────────────┘   │   │
│  │          │                                │   │
│  │          ▼                                │   │
│  │  ┌──────────────────────────────────┐     │   │
│  │  │   AhpSpatialEngine.pyt (Python)  │     │   │
│  │  │   - Weighted Overlay (ArcPy)     │     │   │
│  │  │   - Raster Reclassification      │     │   │
│  │  │   - Map Layer Integration        │     │   │
│  │  └──────────────────────────────────┘     │   │
│  └──────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘
```

- **Frontend/Logic**: C# (.NET 8.0 / WPF) with MVVM pattern
- **Spatial Processing**: Python Toolbox (`.pyt`) using ArcPy
- **Communication**: ArcGIS Pro SDK's `Geoprocessing.ExecuteToolAsync()`

---

## 📁 Project Structure

```
AhpFahpAnalyzer/
├── AhpFahpAnalyzer.esriAddinX    # ← Pre-built add-in (install directly)
├── AhpFahpAnalyzer.sln           # Visual Studio solution
├── AhpFahpAnalyzer.csproj        # Project file (.NET 8.0)
├── Config.daml                   # ArcGIS Pro add-in manifest
│
├── AhpDockpane.xaml              # Main dockpane UI (WPF/XAML)
├── AhpDockpane.xaml.cs           # Code-behind
├── AhpDockpaneViewModel.cs       # ViewModel — all analysis logic
├── AhpMathEngine.cs              # AHP/FAHP math engine (weights, CR, SA)
│
├── ReclassManagerView.xaml       # Reclassification Manager dialog UI
├── ReclassManagerView.xaml.cs    # Code-behind
├── ReclassManagerViewModel.cs    # ViewModel — reclassification logic
│
├── AhpSpatialEngine.pyt          # Python Toolbox (ArcPy geoprocessing)
├── Module1.cs                    # ArcGIS Pro Module entry point
│
├── Images/                       # Light theme icons
├── DarkImages/                   # Dark theme icons
├── Properties/                   # Launch settings
│
├── docs/
│   ├── METHODOLOGY.md            # AHP & FAHP mathematical foundations
│   └── images/                   # Screenshots
│
├── .gitignore
├── .gitattributes
├── citation.cff                  # Academic citation metadata
└── LICENSE                       # MIT License
```

---

## 📐 Methodology

This add-in implements three core methods:

| Method | Approach | Key Advantage |
|--------|----------|---------------|
| **AHP** | Eigenvector (Power) Method | Simple, well-established, deterministic |
| **FAHP — Chang** | Extent Analysis with TFNs | Captures judgment uncertainty via fuzzy arithmetic |
| **FAHP — Buckley** | Geometric Mean with TFNs | Robust alternative; avoids zero-weight issues |

All methods produce a **normalized weight vector** that sums to 1, which is then used in the spatial weighted overlay.

👉 For the full mathematical formulation, see **[docs/METHODOLOGY.md](docs/METHODOLOGY.md)**.

---

## 🔄 How to Adapt for Your Own Study

This tool is designed to be **domain-agnostic**. It can be used for:
- 🏙️ Urban site suitability analysis
- 🌿 Environmental impact assessment
- 🏥 Healthcare facility location planning
- 🚰 Groundwater potential mapping
- 🛣️ Transportation route selection

### Steps to adapt:

1. **Prepare your criteria rasters** — Ensure all rasters are in the same coordinate system and resolution
2. **Decide on normalization** — Rasters can be pre-classified (1–5, 1–10) or fed as raw/unclassified data
3. **Load into ArcGIS Pro** — Add rasters to your active map's Table of Contents
4. **Run the add-in** — Follow the [Usage Workflow](#-usage-workflow) above
5. **Customize reclassification** — Use the Reclassification Manager if your rasters are unclassified

---

## 📚 Documentation

- **[Methodology](docs/METHODOLOGY.md)** — Full mathematical background on AHP, FAHP, and Monte Carlo SA
- **[License](LICENSE)** — MIT License

---

## 📝 Citation

If you use this software in your research, please cite it:

```
Ashfaq, T. (2026). AHP/FAHP Spatial Analyzer for ArcGIS Pro (Version 1.0.0) [Computer software].
https://github.com/tcantbenormal/Ahp-Fahp-Analyzer-For-ArcGIS-Pro
```

> GitHub also provides a **"Cite this repository"** button on the repository page using the included `citation.cff` file.

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Feel free to:

1. **Fork** the repository
2. Create a **feature branch** (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'Add amazing feature'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. Open a **Pull Request**

For bugs or suggestions, please [open an issue](https://github.com/tcantbenormal/Ahp-Fahp-Analyzer-For-ArcGIS-Pro/issues).

---

## 👨‍💻 Author

**Taimoor Ashfaq**  
IGIS, National University of Sciences and Technology (NUST)  
📧 [taimoorcantbenormal@gmail.com](mailto:taimoorcantbenormal@gmail.com)  
🔗 [GitHub](https://github.com/tcantbenormal)

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.
