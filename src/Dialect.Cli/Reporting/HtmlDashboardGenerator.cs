using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Dialect.Cli.FileRewriting;

namespace Dialect.Cli.Reporting
{
    /// <summary>
    /// HTML dashboard generator aligned to docs/documentation.html layout.
    /// Produces a three-column layout with sidebar, main content and page-contents.
    /// </summary>
    public sealed class HtmlDashboardGenerator : IReportWriter
    {
        private readonly ILogger<HtmlDashboardGenerator> _logger;

        public string FileExtension => "html";

        public HtmlDashboardGenerator(ILogger<HtmlDashboardGenerator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> WriteReportAsync(BulkFileRewriteResult result, string title, string outputPath)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path cannot be empty", nameof(outputPath));

            try
            {
                var report = new TranslationReport
                {
                    Title = title ?? "SQL Translation Report",
                    GeneratedAt = DateTime.UtcNow,
                    TotalFiles = result.TotalFilesFound,
                    SuccessfulFiles = result.FilesProcessed,
                    FailedFiles = Math.Max(0, result.TotalFilesFound - result.FilesProcessed),
                    TotalTranslations = result.TotalReplacements,
                    TranslationErrors = result.Errors?.Count ?? 0,
                    FileDetails = BuildFileDetails(result),
                    ErrorSummaries = BuildErrorSummaries(result)
                };

                var html = RenderHtml(report);
                await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8);

                _logger.LogInformation("HTML dashboard written to: {OutputPath}", outputPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing HTML dashboard: {Message}", ex.Message);
                return false;
            }
        }

        private string RenderHtml(TranslationReport report)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"  <title>{EscapeHtml(report.Title)}</title>");

            // Include fonts and highlight.css (match docs)
            sb.AppendLine("  <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
            sb.AppendLine("  <link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
            sb.AppendLine("  <link href=\"https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500;600&display=swap\" rel=\"stylesheet\">");
            sb.AppendLine("  <link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/base16/atelier-cave-light.min.css\">");
            sb.AppendLine("  <link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css\" rel=\"stylesheet\">");
            sb.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/chart.js\"></script>");
            sb.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/three@0.152.2/build/three.min.js\"></script>");

            sb.AppendLine("  <style>");
            RenderStyles(sb);
            sb.AppendLine("  </style>");

            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Top navigation
            RenderNavigation(sb, report.Title);

            // Shell
            sb.AppendLine("  <div class=\"app-container\"> ");
            sb.AppendLine("    <div id=\"shell\"> ");

            // Left sidebar
            RenderSidebar(sb, report);

            // Main
            sb.AppendLine("      <main>");
            sb.AppendLine("        <section id=\"overview\"> ");
            RenderHeader(sb, report);
            RenderSummaryCards(sb, report);
            sb.AppendLine("        </section>");

