using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// データ構造
class Column
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool IsPrimaryKey { get; set; }
    public Column(string name, string type, bool isPrimaryKey)
    {
        Name = name;
        Type = type;
        IsPrimaryKey = isPrimaryKey;
    }
}

class Table
{
    public string Name { get; set; }
    public List<Column> Columns { get; set; }
    public Table(string name, List<Column> columns)
    {
        Name = name;
        Columns = columns;
    }
}

class Relationship
{
    public string TableA { get; set; }
    public string TableB { get; set; }
    public string Notation { get; set; }
    public string Label { get; set; }
    public Relationship(string tableA, string tableB, string notation, string label)
    {
        TableA = tableA;
        TableB = tableB;
        Notation = notation;
        Label = label;
    }
}

// JSON読み込み用
class JsonRelation
{
    public string from { get; set; }
    public string to { get; set; }
    public string type { get; set; }
    public string label { get; set; }
}

class SubSystemDef
{
    public List<string> tables { get; set; }
    public List<JsonRelation> relations { get; set; }
}

class Program
{
    // 多重度タイプをMermaidの記号に変換
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

    static string GenerateHtml(string mermaid)
    {
        return $@"<!DOCTYPE html>
<html lang=""ja"">
<head>
    <meta charset=""UTF-8"">
    <title>ER Diagram</title>
    <script src=""https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js""></script>
    <style>
        body {{ font-family: sans-serif; margin: 0; padding: 0; background: #f9f9f9; overflow: hidden; }}
        #container {{ width: 100vw; height: 100vh; display: flex; align-items: center; justify-content: center; overflow: auto; background: #f9f9f9; }}
        .mermaid {{ background: white; padding: 40px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        #zoom-controls {{ position: fixed; top: 20px; right: 20px; z-index: 1000; background: white; padding: 10px; border-radius: 4px; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }}
        button {{ padding: 8px 12px; margin: 5px; cursor: pointer; background: #007bff; color: white; border: none; border-radius: 4px; font-size: 14px; }}
        button:hover {{ background: #0056b3; }}
    </style>
</head>
<body>
    <div id=""zoom-controls"">
        <button onclick=""zoomIn()"">🔍+ 拡大</button>
        <button onclick=""zoomOut()"">🔍- 縮小</button>
        <button onclick=""resetZoom()"">リセット</button>
    </div>
    <div id=""container"">
        <pre id=""mermaid-content"" class=""mermaid"">
{mermaid}
        </pre>
    </div>

    <script>
        let currentScale = 1;
        const content = document.getElementById('mermaid-content');

        function updateScale() {{
            content.style.transform = `scale(${{currentScale}})`;
            content.style.transformOrigin = 'top center';
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

        mermaid.initialize({{startOnLoad: true}});
        mermaid.contentLoaded();
    </script>
</body>
</html>";
    }

    static (List<Table> Tables, List<Relationship> Rels) ParseDDLFull(string ddl)
    {
        var tables = new List<Table>();
        var rels = new List<Relationship>();
        var tableRegex = new Regex(@"CREATE\s+TABLE\s+([\w.]+)\s*\((.*?)\)\s*(?:COMMENT\s+'.*?')?\s*;", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        static string NormalizeTableName(string rawTableName)
        {
            return rawTableName.Contains('.') ? rawTableName.Split('.').Last() : rawTableName;
        }

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
                columns.Add(new Column(parsed.Value.Name, parsed.Value.Type, isPK));
            }
            tables.Add(new Table(tableName, columns));
        }
        return (tables, rels);
    }

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