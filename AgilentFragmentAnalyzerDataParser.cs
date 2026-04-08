/* * Agilent Fragment Analyzer Data Parser
 * --------------------------------------------------------
 * LICENSE: MIT (Free, no-strings-attached)
 * ORIGIN: Human + a Free Tier Gemini AI model(sorry for the imperfections and "hallucinations")
 * * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files, to deal in the Software 
 * without restriction, including without limitation the rights to use, copy, 
 * modify, merge, publish, distribute, sublicense, and/or sell copies.
 * * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.
 * --------------------------------------------------------
 Compile on ANY Wimdows PC using the internal Windows C# compiler as : 
 C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /out:AgilentFragmentAnalyzerDataParser.exe /r:System.Drawing.dll AgilentFragmentAnalyzerDataParser.cs
 Place compiled AgilentFragmentAnalyzerDataParser.exe into the Agilent Fragment Analyzer Data folder
 Run it and enjoy browsing through parsed run data from allsubfolders saved into MasterReport.html
*/
using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

// NOTICE: System.Linq has been intentionally excluded per Gemini AI request to let him see the code logic clearly

class AgilentFragmentAnalyzerDataParser {
    // Layout Constants
    const int W = 1200;
    const float TOP_MARGIN_PX = 50f;
    const float BOT_MARGIN_PX = 60f;
    const float PLOT_GAP_PX = 5f;
    const float LEFT_MARGIN_PX = 100f;
    const float RIGHT_MARGIN_PX = 40f;

    // Binary Data Constants
    const int OFFSET_DATA = 2000;
    const int OFFSET_SAMPLES = 255;
    const int OFFSET_PEAK_OLD = 0x03E8; // 1000
    const int OFFSET_PEAK_NEW = 0x05DC; // 1500
    
	// Layout Defaults (Adjustable via CLI)
    static double aspectRatio = 0.75;
    static int WIN_HALF = -1; // Default -1: Use raw file positions
    static int PEAK_GAP = 15;

