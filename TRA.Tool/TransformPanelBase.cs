using egbt22lib;
using HarfBuzzSharp;
using Microsoft.Extensions.Logging;
using OSGeo.OSR;
using ScottPlot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using TRA_Lib;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace TRA.Tool
{
    public partial class TransformPanelBase : BasePanel
    {
        public TransformPanelBase()
        {
            InitializeComponent();
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }

        internal struct TransformSetup
        {
            public Func<double, double, (double x, double y)> ConvertFunc;
            public Func<double, double, (double gamma, double k, bool IsInside)> GammaK_From;
            public Func<double, double, (double gamma, double k, bool IsInside)> GammaK_To;
            public string Target_CRS;
            public TransformSetup()
            {
            }
        }

        public static double[][] CalcArray2(double[][] points, Func<double, double, (double x, double y)> calc)
        {
            int n = points[0].Length;
            double[][] xy = new double[2][];
            xy[0] = new double[n];
            xy[1] = new double[n];
            for (int i = 0; i < n; i++)
            {
                var (x, y) = calc(points[0][i], points[1][i]);
                xy[0][i] = x;
                xy[1][i] = y;
            }
            return xy;
        }

        /// <summary>
        /// Transforms all Elements of a TRA by the given transformSetup
        /// </summary>
        /// <param name="trasse"></param>
        /// <param name="transformSetup"></param>
        private void TrassenTransform(TRATrasse trasse, TransformSetup transformSetup)
        {
            double previousdK = double.NaN; //Scale from previous element
            if (trasse == null) return;
            trasse.CRS_Name = transformSetup.Target_CRS; //Store new CRS of this trasse
            HashSet<TrassenElementExt> elementsOutsideSource = new HashSet<TrassenElementExt>();
            HashSet<TrassenElementExt> elementsOutsideTarget = new HashSet<TrassenElementExt>();
            HashSet<TrassenElementExt> elementsExeedingPPM = new HashSet<TrassenElementExt>();
            foreach (TrassenElementExt element in trasse.Elemente.Reverse()) //run reverse for having X/Yend from the successor is already transformed for plausability checks 
            {
                //Transform Interpolation Points
                Interpolation interp = element.InterpolationResult;
                if (!interp.IsEmpty())
                {
                    if (interp.H == null) { interp.H = new double[interp.X.Length]; }
                    try
                    {
                        //Transform is done in 2D. Normal Heights are not transformed, but the X/Y coordinates are transformed.
                        double[][] points = { interp.Y, interp.X};
                        double[] gamma_from, k_from, gamma_to, k_to = new double[interp.X.Length];
                        bool[] inside_from, inside_to = new bool[interp.X.Length];
                    
                        (gamma_from, k_from, inside_from) = egbt22lib.Convert.CalcArrays2(points[0], points[1],transformSetup.GammaK_From);
                        points = CalcArray2(points, transformSetup.ConvertFunc);
                        (gamma_to, k_to, inside_to) = egbt22lib.Convert.CalcArrays2(points[0], points[1], transformSetup.GammaK_To);
                        //Workaround to set values in place
                        for (int i = 0; i < interp.X.Length; i++)
                        {
                            interp.Y[i] = points[0][i];
                            interp.X[i] = points[1][i];
                            interp.H[i] = interp.H[i];
                            interp.T[i] = interp.T[i] - DegreesToRadians(gamma_to[i] - gamma_from[i]);
                        }
                        if (inside_from.Contains(false))
                        {
                            elementsOutsideSource.Add(element);
                            TrassierungLog.Logger?.Log_Async(LogLevel.Warning, trasse.Filename + ":Element#" + element.ID + " one or more InterpolationPoint coordinate is outside valid range of SourceCRS");

                        }
                        if (inside_to.Contains(false))
                        {
                            elementsOutsideTarget.Add(element);
                            TrassierungLog.Logger?.Log_Async(LogLevel.Warning, trasse.Filename + ":Element#" + element.ID + " one or more InterpolationPoint coordinate is outside valid range of SourceCRS");
                        }
                        //Check for PPM greather 10 
                        //if(Math.Abs(k_to.First()-1)*1000000 > 10 || Math.Abs(k_to.Last() - 1) * 1000000 > 10) //we only check first and last point for simplicity
                        //{
                        //    elementsExeedingPPM.Add(element);
                        //    TrassierungLog.Logger?.Log_Async(LogLevel.Warning, trasse.Filename + ":Element#" + element.ID + " scale exceeds 10ppm (" + Math.Max(Math.Abs(k_to.First() - 1) * 1000000, Math.Abs(k_to.Last() - 1) * 1000000) + ")");
                        //}
                    }
                    catch
                    {
                    }
                }
                //Transform Element
                try
                {
                    double gamma_i, k_i, gamma_o, k_o;
                    double rechts, hoch;
                    bool inside_from, inside_to;
                    (gamma_i, k_i, inside_from) = transformSetup.GammaK_From(element.Ystart, element.Xstart);
                    (rechts, hoch) = transformSetup.ConvertFunc(element.Ystart, element.Xstart);
                    (gamma_o, k_o, inside_to) = transformSetup.GammaK_To(rechts, hoch);
                    double dK = (k_o / k_i);
                    double dT = DegreesToRadians(gamma_o - gamma_i);
                    element.Relocate(hoch, rechts, dT, dK, previousdK, checkBox_RecalcHeading.Checked, checkBox_RecalcLength.Checked);
                    previousdK = dK;
                    if (!inside_from)
                    {
                        elementsOutsideSource.Add(element);
                    }
                    if (!inside_to)
                    {
                        elementsOutsideTarget.Add(element);
                    }
                    //Check for PPM greather 10 
                    //if (Math.Abs(k_o - 1) * 1000000 > 10)
                    //{
                    //    elementsExeedingPPM.Add(element);
                    //}
                }
                catch
                {
                }
            }

            //Message if elements are outside CRS-BBox;
            if (elementsOutsideSource.Count > 0)
            {
                MessageBox.Show(trasse.Filename + ": has Elements outside SourceCRS BoundingBox:\n" + string.Join(", ", elementsOutsideSource.Select(p => p.ID)) + "\n (see log for more information)", "Transform: " + trasse.Filename);
            }
            if (elementsOutsideTarget.Count > 0 || elementsExeedingPPM.Count > 0)
            {
                MessageBox.Show(trasse.Filename + ": has Elements outside TargetCRS BoundingBox:\n" + string.Join(", ", elementsOutsideTarget.Select(p => p.ID)) + 
                    //"\n Element-Scale exceeding 10ppm:\n" + string.Join(", ", elementsExeedingPPM.Select(p => p.ID)) +
                    "\n(see log for more information)", "Transform: " + trasse.Filename);
            }

            //Set Heading to End element as this is only an empty Geometry and we started to iterate reverse heading could not be calculated. Set heading from the second last element
            double heading = 0;
            (_,_,heading) = trasse.Elemente[^2].GetPointAtS(trasse.Elemente[^2].L, true);
            trasse.Elemente.Last().T = heading;
            //Try Removing unnecessary KSprung-Elements.This can happen if previous scale was saved to TRA using KSprung, and inverted Transform was applied. Else Update
            foreach (TrassenElementExt element in trasse.Elemente)
            {
                if (element.Successor != null && element.Successor.GetGeometryType() == typeof(KSprung) && element.Successor.Successor != null)
                {
                    if (Math.Abs(element.S + element.L * element.Scale - element.Successor.Successor.S) < Trassierung.StationMismatchTolerance)
                    {
                        //Replace KSprung
                        element.ApplyScale(); //Apply Scale to pre-KSprung element
                        element.Successor.L = 0; //Set Length of KSprung to 0 (we delete it later by filtering on zero)
                        element.Successor = element.Successor.Successor; //move successor to KSprung-successor
                        if (element.Successor.Successor != null) element.Successor.Successor.Predecessor = element; //if there is a successor update also its predecessor 
                        element.Relocate(bFitHeading: true); //we changed Length so it makes sense to update Heading.
                        element.PlausibilityCheck(); // Rerun Check as we changed Length and Heading Parameters
                    }
                    else
                    {
                        element.Successor.L = element.Successor.Successor.S - element.S - element.L * element.Scale; //if there is still a deviation in Sation-values update this in KSprung-Length
                        element.Successor.T = element.Successor.Successor.T;
                    }
                }
            }
            trasse.Elemente = new BindingList<TrassenElementExt>(trasse.Elemente.Where(x => !(x.GetGeometryType() == typeof(KSprung) && x.L == 0)).ToList()); //Remove KSprung elements of length 0
            //Try Removing unnecessary Scale.This can happen if previous scale was saved applied for saving, and inverted Transform was applied.
            foreach (TrassenElementExt element in trasse.Elemente)
            {
                if (element.Successor != null)
                {
                    if (Math.Abs(element.S + element.L * element.Scale - element.Successor.S) < Trassierung.StationMismatchTolerance)
                    {
                        element.ApplyScale(); //Apply Scale
                    }
                }
            }     
        }
        internal virtual TransformSetup GetTransformSetup() { return new TransformSetup(); }

        private void btn_Transform_Click(object sender, EventArgs e)
        {
            Transform();
        }
        private void Transform()
        {
            FlowLayoutPanel owner = Parent as FlowLayoutPanel;
            if (owner == null) { return; }
            int idx = owner.Controls.GetChildIndex(this) - 1;

            var trassenToProcess = new List<TRATrasse>();
            while (idx >= 0 && owner.Controls[idx].GetType() != typeof(TransformPanelBase))
            {
                if (owner.Controls[idx].GetType() == typeof(TrassenPanel))
                {
                    TrassenPanel panel = (TrassenPanel)owner.Controls[idx];
                    if (panel.trasseL != null && !trassenToProcess.Contains(panel.trasseL)) trassenToProcess.Add(panel.trasseL);
                    if (panel.trasseS != null && !trassenToProcess.Contains(panel.trasseS)) trassenToProcess.Add(panel.trasseS);
                    if (panel.trasseR != null && !trassenToProcess.Contains(panel.trasseR)) trassenToProcess.Add(panel.trasseR);
                }
                idx--;
            }
            var transformTask = Transform_Async(trassenToProcess);
            transformTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    TrassierungLog.Logger?.Log_Async(LogLevel.Error, $"Transform failed: {t.Exception?.GetBaseException().Message}", nameof(Transform));
                }

                // Starte Interpolation auf dem UI‑Thread (BeginInvoke ist non‑blocking)
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    try
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            _ = InterpolationPanel.Interpolate_Async(trassenToProcess);
                        }));
                    }
                    catch { /* ignore if form closing */ }
                }
                else
                {
                    // Fallback: starte trotzdem (möglicherweise marshalled intern)
                    _ = InterpolationPanel.Interpolate_Async(trassenToProcess);
                }
            }, TaskScheduler.Default);
        }
        private async Task Transform_Async(List<TRATrasse> trassenToProcess)
        {
            using var cts = new System.Threading.CancellationTokenSource();

            LoadingForm loadingForm = null;
            loadingForm = new LoadingForm();
            loadingForm.Text = "Transforming...";
            loadingForm.FormClosing += (s, e) =>
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
            };
            // show the LoadingForm on the UI thread and ensure it's in front
            loadingForm.Show();
            loadingForm.BringToFront();
            // allow the form to render before heavy work
            await Task.Yield();

            try
            {
                // wait for loadingForm to be created and handle ready
                int waitMs = 0;
                while (loadingForm == null || !loadingForm.IsHandleCreated)
                {
                    await Task.Delay(25).ConfigureAwait(false);
                    waitMs += 25;
                    if (waitMs > 5000) break;
                }

                // prepare per-trasse progress reporters (on LoadingForm thread)
                var progressMap = new Dictionary<TRATrasse, IProgress<ProgressReport>>();
                if (loadingForm != null && loadingForm.IsHandleCreated)
                {
                    foreach (var trasse in trassenToProcess)
                    {
                        trasse.Plot(); // prepare UI
                        int total = trasse.Elemente?.Count ?? 0;

                        // composite: forward to UI progress bar and trigger single-element plot when ElementIndex >= 0
                        var uiPr = loadingForm.AddProgressBar(trasse.Filename, total);

                        progressMap[trasse] = new Progress<ProgressReport>(report =>
                        {
                            // forward to loading form bar
                            try { uiPr.Report(new ProgressReport(report.Current, report.Total, report.ElementIndex)); } catch { }
                        });

                        // initial report damit Label/Bar sofort sichtbar sind
                        try
                        {
                            progressMap[trasse].Report(new ProgressReport(0, total));
                        }
                        catch { /* safe: ignore if UI already closing */ }
                    }
                    loadingForm.Refresh();
                }
                else
                {
                    // fallback: no UI -> use noop progressors
                    foreach (var trasse in trassenToProcess) progressMap[trasse] = new Progress<ProgressReport>(_ => { });
                }

                // start all per-trasse interpolation tasks in parallel and map task -> trasse
                var taskMap = new Dictionary<Task, TRATrasse>();
                foreach (var trasse in trassenToProcess)
                {
                    var progress = progressMap[trasse];
                    var task = Task.Run(() =>
                    {
                        try
                        {
                            int total = trasse.Elemente?.Count ?? 0;
                            int completed = 0;

                            // initial report
                            try { progress.Report(new ProgressReport(0, total)); } catch { }

                            // Transform (heavy work)
                            TrassenTransform(trasse, GetTransformSetup());

                            // Calc deviations and report per element
                            for (int i = 0; i < trasse.Elemente.Count; i++)
                            {
                                var element = trasse.Elemente[i];
                                element.ClearProjections();
                                Interpolation interp = element.InterpolationResult;
                                if (!interp.IsEmpty())
                                {
                                    float deviation = ((TRATrasse)element.owner).ProjectPoints(interp.X, interp.Y, true);
                                    string ownerString = element.owner.Filename + "_" + element.ID;
                                    TrassierungLog.Logger?.Log_Async(LogLevel.Information, ownerString + " Deviation to geometry after transform: " + deviation, element);
                                }

                                completed++;

                                // Report finished count and element index (i)
                                try { progress.Report(new ProgressReport(completed, total, i)); } catch { }
                            }

                            // final report
                            try { progress.Report(new ProgressReport(total, total)); } catch { }
                        }
                        catch (Exception ex)
                        {
                            TrassierungLog.Logger?.Log_Async(LogLevel.Error, $"Transform task failed for {trasse.Filename}: {ex.Message}", nameof(Transform_Async));
                            throw;
                        }
                    });
                    taskMap[task] = trasse;

                }
                var pending = taskMap.Keys.ToList();
                while (pending.Count > 0)
                {
                    Task finished = await Task.WhenAny(pending).ConfigureAwait(false);
                    pending.Remove(finished);

                    TRATrasse finishedTrasse = taskMap[finished];
                    try
                    {
                        // observe result / exceptions (await to propagate)
                        await finished.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        TrassierungLog.Logger?.Log_Async(LogLevel.Information, $"Transformation cancelled for {finishedTrasse.Filename}", nameof(Transform_Async));
                        continue;
                    }
                    catch (Exception ex)
                    {
                        TrassierungLog.Logger?.Log_Async(LogLevel.Error, $"Transformation failed for {finishedTrasse.Filename}: {ex.Message}", nameof(Transform_Async));
                        // still try to plot what exists
                    }
                }
                if (loadingForm != null)
                {
                    // Use Invoke to close the form from the background thread
                    loadingForm.Invoke(new Action(() =>
                    {
                        loadingForm.Close(); // Close the form
                    }));
                }
            }
            catch { }
        }
    }
}
