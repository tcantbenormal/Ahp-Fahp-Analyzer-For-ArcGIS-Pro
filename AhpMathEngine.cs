using System;
using System.Collections.Generic;
using System.Linq;

namespace AhpFahpAnalyzer
{
    /// <summary>
    /// Result container for AHP weight computation.
    /// </summary>
    public class AhpResult
    {
        public double[] Weights { get; set; }
        public double LambdaMax { get; set; }
        public double ConsistencyIndex { get; set; }
        public double RandomIndex { get; set; }
        public double ConsistencyRatio { get; set; }
        public bool IsConsistent => Weights.Length <= 2 || ConsistencyRatio < 0.10;
        public string[] CriteriaNames { get; set; }
    }

    /// <summary>
    /// Result container for FAHP weight computation.
    /// </summary>
    public class FahpResult
    {
        public double[] Weights { get; set; }
        public string Approach { get; set; }
        public string[] CriteriaNames { get; set; }
    }

    /// <summary>
    /// Sensitivity analysis result for a single simulation run.
    /// </summary>
    public class SensitivityResult
    {
        public int TotalSimulations { get; set; }
        public string PerturbedCriterion { get; set; }
        public double[] OriginalWeights { get; set; }
        public int[] OriginalRanking { get; set; }
        public int RankReversalCount { get; set; }
        public double RankReversalPercentage { get; set; }
        public double[][] WeightRanges { get; set; } // [criterionIndex][0=min, 1=max]
        public string[] CriteriaNames { get; set; }
    }

    /// <summary>
    /// Triangular Fuzzy Number (l, m, u).
    /// </summary>
    public struct TriangularFuzzyNumber
    {
        public double L; // Lower
        public double M; // Middle
        public double U; // Upper

        public TriangularFuzzyNumber(double l, double m, double u)
        {
            L = l; M = m; U = u;
        }

        public static TriangularFuzzyNumber operator +(TriangularFuzzyNumber a, TriangularFuzzyNumber b)
            => new TriangularFuzzyNumber(a.L + b.L, a.M + b.M, a.U + b.U);

        public static TriangularFuzzyNumber operator *(TriangularFuzzyNumber a, TriangularFuzzyNumber b)
            => new TriangularFuzzyNumber(a.L * b.L, a.M * b.M, a.U * b.U);

        public TriangularFuzzyNumber Reciprocal()
            => new TriangularFuzzyNumber(1.0 / U, 1.0 / M, 1.0 / L);

        public override string ToString() => $"({L:F3}, {M:F3}, {U:F3})";
    }

    /// <summary>
    /// Core mathematical engine for AHP, FAHP, and Sensitivity Analysis computations.
    /// All methods are pure C# with no external dependencies.
    /// </summary>
    public static class AhpMathEngine
    {
        // Saaty's Random Index table for matrices of size 1..15
        private static readonly double[] RandomIndexTable = 
        { 
            0.00, 0.00, 0.58, 0.90, 1.12, 1.24, 1.32, 1.41, 1.45, 1.49, 
            1.51, 1.48, 1.56, 1.57, 1.59 
        };

        // Conversion table: crisp AHP value -> Triangular Fuzzy Number
        // Based on standard fuzzy AHP scale (Buckley, 1985)
        private static readonly Dictionary<int, TriangularFuzzyNumber> CrispToFuzzyScale = new()
        {
            { 1, new TriangularFuzzyNumber(1, 1, 1) },
            { 2, new TriangularFuzzyNumber(1, 2, 3) },
            { 3, new TriangularFuzzyNumber(2, 3, 4) },
            { 4, new TriangularFuzzyNumber(3, 4, 5) },
            { 5, new TriangularFuzzyNumber(4, 5, 6) },
            { 6, new TriangularFuzzyNumber(5, 6, 7) },
            { 7, new TriangularFuzzyNumber(6, 7, 8) },
            { 8, new TriangularFuzzyNumber(7, 8, 9) },
            { 9, new TriangularFuzzyNumber(8, 9, 9) },
        };

        #region AHP Methods

        /// <summary>
        /// Computes AHP weights using the eigenvector (power) method and consistency ratio.
        /// </summary>
        /// <param name="matrix">NxN pairwise comparison matrix (row-major).</param>
        /// <param name="criteriaNames">Names of the criteria corresponding to rows/columns.</param>
        /// <returns>AhpResult containing weights, lambda_max, CI, RI, CR.</returns>
        public static AhpResult ComputeAhpWeights(double[,] matrix, string[] criteriaNames)
        {
            int n = matrix.GetLength(0);

            // Step 1: Compute priority vector using the power method (iterative)
            double[] weights = ComputeEigenvector(matrix, n);

            // Step 2: Compute lambda_max
            double lambdaMax = ComputeLambdaMax(matrix, weights, n);

            // Step 3: Compute CI and CR
            double ci = (n <= 1) ? 0 : (lambdaMax - n) / (n - 1);
            double ri = (n <= 1) ? 0 : (n - 1 < RandomIndexTable.Length ? RandomIndexTable[n - 1] : 1.59);
            double cr = (ri == 0) ? 0 : ci / ri;

            return new AhpResult
            {
                Weights = weights,
                LambdaMax = lambdaMax,
                ConsistencyIndex = ci,
                RandomIndex = ri,
                ConsistencyRatio = cr,
                CriteriaNames = criteriaNames
            };
        }

