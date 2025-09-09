# 🧰 Haptics Analysis Suite (WinForms, .NET 8)

A Windows desktop application to **load haptic acquisition data**, **plot it**, and **compute key haptic characteristics** (Fa, Fra, Frr, ΔF, Tm).  
This project was developed as a **technical assignment for Methode Electronics**, fulfilling Task 1 requirements (data loading, plotting, secondary plots, and metrics) and presented with a clean, user-friendly UI.

---

## 📝 Project Overview

The app provides:

- **CSV loader** (robust to header spacing) for acquisition files
- **Time-series plot**: Force & Voltage vs **Index**
- **Force–Distance plot** with **Voltage on a secondary (right) Y‑axis**
- **Automatic metrics**:
  - **Fa** (actuation force at electrical actuation)
  - **Fra** (return force at electrical de‑actuation)
  - **Frr** (return force near rest)
  - **Tm** (mechanical travel, total travel)
  - **ΔF = Fa − Fra** (tactile effect)
  - Voltage **threshold** computed automatically from medians
- **Markers** on plots for Fa/Fra/Frr (with values in legend)

---

## 🛠️ Tech Stack

- **C# / .NET 8**
- **Windows Forms**
- **ScottPlot.WinForms** `5.0.56` (interactive charts)
- **CsvHelper** `33.1.0` (CSV parsing)
- **xUnit** + **coverlet.collector** (tests & coverage)

---

## 📂 Solution Structure

```
HapticsAnalysisSuite/
├── HapticsAnalysisSuite.sln
├── Haptics.Core/
│   ├── Haptics.Core.csproj
│   ├── Models/
│   │   ├── DataPoint.cs              # index, force (N), voltage (V), linear (mm)
│   │   └── HapticMetrics.cs          # Fa, Fra, Frr, Tm, ΔF, medians, threshold
│   └── Services/
│       ├── DataLoader.cs             # robust CSV loader
│       └── HapticsAnalyzer.cs        # metrics computation
├── Haptics.UI/
│   ├── Haptics.UI.csproj
│   ├── Program.cs                    # App entry
│   ├── MainForm.cs                   # WinForms desktop app (UI, plots, markers, About tab)
│   └── data/
│       └── TaskData 1.csv            # sample dataset (copied to output)
└── Haptics.Tests/
    ├── Haptics.Tests.csproj
    ├── MetricsTests.cs               # unit tests and tolerances
    └── data/
        └── TaskData 1.csv            # sample dataset (copied to output)
```

---

## ▶️ How to Run (Windows)

### Requirements
- **Windows 10/11**
- **.NET SDK 8.0+**
- (Optional) **Visual Studio 2022 17.8+** with .NET desktop workload

### Run with Visual Studio
1. Open `HapticsAnalysisSuite.sln`
2. Set **Haptics.UI** as the startup project
3. Press **F5** (Debug) or **Ctrl+F5** (Run)

### Run from CLI
```bash
# in the solution folder
dotnet build
dotnet run --project Haptics.UI
```

> A sample file is bundled at `Haptics.UI/data/TaskData 1.csv` (copied to output on build).

---

## 💡 Using the App

1. **Load CSV…** → pick your acquisition file
2. Click **Calculate** →
   - The **Time** tab renders **Force & Voltage vs Index**
   - The **Force–Distance** tab renders **Force vs Linear (mm)** with **Voltage** on a **right Y‑axis**
3. **Metrics** panel shows: Fa, Fra, Frr, Tm, ΔF, and the voltage medians & threshold

**Data columns expected:**
- `Index`, `Force (N)`, `Voltage (V)`, `Linear (mm)`
- Optional: `Date Time` (kept but not used by analysis)

---

## 📐 How Metrics Are Computed

Implemented in `HapticsAnalyzer`:

- **Mechanical travel Tm** = `max(Linear) − min(Linear)`  
- Split the sequence at the **maximum Linear** into **press** (downstroke) and **release** (upstroke).
- **Voltage levels** are estimated automatically:
  - Compute median voltage near start (top X% of travel) → **High**
  - Compute median voltage near bottom (bottom X% of travel) → **Low**
  - **Threshold** = midpoint between **High** and **Low** (Options: `TravelPercentForVoltageMedians`, default **5%**)
- **Fa**: first **press** sample where `Voltage < Threshold`
- **Fra**: first **release** sample where `Voltage > Threshold`
- **Frr**: median force in a **window** near the end after return, constrained to be within **±ReturnWindowMm** of the starting position (`FrrWindowSamples` default **50**, `ReturnWindowMm` default **0.02 mm**)
- **ΔF** = `Fa − Fra`

All values are surfaced via `HapticMetrics` and used to place **marker pins** (with labels) on the plots.

---

## 🧪 Tests

```bash
dotnet test
```

The test suite (`Haptics.Tests`) validates that:
- CSV loads successfully
- Metrics are within **reasonable tolerances** on the sample dataset:
  - Fa, Fra, **Tm ≈ 2.286 mm**, **Frr ≈ −0.106 N**, **ΔF ≈ 1.304 N**
  - Voltage medians (**High ≈ 8.412 V**, **Low ≈ 1.976 V**) and the chosen **Threshold** (midpoint)

> Coverage collection is enabled via `coverlet.collector`.

---

## 🧩 Design Decisions

The following design choices were made to ensure robustness, clarity, and alignment with the assignment goals:

- **Project structure**:
  - Separated into **Core**, **UI**, and **Tests** for maintainability and clean layering.
- **ScottPlot v5** was chosen for plotting because it offers an interactive WinForms control, is actively maintained, and supports dual Y-axes (used for Force vs Distance + Voltage).
- **CsvHelper** was used instead of manual parsing to handle column name variations (e.g., extra spaces, case-insensitivity) and ensure resilience when loading data from different acquisition setups.
- **Metrics computation**:
  - Voltage medians (High/Low) are estimated from configurable travel windows instead of fixed values, making the algorithm robust to noise and offsets.
  - The force return point (Frr) is computed using a moving window near the end of travel to filter out spurious noise.
- **UI Layout**:
  - A tabbed interface keeps plots uncluttered and focused.
- **Testing**:
  - Metrics are validated against expected tolerances to demonstrate repeatability.
  - Coverage reporting included via `coverlet.collector`.

---

## ⚠️ Known Limitations

- Assumes availability of the columns: Index, Force (N), Voltage (V), Linear (mm) (case/spacing tolerant).
- Works best when travel increases then decreases once (single press-release cycle). Strongly non-monotonic travel can reduce accuracy.
- The actuation threshold is computed from voltage medians in small travel windows. While this reduces noise, extreme outliers or drift can still bias the result, so highly noisy datasets may require tuning of the window size/percent or pre-filtering.

---

## 🔮 Next Steps

- Add a column-mapping UI + basic header validation.
- Segment by voltage transitions/travel peaks and compute per-cycle metrics.
-  Optional pre-filters (detrend/smooth/outlier) + robust stats (trimmed medians/MAD) + quality flags.
- Export plots and metrics to **CSV / PNG / PDF**
- **Parameterize** thresholds and windows in the UI
- **Tooltips** and **crosshair** interaction on plots
- Package as a **single‑file** self‑contained build for easy distribution
- Add **CI** (build + tests) with GitHub Actions

---

## 👤 Author

- **Artur Gomes Barreto**  
  - [LinkedIn](https://www.linkedin.com/in/arturgomesbarreto/)  
  - [GitHub](https://github.com/ArturBarreto) 
  - [Email](mailto:artur.gomes.barreto@gmail.com)  
  - [WhatsApp](https://api.whatsapp.com/send?phone=35677562008)  

