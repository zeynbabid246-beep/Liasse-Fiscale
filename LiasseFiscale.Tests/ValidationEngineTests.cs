using System.Text;
using System.Text.RegularExpressions;
using LiasseFiscale.Api.Services;
using Xunit;

namespace LiasseFiscale.Tests;

public class ValidationEngineTests
{
    private static readonly Regex NomFichierRegex =
        new(@"^(?<code>F\d{4}(-MODELE-AUT)?)-(?<matricule>[0-9]{7}[ABCDEFGHJKLMNPQRSTVWXYZ])-(?<exercice>\d{4})\.xml$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void XPathEvaluator_SommeSimple_Valide()
    {
        var context = new EvaluationContext();
        context.FieldValues["F60010001"] = 100m;
        context.FieldValues["F60010002"] = 40m;
        context.FieldValues["F60010031"] = 60m;

        string formula = "lf:F60010001 = sum( ((lf:F60010002) , (lf:F60010031)))";
        bool result = XPathAssertEvaluator.Evaluate(formula, context, out var message);

        Assert.True(result);
        Assert.Empty(message);
    }

    [Fact]
    public void XPathEvaluator_SommeSimple_Invalide()
    {
        var context = new EvaluationContext();
        context.FieldValues["F60010001"] = 100m;
        context.FieldValues["F60010002"] = 40m;
        context.FieldValues["F60010031"] = 50m; // 40 + 50 = 90 != 100

        string formula = "lf:F60010001 = sum( ((lf:F60010002) , (lf:F60010031)))";
        bool result = XPathAssertEvaluator.Evaluate(formula, context, out var message);

        Assert.False(result);
        Assert.Contains("attendu", message);
    }

    [Fact]
    public void XPathEvaluator_SoustractionAvecCoefficientMoinsUn_Valide()
    {
        var context = new EvaluationContext();
        context.FieldValues["F60030003"] = 70m;
        context.FieldValues["F60030004"] = 100m;
        context.FieldValues["F60030005"] = 30m;

        string formula = "lf:F60030003 = sum( ((lf:F60030004) , (-1)*(lf:F60030005)))";
        bool result = XPathAssertEvaluator.Evaluate(formula, context, out var message);

        Assert.True(result);
    }

    [Fact]
    public void XPathEvaluator_FormuleComplexeF6004_MultiplesSoustractions_Valide()
    {
        var context = new EvaluationContext();
        context.FieldValues["F60040001"] = 50m;
        context.FieldValues["F60040002"] = 100m;
        context.FieldValues["F60040012"] = 20m;
        context.FieldValues["F60040023"] = 10m;
        context.FieldValues["F60040032"] = 15m;
        context.FieldValues["F60040045"] = 5m;
        // 100 - 20 - 10 - 15 - 5 = 50

        string formula = "lf:F60040001 = sum( ((lf:F60040002) , (-1)*(lf:F60040012) , (-1)*(lf:F60040023) , (-1)*(lf:F60040032) , (-1)*(lf:F60040045)))";
        bool result = XPathAssertEvaluator.Evaluate(formula, context, out var message);

        Assert.True(result);
    }

    [Fact]
    public void XPathEvaluator_ConditionnelF6005_SocieteDeCapitaux_AccepteValeur()
    {
        var context = new EvaluationContext();
        context.FieldValues["F60050026"] = 1500m;
        context.AttributeValues["F60050000/@codeformejuridique"] = "SC";

        string formula = "lf:F60050026 = (if (lf:F60050000/@codeformejuridique eq 'SC' ) then lf:F60050026 else 0 )";
        bool result = XPathAssertEvaluator.Evaluate(formula, context, out var message);

        Assert.True(result);
    }

    [Fact]
    public void XPathEvaluator_ConditionnelF6005_PersonnePhysique_RejetteValeurNonNulle()
    {
        var context = new EvaluationContext();
        context.FieldValues["F60050026"] = 1500m; // non autorisé pour PP
        context.AttributeValues["F60050000/@codeformejuridique"] = "PP";

        string formula = "lf:F60050026 = (if (lf:F60050000/@codeformejuridique eq 'SC' ) then lf:F60050026 else 0 )";
        bool result = XPathAssertEvaluator.Evaluate(formula, context, out var message);

        Assert.False(result);
    }

    [Fact]
    public void XPathEvaluator_ConditionnelF6005_PersonnePhysique_AccepteZero()
    {
        var context = new EvaluationContext();
        context.FieldValues["F60050026"] = 0m;
        context.AttributeValues["F60050000/@codeformejuridique"] = "PP";

        string formula = "lf:F60050026 = (if (lf:F60050000/@codeformejuridique eq 'SC' ) then lf:F60050026 else 0 )";
        bool result = XPathAssertEvaluator.Evaluate(formula, context, out var message);

        Assert.True(result);
    }

    [Fact]
    public void XPathEvaluator_ConditionnelF6005_OuLogique_PP_SP()
    {
        var context = new EvaluationContext();
        context.FieldValues["F60051003"] = 250m;
        context.AttributeValues["F60051000/@codeformejuridique"] = "SP";

        string formula = "lf:F60051003 = (if (lf:F60051000/@codeformejuridique eq 'PP' or lf:F60051000/@codeformejuridique eq 'SP') then lf:F60051003 else 0 )";
        bool result = XPathAssertEvaluator.Evaluate(formula, context, out var message);

        Assert.True(result);
    }

    [Fact]
    public void XmlFieldExtractor_ExtraitEnteteEtValeursCorrectement()
    {
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<F6001 xmlns=""http://www.impots.finances.gov.tn/liasse"">
    <VersionDocument>1.0</VersionDocument>
    <Entete>
        <MatriculeFiscalDeclarant>1234567APM000</MatriculeFiscalDeclarant>
        <NometPrenomouRaisonSociale>STE EXEMPLE</NometPrenomouRaisonSociale>
        <Activite>Informatique</Activite>
        <Adresse>Tunis</Adresse>
        <Exercice>2024</Exercice>
        <DateDebutExercice>2024-01-01</DateDebutExercice>
        <DateClotureExercice>2024-12-31</DateClotureExercice>
        <ActeDeDepot>0</ActeDeDepot>
        <NatureDepot>1</NatureDepot>
    </Entete>
    <Details>
        <F60010001>100.5</F60010001>
        <F60010002>40.5</F60010002>
        <F60010031>60.0</F60010031>
    </Details>
</F6001>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var data = XmlFieldExtractor.ExtraireTout(stream);

        Assert.Equal("1234567APM000", data.Header.MatriculeFiscalDeclarant);
        Assert.Equal(2024, data.Header.Exercice);
        Assert.Equal("STE EXEMPLE", data.Header.RaisonSociale);
        Assert.Equal(100.5m, data.Context.FieldValues["F60010001"]);
        Assert.Equal(40.5m, data.Context.FieldValues["F60010002"]);
        Assert.Equal(60.0m, data.Context.FieldValues["F60010031"]);
    }

    [Theory]
    [InlineData("F6001-1234567A-2024.xml", true, "F6001", "1234567A", 2024)]
    [InlineData("F6004-MODELE-AUT-7654321B-2023.xml", true, "F6004-MODELE-AUT", "7654321B", 2023)]
    [InlineData("F6001-1234567A-2024.pdf", false, null, null, 0)]
    [InlineData("INVALID_NAME.xml", false, null, null, 0)]
    [InlineData("F6001-12345-2024.xml", false, null, null, 0)]
    public void NomFichier_ValidationPattern(string nomFichier, bool valide, string? codeAttendu, string? matriculeAttendu, int exerciceAttendu)
    {
        var match = NomFichierRegex.Match(nomFichier);
        Assert.Equal(valide, match.Success);

        if (valide)
        {
            Assert.Equal(codeAttendu, match.Groups["code"].Value);
            Assert.Equal(matriculeAttendu, match.Groups["matricule"].Value);
            Assert.Equal(exerciceAttendu, int.Parse(match.Groups["exercice"].Value));
        }
    }
}