            sb.AppendLine("        <div class=\"row\"> ");
            RenderSuccessChart(sb, report);
            RenderTranslationsChart(sb, report);
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"row\"> ");
            Render3DVisualization(sb, report);
            sb.AppendLine("        </div>");

            sb.AppendLine("        <section id=\"files\"> ");
            RenderFileDetailsTable(sb, report);
            sb.AppendLine("        </section>");

            if (report.ErrorSummaries != null && report.ErrorSummaries.Any())
            {
                sb.AppendLine("        <section id=\"error-summary\"> ");
                RenderErrorSummary(sb, report);
                sb.AppendLine("        </section>");
            }

            sb.AppendLine("        <footer>");
            sb.AppendLine($"          <p>Generated on {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC by Dialect SQL Translation Tool</p>");
            sb.AppendLine("        </footer>");

            sb.AppendLine("      </main>");

            // Right TOC
            RenderPageContents(sb, report);

            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");

            // Scripts
            RenderScripts(sb, report);

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private void RenderStyles(StringBuilder sb)
        {
            sb.AppendLine(@"/* Base styling adapted from docs/documentation.html */");
            sb.AppendLine(@":root {" );
            sb.AppendLine("  --paper:#FAF9F6;");
            sb.AppendLine("  --paper-raised:#FFFFFF;");
            sb.AppendLine("  --ink:#1C1D21;");
            sb.AppendLine("  --ink-soft:#5A5C64;");
            sb.AppendLine("  --ink-faint:#9294A0;");
            sb.AppendLine("  --line:#E5E2DC;");
            sb.AppendLine("  --line-strong:#D3D0C8;");
            sb.AppendLine("  --accent:#4A55C4;");
            sb.AppendLine("  --accent-soft:#EEF0FC;");
            sb.AppendLine("  --accent-ink:#33399A;");
            sb.AppendLine("  --code-bg:#F1EFEA;");
            sb.AppendLine("  --radius:6px;");
            sb.AppendLine("  --font-ui:'IBM Plex Sans', -apple-system, sans-serif;");
            sb.AppendLine("  --font-mono:'IBM Plex Mono', 'SFMono-Regular', monospace;");
            sb.AppendLine("}");
            sb.AppendLine("*{box-sizing:border-box;}");
            sb.AppendLine("body{ margin:0; background:var(--paper); color:var(--ink); font-family:var(--font-ui); font-size:16px; line-height:1.65; -webkit-font-smoothing:antialiased; padding:20px; }");
            sb.AppendLine(".app-container{ max-width:1200px; margin:0 auto; background:var(--paper-raised); border-radius:var(--radius); box-shadow:0 6px 30px rgba(0,0,0,0.06); overflow:hidden; }");
            sb.AppendLine("nav{ background:var(--accent); padding:12px 20px; }");
            sb.AppendLine("nav h1{ color:#fff; margin:0; font-size:1.125rem; font-weight:600; }");
            sb.AppendLine("nav span{ color:rgba(255,255,255,0.9); font-size:0.95rem; margin-left:8px; }");
            sb.AppendLine(".header{ text-align:center; padding:24px 16px; border-bottom:1px solid var(--line); }");
            sb.AppendLine(".summary-cards{ display:grid; grid-template-columns:repeat(auto-fit,minmax(180px,1fr)); gap:12px; margin:18px 0; padding:0 12px; }");
            sb.AppendLine(".card{ border:1px solid var(--line); border-radius:var(--radius); padding:16px; background:var(--paper-raised); text-align:center; }");
            sb.AppendLine(".card .card-value{ font-size:1.75rem; font-weight:700; color:var(--accent-ink); }");
            sb.AppendLine(".row{ display:flex; gap:16px; flex-wrap:wrap; padding:0 12px; margin-bottom:16px; }");
            sb.AppendLine(".chart-container{ flex:1 1 480px; min-height:240px; background:var(--paper-raised); padding:12px; border-radius:var(--radius); border:1px solid var(--line); }");
            sb.AppendLine("#threeScene{ height:400px; border-radius:var(--radius); background:var(--accent-soft); border:1px solid var(--line); }");
            sb.AppendLine(@"table{ width:100%; border-collapse:collapse; margin-top:12px; } th, td{ padding:10px; border-bottom:1px solid var(--line); text-align:left; } th{ color:var(--ink-soft); font-weight:600; font-size:0.78rem; }");
            sb.AppendLine("footer{ padding:12px; text-align:center; color:var(--ink-faint); border-top:1px solid var(--line); background:var(--paper-raised); }");
            sb.AppendLine("@media (max-width:768px){ .row{ flex-direction:column; } .chart-container{ min-height:200px; } }");
        }

        private void RenderNavigation(StringBuilder sb, string title)
        {
            sb.AppendLine("  <nav>");
            sb.AppendLine("    <h1>Dialect Dashboard</h1>");
            sb.AppendLine($"    <span>{EscapeHtml(title)}</span>");
            sb.AppendLine("  </nav>");
        }

        private void RenderHeader(StringBuilder sb, TranslationReport report)
        {
            sb.AppendLine("    <div class=\"header\">");
            sb.AppendLine($"      <h1>{EscapeHtml(report.Title)}</h1>");
            sb.AppendLine($"      <p>Translation Report • Generated {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
            sb.AppendLine("    </div>");
        }

        private void RenderSummaryCards(StringBuilder sb, TranslationReport report)
        {
            sb.AppendLine("    <div class=\"summary-cards\">");
            sb.AppendLine("      <div class=\"card\">");
            sb.AppendLine($"        <div class=\"card-value\">{report.TotalFiles}</div>");
            sb.AppendLine("        <div class=\"card-label\">Total Files</div>");
            sb.AppendLine("      </div>");

            sb.AppendLine("      <div class=\"card success\">");
            sb.AppendLine($"        <div class=\"card-value\">{report.SuccessfulFiles}</div>");
            sb.AppendLine("        <div class=\"card-label\">Successful ✅</div>");
            sb.AppendLine("      </div>");

            if (report.FailedFiles > 0)
            {
                sb.AppendLine("      <div class=\"card danger\">");
                sb.AppendLine($"        <div class=\"card-value\">{report.FailedFiles}</div>");
                sb.AppendLine("        <div class=\"card-label\">Failed ❌</div>");
                sb.AppendLine("      </div>");
            }

            sb.AppendLine("      <div class=\"card\">");
            sb.AppendLine($"        <div class=\"card-value\">{report.TotalTranslations}</div>");
            sb.AppendLine("        <div class=\"card-label\">SQL Strings</div>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </div>");
        }

        private void RenderSuccessChart(StringBuilder sb, TranslationReport report)
        {
            sb.AppendLine("      <div class=\"chart-container\">");
            sb.AppendLine("        <h3>Success Rate</h3>");
            sb.AppendLine("        <canvas id=\"successChart\"></canvas>");
            sb.AppendLine("      </div>");
        }

        private void RenderTranslationsChart(StringBuilder sb, TranslationReport report)
        {
            sb.AppendLine("      <div class=\"chart-container\">");
            sb.AppendLine("        <h3>Translations by File</h3>");
            sb.AppendLine("        <canvas id=\"translationsChart\"></canvas>");
            sb.AppendLine("      </div>");
        }

        private void Render3DVisualization(StringBuilder sb, TranslationReport report)
        {
            sb.AppendLine("      <div class=\"chart-container\">");
            sb.AppendLine("        <h3>3D Visualization</h3>");
            sb.AppendLine("        <div id=\"threeScene\"></div>");
            sb.AppendLine("      </div>");
        }

        private void RenderSidebar(StringBuilder sb, TranslationReport report)
        {
            sb.AppendLine("      <aside id=\"sidebar\"> ");
            sb.AppendLine("        <div class=\"brand-row\"> ");
            sb.AppendLine("          <div class=\"brand\">Dialect <span>Report</span></div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <div class=\"search-wrap\"> ");
            sb.AppendLine("          <input id=\"nav-search\" placeholder=\"Search files...\"> ");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <nav class=\"toc-nav\"> ");
            sb.AppendLine("          <div class=\"nav-group\"> ");
            sb.AppendLine("            <button class=\"nav-group-btn\">Files</button>");
            sb.AppendLine("            <ul class=\"nav-items\">");

            foreach (var f in report.FileDetails ?? Array.Empty<FileTranslationDetail>())
            {
                var name = Path.GetFileName(f.FilePath ?? "");
                var id = MakeId(name);
                sb.AppendLine($"              <li><a href=\"#file-{id}\">{EscapeHtml(name)}</a></li>");
            }

            sb.AppendLine("            </ul>");
            sb.AppendLine("          </div>");
            sb.AppendLine("        </nav>");
            sb.AppendLine("      </aside>");
        }

        private void RenderPageContents(StringBuilder sb, TranslationReport report)
        {
            sb.AppendLine("      <aside id=\"page-contents\"> ");
            sb.AppendLine("        <div class=\"contents-block\"> ");
            sb.AppendLine("          <div class=\"contents-title\">Contents</div>");
            sb.AppendLine("          <ul id=\"contents-list\"> ");
            sb.AppendLine("            <li><a href=\"#overview\" class=\"active\">Overview</a></li>");
            sb.AppendLine("            <li><a href=\"#files\">Files</a></li>");
            if (report.ErrorSummaries != null && report.ErrorSummaries.Any()) sb.AppendLine("            <li><a href=\"#error-summary\">Errors</a></li>");
            sb.AppendLine("          </ul>");
            sb.AppendLine("        </div>");
            sb.AppendLine("      </aside>");
        }

        private void RenderFileDetailsTable(StringBuilder sb, TranslationReport report)
        {
            if (report.FileDetails == null || !report.FileDetails.Any()) return;

            sb.AppendLine("    <h2>File Details</h2>");
            sb.AppendLine("    <table>");
            sb.AppendLine("      <thead><tr>");
            sb.AppendLine("        <th>File</th>");
            sb.AppendLine("        <th>Status</th>");
            sb.AppendLine("        <th>Translations</th>");
            sb.AppendLine("        <th>Error</th>");
            sb.AppendLine("      </tr></thead>");
            sb.AppendLine("      <tbody>");

            foreach (var file in report.FileDetails)
            {
                var status = file.Success ? "<span class=\"success\">✅ Success</span>" : "<span class=\"danger\">❌ Failed</span>";
                var error = file.Error ?? "-";
                var name = Path.GetFileName(file.FilePath ?? "");
                var id = MakeId(name);
                sb.AppendLine($"      <tr id=\"file-{id}\">");
                sb.AppendLine($"        <td>{EscapeHtml(file.FilePath)}</td>");
                sb.AppendLine($"        <td>{status}</td>");
                sb.AppendLine($"        <td>{file.TranslationCount}</td>");
                sb.AppendLine($"        <td>{EscapeHtml(error)}</td>");
                sb.AppendLine("      </tr>");
            }

            sb.AppendLine("      </tbody>");
            sb.AppendLine("    </table>");
        }

        private void RenderErrorSummary(StringBuilder sb, TranslationReport report)
        {
            sb.AppendLine("    <h2>Error Summary</h2>");
            foreach (var errorSummary in report.ErrorSummaries ?? Array.Empty<ErrorSummary>())
            {
                sb.AppendLine($"    <h3>{EscapeHtml(errorSummary.Category)}</h3>");
                sb.AppendLine($"    <p><strong>Count:</strong> {errorSummary.Count}</p>");
                sb.AppendLine("    <ul>");
                foreach (var example in errorSummary.Examples)
                {
                    sb.AppendLine($"      <li>{EscapeHtml(example)}</li>");
                }
                sb.AppendLine("    </ul>");
            }
        }

        private void RenderScripts(StringBuilder sb, TranslationReport report)
        {
            sb.AppendLine("  <script>");
            sb.AppendLine("    (function(){");
            sb.AppendLine("      const root = document.documentElement;");
            sb.AppendLine("      const PALETTE = {");
            sb.AppendLine("        primary: (getComputedStyle(root).getPropertyValue('--accent') || '#4A55C4').trim(),");
            sb.AppendLine("        success: (getComputedStyle(root).getPropertyValue('--accent-ink') || '#28a745').trim(),");
            sb.AppendLine("        warning: (getComputedStyle(root).getPropertyValue('--warning') || '#ffc107').trim(),");
            sb.AppendLine("        danger: (getComputedStyle(root).getPropertyValue('--danger') || '#dc3545').trim()");
            sb.AppendLine("      };");

            // Three.js
            sb.AppendLine("      function initThree() {");
            sb.AppendLine("        try {");
            sb.AppendLine("          if (typeof THREE === 'undefined') return;");
            sb.AppendLine("          const container = document.getElementById('threeScene');");
            sb.AppendLine("          if (!container) return;");
            sb.AppendLine("          const scene = new THREE.Scene();");
            sb.AppendLine("          const camera = new THREE.PerspectiveCamera(60, container.clientWidth / 400, 0.1, 1000);");
            sb.AppendLine("          const renderer = new THREE.WebGLRenderer({ antialias: true });");
            sb.AppendLine("          renderer.setSize(container.clientWidth, 400);");
            sb.AppendLine("          container.appendChild(renderer.domElement);");
            sb.AppendLine("          const light = new THREE.AmbientLight(0xffffff, 0.8); scene.add(light);");
            sb.AppendLine("          const dir = new THREE.DirectionalLight(0xffffff, 0.6); dir.position.set(5,10,7); scene.add(dir);");
            sb.AppendLine("          const geometry = new THREE.SphereGeometry(0.6, 16, 12);");
            var filesJson = JsonSerializer.Serialize((report.FileDetails ?? new List<FileTranslationDetail>())
                .Select(f => new { name = Path.GetFileName(f.FilePath), status = f.Success ? "success" : "danger" })
                .ToList());
            sb.AppendLine($"          const files = {filesJson};");
            sb.AppendLine("          files.forEach((f, i) => { const color = f.status === 'success' ? PALETTE.success : PALETTE.danger; const material = new THREE.MeshStandardMaterial({ color }); const mesh = new THREE.Mesh(geometry, material); mesh.position.set((i % 5) - 2, Math.floor(i / 5) - 2, (i % 3) - 1); scene.add(mesh); });");
            sb.AppendLine("          camera.position.z = 8;");
            sb.AppendLine("          function animate() { requestAnimationFrame(animate); scene.rotation.y += 0.002; renderer.render(scene, camera); }");
            sb.AppendLine("          animate();");
            sb.AppendLine("          window.addEventListener('resize', () => { renderer.setSize(container.clientWidth, 400); camera.aspect = container.clientWidth / 400; camera.updateProjectionMatrix(); });");
            sb.AppendLine("        } catch(e) { console.warn('Three.js init failed:', e); } ");
            sb.AppendLine("      }");
            sb.AppendLine("      initThree();");

            // Charts
            sb.AppendLine($"      new Chart(document.getElementById('successChart'), {{ type: 'doughnut', data: {{ labels: ['Successful','Failed'], datasets: [{{ data: [{report.SuccessfulFiles},{report.FailedFiles}], backgroundColor: [PALETTE.success, PALETTE.danger], borderColor: [PALETTE.success, PALETTE.danger], borderWidth:2 }}] }}, options: {{ responsive:true, maintainAspectRatio:false }} }}); ");

            var topFiles = (report.FileDetails ?? Array.Empty<FileTranslationDetail>()).OrderByDescending(f => f.TranslationCount).Take(5).ToList();
            var labels = JsonSerializer.Serialize(topFiles.Select(f => Path.GetFileName(f.FilePath)).ToList());
            var data = JsonSerializer.Serialize(topFiles.Select(f => f.TranslationCount).ToList());

            sb.AppendLine($"      new Chart(document.getElementById('translationsChart'), {{ type: 'bar', data: {{ labels: {labels}, datasets: [{{ label: 'Translations', data: {data}, backgroundColor: PALETTE.primary, borderColor: PALETTE.primary, borderWidth:1 }}] }}, options: {{ responsive:true, maintainAspectRatio:false, indexAxis:'y' }} }}); ");

            sb.AppendLine("    })();");
            sb.AppendLine("  </script>");
        }

        private List<FileTranslationDetail> BuildFileDetails(BulkFileRewriteResult result)
        {
            return result.FileResults?.Select(fr => new FileTranslationDetail
            {
                FilePath = fr.FilePath ?? "unknown",
                TranslationCount = fr.ReplacedCount,
                Success = fr.Success,
                Error = fr.Error,
                BackupPath = fr.BackupPath,
                TranslationPairs = fr.ReplacedStrings?.Select(p => (p.Original, p.Translated)).ToArray() ?? Array.Empty<(string, string)>()
            }).ToList() ?? new List<FileTranslationDetail>();
        }

        private List<ErrorSummary> BuildErrorSummaries(BulkFileRewriteResult result)
        {
            if (result.Errors == null || !result.Errors.Any()) return new List<ErrorSummary>();
            var groups = result.Errors.GroupBy(e => ExtractErrorCategory(e)).ToList();
            return groups.Select(g => new ErrorSummary { Category = g.Key, Count = g.Count(), Examples = g.Take(3).ToList() }).ToList();
        }

        private string ExtractErrorCategory(string error)
        {
            if (error.Contains("not found", StringComparison.OrdinalIgnoreCase)) return "File Not Found";
            if (error.Contains("parse", StringComparison.OrdinalIgnoreCase)) return "Parse Error";
            if (error.Contains("translation", StringComparison.OrdinalIgnoreCase)) return "Translation Error";
            if (error.Contains("backup", StringComparison.OrdinalIgnoreCase)) return "Backup Error";
            if (error.Contains("permission", StringComparison.OrdinalIgnoreCase)) return "Permission Error";
            return "Other Error";
        }

        private string EscapeHtml(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private string MakeId(string name)
        {
            if (string.IsNullOrEmpty(name)) return "file";
            var id = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
            return string.IsNullOrEmpty(id) ? "file" : id.ToLowerInvariant();
        }
    }
}
