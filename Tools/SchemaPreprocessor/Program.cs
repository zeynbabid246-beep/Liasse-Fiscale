using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Outil de build, a lancer manuellement (dotnet run) depuis Tools/SchemaPreprocessor
// quand les XSD originaux changent. Ne fait PAS partie du pipeline de requetes de l'API.
//
// Usage : dotnet run -- <dossier_xsd_originaux> <dossier_structural_sortie> <dossier_rules_sortie>
//
// IMPORTANT : chaque document (F6001, F6002, ...) est APLATI en un seul fichier XSD
// autonome (fusion de Typescommuns.xsd + Entete.xsd + le document, sans xsd:include).
// Raison (confirmee en test reel) : F6001.xsd inclut Typescommuns.xsd ET Entete.xsd, et
// Entete.xsd inclut LUI-MEME Typescommuns.xsd (pattern diamant). System.Xml.Schema.XmlSchemaSet
// ne fusionne pas correctement ce cas (l'inclusion d'Entete.xsd est silencieusement ignoree,
// d'ou l'erreur "Type T_Entete is not declared" constatee). Un fichier aplati, sans aucun
// xsd:include, elimine le probleme a la racine.
//
// F6005 est exclu de l'aplatissement : il utilise en plus xsd:alternative (XSD 1.1),
// une fonctionnalite distincte des assertions, qui necessite un traitement dedie.

if (args.Length != 3)
{
    Console.WriteLine("Usage : dotnet run -- <original> <structural> <rules>");
    return 1;
}

var (origDir, structDir, rulesDir) = (args[0], args[1], args[2]);
Directory.CreateDirectory(structDir);
Directory.CreateDirectory(rulesDir);

var assertRegex = new Regex("<xsd:assert test=\"([^\"]*)\"\\s*/>", RegexOptions.Compiled);
var simpleSumRegex = new Regex(@"^lf:([A-Za-z0-9]+)\s*=\s*sum\(\s*\(\s*(.*?)\s*\)\s*\)$", RegexOptions.Compiled);
var operandRegex = new Regex("lf:([A-Za-z0-9]+)", RegexOptions.Compiled);

(XElement root, List<XElement> components) LoadComponents(string path)
{
    var raw = File.ReadAllText(path);
    var withoutAsserts = Regex.Replace(raw, "<xsd:assert[^/]*/>\r?\n?", string.Empty);
    var doc = XDocument.Parse(withoutAsserts);
    var root = doc.Root!;
    var components = root.Elements()
        .Where(e => e.Name.LocalName != "include" && e.Name.LocalName != "import")
        .ToList();
    return (root, components);
}

string Flatten(string docFileName, string typesPath, string entetePath)
{
    var (docRoot, docComponents) = LoadComponents(Path.Combine(origDir, docFileName));
    var (_, typesComponents) = LoadComponents(typesPath);
    var (_, enteteComponents) = LoadComponents(entetePath);

    var seen = new HashSet<(string tag, string? name)>();
    var merged = new List<XElement>();

    foreach (var group in new[] { typesComponents, enteteComponents, docComponents })
    {
        foreach (var el in group)
        {
            var key = (el.Name.LocalName, el.Attribute("name")?.Value);
            if (!seen.Add(key))
            {
                continue;
            }
            merged.Add(el);
        }
    }

    var newRoot = new XElement(docRoot.Name, docRoot.Attributes(), merged);
    var outPath = Path.Combine(structDir, docFileName);
    new XDocument(new XDeclaration("1.0", "UTF-8", null), newRoot).Save(outPath);
    return outPath;
}

var typesPath = Path.Combine(origDir, "Typescommuns.xsd");
var entetePath = Path.Combine(origDir, "Entete.xsd");

var allXsdFiles = Directory.GetFiles(origDir, "F*.xsd");
foreach (var file in allXsdFiles)
{
    var docFile = Path.GetFileName(file);
    if (docFile.Equals("Typescommuns.xsd", StringComparison.OrdinalIgnoreCase) ||
        docFile.Equals("Entete.xsd", StringComparison.OrdinalIgnoreCase) ||
        docFile.Equals("F6005.xsd", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    try
    {
        var outPath = Flatten(docFile, typesPath, entetePath);
        Console.WriteLine($"{docFile} -> aplati dans {outPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de l'aplatissement de {docFile} : {ex.Message}");
    }
}

foreach (var file in Directory.GetFiles(origDir, "*.xsd"))
{
    var fileName = Path.GetFileName(file);
    var content = await File.ReadAllTextAsync(file);
    var asserts = assertRegex.Matches(content).Select(m => m.Groups[1].Value).ToList();

    if (asserts.Count == 0)
    {
        continue;
    }

    var simpleRules = new List<object>();
    var complexRules = new List<object>();

    foreach (var test in asserts)
    {
        var m = simpleSumRegex.Match(test);
        if (m.Success)
        {
            var target = m.Groups[1].Value;
            var operands = operandRegex.Matches(m.Groups[2].Value).Select(x => x.Groups[1].Value).ToList();
            simpleRules.Add(new { target, operation = "sum", operands, rawTest = test });
        }
        else
        {
            complexRules.Add(new { rawTest = test });
        }
    }

    var docName = Path.GetFileNameWithoutExtension(fileName);
    var rulesDoc = new
    {
        document = docName,
        totalAssertions = asserts.Count,
        simpleSumRules = simpleRules,
        complexRules
    };

    var json = JsonSerializer.Serialize(rulesDoc, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(rulesDir, $"{docName}.rules.json"), json);

    Console.WriteLine($"{fileName}: {asserts.Count} assertions -> {simpleRules.Count} simples, {complexRules.Count} complexes");
}

return 0;