    static void Main(string[] args) {
// --- COMMAND LINE ARGUMENT PARSER ---
        for (int a = 0; a < args.Length; a++) {
            string arg = args[a].ToLower();
            if (arg == "-h" || arg == "--help") {
                Console.WriteLine("Agilent Fragment Analyzer Data Parser");
                Console.WriteLine("Usage: Analyzer.exe [options]");
                Console.WriteLine("Options:");
                Console.WriteLine(string.Format("  -a [double]  Aspect Ratio (Default: {0})", aspectRatio));
                Console.WriteLine(string.Format("  -g [int]     Peak Gap (Default: {0})", PEAK_GAP));
                Console.WriteLine(string.Format("  -w [int]     Peak Half-Width (Default: {0}, -1 uses capillary positions from .raw file )", WIN_HALF));
                Console.WriteLine("  -h, --help   Show this help message");
                return; // Exit after showing help
            }

            if (a + 1 < args.Length) {
                if (arg == "-a") double.TryParse(args[a + 1], out aspectRatio);
                if (arg == "-g") int.TryParse(args[a + 1], out PEAK_GAP);
                if (arg == "-w") int.TryParse(args[a + 1], out WIN_HALF);
            }
        }

        string rootPath = Directory.GetCurrentDirectory();
        string[] allFilesRaw = Directory.GetFiles(rootPath, "*.raw", SearchOption.AllDirectories);
        List<string> files = new List<string>();
        foreach (string f in allFilesRaw) if (f.EndsWith(".raw", StringComparison.OrdinalIgnoreCase)) files.Add(f);
        files.Sort(delegate(string x, string y) { return string.Compare(Path.GetFileName(x), Path.GetFileName(y), StringComparison.OrdinalIgnoreCase); });

        if (files.Count == 0) { Console.WriteLine("No .raw files found."); return; }

        string htmlPath = Path.Combine(rootPath, "MasterReport.html");
        using (StreamWriter sw = new StreamWriter(htmlPath, false, Encoding.UTF8)) {
            sw.WriteLine(string.Format(@"
<html><head><style>
    body {{ background:#525659; display:flex; flex-direction:column; align-items:center; padding:60px 20px 20px 20px; font-family:sans-serif; }}
    #toolbar {{ position: fixed; top: 0; left: 0; right: 0; height: 45px; background: #323639; color: white; display: flex; align-items: center; justify-content: center; z-index: 1000; box-shadow: 0 2px 5px rgba(0,0,0,0.3); }}
    #toolbar span {{ font-size: 14px; margin: 0 10px; color: #ccc; }}
    #pageNum {{ width: 65px; background: #1a1c1e; border: 1px solid #555; color: white; text-align: center; padding: 4px; border-radius: 3px; outline: none; font-weight: bold; }}
    .p-wrap {{ background:white; margin-bottom:40px; box-shadow:0 0 15px #000; scroll-margin-top: 60px; }}
    img {{ display: block; }}
</style>
<script>
    function jump() {{
        var val = document.getElementById('pageNum').value;
        var el = document.getElementById('p' + val);
        if (el) {{ el.scrollIntoView(); }}
    }}
</script>
</head>
<body>
    <div id='toolbar'>
        <span>PLOT</span>
        <input type='number' id='pageNum' value='1' min='1' max='{0}' onchange='jump()' onkeyup='if(event.key===""Enter"") jump()'>
        <span> / {0}</span>
    </div>", files.Count));

            for (int i = 0; i < files.Count; i++) {
                string b64 = RenderManual(files[i], W);
                sw.WriteLine(string.Format("<div class='p-wrap' id='p{0}'><img src='data:image/png;base64,{1}'></div>", i + 1, b64));
                Console.WriteLine(string.Format("[{0}/{1}] {2} -> OK", i + 1, files.Count, Path.GetFileName(files[i])));
            }
            sw.WriteLine("</body></html>");
        }
    }

    static List<double> GetNiceTicks(double maxVal, int targetTicks) {
        double range = maxVal;
        double roughStep = range / (targetTicks - 1);
        double exponent = Math.Floor(Math.Log10(roughStep));
        double fraction = roughStep / Math.Pow(10, exponent);
        double niceFraction = fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10;
        double step = niceFraction * Math.Pow(10, exponent);
        List<double> ticks = new List<double>();
        for (double t = 0; t <= maxVal + (step / 10); t += step) ticks.Add(t);
        return ticks;
    }

    static string RenderManual(string path, int W) {
        byte[] b = File.ReadAllBytes(path);
        int rows = (b[OFFSET_SAMPLES] << 8) | b[OFFSET_SAMPLES + 1];
        int cycles = (b.Length - OFFSET_DATA) / (rows * 2);

        // --- DYNAMIC PEAK DISCOVERY (Limit 96) ---
        List<int> rawP1 = new List<int>();
        List<int> rawP2 = new List<int>();
        for (int j = 0; j < 96; j++) {
            int off1 = OFFSET_PEAK_OLD + (j * 2);
            int off2 = OFFSET_PEAK_NEW + (j * 2);
            if (off1 + 1 >= b.Length) break;
            int v1 = (b[off1] << 8) | b[off1 + 1];
            if (v1 == 0) break; // End of list sentinel
            rawP1.Add(v1);
            if (off2 + 1 < b.Length) rawP2.Add((b[off2] << 8) | b[off2 + 1]);
        }

        double[] refR = new double[rows];
        ushort[] allData = new ushort[rows * cycles];
        for (int i = 0; i < allData.Length; i++) allData[i] = (ushort)((b[OFFSET_DATA + i * 2] << 8) | b[OFFSET_DATA + i * 2 + 1]);
        int rLim = Math.Min(20, cycles);
        for (int r = 0; r < rows; r++) {
            double s = 0; for (int c = 0; c < rLim; c++) s += allData[c * rows + r];
            refR[r] = (rLim > 0) ? s / rLim : 0;
        }

		// --- EXPLICIT PEAK DETECTION (C# 5 COMPATIBLE) ---
// 1. SCI-PY PLATEAU ALGORITHM (Clean For-Loop)
        int[] pIdx;
        if (WIN_HALF == -1) {
            pIdx = rawP1.ToArray();
        } 
		else {
			List<int> candidates = new List<int>();
			for (int i = PEAK_GAP; i < rows - PEAK_GAP - 1; i++) {
				if (refR[i] > refR[i - 1]) {
					int i_start = i;
					int i_end = i;

					// Move to the end of the plateau
					while (i_end < rows - 1 && refR[i_end] == refR[i_end + 1]) i_end++;

					// If it drops after the plateau, it's a peak
					if (i_end < rows - 1 && refR[i_end] > refR[i_end + 1]) {
						candidates.Add((i_start + i_end) / 2);
					}
					
					// Fast-forward 'i' to the end of the flat part
					i = i_end; 
				}
			}

			// 2. SORT (Rename variables pA/pB to stop the compiler from crying about byte[] b)
			candidates.Sort(delegate(int pA, int pB) {
				return refR[pB].CompareTo(refR[pA]);
			});

			// 3. DISTANCE FILTER (Clean SciPy logic)
			List<int> kept = new List<int>();
			foreach (int p in candidates) {
				if (kept.Count >= 12) break;
				
				bool tooClose = false;
				foreach (int k in kept) {
					if (Math.Abs(p - k) < PEAK_GAP) { tooClose = true; break; }
				}
				if (!tooClose) kept.Add(p);
			}

			kept.Sort();
			pIdx = kept.ToArray();
        }
        List<double[]> plotData = new List<double[]>();
        List<string> titles = new List<string>();
        plotData.Add(refR); titles.Add("Ref");
        
        int winSize = (WIN_HALF == -1) ? 0 : WIN_HALF;
        for (int p = 0; p < pIdx.Length; p++) {
            double[] v = new double[cycles];
            for (int c = 0; c < cycles; c++) {
                double sum = 0; int count = 0;
                for (int r = pIdx[p] - winSize; r <= pIdx[p] + winSize; r++) 
                    if (r >= 0 && r < rows) { sum += allData[c * rows + r]; count++; }
                v[c] = (count > 0) ? sum / count : 0;
            }
            plotData.Add(v); titles.Add("P" + (p + 1));
        }

        string[] sNames = { "Cur (uA)", "Vol (kV)", "Pres (PSI)" };
        string cPath = Path.ChangeExtension(path, ".current");
        for (int s = 0; s < 3; s++) {
            double[] sv = new double[cycles];
            if (File.Exists(cPath)) {
                string[] lines = File.ReadAllLines(cPath);
                // Start loop at second line to skeep the header
                for (int i = 0; i < Math.Min(cycles, lines.Length - 1); i++) {
                    string[] sp = lines[i + 1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (sp.Length > s) double.TryParse(sp[s], out sv[i]);
                }
            }
            plotData.Add(sv); titles.Add(sNames[s]);
        }

        int totalPlots = plotData.Count;
        int H = (int)(W * aspectRatio * totalPlots / 16.0f );

        using (Bitmap bmp = new Bitmap(W, H))
        using (Graphics g = Graphics.FromImage(bmp)) {
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

            g.Clear(Color.White);
                using (Font fNum = new Font("Arial", 10))
                using (Font fTitle = new Font("Arial", 10, FontStyle.Bold))
                using (Font fHeader = new Font("Arial", 11, FontStyle.Bold))
                using (Pen pBlack = new Pen(Color.Black, 1f))
                using (Pen pBlue = new Pen(Color.Blue, 1f))
                using (Pen pRed = new Pen(Color.Red, 1f))
                using (Pen pGreen = new Pen(Color.DarkGreen, 1f))
                using (Pen pPurple = new Pen(Color.Purple, 1f))
                using (Pen pDashBlue = new Pen(Color.Blue, 1f) { DashStyle = DashStyle.Dash }) {
            float numH = g.MeasureString("0000", fNum).Height + 2;
            float ccdH = g.MeasureString("CCD pixels", fTitle).Height + 2;
            float totalFixedOverhead = TOP_MARGIN_PX + BOT_MARGIN_PX + ((totalPlots-2) * PLOT_GAP_PX) + (2 * numH) + ccdH;
                float hData = (H - totalFixedOverhead) / (float)totalPlots;

            float curY = TOP_MARGIN_PX;
            float plotW = W - LEFT_MARGIN_PX - RIGHT_MARGIN_PX;

            for (int i = 0; i < totalPlots; i++) {
                        RectangleF rect = new RectangleF(LEFT_MARGIN_PX, curY, plotW, hData);
                        g.DrawLine(pBlack, rect.Left, rect.Top, rect.Left, rect.Bottom);
                        
                        Brush titleBrush = Brushes.Black;
                        Pen plotPen = pBlue; // Default blue for P1-P12

                        if (i == 0) { plotPen = pBlack; }
                        else if (i == totalPlots - 3) { titleBrush = Brushes.Red; plotPen = pRed; }
                        else if (i == totalPlots - 2) { titleBrush = Brushes.DarkGreen; plotPen = pGreen; }
                        else if (i == totalPlots - 1 ) { titleBrush = Brushes.Purple; plotPen = pPurple; }

                if (i < titles.Count)
                    g.DrawString(titles[i], fTitle, titleBrush, new RectangleF(0, curY, LEFT_MARGIN_PX, hData), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

                if (i < plotData.Count) {
                    double[] d = plotData[i];
                    if (d.Length > 1) {
                        double min = d[0], max = d[0];
                        foreach (double val in d) { if (val < min) min = val; if (val > max) max = val; }
                        double rng = (max == min) ? 1.0 : max - min;
                        g.DrawLine(pBlack, rect.Left - 5, rect.Top, rect.Left, rect.Top);
                        g.DrawLine(pBlack, rect.Left - 5, rect.Bottom, rect.Left, rect.Bottom);

                        if (i == 0) {
                            for (int j = 0; j < rawP1.Count; j++) {
                                float x1 = rect.X + (rawP1[j] * rect.Width / (rows - 1));
                                g.DrawLine(pBlue, x1, rect.Top, x1, rect.Bottom);
                                if (j < rawP2.Count) {
                                    float x2 = rect.X + (rawP2[j] * rect.Width / (rows - 1));
                                    g.DrawLine(pDashBlue, x2, rect.Top, x2, rect.Bottom);
                                }
                            }
                        }

                        PointF[] pts = new PointF[d.Length];
                        for (int j = 0; j < d.Length; j++) pts[j] = new PointF(rect.X + (j * rect.Width / (d.Length - 1)), rect.Y + (float)((max - d[j]) * rect.Height / rng));
                        g.DrawLines(plotPen, pts);
                        if (i == 0) foreach (int pk in pIdx) {
                            int px = (int)Math.Round(rect.X + (pk * rect.Width / (rows - 1)));
                            int py = (int)Math.Round(rect.Y + (float)((max - refR[pk]) * rect.Height / rng));
                            g.FillRectangle(Brushes.Red, px - 1, py - 1, 3, 3);
                        }

                                string yFmt = (i <= totalPlots - 4) ? "F0" : "G4";
                                g.DrawString(max.ToString(yFmt), fNum, Brushes.Black, LEFT_MARGIN_PX - 7, rect.Y, new StringFormat { Alignment = StringAlignment.Far });
                                g.DrawString(min.ToString(yFmt), fNum, Brushes.Black, LEFT_MARGIN_PX - 7, rect.Bottom, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Far });
                            
                        
                        curY += hData;
                    if (i == 0 || i == totalPlots - 1) {
                            double xMax = (i == 0) ? rows : cycles;
                            g.DrawLine(pBlack, rect.Left, rect.Bottom, rect.Right, rect.Bottom);
                            var ticks = GetNiceTicks(xMax - 1, 6);
                            foreach (var t in ticks) {
                                float xP = (float)Math.Round(rect.X + (float)(t * rect.Width / (xMax - 1)));
                                if (xP > rect.Right + 0.1f) continue;
                                g.DrawLine(pBlack, xP, rect.Bottom, xP, rect.Bottom + 5);
                                g.DrawString(t.ToString("F0"), fNum, Brushes.Black, xP, curY + 6, new StringFormat { Alignment = StringAlignment.Center });
                            }
                            curY += (numH + 5);
                            if (i == 0) {
                                g.DrawString("CCD pixels", fTitle, Brushes.Black, new RectangleF(rect.X, curY, rect.Width, ccdH), new StringFormat { Alignment = StringAlignment.Center });
                                curY += ccdH;
                            }
                        }
                    if (i != 0 && i < totalPlots - 1) curY += PLOT_GAP_PX;
                    }
                    StringFormat centerTitle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("Run: " + Path.GetFileNameWithoutExtension(path), fHeader, Brushes.Black, new RectangleF(0, 0, W, TOP_MARGIN_PX), centerTitle);
                    g.DrawString("Time (sec)", fTitle, Brushes.Black, new RectangleF(0, H - BOT_MARGIN_PX, W, BOT_MARGIN_PX), centerTitle);
                }
            }

            using (Bitmap indexedBmp = bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format8bppIndexed)) {
                using (MemoryStream ms = new MemoryStream()) {
                    indexedBmp.Save(ms, ImageFormat.Png);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
    }
	}
}
