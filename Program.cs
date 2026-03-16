using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// データ構造
/// <summary>
/// テーブルのカラム定義を表します。
/// </summary>
class Column
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool IsPrimaryKey { get; set; }
    /// <summary>
    /// Column クラスの新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="name">カラム名。</param>
    /// <param name="type">カラム型。</param>
    /// <param name="isPrimaryKey">主キーかどうか。</param>
    public Column(string name, string type, bool isPrimaryKey)
    {
        Name = name;
        Type = type;
        IsPrimaryKey = isPrimaryKey;
    }
}

/// <summary>
/// テーブル定義を表します。
/// </summary>
class Table
{
    public string Name { get; set; }
    public List<Column> Columns { get; set; }
    /// <summary>
    /// Table クラスの新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="name">テーブル名。</param>
    /// <param name="columns">カラム一覧。</param>
    public Table(string name, List<Column> columns)
    {
        Name = name;
        Columns = columns;
    }
}

/// <summary>
/// テーブル間リレーション定義を表します。
/// </summary>
class Relationship
{
    public string TableA { get; set; }
    public string TableB { get; set; }
    public string Notation { get; set; }
    public string Label { get; set; }
    /// <summary>
    /// Relationship クラスの新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="tableA">リレーション元テーブル名。</param>
    /// <param name="tableB">リレーション先テーブル名。</param>
    /// <param name="notation">Mermaid 形式の多重度記号。</param>
    /// <param name="label">リレーションラベル。</param>
    public Relationship(string tableA, string tableB, string notation, string label)
    {
        TableA = tableA;
        TableB = tableB;
        Notation = notation;
        Label = label;
    }
}

// JSON読み込み用
/// <summary>
/// JSON で定義されるリレーション情報を表します。
/// </summary>
class JsonRelation
{
    public string from { get; set; }
    public string to { get; set; }
    public string type { get; set; }
    public string label { get; set; }
}

/// <summary>
/// サブシステム定義（対象テーブルとリレーション）を表します。
/// </summary>
class SubSystemDef
{
    public List<string> tables { get; set; }
    public List<JsonRelation> relations { get; set; }
}

/// <summary>
/// ER 図生成処理のエントリポイントと主要処理を提供します。
/// </summary>
class Program
{
    // 多重度タイプをMermaidの記号に変換
    /// <summary>
    /// 多重度タイプ文字列を Mermaid の記号表現に変換します。
    /// </summary>
    /// <param name="type">多重度タイプ（例: 1:N）。</param>
    /// <returns>Mermaid 形式の多重度記号。</returns>
    static string ConvertTypeToNotation(string type)
    {
        return type?.ToLower() switch
        {
            "1:1" => "||--||",        // 1対1
            "1:n" => "||--o{",        // 1対多
            "n:1" => "}o--||",        // 多対1
            "n:n" => "}o--o{",        // 多対多
            "0..1:1" => "|o--||",     // 0or1対1
            "1:0..1" => "||--o|",     // 1対0or1
            "0..1:0..1" => "|o--|o",  // 0or1対0or1
            "1:0..*" => "||--o{",     // 1対0以上
            "0..*:1" => "}o--||",     // 0以上対1
            _ => "||--o{"              // デフォルト: 1対多
        };
    }

    /// <summary>
    /// アプリケーションのメインエントリポイントです。
    /// </summary>
    /// <param name="args">コマンドライン引数。</param>
    static void Main(string[] args)
    {
        // 引数チェック
        if (args.Length < 2)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== エラー: 引数が足りません ===");
            Console.ResetColor();
            Console.WriteLine("\n【使用方法】");
            Console.WriteLine("  ERGenerator.exe <SQLファイルディレクトリ> <JSONファイルパス>");
            Console.WriteLine("\n【例】");
            Console.WriteLine("  ERGenerator.exe C:\\Database\\SQL C:\\Database\\relations.json");
            Console.WriteLine("\n【引数説明】");
            Console.WriteLine("  <SQLファイルディレクトリ>  : SQL ファイルが格納されているディレクトリ");
            Console.WriteLine("  <JSONファイルパス>          : サブシステム定義の JSON ファイルパス");
            return;
        }

        string sqlDir = args[0];
        string jsonPath = args[1];

