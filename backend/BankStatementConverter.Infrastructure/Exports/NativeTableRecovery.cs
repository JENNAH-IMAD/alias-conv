using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

// Recover selectable body text when table headings are images and VL has merged/damaged rows.
// Only the OCR-confirmed Date/Description/ValueDate/Debit/Credit layout is supported here.
// Printed final totals and the closing balance must independently reconcile before recovery succeeds.
public static class NativeTableRecovery
{
    private static readonly TimeSpan Timeout=TimeSpan.FromSeconds(1);
    private static string Fold(string s)=>new(s.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).Select(char.ToUpperInvariant).ToArray());
    private static bool Has(string s,string pattern)=>Regex.IsMatch(Fold(s),pattern,RegexOptions.CultureInvariant,Timeout);
    private sealed record Row(double Top,IReadOnlyList<PdfTextBox> Words);
    private sealed record Amount(decimal Value,double Left,double Right);
    private static IReadOnlyList<Row> Rows(PdfTextPage page)
    {
        var groups=new List<List<PdfTextBox>>();
        foreach(var box in page.Boxes.Where(b=>!string.IsNullOrWhiteSpace(b.Text)).OrderBy(b=>b.Bottom).ThenBy(b=>b.Left))
        {
            if(groups.Count==0 || Math.Abs(groups[^1][0].Bottom-box.Bottom)>2.6) groups.Add([]);
            groups[^1].Add(box);
        }
        return groups.Select(g=>new Row(g.Min(b=>b.Top),g.OrderBy(b=>b.Left).ToList())).ToList();
    }
    private static IReadOnlyList<Amount> Amounts(Row row,double width)
    {
        var amounts=new List<Amount>();
        for(var i=0;i<row.Words.Count;i++)
        {
            var word=row.Words[i];
            if(word.Right<width*0.6 || !Regex.IsMatch(word.Text,@"^[+-]?\d+(?:[.,]\d+)*[.,]\d{2}$",RegexOptions.None,Timeout)) continue;
            var first=i;
            while(first>0 && row.Words[first].Left-row.Words[first-1].Right<15 && Regex.IsMatch(row.Words[first-1].Text,@"^\d{1,3}$",RegexOptions.None,Timeout)) first--;
            var text=string.Concat(row.Words.Skip(first).Take(i-first+1).Select(b=>b.Text));
            if(text.Contains(',')) text=text.Replace(".","").Replace(',','.');
            if(decimal.TryParse(text,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var value)) amounts.Add(new(value,row.Words[first].Left,word.Right));
        }
        return amounts;
    }
    private static DateOnly? DatePair(IReadOnlyList<PdfTextBox> words,int i,DateOnly start,DateOnly end)
    {
        if(i+1>=words.Count || !Regex.IsMatch(words[i].Text,@"^\d{2}$",RegexOptions.None,Timeout) || !Regex.IsMatch(words[i+1].Text,@"^\d{2}$",RegexOptions.None,Timeout)
            || words[i+1].Left-words[i].Right>15) return null;
        if(i+2<words.Count && Regex.IsMatch(words[i+2].Text,@"^\d{4}$",RegexOptions.None,Timeout)) return null;
        var dates=new List<DateOnly>();
        for(var year=start.Year-1;year<=end.Year+1;year++)
            if(DateOnly.TryParseExact($"{words[i].Text}/{words[i+1].Text}/{year}","dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)) dates.Add(date);
        return dates.OrderBy(d=>Math.Abs(d.DayNumber-start.DayNumber)).Select(d=>(DateOnly?)d).FirstOrDefault();
    }
    private static AppException Incomplete()=>new(422,"La reprise du PDF ne concorde pas avec les totaux et les soldes imprimés. Vérifiez les tableaux avant l’export.");
    public static PdfStatementExtraction? TryNormalize(PdfTextDocument source,string bankName,string bankCode,string currency,CancellationToken ct,BankStatement? metadata=null)
    {
        if(source.NativePages is not {Count:>0} || source.NativePages.Count!=source.Pages.Count) return null;
        var header=source.Pages.SelectMany(p=>p.Tables??[]).SelectMany(t=>t.Rows).Select(StructuredStatementNormalizer.Header)
            .FirstOrDefault(h=>h!=null && h.ValueDate.HasValue && h.Date<h.Description && h.Description<h.ValueDate && h.ValueDate<h.Debit && h.Debit<h.Credit);
        if(header==null) return null;
        var text=string.Join("\n",source.Pages.SelectMany(p=>p.Boxes).Select(b=>b.Text));
        var pattern=@"\bDU\s+(\d{2}(?:[/.-]|\s+)\d{2}(?:[/.-]|\s+)\d{4})[^\d\n]{0,35}(\d{2}(?:[/.-]|\s+)\d{2}(?:[/.-]|\s+)\d{4})";
        var period=Regex.Match(Fold(text),pattern,RegexOptions.None,Timeout);
        DateOnly? ParseDate(string s)=>DateOnly.TryParseExact(Regex.Replace(s,@"[/.-]|\s+","/",RegexOptions.None,Timeout),"dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:null;
        var start=metadata?.PeriodStart ?? (period.Success?ParseDate(period.Groups[1].Value):null);
        var end=metadata?.PeriodEnd ?? (period.Success?ParseDate(period.Groups[2].Value):null);
        if(!start.HasValue || !end.HasValue || start>end) return null;
        var transactions=new List<StatementTransaction>();
        decimal? opening=null,closing=null,totalDebit=null,totalCredit=null;DateOnly? openingDate=null;
        foreach(var page in source.NativePages)
        {
            ct.ThrowIfCancellationRequested();
            var rows=Rows(page);
            var candidates=rows.Where(r=>r.Words.Count>2 && r.Words[0].Left<page.Width*0.15 && DatePair(r.Words,0,start.Value,end.Value).HasValue).ToList();
            if(candidates.Count==0) throw Incomplete();
            var endings=rows.SelectMany(r=>Amounts(r,page.Width)).Select(a=>a.Right/page.Width).Order().ToList();
            var clusters=new List<List<double>>();
            foreach(var ending in endings) {if(clusters.Count==0 || ending-clusters[^1][^1]>0.035) clusters.Add([]);clusters[^1].Add(ending);}
            // Caption order is confirmed by OCR; two independent numeric alignment columns are required.
            if(clusters.Count!=2) throw Incomplete();
            var split=(clusters[0].Average()+clusters[1].Average())/2*page.Width;
            var valueStarts=candidates.SelectMany(r=>Enumerable.Range(2,r.Words.Count-2).Where(i=>r.Words[i].Left>page.Width*0.4 && r.Words[i].Left<page.Width*0.7 && DatePair(r.Words,i,start.Value,end.Value).HasValue).Select(i=>r.Words[i].Left)).ToList();
            if(valueStarts.Count==0) throw Incomplete();
            var valueLeft=valueStarts.Order().ElementAt(valueStarts.Count/2);
            var descriptionLeft=candidates.Select(r=>r.Words[2].Left).Order().ElementAt(candidates.Count/2);
            var pageCountBefore=transactions.Count;
            foreach(var anchor in candidates)
            {
                var label=string.Join(" ",anchor.Words.Skip(2).Where(w=>w.Left<valueLeft-2).Select(w=>w.Text));
                var financial=Amounts(anchor,page.Width);
                if(Has(label,@"\bSOLDE\b"))
                {
                    if(page.Number!=source.NativePages[0].Number || opening.HasValue || financial.Count!=1) throw Incomplete();
                    opening=financial[0].Right<split?-Math.Abs(financial[0].Value):financial[0].Value;
                    openingDate=DatePair(anchor.Words,0,start.Value,end.Value);continue;
                }
                if(Has(label,@"^\s*REPORT\b|^\s*TOTAL\b")) continue;
                if(financial.Count!=1) throw Incomplete();
                var next=candidates.FirstOrDefault(r=>r.Top>anchor.Top+2.6)?.Top ?? double.MaxValue;
                var content=rows.Where(r=>r.Top>=anchor.Top-0.1 && r.Top<next).Select(r=>string.Join(" ",r.Words.Where(w=>w.Left>=descriptionLeft-1 && w.Left<valueLeft-2).Select(w=>w.Text))).Where(s=>s.Length>0);
                var valueIndex=Enumerable.Range(2,anchor.Words.Count-2).LastOrDefault(i=>Math.Abs(anchor.Words[i].Left-valueLeft)<12 && DatePair(anchor.Words,i,start.Value,end.Value).HasValue,-1);
                var valueDate=valueIndex<0?null:DatePair(anchor.Words,valueIndex,start.Value,end.Value);
                if(!valueDate.HasValue) throw Incomplete();
                var amount=financial[0];var debit=amount.Right<split;
                transactions.Add(new(){Sequence=transactions.Count+1,Date=DatePair(anchor.Words,0,start.Value,end.Value),ValueDate=valueDate,Description=string.Join("\n",content),
                    Debit=debit?Math.Abs(amount.Value):null,Credit=debit?null:Math.Abs(amount.Value),Amount=Math.Abs(amount.Value),Direction=debit?"DEBIT":"CREDIT"});
            }
            if(transactions.Count==pageCountBefore) throw Incomplete();
            if(page.Number==source.NativePages[^1].Number)
            {
                var lastDate=candidates[^1].Top;
                var footer=rows.Where(r=>r.Top>lastDate && !DatePair(r.Words,0,start.Value,end.Value).HasValue).Select(r=>Amounts(r,page.Width)).Where(a=>a.Count>0).ToList();
                var totals=footer.LastOrDefault(a=>a.Count==2);var balance=footer.LastOrDefault();
                var closingRows=source.Pages[^1].Tables?.SelectMany(t=>t.Rows).Where(r=>Has(string.Join(" ",r),@"NOUVEAU\s+SOLDE|SOLDE\s+FINAL")).ToList()??[];
                if(totals==null || balance is not {Count:1} || closingRows.Count==0 || !closingRows.SelectMany(r=>r).Any(s=>Regex.Replace(s,@"\s+","",RegexOptions.None,Timeout).Contains(balance[0].Value.ToString("0.00",CultureInfo.InvariantCulture).Replace('.',',')))) throw Incomplete();
                totalDebit=totals.Single(a=>a.Right<split).Value;totalCredit=totals.Single(a=>a.Right>=split).Value;
                closing=balance[0].Right<split?-Math.Abs(balance[0].Value):balance[0].Value;
            }
        }
        if(!opening.HasValue || !closing.HasValue || totalDebit!=transactions.Sum(t=>t.Debit??0) || totalCredit!=transactions.Sum(t=>t.Credit??0)
            || opening+totalCredit-totalDebit!=closing) throw Incomplete();
        var nativeText=string.Join("\n",source.NativePages.SelectMany(p=>Rows(p)).Select(r=>string.Join(" ",r.Words.Select(w=>w.Text))));
        var rib=Regex.Match(nativeText,@"\b\d{3}\s+\d{3}\s+\d{16}\s+\d{2}\b",RegexOptions.None,Timeout);
        return PdfStatementMetadata.Complete(new(new BankStatement {BankName=bankName,BankCode=bankCode,Currency=currency,AccountNumber=metadata?.AccountNumber ?? (rib.Success?rib.Value:null),
            PeriodStart=start,PeriodEnd=end,OpeningBalance=opening,ClosingBalance=closing,OpeningBalanceDate=openingDate,ClosingBalanceDate=end,
            TotalDebit=totalDebit,TotalCredit=totalCredit,BalanceConvention="CREDIT_INCREASES",Transactions=transactions},
            [new("PDF_REVIEW_REQUIRED","Vérifiez les opérations récupérées dans le PDF original avant de valider le relevé.")],source),metadata);
    }
}
