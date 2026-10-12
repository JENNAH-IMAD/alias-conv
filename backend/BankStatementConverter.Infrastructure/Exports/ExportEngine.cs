using System.Globalization;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
namespace BankStatementConverter.Infrastructure.Exports;

public sealed class ExportEngine(IExportSourceResolver resolver, ExportDateFormatter dates, ExportNumberFormatter numbers,
    CsvExportWriter csv, DelimitedTextExportWriter txt, ExportFileNameBuilder names, StatementValidator validator) : IExportEngine
{
    private static bool IncludeHeader(ExportTemplate template) => template.Type != "SAGE100" && template.IncludeHeader;
    private sealed record Prepared(string[] Columns, List<string[]> Rows, List<int?> Sequences, List<string> Kinds, List<ExportError> Warnings, List<ExportError> Errors, Encoding Encoding, List<int[]> ColumnIndices);
    private Prepared Prepare(BankStatement statement,ExportTemplate template,CancellationToken ct)
    {
        var validation=validator.Validate(statement);
        var errors=validation.Errors.ToList(); var warnings=validation.Warnings.ToList();
        var fields=template.Fields.OrderBy(f=>f.Position).ToList();
        try
        {
            Validation.Export(new(template.Name,template.Type,template.Description,template.FileExtension,template.Delimiter,
                template.Encoding,template.NumberCulture,template.IncludeHeader,template.IsActive,
                fields.Select(f=>new ExportFieldDto(f.FieldName,f.SourceField,f.Position,f.Format,f.Required)).ToList()));
        }
        catch(AppException)
        {
            foreach(var field in fields)
            {
                var date=field.SourceField is "Date" or "ValueDate";
                if(!Validation.ExportSources.Contains(field.SourceField)) errors.Add(new("UNRESOLVED_SOURCE","Source inconnue dans ce modèle.",null,field.FieldName,field.SourceField));
                else if(!string.IsNullOrEmpty(field.Format) && (date ? !Validation.DateFormats.Contains(field.Format) : field.Format is not ("0.00" or "0.000" or "0.##")))
                    errors.Add(new(date ? "INVALID_DATE_FORMAT" : "INVALID_NUMBER_FORMAT","Format de colonne invalide.",null,field.FieldName,field.SourceField));
            }
            errors.Add(new("INVALID_EXPORT_TEMPLATE","Configuration du modèle invalide. Vérifiez ses paramètres."));
        }
        if(!template.IsActive) errors.Add(new("EXPORT_TEMPLATE_INACTIVE","Ce modèle est inactif."));
        if(template.Type=="SAGEX3")
        {
            if(!statement.OpeningBalance.HasValue) errors.Add(new("OPENING_BALANCE_MISSING","Le solde initial est nécessaire pour Sage X3.",SourceField:"OpeningBalance"));
            if(!statement.ClosingBalance.HasValue) errors.Add(new("CLOSING_BALANCE_MISSING","Le solde final est nécessaire pour Sage X3.",SourceField:"ClosingBalance"));
        }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding=template.Encoding switch
        {
            "windows-1252" => Encoding.GetEncoding(1252,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback),
            "ascii" => Encoding.GetEncoding("us-ascii",EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback),
            _ => new UTF8Encoding(false,true)
        };
        var sage=template.Type is "SAGEX3" or "SAGE100";
        var columns=fields.Select(f=>sage && template.Encoding=="ascii" ? SageTextCompatibility.Ascii(f.FieldName) : f.FieldName).ToArray();
        void CheckEncoding(string text,ExportTemplateField field,int? sequence)
        {
            try { _=encoding.GetByteCount(text); }
            catch(EncoderFallbackException exception)
            {
                var codepoint=exception.IsUnknownSurrogate() ? char.ConvertToUtf32(exception.CharUnknownHigh,exception.CharUnknownLow) : exception.CharUnknown;
                errors.Add(new("ENCODING_ERROR",$"Caractère U+{codepoint:X4} incompatible avec {template.Encoding}.",sequence,field.FieldName,field.SourceField));
            }
        }
        if(IncludeHeader(template)) for(var i=0;i<fields.Count;i++) CheckEncoding(columns[i],fields[i],null);
        if(sage && template.Encoding=="ascii") warnings.Add(new("SAGE_ASCII_NORMALIZATION","Export ASCII : les accents et la ponctuation Unicode compatibles sont normalisés. Les libellés originaux restent conservés dans le relevé et le CSV standard."));
        if(template.Type=="SAGE100")
            foreach(var field in fields.Where(f=>f.FieldName.Trim().Equals("Sens",StringComparison.OrdinalIgnoreCase) && f.SourceField=="Amount"))
                warnings.Add(new("SAGE100_SENS_AMOUNT",
                    $"La colonne « {field.FieldName} » utilise la source Montant (Amount) : elle répète donc le montant du mouvement. Pour obtenir D/C, remplacez sa source par Sens (Direction) dans le modèle, puis enregistrez et actualisez l’aperçu.",
                    FieldName:field.FieldName,SourceField:field.SourceField));
        var rows=new List<string[]>();
        var sequences=new List<int?>(); var kinds=new List<string>();
        var columnIndices=new List<int[]>();
        if(errors.Any(e=>e.Code is "INVALID_EXPORT_TEMPLATE" or "INVALID_TRANSACTIONS")) return new(columns,rows,sequences,kinds,warnings,errors,encoding,columnIndices);
        var exportRows=template.Type=="SAGEX3" ? SageX3Rows.Build(statement) : statement.Transactions.Where(t=>t!=null).Select(t=>new ExportRow(statement,t,t.Sequence,"TRANSACTION"));
        foreach(var exportRow in exportRows)
        {
            ct.ThrowIfCancellationRequested();
            var transaction=exportRow.Transaction;
            var diagnosticSequence=exportRow.OriginalSequence;
            var row=new string[fields.Count];
            for(var i=0;i<fields.Count;i++)
            {
                var field=fields[i]; var resolved=resolver.Resolve(field.SourceField,exportRow.Statement,transaction); var value=resolved.Value;
                var balanceDescription=exportRow.Kind!="TRANSACTION" && field.SourceField is "Description" or "MovementDescription";
                if(balanceDescription) value=null;
                row[i]="";
                var absent=value==null || value is string text && string.IsNullOrWhiteSpace(text);
                if(!resolved.IsKnown || absent)
                {
                    if(field.Required && !balanceDescription) errors.Add(new(!resolved.IsKnown || field.SourceField is "Journal" or "Account" or "Analytic" or "RecordType" ? "UNRESOLVED_SOURCE" : "REQUIRED_VALUE_MISSING",
                        exportRow.Kind=="TRANSACTION" ? "Valeur obligatoire manquante ; renseignez-la explicitement." : $"Valeur obligatoire manquante pour le {(exportRow.Kind=="OPENING_BALANCE" ? "solde initial" : "solde final")}.",diagnosticSequence,field.FieldName,field.SourceField));
                    continue;
                }
                if(field.SourceField=="Direction" && value is string direction && direction=="UNKNOWN")
                {
                    var issue=new ExportError("INVALID_DIRECTION","Le sens UNKNOWN nécessite une revue explicite.",diagnosticSequence,field.FieldName,field.SourceField);
                    if(field.Required) { errors.Add(issue); continue; }
                    warnings.Add(issue);
                }
                try
                {
                    row[i]=value switch
                    {
                        DateOnly date => dates.Format(date,field.Format ?? (template.Type=="SAGE100" ? "dd/MM/yyyy" : null)),
                        decimal number => numbers.Format(template.Type=="SAGE100" && field.SourceField=="Amount" ? Math.Abs(number) : number,field.Format,template.NumberCulture),
                        int number => string.IsNullOrEmpty(field.Format) ? number.ToString(CultureInfo.InvariantCulture) : numbers.Format(number,field.Format,template.NumberCulture),
                        string literal => literal,
                        _ => throw new ArgumentException("Type de source non pris en charge.")
                    };
                }
                catch(ArgumentException)
                {
                    var issue=new ExportError(value is DateOnly ? "INVALID_DATE_FORMAT" : "INVALID_NUMBER_FORMAT","Valeur impossible à formater.",diagnosticSequence,field.FieldName,field.SourceField);
                    if(field.Required) errors.Add(issue); else warnings.Add(issue);
                }
                if(sage)
                {
                    var original=row[i];
                    if(field.SourceField is "Description" or "MovementDescription" or "DescriptionWithBalances") row[i]=SageTextCompatibility.Description(row[i],template.Type=="SAGEX3");
                    if(template.Type=="SAGE100" && field.SourceField=="Direction") row[i]=SageTextCompatibility.Direction(row[i]);
                    if(template.Encoding=="ascii") row[i]=SageTextCompatibility.Ascii(row[i]);
                    if(original!=row[i] && field.SourceField is "Description" or "MovementDescription" or "DescriptionWithBalances") warnings.Add(new("SAGE_DESCRIPTION_NORMALIZED","Le libellé est adapté au format Sage dans l’export uniquement.",diagnosticSequence,field.FieldName,field.SourceField));
                }
                CheckEncoding(row[i],field,diagnosticSequence);
            }
            var indices=Enumerable.Range(0,fields.Count).Where(i=>exportRow.Kind=="TRANSACTION" || fields[i].SourceField!="Description").ToArray();
            rows.Add(indices.Select(i=>row[i]).ToArray());
            columnIndices.Add(indices);
            sequences.Add(diagnosticSequence); kinds.Add(exportRow.Kind);
        }
        return new(columns,rows,sequences,kinds,warnings,errors,encoding,columnIndices);
    }
    public Task<ExportPreview> PreviewAsync(BankStatement statement,ExportTemplate template,CancellationToken ct)
    {
        var prepared=Prepare(statement,template,ct);
        var text=prepared.Errors.Any(e=>e.Code=="INVALID_EXPORT_TEMPLATE") ? "" : Write(prepared,template);
        if(!statement.IsValidated) prepared.Warnings.Insert(0,new("STATEMENT_NOT_VALIDATED","Aperçu d’un relevé non validé. Validez les données avant de télécharger."));
        return Task.FromResult(new ExportPreview(new(template.Id,template.Name),prepared.Columns,prepared.Rows.Take(50).ToList(),
            prepared.Sequences.Take(50).ToList(),prepared.Rows.Count,statement.IsValidated,
            statement.OpeningBalance,statement.ClosingBalance,prepared.Warnings,prepared.Errors,RowKinds:prepared.Kinds.Take(50).ToList(),RowColumnIndices:prepared.ColumnIndices.Take(50).ToList(),
            Text:text.Length>2000000 ? text[..2000000] : text,TextTruncated:text.Length>2000000));
    }
    private string Write(Prepared prepared,ExportTemplate template) =>
        (template.Type=="CUSTOMCSV" ? (IExportWriter)csv : txt).Write(prepared.Columns,prepared.Rows,template.Delimiter,IncludeHeader(template));
    public Task<ExportResult> GenerateAsync(BankStatement statement,ExportTemplate template,CancellationToken ct,ExportGenerationOptions? options=null)
    {
        options ??= new();
        if(options.EditedText is {Length:>2000000}) throw new AppException(400,"Le texte modifié est limité à 2 000 000 caractères.");
        var prepared=Prepare(statement,template,ct);
        if(!statement.IsValidated) prepared.Errors.Insert(0,new("STATEMENT_NOT_VALIDATED","Validez le relevé avant le téléchargement."));
        byte[] content=[];
        var encoding=prepared.Encoding;
        var allowed=false;
        // Configuration errors cannot produce a well-defined file. Data diagnostics remain visible in forced exports.
        if(!prepared.Errors.Any(e=>e.Code is "INVALID_EXPORT_TEMPLATE" or "EXPORT_TEMPLATE_INACTIVE") && (prepared.Errors.Count==0 || options.AllowErrors))
        {
            ct.ThrowIfCancellationRequested();
            var text=options.EditedText ?? Write(prepared,template);
            try { content=encoding.GetBytes(text); allowed=true; }
            catch(EncoderFallbackException)
            {
                prepared.Errors.Add(new("ENCODING_ERROR","Le fichier contient un caractère incompatible avec l’encodage."));
                if(options.AllowErrors)
                {
                    encoding=new UTF8Encoding(false,true);content=encoding.GetBytes(text);allowed=true;
                    prepared.Warnings.Add(new("FORCED_UTF8_ENCODING","Téléchargement avec erreurs : le fichier est encodé en UTF-8 pour conserver les caractères incompatibles avec le modèle."));
                }
            }
        }
        var fileName=names.Build(statement,template);
        if(options.AllowErrors) fileName=Path.GetFileNameWithoutExtension(fileName)+"_a_verifier."+template.FileExtension;
        else if(options.EditedText!=null) fileName=Path.GetFileNameWithoutExtension(fileName)+"_modifie."+template.FileExtension;
        return Task.FromResult(new ExportResult(fileName,$"{(template.Type=="CUSTOMCSV" ? "text/csv" : "text/plain")}; charset={encoding.WebName}",
            template.FileExtension,content,options.EditedText==null ? prepared.Rows.Count : ExportTextRecordCount.Count(options.EditedText,IncludeHeader(template)),prepared.Warnings,prepared.Errors,allowed,
            encoding.WebName=="us-ascii" ? "ascii" : encoding.WebName,options.EditedText!=null));
    }
}
