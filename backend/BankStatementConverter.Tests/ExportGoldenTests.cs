using System.Text;
using System.Text.Json;
using BankStatementConverter.Domain;
using Xunit;
namespace BankStatementConverter.Tests;
public class ExportGoldenTests
{
    [Theory]
    [InlineData("custom_csv_fr.csv")][InlineData("custom_csv_en.csv")][InlineData("sage100_windows1252.txt")]
    [InlineData("sage100_standard.txt")][InlineData("sagex3_utf8.txt")][InlineData("sagex3_ascii.txt")]
    public async Task ExecutesAllFiveExistingModelsExactlyAndLeavesMetadataIntact(string fixture)
    {
        var template=fixture switch
        {
            "custom_csv_fr.csv"=>ExportFixtures.Csv(), "custom_csv_en.csv"=>ExportFixtures.Csv("en-US"),
            "sage100_windows1252.txt"=>ExportFixtures.Sage(false), "sage100_standard.txt"=>ExportFixtures.Sage(true),
            "sagex3_utf8.txt"=>ExportFixtures.X3(false), _=>ExportFixtures.X3(true)
        };
        var snapshot=JsonSerializer.Serialize(template); var statement=ExportFixtures.Statement;
        var result=await ExportFixtures.Engine.GenerateAsync(statement,template,default);
        Assert.Empty(result.Errors); Assert.Equal(template.Type=="SAGEX3" ? 4 : 2,result.RowCount); Assert.Equal(template.FileExtension,result.FileExtension);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"golden",fixture)),result.Content);
        Assert.Equal(snapshot,JsonSerializer.Serialize(template));
        if(fixture=="sage100_windows1252.txt")
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var rows=Encoding.GetEncoding(1252).GetString(result.Content).Split("\r\n");
            Assert.Equal("12500,00",rows[0].Split('\t')[4]); Assert.Equal("Amount",template.Fields.Single(f=>f.FieldName=="Sens").SourceField);
        }
    }
}
