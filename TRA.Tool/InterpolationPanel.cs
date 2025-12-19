using Microsoft.Extensions.Logging;
using System.Linq;
using TRA_Lib;

namespace TRA.Tool
{
    public partial class InterpolationPanel : BasePanel
    {
#if USE_SCOTTPLOT
        static bool dynamicUpdate = false; // enable dynamic per-element plot updates during interpolation
#endif
        public InterpolationPanel()
        {
            InitializeComponent();
            this.label_Panel.Text = "Interpolation";
        }

        private void btn_Interpolate_Click(object sender, EventArgs e)
        {
            Interpolate();
        }
        private void Interpolate()
        {
            //Get TRA-Files to Interpolate
            FlowLayoutPanel owner = Parent as FlowLayoutPanel;
            if (owner == null) return;
            int idx = owner.Controls.GetChildIndex(this) - 1;

            var trassenToProcess = new List<TRATrasse>();
            while (idx >= 0 && owner.Controls[idx].GetType() != typeof(InterpolationPanel))
            {
                if (owner.Controls[idx].GetType() == typeof(TrassenPanel))
                {
                    TrassenPanel panel = (TrassenPanel)owner.Controls[idx];
                    if (panel.trasseS != null && !trassenToProcess.Contains(panel.trasseS)) trassenToProcess.Add(panel.trasseS);
                    if (panel.trasseL != null && !trassenToProcess.Contains(panel.trasseL)) trassenToProcess.Add(panel.trasseL);
                    if (panel.trasseR != null && !trassenToProcess.Contains(panel.trasseR)) trassenToProcess.Add(panel.trasseR);
                }
                idx--;
            }
            _ = Interpolate_Async(trassenToProcess, (double)num_InterpDist.Value, (double)num_allowedTolerance.Value / 100);
        }

        public static async Task Interpolate_Async(List<TRATrasse> trassenToProcess, double delta = double.NaN, double tolerance = double.NaN)
        {
            using var cts = new CancellationTokenSource();

            LoadingForm loadingForm = null;
            loadingForm = new LoadingForm();
            loadingForm.Text = "Interpolating...";
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

                        // composite: create the progress ON the loadingForm thread so callbacks run on loadingForm's UI context
                        var uiPr = loadingForm.AddProgressBar(trasse.Filename, total);

                        IProgress<ProgressReport> combined = null;
                        // create Progress on loadingForm thread so its callback executes there
                        loadingForm.Invoke(new Action(() =>
                        {
                            combined = new Progress<ProgressReport>(report =>
                            {
                                // forward to loading form bar (runs on loadingForm UI thread)
                                try { uiPr.Report(new ProgressReport(report.Current, report.Total, report.ElementIndex)); } catch { }
#if USE_SCOTTPLOT
                                // per-element update: run light UI update here (also on loadingForm UI thread)
                                if (dynamicUpdate && report.ElementIndex >= 0)
                                {
                                    try
                                    {
                                        trasse.Plot();
                                    }
                                    catch { }
                                }
#endif
                            });
                        }));

                        progressMap[trasse] = combined;

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
                    // Start interpolation task (assumes overload returns Task or Task<Interpolation>)
                    var task = trasse.Interpolate3D(trasse, delta, tolerance, progress, cts.Token);
                    taskMap[task] = trasse;

                }

                // process tasks as they finish so we can update plot immediately
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
                        TrassierungLog.Logger?.Log_Async(LogLevel.Information, $"Interpolation cancelled for {finishedTrasse.Filename}", nameof(Interpolate_Async));
                        continue;
                    }
                    catch (Exception ex)
                    {
                        TrassierungLog.Logger?.Log_Async(LogLevel.Error, $"Interpolation failed for {finishedTrasse.Filename}: {ex.Message}", nameof(Interpolate_Async));
                        // still try to plot what exists
                    }
                }
#if USE_SCOTTPLOT
                foreach(var trasse in trassenToProcess)
                {
                    trasse.Plot();
                }
#endif
                // all done
            }
            finally
            {
                // Close loading form safely
                if (loadingForm != null && loadingForm.IsHandleCreated)
                {
                    try
                    {
                        loadingForm.Invoke(new Action(() => loadingForm.Close()));
                    }
                    catch { /* ignore if already closing */ }
                }
            }
        }
    }
}