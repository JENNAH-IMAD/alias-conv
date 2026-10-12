namespace BankStatementConverter.Infrastructure.Exports;

// Count logical delimited records, preserving quoted newlines and an optional header.
internal static class ExportTextRecordCount
{
    public static int Count(string text,bool header)
    {
        var count=0;var quoted=false;var pending=false;
        for(var i=0;i<text.Length;i++)
        {
            var character=text[i];
            if(character=='"')
            {
                if(quoted && i+1<text.Length && text[i+1]=='"') i++;
                else quoted=!quoted;
            }
            if(!quoted && character is '\r' or '\n')
            {
                count++;pending=false;
                if(character=='\r' && i+1<text.Length && text[i+1]=='\n') i++;
            }
            else pending=true;
        }
        if(pending) count++;
        return Math.Max(0,count-(header && count>0 ? 1 : 0));
    }
}