        /// <summary>
        /// Computes the principal eigenvector of a positive reciprocal matrix
        /// using the iterative power method.
        /// </summary>
        private static double[] ComputeEigenvector(double[,] matrix, int n, int maxIterations = 100, double tolerance = 1e-8)
        {
            double[] weights = new double[n];
            double[] prevWeights = new double[n];

            // Initial estimate: normalized column sums (geometric mean method as seed)
            for (int i = 0; i < n; i++)
            {
                double product = 1.0;
                for (int j = 0; j < n; j++)
                {
                    product *= matrix[i, j];
                }
                weights[i] = Math.Pow(product, 1.0 / n);
            }
            NormalizeVector(weights);

            // Iterative power method
            for (int iter = 0; iter < maxIterations; iter++)
            {
                Array.Copy(weights, prevWeights, n);

                // Multiply matrix by current weight vector
                double[] newWeights = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double sum = 0;
                    for (int j = 0; j < n; j++)
                    {
                        sum += matrix[i, j] * prevWeights[j];
                    }
                    newWeights[i] = sum;
                }

                // Normalize
                NormalizeVector(newWeights);
                Array.Copy(newWeights, weights, n);

                // Check convergence
                double maxDiff = 0;
                for (int i = 0; i < n; i++)
                {
                    maxDiff = Math.Max(maxDiff, Math.Abs(weights[i] - prevWeights[i]));
                }

                if (maxDiff < tolerance)
                    break;
            }

            return weights;
        }

        /// <summary>
        /// Computes λmax = sum of (column_sum * weight) for each column.
        /// </summary>
        private static double ComputeLambdaMax(double[,] matrix, double[] weights, int n)
        {
            // A * w = lambda_max * w
            // lambda_max = average of (A*w)_i / w_i
            double lambdaMax = 0;
            for (int i = 0; i < n; i++)
            {
                double aw_i = 0;
                for (int j = 0; j < n; j++)
                {
                    aw_i += matrix[i, j] * weights[j];
                }
                if (weights[i] > 1e-10)
                    lambdaMax += aw_i / weights[i];
            }
            return lambdaMax / n;
        }

        private static void NormalizeVector(double[] v)
        {
            double sum = v.Sum();
            if (sum > 1e-10)
            {
                for (int i = 0; i < v.Length; i++)
                    v[i] /= sum;
            }
        }

        #endregion

        #region FAHP Methods

        /// <summary>
        /// Computes FAHP weights using Chang's Extent Analysis method.
        /// </summary>
        public static FahpResult ComputeFahpChangExtent(double[,] crispMatrix, string[] criteriaNames)
        {
            int n = crispMatrix.GetLength(0);
            var fuzzyMatrix = ConvertToFuzzyMatrix(crispMatrix, n);

            // Step 1: Compute fuzzy synthetic extent Si for each criterion
            // Si = Sum_j(Mij) ⊗ [Sum_i(Sum_j(Mij))]^(-1)
            var rowSums = new TriangularFuzzyNumber[n];
            var totalSum = new TriangularFuzzyNumber(0, 0, 0);

            for (int i = 0; i < n; i++)
            {
                rowSums[i] = new TriangularFuzzyNumber(0, 0, 0);
                for (int j = 0; j < n; j++)
                {
                    rowSums[i] = rowSums[i] + fuzzyMatrix[i, j];
                }
                totalSum = totalSum + rowSums[i];
            }

            // Synthetic extents
            var syntheticExtents = new TriangularFuzzyNumber[n];
            for (int i = 0; i < n; i++)
            {
                syntheticExtents[i] = new TriangularFuzzyNumber(
                    rowSums[i].L / totalSum.U,
                    rowSums[i].M / totalSum.M,
                    rowSums[i].U / totalSum.L
                );
            }

            // Step 2: Compute degree of possibility V(Si >= Sj) for all pairs
            double[] dValues = new double[n];
            for (int i = 0; i < n; i++)
            {
                double minV = double.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    double v = DegreeOfPossibility(syntheticExtents[i], syntheticExtents[j]);
                    minV = Math.Min(minV, v);
                }
                dValues[i] = minV;
            }

