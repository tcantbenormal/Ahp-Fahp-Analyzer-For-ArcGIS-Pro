# -*- coding: utf-8 -*-
"""
AHP Spatial Engine - Python Toolbox for ArcGIS Pro
Performs weighted overlay (suitability mapping) using AHP/FAHP weights.
Called from the C# AhpFahpAnalyzer Add-in.
"""

import arcpy
import json
import os
import numpy as np


class Toolbox(object):
    def __init__(self):
        self.label = "AHP Spatial Engine"
        self.alias = "AhpSpatialEngine"
        self.tools = [RunAHP]


class RunAHP(object):
    def __init__(self):
        self.label = "Run AHP Weighted Overlay"
        self.description = (
            "Performs weighted raster overlay using AHP or FAHP weights. "
            "Supports both pre-classified and unclassified (with reclassification) raster inputs."
        )
        self.canRunInBackground = True

    def getParameterInfo(self):
        # Parameter 0: Weights JSON
        param_weights = arcpy.Parameter(
            displayName="Weights JSON",
            name="weights_json",
            datatype="GPString",
            parameterType="Required",
            direction="Input"
        )

        # Parameter 1: Criteria Names JSON
        param_criteria = arcpy.Parameter(
            displayName="Criteria Names JSON",
            name="criteria_json",
            datatype="GPString",
            parameterType="Required",
            direction="Input"
        )

        # Parameter 2: Method (AHP or FAHP)
        param_method = arcpy.Parameter(
            displayName="Method",
            name="method",
            datatype="GPString",
            parameterType="Required",
            direction="Input"
        )
        param_method.filter.type = "ValueList"
        param_method.filter.list = ["AHP", "FAHP"]

        # Parameter 3: FAHP Approach
        param_fahp = arcpy.Parameter(
            displayName="FAHP Approach",
            name="fahp_approach",
            datatype="GPString",
            parameterType="Optional",
            direction="Input"
        )

        # Parameter 4: Reclassification JSON
        param_reclass = arcpy.Parameter(
            displayName="Reclassification JSON",
            name="reclass_json",
            datatype="GPString",
            parameterType="Optional",
            direction="Input"
        )

        # Parameter 5: Output Directory
        param_outdir = arcpy.Parameter(
            displayName="Output Directory",
            name="output_dir",
            datatype="GPString",
            parameterType="Optional",
            direction="Input"
        )

        # Parameter 6: Pre-classified flag
        param_preclassified = arcpy.Parameter(
            displayName="Pre-classified",
            name="pre_classified",
            datatype="GPString",
            parameterType="Optional",
            direction="Input"
        )

        return [param_weights, param_criteria, param_method, param_fahp,
                param_reclass, param_outdir, param_preclassified]

    def isLicensed(self):
        try:
            if arcpy.CheckExtension("Spatial") != "Available":
                return False
        except Exception:
            return False
        return True

    def execute(self, parameters, messages):
        # Check out Spatial Analyst extension
        arcpy.CheckOutExtension("Spatial")

        try:
            # Parse parameters
            weights = json.loads(parameters[0].valueAsText)
            criteria_names = json.loads(parameters[1].valueAsText)
            method = parameters[2].valueAsText
            fahp_approach = parameters[3].valueAsText if parameters[3].value else "None"
            reclass_json_str = parameters[4].valueAsText if parameters[4].value else "None"
            output_dir = parameters[5].valueAsText if parameters[5].value else "None"
            pre_classified_str = parameters[6].valueAsText if parameters[6].value else "true"

            is_pre_classified = pre_classified_str.lower() == "true"
            n = len(criteria_names)

            messages.addMessage(f"Method: {method}")
            messages.addMessage(f"Criteria: {criteria_names}")
            messages.addMessage(f"Weights: {[f'{w:.4f}' for w in weights]}")
            messages.addMessage(f"Pre-classified: {is_pre_classified}")

            # Validate
            if len(weights) != n:
                messages.addErrorMessage(f"Weight count ({len(weights)}) != criteria count ({n}).")
                return

            # Determine output location
            if output_dir == "None" or not output_dir:
                # Use the current project's default geodatabase
                output_dir = arcpy.env.scratchGDB
                messages.addMessage(f"Using scratch GDB for output: {output_dir}")

            # Get the active map
            aprx = arcpy.mp.ArcGISProject("CURRENT")
            active_map = aprx.activeMap

            if active_map is None:
                messages.addErrorMessage("No active map found.")
                return

            # ---- Step 1: Reclassify if needed ----
            raster_layers = {}
            if not is_pre_classified and reclass_json_str != "None":
                reclass_data = json.loads(reclass_json_str)
                messages.addMessage(f"Reclassifying {len(reclass_data)} rasters...")

                for rdef in reclass_data:
                    raster_name = rdef["rasterName"]
                    intervals = rdef["intervals"]
                    reclass_method = rdef.get("method", "Equal Interval")

                    messages.addMessage(f"  Reclassifying: {raster_name} ({reclass_method})")

                    # Find the layer in the map
                    layer = self._find_raster_layer(active_map, raster_name)
                    if layer is None:
                        messages.addErrorMessage(f"Raster layer '{raster_name}' not found in active map.")
                        return

                    # Build remap table for arcpy.sa.Reclassify
                    remap_ranges = []
                    for iv in intervals:
                        remap_ranges.append([iv["min"], iv["max"], iv["newVal"]])

                    remap = arcpy.sa.RemapRange(remap_ranges)

                    # Perform reclassification
                    reclassed = arcpy.sa.Reclassify(layer, "VALUE", remap, "DATA")

                    # Save reclassified raster
                    reclass_output = os.path.join(output_dir, f"Reclass_{raster_name}")
                    reclassed.save(reclass_output)
                    raster_layers[raster_name] = reclassed
                    messages.addMessage(f"    Saved: {reclass_output}")
            else:
                # Pre-classified: use layers directly
                for name in criteria_names:
                    layer = self._find_raster_layer(active_map, name)
                    if layer is None:
                        messages.addErrorMessage(f"Raster layer '{name}' not found in active map.")
                        return
                    raster_layers[name] = arcpy.Raster(layer.dataSource)

            # ---- Step 2: Weighted Overlay ----
            messages.addMessage("Performing weighted overlay...")

            result_raster = None
            for i, name in enumerate(criteria_names):
                raster = raster_layers[name]
                weighted = arcpy.sa.Float(raster) * weights[i]
                messages.addMessage(f"  {name} × {weights[i]:.4f}")

                if result_raster is None:
                    result_raster = weighted
                else:
                    result_raster = result_raster + weighted

            # ---- Step 3: Save and add to map ----
            if result_raster is not None:
                output_name = f"Suitability_{method}"
                output_path = os.path.join(output_dir, output_name)

                # If output is a geodatabase path, it will save as a raster dataset
                # If it's a folder, save as a GeoTIFF
                if not output_dir.endswith(".gdb"):
                    output_path = output_path + ".tif"

                result_raster.save(output_path)
                messages.addMessage(f"Suitability map saved to: {output_path}")

                # Add to the active map
                try:
                    active_map.addDataFromPath(output_path)
                    messages.addMessage("Suitability map added to the active map.")
                except Exception as e:
                    messages.addWarningMessage(f"Could not add to map automatically: {e}")

                messages.addMessage(f"{method} weighted overlay complete!")
            else:
                messages.addErrorMessage("No rasters were processed. Check your inputs.")

        except Exception as e:
            messages.addErrorMessage(f"Error in RunAHP: {str(e)}")
            import traceback
            messages.addErrorMessage(traceback.format_exc())
        finally:
            arcpy.CheckInExtension("Spatial")

    def _find_raster_layer(self, active_map, layer_name):
        """Find a raster layer by name in the active map."""
        for lyr in active_map.listLayers():
            if lyr.name == layer_name and lyr.isRasterLayer:
                return lyr
        return None
