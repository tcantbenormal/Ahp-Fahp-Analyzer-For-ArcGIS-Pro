# Methodology: AHP & FAHP Mathematical Foundations

This document provides a concise overview of the mathematical methods implemented in the **AHP/FAHP Spatial Analyzer** add-in.

---

## 1. Analytic Hierarchy Process (AHP)

The Analytic Hierarchy Process, introduced by **Thomas L. Saaty (1980)**, is a structured technique for organizing and analyzing complex decisions based on mathematics and psychology.

### 1.1 Pairwise Comparison

Criteria are compared in pairs using a fundamental scale of **1 to 9**, where:

| Intensity | Definition |
|:---------:|------------|
| 1 | Equal importance |
| 3 | Moderate importance |
| 5 | Strong importance |
| 7 | Very strong importance |
| 9 | Extreme importance |
| 2, 4, 6, 8 | Intermediate values |

The comparisons form a reciprocal matrix **A** of size *n × n*, where:

```
A[i][j] = 1 / A[j][i]
A[i][i] = 1
```

### 1.2 Weight Calculation (Eigenvector Method)

The priority vector (weights) is computed using the **power method**:

1. Square the pairwise matrix: **A² = A × A**
2. Compute row sums and normalize to get the priority vector **w**
3. Repeat until convergence (the vector stabilizes)

### 1.3 Consistency Check

AHP validates the logical consistency of judgments:

- **Consistency Index (CI)**: `CI = (λ_max - n) / (n - 1)`
- **Consistency Ratio (CR)**: `CR = CI / RI`

Where **RI** is the Random Index for matrices of size *n* (Saaty, 1980). A **CR ≤ 0.10** is generally considered acceptable.

---

## 2. Fuzzy AHP (FAHP)

Fuzzy AHP extends classical AHP by incorporating **Triangular Fuzzy Numbers (TFNs)** to handle the inherent imprecision and vagueness in human judgment.

A TFN is represented as **(l, m, u)** where:
- **l** = lower bound (optimistic minimum)
- **m** = most likely value (modal)
- **u** = upper bound (pessimistic maximum)

The crisp Saaty scale values are converted to TFNs. For example, a crisp value of **3** becomes **(2, 3, 4)**.

### 2.1 Chang's Extent Analysis (1996)

Chang's method computes **synthetic extent values** for each criterion:

1. Calculate the fuzzy synthetic extent: **Sᵢ = Σⱼ Mᵢⱼ ⊗ [Σᵢ Σⱼ Mᵢⱼ]⁻¹**
2. Compare fuzzy numbers pairwise using the **degree of possibility**: `V(S₁ ≥ S₂)`
3. The weight of each criterion is: `d'(Aᵢ) = min V(Sᵢ ≥ Sₖ)` for all `k ≠ i`
4. Normalize to get the final weight vector

### 2.2 Geometric Mean Method (Buckley, 1985)

Buckley's method is an alternative that uses the **fuzzy geometric mean**:

1. Compute the geometric mean of each row of the fuzzy comparison matrix:
   `r̃ᵢ = (M̃ᵢ₁ ⊗ M̃ᵢ₂ ⊗ ... ⊗ M̃ᵢₙ)^(1/n)`
2. Calculate fuzzy weights: `w̃ᵢ = r̃ᵢ ⊗ (r̃₁ ⊕ r̃₂ ⊕ ... ⊕ r̃ₙ)⁻¹`
3. **Defuzzify** using the centroid: `wᵢ = (lᵢ + mᵢ + uᵢ) / 3`
4. Normalize the defuzzified weights

---

## 3. Monte Carlo Sensitivity Analysis

To assess the **robustness** of the ranking, the add-in performs Monte Carlo Sensitivity Analysis:

1. A **main criterion** is selected
2. Its weight is perturbed (increased/decreased) across *N* simulations
3. The remaining criteria weights are proportionally adjusted to maintain a sum of 1
4. For each simulation, the weighted overlay is recomputed
5. Results show how stable the final suitability ranking is under weight perturbation

---

## 4. Weighted Overlay (Spatial Integration)

Once weights are determined, the final suitability map is computed as:

```
Suitability = Σ (wᵢ × Rᵢ)
```

Where **wᵢ** is the weight and **Rᵢ** is the reclassified raster for criterion *i*.

This operation is performed by the Python-based **AhpSpatialEngine** using ArcPy's `WeightedSum` function with the Spatial Analyst extension.

---

## References

- Saaty, T. L. (1980). *The Analytic Hierarchy Process*. McGraw-Hill, New York.
- Chang, D. Y. (1996). Applications of the extent analysis method on fuzzy AHP. *European Journal of Operational Research*, 95(3), 649–655.
- Buckley, J. J. (1985). Fuzzy hierarchical analysis. *Fuzzy Sets and Systems*, 17(3), 233–247.
