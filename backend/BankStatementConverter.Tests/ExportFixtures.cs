using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
namespace BankStatementConverter.Tests;
internal static class ExportFixtures
{
    public static BankStatement Statement => new()
    {
        Id=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),BankName="BANK OF AFRICA",BankCode="BOA",AccountNumber="01178000012345678901234",Currency="MAD",
        StatementNumber="TEST-202609",PeriodStart=new(2026,9,1),PeriodEnd=new(2026,9,30),OpeningBalance=1000m,ClosingBalance=13374.50m,
        TotalDebit=125.50m,TotalCredit=12500m,BalanceConvention="CREDIT_INCREASES",IsValidated=true,RecordType="1",
        Transactions=[new() { Id=Guid.Parse("11111111-1111-1111-1111-111111111111"),Sequence=1,Date=new(2026,9,1),ValueDate=new(2026,9,1),Description="VIREMENT CLIENT",Credit=12500m,Amount=12500m,Direction="CREDIT" },
            new() { Id=Guid.Parse("22222222-2222-2222-2222-222222222222"),Sequence=2,Date=new(2026,9,2),ValueDate=new(2026,9,2),Description="FRAIS BANCAIRES",Debit=125.50m,Amount=125.50m,Direction="DEBIT" }]
    };
    public static ExportEngine Engine => new(new ExportSourceResolver(),new(),new(),new(),new(),new(),new());
    private static ExportTemplateField Field(string name,string source,int position,string? format=null,bool required=false)
        => new() { FieldName=name,SourceField=source,Position=position,Format=format,Required=required };
    public static ExportTemplate Csv(string culture="fr-FR") => new()
    {
        Id=Guid.Parse("5e0d7827-3829-4aea-877d-c3f140274c33"),Name="CSV personnalisé",NumberCulture=culture,
        Fields=[Field("Date","Date",0,"dd/MM/yyyy",true),Field("Libellé","Description",1,null,true),Field("Débit","Debit",2),Field("Crédit","Credit",3),Field("Solde","Balance",4)]
    };
    public static ExportTemplate Sage(bool standard) => new()
    {
        Id=Guid.Parse(standard ? "25c42a40-5601-4349-835d-269f9ef421c4" : "a4ee8f3e-0ba3-434f-a487-eb1dfae5cf83"),
        Name=standard ? "Sage 100 Standard /MD MC" : "Sage 100 Sens",Type="SAGE100",FileExtension="txt",Delimiter="\t",Encoding="windows-1252",
        Fields=[Field("Date opération","Date",0,"dd/MM/yyyy",true),Field(standard ? "Date Valeur" : "Date valeur","ValueDate",1,null,true),
            Field("Libellé","Description",2,null,standard),Field("Montant","Amount",3),Field("Sens",standard ? "Direction" : "Amount",4)]
    };
    public static ExportTemplate X3(bool ascii) => new()
    {
        Id=Guid.Parse(ascii ? "4a885f6a-87e7-4b25-b57c-83c07e2d13f8" : "d962f970-ffff-463a-9280-cb2505659c51"),Name=ascii ? "Sage X3 ASCII" : "Sage X3 – Import banque",
        Type="SAGEX3",FileExtension="txt",Encoding=ascii ? "ascii" : "utf-8",NumberCulture="en-US",IncludeHeader=false,
        Fields=[Field("Date opération","Date",0,"yyyyMMdd",true),Field("Date valeur","ValueDate",1,"yyyyMMdd",true),Field("Libellé","Description",2),
            Field("Montant en devise","Amount",3,"0.00",true),Field("Sens","Direction",4,null,true),Field("Code","BankCode",5,null,true),Field("Devise","Currency",6,null,true),
            Field("Numéro relevé","StatementNumber",7,null,true),Field("Numéro","RecordType",8,null,true),Field("Numéro interne","Sequence",9,null,true)]
    };
}