        // ディレクトリ存在確認
        if (!Directory.Exists(sqlDir))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"エラー: SQL ディレクトリが見つかりません: {sqlDir}");
            Console.ResetColor();
            return;
        }

        // JSON ファイル存在確認
        if (!File.Exists(jsonPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"エラー: JSON ファイルが見つかりません: {jsonPath}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"SQL Directory: {sqlDir}");
        Console.WriteLine($"JSON File: {jsonPath}");

        // 1. 全ての .sql ファイルを結合して1つのDDLとして扱う
        var sqlFiles = Directory.GetFiles(sqlDir, "*.sql");
        if (sqlFiles.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"警告: SQL ファイルが見つかりません: {sqlDir}");
            Console.ResetColor();
        }
        var fullDdl = string.Join("\n", sqlFiles.Select(File.ReadAllText));

        // 2. DDLの解析
        var (allTables, relsFromSql) = ParseDDLFull(fullDdl);

        // 3. JSONファイルから追加のリレーションを読み込む
        string outputDir = Path.GetDirectoryName(jsonPath);
        
        if (File.Exists(jsonPath))
        {
            var jsonText = File.ReadAllText(jsonPath);
            var subsystems = JsonSerializer.Deserialize<Dictionary<string, SubSystemDef>>(jsonText);
            if (subsystems != null)
            {
                Console.WriteLine($"Loaded {subsystems.Count} subsystems from relations.json\n");
                
                // サブシステムごとに HTML を生成
                foreach (var kvp in subsystems)
                {
                    var subsystemName = kvp.Key;
                    var subsystem = kvp.Value;
                    Console.WriteLine($"Processing subsystem: {subsystemName}");
                    
                    // このサブシステムに含まれるテーブルをフィルタリング
                    var subsystemTables = allTables
                        .Where(t => subsystem.tables != null && subsystem.tables.IndexOf(t.Name) >= 0)
                        .ToList();
                    
                    Console.WriteLine($"  Tables: {string.Join(", ", subsystem.tables ?? new List<string>())}");
                    
                    // リレーションを変換
                    var rels = new List<Relationship>();
                    foreach (var r in subsystem.relations)
                    {
                        string notation = ConvertTypeToNotation(r.type);
                        rels.Add(new Relationship(r.from, r.to, notation, r.label));
                        Console.WriteLine($"  Relation: {r.from} {notation} {r.to} : \"{r.label}\"");
                    }
                    
                    // Mermaid生成
                    var mermaid = GenerateMermaid(subsystemTables, rels);
                    
                    // HTML生成
                    string htmlContent = GenerateHtml(mermaid);
                    
                    // ファイル出力
                    string outputPath = Path.Combine(outputDir, $"{subsystemName}.html");
                    File.WriteAllText(outputPath, htmlContent);
                    Console.WriteLine($"  Generated: {outputPath}\n");
                }
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ すべてのサブシステム図が生成されました！");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Mermaid 定義文字列を埋め込んだ表示用 HTML を生成します。
    /// </summary>
    /// <param name="mermaid">Mermaid のER図定義。</param>
    /// <returns>生成された HTML 文字列。</returns>
    static string GenerateHtml(string mermaid)
    {
        return $@"<!DOCTYPE html>
<html lang=""ja"">
<head>
    <meta charset=""UTF-8"">
    <title>ER Diagram</title>
    <style>
        body {{ font-family: sans-serif; margin: 0; padding: 0; background: #ffffff; overflow: hidden; }}
        #container {{ width: 100vw; height: 100vh; overflow: auto; background: #ffffff; padding-top: 88px; box-sizing: border-box; }}
        #diagram {{ background: #ffffff; padding: 40px; width: max-content; min-width: 100%; box-sizing: border-box; }}
        #zoom-controls {{ position: fixed; top: 20px; right: 20px; z-index: 1000; display: flex; align-items: center; gap: 10px; }}
        #zoom-bar {{ display: inline-flex; align-items: center; background: #007bff; border-radius: 6px; overflow: hidden; box-shadow: 0 2px 5px rgba(0,0,0,0.2); }}
        .zoom-btn {{ width: 42px; height: 38px; border: none; background: transparent; color: #ffffff; font-size: 22px; line-height: 1; cursor: pointer; }}
        .zoom-btn:hover {{ background: #0056b3; }}
        #zoom-level {{ min-width: 74px; text-align: center; color: #ffffff; font-size: 20px; font-weight: 600; letter-spacing: 0.5px; }}
        #actions {{ display: inline-flex; gap: 8px; }}
        .action-btn {{ padding: 8px 12px; cursor: pointer; background: #007bff; color: white; border: none; border-radius: 4px; font-size: 14px; }}
        .action-btn:hover {{ background: #0056b3; }}
        #layout-controls {{ display: inline-flex; align-items: center; gap: 8px; background: #007bff; padding: 6px 8px; border-radius: 4px; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }}
        #layout-controls label {{ color: #ffffff; font-size: 13px; font-weight: 600; }}
        #node-placement {{ height: 32px; padding: 0 8px; border: 1px solid #0056b3; border-radius: 4px; background: #ffffff; color: #1f2937; font-size: 13px; }}
        #node-placement:hover {{ background: #eef5ff; }}
        #node-placement option {{ background: #ffffff; color: #1f2937; }}
        .table-title-clickable {{ cursor: pointer; text-decoration: underline; text-underline-offset: 2px; }}
        #error-message {{ position: fixed; left: 20px; bottom: 20px; max-width: 560px; padding: 12px 16px; color: #b00020; background: rgba(255,255,255,0.95); border: 1px solid #f2b8c4; border-radius: 6px; box-shadow: 0 2px 5px rgba(0,0,0,0.08); display: none; white-space: pre-wrap; }}
    </style>
</head>
<body>
    <div id=""zoom-controls"">
        <div id=""zoom-bar"">
            <button class=""zoom-btn"" onclick=""zoomOut()"" aria-label=""縮小"">−</button>
            <div id=""zoom-level"">100%</div>
            <button class=""zoom-btn"" onclick=""zoomIn()"" aria-label=""拡大"">+</button>
        </div>
        <div id=""actions"">
            <button class=""action-btn"" onclick=""resetZoom()"">リセット</button>
            <button class=""action-btn"" onclick=""expandAll()"">一括展開</button>
            <button class=""action-btn"" onclick=""collapseAll()"">一括クローズ</button>
        </div>
        <div id=""layout-controls"">
            <label for=""node-placement"">ELK配置</label>
            <select id=""node-placement"" onchange=""onNodePlacementChange()"">
                <option value=""BRANDES_KOEPF"" selected>BRANDES_KOEPF (default)</option>
                <option value=""LINEAR_SEGMENTS"">LINEAR_SEGMENTS</option>
                <option value=""NETWORK_SIMPLEX"">NETWORK_SIMPLEX</option>
                <option value=""SIMPLE"">SIMPLE</option>
            </select>
        </div>
    </div>
    <div id=""container"">
        <div id=""diagram""></div>
    </div>
    <div id=""error-message""></div>

    <script type=""module"">
        import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
        import elkLayouts from 'https://cdn.jsdelivr.net/npm/@mermaid-js/layout-elk@0/dist/mermaid-layout-elk.esm.min.mjs';

        let currentScale = 1;
        const sourceMermaid = `{mermaid}`;
        const diagram = document.getElementById('diagram');
        const errorMessage = document.getElementById('error-message');
        const zoomLevel = document.getElementById('zoom-level');
        const nodePlacementSelect = document.getElementById('node-placement');
        const collapsedTables = new Set();
        const parsed = parseMermaid(sourceMermaid);

        mermaid.registerLayoutLoaders(elkLayouts);

        function parseMermaid(text) {{
            const lines = text.split(/\r?\n/);
            const tables = [];
            const relations = [];
            let currentTable = null;

            for (const raw of lines) {{
                const line = raw.trim();
                if (!line || line === 'erDiagram') {{
                    continue;
                }}

                const tableStart = line.match(/^([\w.]+)\s*\{{$/);
                if (tableStart) {{
                    currentTable = {{ name: tableStart[1], columns: [] }};
                    tables.push(currentTable);
                    continue;
                }}

                if (line === '}}' && currentTable) {{
                    currentTable = null;
                    continue;
                }}

                if (currentTable) {{
                    currentTable.columns.push(line);
                    continue;
                }}

                relations.push(line);
            }}

            return {{ tables, relations }};
        }}

        function buildMermaid() {{
            const strategy = nodePlacementSelect?.value || 'BRANDES_KOEPF';
            const lines = [
                '---',
                'config:',
                '  layout: elk',
                '  elk:',
                `    nodePlacementStrategy: ${{strategy}}`,
                '---',
                'erDiagram'
            ];

            for (const table of parsed.tables) {{
                lines.push(`    ${{table.name}} {{`);
                if (!collapsedTables.has(table.name)) {{
                    for (const column of table.columns) {{
                        lines.push(`        ${{column}}`);
                    }}
                }}
                lines.push('    }}');
            }}

            for (const relation of parsed.relations) {{
                lines.push(`    ${{relation}}`);
            }}

            return lines.join('\n');
        }}

        function updateScale() {{
            const svg = diagram.querySelector('svg');
            if (!svg) {{
                return;
            }}

            const viewBox = svg.viewBox?.baseVal;
            const baseWidth = viewBox && viewBox.width > 0 ? viewBox.width : svg.getBoundingClientRect().width;
            const baseHeight = viewBox && viewBox.height > 0 ? viewBox.height : svg.getBoundingClientRect().height;

            svg.style.display = 'block';
            svg.style.width = `${{baseWidth}}px`;
            svg.style.height = `${{baseHeight}}px`;
            svg.style.transform = `scale(${{currentScale}})`;
            svg.style.transformOrigin = 'top left';

            // transform は見た目だけ拡大するため、親要素のサイズも明示的に拡大してスクロール領域を確保する
            const padding = 80; // left + right (40px + 40px)
            diagram.style.width = `${{(baseWidth * currentScale) + padding}}px`;
            diagram.style.minWidth = `${{Math.max((baseWidth * currentScale) + padding, window.innerWidth)}}px`;
            diagram.style.height = `${{(baseHeight * currentScale) + padding}}px`;

            const percent = Math.round(currentScale * 100);
            zoomLevel.textContent = `${{percent}}%`;
        }}

        function zoomIn() {{
            currentScale += 0.1;
            updateScale();
        }}

        function zoomOut() {{
            if (currentScale > 0.3) {{
                currentScale -= 0.1;
                updateScale();
            }}
        }}

        function resetZoom() {{
            currentScale = 1;
            updateScale();
        }}

        function expandAll() {{
            collapsedTables.clear();
            renderDiagram();
        }}

        function collapseAll() {{
            for (const table of parsed.tables) {{
                collapsedTables.add(table.name);
            }}
            renderDiagram();
        }}

        function toggleTable(tableName) {{
            if (collapsedTables.has(tableName)) {{
                collapsedTables.delete(tableName);
            }} else {{
                collapsedTables.add(tableName);
            }}
            renderDiagram();
        }}

        function onNodePlacementChange() {{
            renderDiagram();
        }}

        function bindTableTitleClicks() {{
            const titleTexts = diagram.querySelectorAll('text');
            for (const node of titleTexts) {{
                const label = (node.textContent || '').trim();
                if (!parsed.tables.some(t => t.name === label)) {{
                    continue;
                }}

                const marker = collapsedTables.has(label) ? ' [▶]' : ' [▼]';
                node.textContent = `${{label}}${{marker}}`;
                node.classList.add('table-title-clickable');
                node.addEventListener('click', () => toggleTable(label));
            }}
        }}

        async function renderDiagram() {{
            const mermaidText = buildMermaid();
            const id = `er-diagram-${{Date.now()}}`;
            errorMessage.style.display = 'none';

            try {{
                const result = await mermaid.render(id, mermaidText);
                diagram.innerHTML = result.svg;
                bindTableTitleClicks();
                updateScale();
            }} catch (error) {{
                diagram.innerHTML = '';
                errorMessage.textContent = `Mermaid の描画に失敗しました。\n${{error instanceof Error ? error.message : String(error)}}`;
                errorMessage.style.display = 'block';
                throw error;
            }}
        }}

        // Ctrl + スクロールでもズーム可能
        document.getElementById('container').addEventListener('wheel', (e) => {{
            if (e.ctrlKey) {{
                e.preventDefault();
                if (e.deltaY < 0) {{
                    zoomIn();
                }} else {{
                    zoomOut();
                }}
            }}
        }});

        window.zoomIn = zoomIn;
        window.zoomOut = zoomOut;
        window.resetZoom = resetZoom;
        window.expandAll = expandAll;
        window.collapseAll = collapseAll;
        window.onNodePlacementChange = onNodePlacementChange;

        mermaid.initialize({{ startOnLoad: false, layout: 'elk' }});
        renderDiagram();
    </script>
</body>
</html>";
    }

    /// <summary>
    /// DDL 全文を解析してテーブル定義とリレーションを抽出します。
    /// </summary>
    /// <param name="ddl">解析対象の DDL 文字列。</param>
    /// <returns>抽出されたテーブル一覧とリレーション一覧。</returns>
    static (List<Table> Tables, List<Relationship> Rels) ParseDDLFull(string ddl)
    {
        var tables = new List<Table>();
        var rels = new List<Relationship>();
        var tableRegex = new Regex(@"CREATE\s+TABLE\s+([\w.]+)\s*\((.*?)\)\s*(?:COMMENT\s+'.*?')?\s*;", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        /// <summary>
        /// スキーマ名付きテーブル名を正規化してテーブル名のみを返します。
        /// </summary>
        /// <param name="rawTableName">生のテーブル名。</param>
        /// <returns>正規化したテーブル名。</returns>
        static string NormalizeTableName(string rawTableName)
        {
            return rawTableName.Contains('.') ? rawTableName.Split('.').Last() : rawTableName;
        }

        /// <summary>
        /// カラム定義行からカラム名と型を抽出します。
        /// </summary>
        /// <param name="line">解析対象の1行。</param>
        /// <returns>抽出結果。解析できない場合は null。</returns>
        static (string Name, string Type)? ParseColumnDefinition(string line)
        {
            // 制約語（NOT NULL, DEFAULT など）より前を型として抽出する
            var match = Regex.Match(
                line,
                @"^(?<name>\S+)\s+(?<rest>.+)$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return null;
            }

            var columnName = match.Groups["name"].Value;
            var rest = match.Groups["rest"].Value;
            var constraintKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NOT", "NULL", "DEFAULT", "PRIMARY", "UNIQUE", "CHECK", "REFERENCES", "CONSTRAINT", "COMMENT"
            };

            var rawTokens = Regex.Split(rest.Trim(), @"\s+").Where(t => !string.IsNullOrWhiteSpace(t));
            var typeTokens = new List<string>();
            foreach (var token in rawTokens)
            {
                var normalized = token.Trim().TrimEnd(',');
                if (constraintKeywords.Contains(normalized))
                {
                    break;
                }
                typeTokens.Add(normalized);
            }

            var typePart = string.Join(" ", typeTokens);
            var withoutSize = Regex.Replace(typePart, @"\s*\([^)]*\)", "");
            var columnType = Regex.Replace(withoutSize, @"\s+", " ").Trim().TrimEnd(',');

            if (string.IsNullOrEmpty(columnType))
            {
                return null;
            }

            return (columnName, columnType);
        }

        foreach (Match tableMatch in tableRegex.Matches(ddl))
        {
            var tableName = NormalizeTableName(tableMatch.Groups[1].Value);
            var body = tableMatch.Groups[2].Value;

            // PK/FK 解析
            var pkCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pkMatch = Regex.Match(body, @"PRIMARY KEY\s*\((.*?)\)", RegexOptions.IgnoreCase);
            if (pkMatch.Success) foreach (var p in pkMatch.Groups[1].Value.Split(',').Select(x => x.Trim())) pkCols.Add(p);

            var fkRegex = new Regex(@"FOREIGN KEY\s*\((.*?)\)\s+REFERENCES\s+([\w.]+)", RegexOptions.IgnoreCase);
            foreach (Match fkMatch in fkRegex.Matches(body))
            {
                var refTableName = NormalizeTableName(fkMatch.Groups[2].Value);
                rels.Add(new Relationship(refTableName, tableName, "||--o{", "fk"));
            }

            // カラム解析
            var columns = new List<Column>();
            var lines = body.Split('\n');
            foreach (var rawLine in lines)
            {
                // 行内コメントを除去し、カンマ終端を吸収する
                var line = Regex.Replace(rawLine, @"--.*$", "").Trim().TrimEnd(',').Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) || line.StartsWith("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)) continue;
                var parsed = ParseColumnDefinition(line);
                if (!parsed.HasValue) continue;
                var isPK = line.ToUpper().Contains("PRIMARY KEY") || (pkCols != null && pkCols.Contains(parsed.Value.Name));
                Console.WriteLine($"  [DDL] Table={tableName}, Column={parsed.Value.Name}, Type={parsed.Value.Type}, PK={isPK}");
                columns.Add(new Column(parsed.Value.Name, parsed.Value.Type, isPK));
            }
            tables.Add(new Table(tableName, columns));
        }
        return (tables, rels);
    }

    /// <summary>
    /// テーブル定義とリレーションから Mermaid ER 図文字列を生成します。
    /// </summary>
    /// <param name="tables">描画対象テーブル一覧。</param>
    /// <param name="rels">描画対象リレーション一覧。</param>
    /// <returns>Mermaid ER 図文字列。</returns>
    static string GenerateMermaid(List<Table> tables, List<Relationship> rels)
    {
        var sb = new StringBuilder("erDiagram\n");
        foreach (var t in tables)
        {
            sb.AppendLine($"    {t.Name} {{");
            foreach (var c in t.Columns)
            {
                // Mermaid ERDの型名は空白をアンダースコアに置換
                var mermaidType = c.Type.Replace(" ", "_");
                sb.AppendLine($"        {mermaidType} {c.Name} {(c.IsPrimaryKey ? "PK" : "")}");
            }
            sb.AppendLine("    }");
        }
        foreach (var r in rels) sb.AppendLine($"    {r.TableA} {r.Notation} {r.TableB} : \"{r.Label}\"");
        return sb.ToString();
    }
}