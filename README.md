# Agilent-Fragment-Analyzer-Data-Parser
A lightweight, zero-dependency C# tool to parse Agilent Fragment Analyzer .raw binary data into a navigable HTML report. Supports 12/96 capillary arrays and sensor data. Built via Human-AI collaboration (Free Tier Gemini). No installation required; compiles with built-in Windows tools.
Converts Agilent Fragment Analyzer binary `.raw` files into a single `MasterReport.html`.

## Features
* **Zero Dependencies:** No Visual Studio or external libraries required.
* **Auto-Scaling:** Automatically detects and handles both **12-capillary** and **96-capillary** arrays.
* **Full Data Parsing:** Extracts sample peaks, reference rows, and system sensor data (Current, Voltage, Pressure).
* **Smart Visualization:** Generates a HTML report with a built-in "Jump to Plot" navigation bar.
* **Universal Compatibility:** Compiles on any Windows machine using the built-in .NET Framework compiler.

## The Project Story
* ** Needed to quickly browse through all ~500 runs stored on Agilent Fragment Analyzer Windows PC without installing anything.
* ** Had zero trust in any external binaries.
* ** Looked for a true "open source" solution.
* ** **Free Tier Gemini AI** offered help to pity Human.
* **The Human:** Provided the domain expertise, binary offsets, and logic corrections.
* **The AI:** Assisted with the GDI+ rendering, HTML/JS boilerplate, and "hallucinated" several extra braces along the way.
* **The Result:** A robust, honest, and functional lab tool that shows that with right oversight **Free Tier AI** can do really useful coding.

## How to Compile & Use
1. Download `AgilentFragmentAnalyzerDataParser.cs`.
2. Open a Command Prompt and run:
   ```cmd
   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /out:Analyzer.exe /r:System.Drawing.dll AgilentFragmentAnalyzerDataParser.cs
   ```