            // Step 3: Normalize
            double sumD = dValues.Sum();
            double[] weights = new double[n];
            if (sumD > 1e-10)
            {
                for (int i = 0; i < n; i++)
                    weights[i] = dValues[i] / sumD;
            }
            else
            {
                // Fallback: equal weights if all possibilities are zero
                for (int i = 0; i < n; i++)
                    weights[i] = 1.0 / n;
            }

            return new FahpResult
            {
                Weights = weights,
                Approach = "Chang's Extent Analysis",
                CriteriaNames = criteriaNames
            };
        }

        /// <summary>
        /// Computes FAHP weights using the Geometric Mean (Buckley) method.
        /// </summary>
        public static FahpResult ComputeFahpGeometricMean(double[,] crispMatrix, string[] criteriaNames)
        {
            int n = crispMatrix.GetLength(0);
            var fuzzyMatrix = ConvertToFuzzyMatrix(crispMatrix, n);

            // Step 1: Compute geometric mean of each row's fuzzy values
            var geoMeans = new TriangularFuzzyNumber[n];
            for (int i = 0; i < n; i++)
            {
                double prodL = 1, prodM = 1, prodU = 1;
                for (int j = 0; j < n; j++)
                {
                    prodL *= fuzzyMatrix[i, j].L;
                    prodM *= fuzzyMatrix[i, j].M;
                    prodU *= fuzzyMatrix[i, j].U;
                }
                geoMeans[i] = new TriangularFuzzyNumber(
                    Math.Pow(prodL, 1.0 / n),
                    Math.Pow(prodM, 1.0 / n),
                    Math.Pow(prodU, 1.0 / n)
                );
            }

            // Step 2: Compute total sum for normalization
            double sumL = 0, sumM = 0, sumU = 0;
            for (int i = 0; i < n; i++)
            {
                sumL += geoMeans[i].L;
                sumM += geoMeans[i].M;
                sumU += geoMeans[i].U;
            }

            // Step 3: Compute fuzzy weights (normalized)
            // wi = ri / (r1 + r2 + ... + rn)
            // Defuzzify using centroid: w = (l + m + u) / 3
            double[] weights = new double[n];
            for (int i = 0; i < n; i++)
            {
                double wL = geoMeans[i].L / sumU;
                double wM = geoMeans[i].M / sumM;
                double wU = geoMeans[i].U / sumL;
                weights[i] = (wL + wM + wU) / 3.0; // Centroid defuzzification
            }

            // Final normalization to ensure sum = 1
            NormalizeVector(weights);

            return new FahpResult
            {
                Weights = weights,
                Approach = "Geometric Mean (Buckley)",
                CriteriaNames = criteriaNames
            };
        }

        /// <summary>
        /// Converts a crisp pairwise comparison matrix to a fuzzy matrix using standard TFN scale.
        /// </summary>
        private static TriangularFuzzyNumber[,] ConvertToFuzzyMatrix(double[,] crispMatrix, int n)
        {
            var fuzzyMatrix = new TriangularFuzzyNumber[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double val = crispMatrix[i, j];
                    if (i == j)
                    {
                        fuzzyMatrix[i, j] = new TriangularFuzzyNumber(1, 1, 1);
                    }
                    else if (val >= 1)
                    {
                        int rounded = Math.Max(1, Math.Min(9, (int)Math.Round(val)));
                        fuzzyMatrix[i, j] = CrispToFuzzyScale[rounded];
                    }
                    else
                    {
                        // Reciprocal: 1/val -> get TFN for the integer value, then take reciprocal
                        double invVal = 1.0 / val;
                        int rounded = Math.Max(1, Math.Min(9, (int)Math.Round(invVal)));
                        fuzzyMatrix[i, j] = CrispToFuzzyScale[rounded].Reciprocal();
                    }
                }
            }
            return fuzzyMatrix;
        }

        /// <summary>
        /// Degree of possibility that S1 >= S2 for two TFNs.
        /// V(S1 >= S2) = 1 if m1 >= m2
        ///             = 0 if l2 >= u1
        ///             = (l2 - u1) / ((m1 - u1) - (m2 - l2)) otherwise
        /// </summary>
        private static double DegreeOfPossibility(TriangularFuzzyNumber s1, TriangularFuzzyNumber s2)
        {
            if (s1.M >= s2.M)
                return 1.0;
            if (s2.L >= s1.U)
                return 0.0;

            double numerator = s2.L - s1.U;
            double denominator = (s1.M - s1.U) - (s2.M - s2.L);

            if (Math.Abs(denominator) < 1e-10)
                return 1.0;

            return numerator / denominator;
        }

        #endregion

        #region Sensitivity Analysis

        /// <summary>
        /// Performs Monte Carlo one-at-a-time sensitivity analysis.
        /// Perturbs the weight of the selected criterion and re-normalizes others proportionally.
        /// </summary>
        /// <param name="originalWeights">Original normalized weight vector.</param>
        /// <param name="criteriaNames">Names of criteria.</param>
        /// <param name="perturbedIndex">Index of the criterion to perturb.</param>
        /// <param name="numSimulations">Number of Monte Carlo simulations.</param>
        /// <param name="perturbationRange">Fractional range for perturbation (e.g., 0.5 means ±50%).</param>
        public static SensitivityResult PerformSensitivityAnalysis(
            double[] originalWeights, 
            string[] criteriaNames,
            int perturbedIndex,
            int numSimulations = 1000,
            double perturbationRange = 0.5)
        {
            int n = originalWeights.Length;
            var rng = new Random(42); // Fixed seed for reproducibility

            // Compute original ranking (descending by weight)
            int[] originalRanking = ComputeRanking(originalWeights);

            int rankReversals = 0;
            double[][] weightRanges = new double[n][];
            for (int i = 0; i < n; i++)
                weightRanges[i] = new double[] { double.MaxValue, double.MinValue };

            for (int sim = 0; sim < numSimulations; sim++)
            {
                // Perturb the selected weight within [w*(1-range), w*(1+range)], clamped to (0, 1)
                double originalW = originalWeights[perturbedIndex];
                double perturbedW = originalW * (1.0 + (rng.NextDouble() * 2 - 1) * perturbationRange);
                perturbedW = Math.Max(0.001, Math.Min(0.999, perturbedW));

                // Re-normalize remaining weights proportionally
                double[] newWeights = new double[n];
                double remainingOriginal = 1.0 - originalW;
                double remainingNew = 1.0 - perturbedW;

                for (int i = 0; i < n; i++)
                {
                    if (i == perturbedIndex)
                    {
                        newWeights[i] = perturbedW;
                    }
                    else
                    {
                        newWeights[i] = (remainingOriginal > 1e-10)
                            ? originalWeights[i] * (remainingNew / remainingOriginal)
                            : remainingNew / (n - 1);
                    }
                }

                // Track min/max ranges
                for (int i = 0; i < n; i++)
                {
                    weightRanges[i][0] = Math.Min(weightRanges[i][0], newWeights[i]);
                    weightRanges[i][1] = Math.Max(weightRanges[i][1], newWeights[i]);
                }

                // Check for rank reversal
                int[] newRanking = ComputeRanking(newWeights);
                if (!RankingsEqual(originalRanking, newRanking))
                    rankReversals++;
            }

            return new SensitivityResult
            {
                TotalSimulations = numSimulations,
                PerturbedCriterion = criteriaNames[perturbedIndex],
                OriginalWeights = originalWeights,
                OriginalRanking = originalRanking,
                RankReversalCount = rankReversals,
                RankReversalPercentage = (double)rankReversals / numSimulations * 100.0,
                WeightRanges = weightRanges,
                CriteriaNames = criteriaNames
            };
        }

        /// <summary>
        /// Returns ranking indices (1-based) where rank 1 = highest weight.
        /// </summary>
        private static int[] ComputeRanking(double[] weights)
        {
            int n = weights.Length;
            var indices = Enumerable.Range(0, n).OrderByDescending(i => weights[i]).ToArray();
            int[] ranks = new int[n];
            for (int r = 0; r < n; r++)
                ranks[indices[r]] = r + 1;
            return ranks;
        }

        private static bool RankingsEqual(int[] a, int[] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        #endregion

        #region Utility

        /// <summary>
        /// Extracts a double[,] matrix from a list of lists (for use from DataTable extraction).
        /// </summary>
        public static double[,] ToMatrix(List<List<double>> rows)
        {
            int n = rows.Count;
            var matrix = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    matrix[i, j] = rows[i][j];
                }
            }
            return matrix;
        }

        /// <summary>
        /// Validates that a matrix is a proper positive reciprocal matrix.
        /// Returns null if valid, or an error message string if invalid.
        /// </summary>
        public static string ValidateMatrix(double[,] matrix)
        {
            int n = matrix.GetLength(0);

            for (int i = 0; i < n; i++)
            {
                if (Math.Abs(matrix[i, i] - 1.0) > 1e-6)
                    return $"Diagonal element [{i},{i}] must be 1.0 (found {matrix[i, i]}).";

                for (int j = 0; j < n; j++)
                {
                    if (matrix[i, j] <= 0)
                        return $"All matrix values must be positive. Found {matrix[i, j]} at [{i},{j}].";

                    double reciprocal = 1.0 / matrix[i, j];
                    if (Math.Abs(matrix[j, i] - reciprocal) > 0.01)
                        return $"Reciprocal mismatch: [{i},{j}]={matrix[i, j]:F3} but [{j},{i}]={matrix[j, i]:F3} (expected {reciprocal:F3}).";
                }
            }

            return null; // Valid
        }

        #endregion
    }
}
