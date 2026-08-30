using System.Text;
using LiasseFiscale.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LiasseFiscale.Tests;

public class XmlValidationServiceIntegrationTests
{
    private readonly XmlValidationService _validationService;
    private readonly AssertRuleEngine _ruleEngine;

    public XmlValidationServiceIntegrationTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SchemaAssets:StructuralPath"] = "SchemaAssets/structural",
                ["SchemaAssets:RulesPath"] = "SchemaAssets/rules"
            })
            .Build();

        _ruleEngine = new AssertRuleEngine(config, NullLogger<AssertRuleEngine>.Instance);
        _validationService = new XmlValidationService(config, _ruleEngine, NullLogger<XmlValidationService>.Instance);
    }

    [Fact]
    public void ValiderStructure_DocumentInexistant_RetourneErreur()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<test/>"));
        var resultat = _validationService.ValiderStructure("F9999", stream);

        Assert.False(resultat.EstValide);
        Assert.Contains(resultat.Erreurs, e => e.Message.Contains("F9999"));
    }

    [Fact]
    public void ValiderDocumentComplet_XmlMalForme_DetecteErreurStructurelle()
    {
        string malformedXml = "<F6001><Entete><MatriculeFiscalDeclarant>1234567APM000</Entete>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(malformedXml));

        var resultat = _validationService.ValiderDocumentComplet("F6001", stream);

        Assert.False(resultat.EstValide);
        Assert.Contains(resultat.Erreurs, e => e.Source == "Structurelle");
    }

    [Fact]
    public void AssertRuleEngine_ValiderDocument_F6001_DetecteIncoherence()
    {
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<F6001 xmlns=""http://www.impots.finances.gov.tn/liasse"">
    <VersionDocument>1.0</VersionDocument>
    <Entete>
        <MatriculeFiscalDeclarant>1234567APM000</MatriculeFiscalDeclarant>
        <Exercice>2024</Exercice>
    </Entete>
    <Details>
        <F60010001>250</F60010001>
        <F60010002>100</F60010002>
        <F60010031>50</F60010031>
    </Details>
</F6001>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var erreurs = _ruleEngine.ValiderDocument("F6001", stream);

        Assert.NotEmpty(erreurs);
        Assert.Contains(erreurs, e => e.Source == "RegleMetier" && e.Champ == "F60010001");
    }

    [Fact]
    public void AssertRuleEngine_ObtenirTableReglesSimples_RetourneReglesF6001()
    {
        var table = _ruleEngine.ObtenirTableReglesSimples("F6001");

        Assert.NotEmpty(table);
        Assert.True(table.ContainsKey("F60010001"));
        Assert.Contains("F60010002", table["F60010001"]);
        Assert.Contains("F60010031", table["F60010001"]);
    }

    [Fact]
    public void ValiderDocumentComplet_F6001_Complet_100PourcentValide()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples", "F6001-1234567A-2024.xml");
        if (!File.Exists(samplePath))
        {
            samplePath = Path.Combine(Directory.GetCurrentDirectory(), "Samples", "F6001-1234567A-2024.xml");
        }

        Assert.True(File.Exists(samplePath), $"Fichier d'exemple non trouvé : {samplePath}");

        using var stream = File.OpenRead(samplePath);
        var resultat = _validationService.ValiderDocumentComplet("F6001", stream);

        Assert.True(resultat.EstValide, string.Join("; ", resultat.Erreurs.Select(e => $"[{e.Source}] {e.Champ}: {e.Message}")));
        Assert.Empty(resultat.Erreurs);
    }

    [Fact]
    public void ValiderDocumentComplet_F6002_Complet_100PourcentValide()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples", "F6002-1234567A-2024.xml");
        if (!File.Exists(samplePath))
        {
            samplePath = Path.Combine(Directory.GetCurrentDirectory(), "Samples", "F6002-1234567A-2024.xml");
        }

        Assert.True(File.Exists(samplePath), $"Fichier d'exemple non trouvé : {samplePath}");

        using var stream = File.OpenRead(samplePath);
        var resultat = _validationService.ValiderDocumentComplet("F6002", stream);

        Assert.True(resultat.EstValide, string.Join("; ", resultat.Erreurs.Select(e => $"[{e.Source}] {e.Champ}: {e.Message}")));
        Assert.Empty(resultat.Erreurs);
    }

    [Fact]
    public void ValiderDocumentComplet_F6201_Complet_100PourcentValide()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples", "F6201-1234567A-2024.xml");
        if (!File.Exists(samplePath))
        {
            samplePath = Path.Combine(Directory.GetCurrentDirectory(), "Samples", "F6201-1234567A-2024.xml");
        }

        Assert.True(File.Exists(samplePath), $"Fichier d'exemple non trouvé : {samplePath}");

        using var stream = File.OpenRead(samplePath);
        var resultat = _validationService.ValiderDocumentComplet("F6201", stream);

        Assert.True(resultat.EstValide, string.Join("; ", resultat.Erreurs.Select(e => $"[{e.Source}] {e.Champ}: {e.Message}")));
        Assert.Empty(resultat.Erreurs);
    }

    [Fact]
    public void ValiderDocumentComplet_F6003_Complet_100PourcentValide()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples", "F6003-1234567A-2024.xml");
        if (!File.Exists(samplePath))
        {
            samplePath = Path.Combine(Directory.GetCurrentDirectory(), "Samples", "F6003-1234567A-2024.xml");
        }

        Assert.True(File.Exists(samplePath), $"Fichier d'exemple non trouvé : {samplePath}");

        using var stream = File.OpenRead(samplePath);
        var resultat = _validationService.ValiderDocumentComplet("F6003", stream);

        Assert.True(resultat.EstValide, string.Join("; ", resultat.Erreurs.Select(e => $"[{e.Source}] {e.Champ}: {e.Message}")));
        Assert.Empty(resultat.Erreurs);
    }

    [Fact]
    public void ValiderDocumentComplet_F6004_Complet_100PourcentValide()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples", "F6004-1234567A-2024.xml");
        if (!File.Exists(samplePath))
        {
            samplePath = Path.Combine(Directory.GetCurrentDirectory(), "Samples", "F6004-1234567A-2024.xml");
        }

        Assert.True(File.Exists(samplePath), $"Fichier d'exemple non trouvé : {samplePath}");

        using var stream = File.OpenRead(samplePath);
        var resultat = _validationService.ValiderDocumentComplet("F6004", stream);

        Assert.True(resultat.EstValide, string.Join("; ", resultat.Erreurs.Select(e => $"[{e.Source}] {e.Champ}: {e.Message}")));
        Assert.Empty(resultat.Erreurs);
    }
}


